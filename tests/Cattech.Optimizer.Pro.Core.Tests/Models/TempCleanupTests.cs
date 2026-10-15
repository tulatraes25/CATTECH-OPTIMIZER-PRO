using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Infrastructure.Cleanup;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class TempCleanupTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };


    [Fact]
    public async Task Scan_BackgroundWorker_DoesNotBlockInvokingThread()
    {
        // RECYCLE_BIN is intentionally disabled; no filesystem scan, cleanup
        // or deletion is performed in this threading-only test.
        var target = new TempCleanupTarget
        {
            Id = "RECYCLE_BIN",
            Path = "synthetic-never-accessed",
            IsSelected = false
        };
        using var started = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        int reportThreadId = -1;
        var callerThreadId = Environment.CurrentManagedThreadId;

        var reporter = new InlineProgress(percent =>
        {
            if (percent != 10)
                return;

            reportThreadId = Environment.CurrentManagedThreadId;
            started.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Synthetic scan reporter was not released.");
        });

        Task<List<TempCleanupTarget>>? task = null;
        try
        {
            task = TempCleanupService.ScanTargetsAsync(
                new List<TempCleanupTarget> { target }, reporter);
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Assert.NotEqual(callerThreadId, reportThreadId);
            Assert.False(task.IsCompleted);
        }
        finally
        {
            release.Set();
        }

        var scanned = await task!.WaitAsync(TimeSpan.FromSeconds(5));
        var recycle = Assert.Single(scanned);
        Assert.True(recycle.IsScanned);
        Assert.False(recycle.IsSelected);
        Assert.False(recycle.IsAccessible);
    }

    private sealed class InlineProgress(Action<int> onReport) : IProgress<int>
    {
        public void Report(int value) => onReport(value);
    }

    [Fact]
    public async Task Scan_TrustedDefaultSelection_IsPreservedOnlyWhenAccessible()
    {
        // Scan-only: never invokes CleanupAsync or removes any real files.
        var defaults = CleanupTargets.GetDefaultTargets();
        var original = defaults.Single(t => t.Id == "USER_TEMP");
        var scanned = await new TempCleanupService().ScanAsync();
        var userTemp = scanned.Single(t => t.Id == "USER_TEMP");

        Assert.Equal(original.IsSelected && userTemp.IsAccessible, userTemp.IsSelected);
        if (!userTemp.IsAccessible)
            Assert.False(userTemp.IsSelected);
    }

    [Fact]
    public async Task Scan_RecycleBin_IsExplicitlyUnavailable()
    {
        // Scan-only test: no cleanup is ever requested or performed.
        var service = new TempCleanupService();
        var targets = await service.ScanAsync();
        var recycleBin = Assert.Single(targets, target => target.Id == "RECYCLE_BIN");

        Assert.True(recycleBin.IsScanned);
        Assert.False(recycleBin.IsAccessible);
        Assert.False(recycleBin.IsSelected);
        Assert.Equal(0, recycleBin.EstimatedSizeBytes);
        Assert.Equal(0, recycleBin.EstimatedFileCount);
        Assert.Contains("No disponible", recycleBin.ScanNotes);
    }

    [Fact]
    public async Task Cleanup_DirectRecycleBinSelection_FailsClosed_WithoutDeletingAnyFile()
    {
        // Deliberately bypass UI selection/accessibility state. The service
        // must reject a selected RECYCLE_BIN target with no OS mutation.
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-recycle-denied-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sentinel = Path.Combine(root, "keep-me.txt");
        const string original = "SYNTHETIC-DO-NOT-DELETE";
        await File.WriteAllTextAsync(sentinel, original);
        try
        {
            var service = new TempCleanupService(root);
            var target = new TempCleanupTarget
            {
                Id = "RECYCLE_BIN",
                DisplayName = "Papelera sintética",
                Path = root,
                IsSelected = true,
                IsAccessible = true
            };

            var result = await service.CleanupAsync(new[] { target });

            Assert.False(result.IsSuccess);
            Assert.Equal(0, result.DeletedFiles);
            Assert.Equal(0L, result.DeletedBytes);
            Assert.Equal(0, result.FailedFiles); // Rejected target, not a deleted file.
            var error = Assert.Single(result.Errors);
            Assert.Equal("UnsupportedOperation", error.ErrorType);
            Assert.Contains("deshabilitada", error.Message);
            var details = Assert.Single(result.TargetResults);
            Assert.False(details.IsSuccess);
            Assert.Equal(0, details.DeletedFiles);
            Assert.Equal(0L, details.DeletedBytes);
            Assert.Equal(original, await File.ReadAllTextAsync(sentinel));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    [Theory]
    [InlineData("USER_TEMP")]
    [InlineData("WINDOWS_TEMP")]
    [InlineData("THUMBNAILS")]
    [InlineData("UNRECOGNIZED")]
    public async Task Cleanup_ForgedTargetPath_FailsClosed_WithoutTouchingFiles(string id)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-cleanup-allowlist-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sentinel = Path.Combine(root, "thumbcache_synthetic.db");
        const string contents = "KEEP-ME-SYNTHETIC";
        await File.WriteAllTextAsync(sentinel, contents);
        try
        {
            var service = new TempCleanupService(root);
            var target = new TempCleanupTarget
            {
                Id = id,
                DisplayName = "Forged target",
                Path = root,
                IsSelected = true,
                IsScanned = true,
                IsAccessible = true
            };

            var result = await service.CleanupAsync(new[] { target });

            Assert.False(result.IsSuccess);
            Assert.Equal(0, result.DeletedFiles);
            Assert.Equal(0L, result.DeletedBytes);
            Assert.Equal(contents, await File.ReadAllTextAsync(sentinel));
            var error = Assert.Single(result.Errors);
            Assert.Equal("UnsafeTarget", error.ErrorType);
            Assert.Equal(id, error.FilePath); // No absolute paths in error metadata.
            var details = Assert.Single(result.TargetResults);
            Assert.False(details.IsSuccess);
            Assert.Equal(0, details.DeletedFiles);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Cleanup_InvalidTarget_BlocksEntireBatchBeforeAnyDeletion()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-cleanup-batch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sentinel = Path.Combine(root, "keep-me.txt");
        await File.WriteAllTextAsync(sentinel, "unchanged");
        try
        {
            var service = new TempCleanupService(root);
            var targets = new[]
            {
                new TempCleanupTarget
                {
                    Id = "USER_TEMP",
                    Path = root,
                    IsSelected = true,
                    IsScanned = true,
                    IsAccessible = true
                },
                new TempCleanupTarget
                {
                    Id = "RECYCLE_BIN",
                    Path = root,
                    IsSelected = true
                }
            };

            var result = await service.CleanupAsync(targets);

            Assert.False(result.IsSuccess);
            Assert.Equal(2, result.Errors.Count);
            Assert.Equal(2, result.TargetResults.Count);
            Assert.All(result.TargetResults, item => Assert.False(item.IsSuccess));
            Assert.Equal(0, result.DeletedFiles);
            Assert.Equal(0L, result.DeletedBytes);
            Assert.Equal("unchanged", await File.ReadAllTextAsync(sentinel));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveHistory_ConcurrentIndependentInstances_PreserveAllSnapshots()
    {
        const int count = 36;
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-cleanup-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var timestamp = new DateTime(2026, 10, 14, 10, 0, 0);
            var records = Enumerable.Range(0, count).Select(i => new TempCleanupResult
            {
                Id = $"SYNTH-{i:D3}",
                StartedAt = timestamp,
                FinishedAt = timestamp.AddSeconds(1),
                DeletedFiles = i,
                TechnicianName = "Synthetic QA"
            }).ToArray();

            var names = await Task.WhenAll(records.Select(record =>
                new TempCleanupService(root).SaveResultAsync(record)));
            Assert.Equal(count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());

            var dir = Path.Combine(root, "data", "cleanup-results");
            var files = Directory.GetFiles(dir, "cleanup-result-*.json");
            Assert.Equal(count, files.Length);
            var stored = await Task.WhenAll(files.Select(async file =>
                JsonSerializer.Deserialize<TempCleanupResult>(
                    await File.ReadAllTextAsync(file), SerializerOptions)));
            Assert.All(stored, entry => Assert.NotNull(entry));
            var byId = stored.ToDictionary(item => item!.Id);
            Assert.Equal(count, byId.Count);
            foreach (var original in records)
            {
                Assert.True(byId.TryGetValue(original.Id, out var persisted));
                Assert.Equal(original.DeletedFiles, persisted!.DeletedFiles);
                Assert.Equal(original.TechnicianName, persisted.TechnicianName);
            }

            Assert.Empty(Directory.GetFiles(dir, ".cattech-cleanup-*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveHistory_RepeatedUntrustedId_NeverOverwritesOldAuditFile()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-cleanup-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var service = new TempCleanupService(root);
            var record = new TempCleanupResult
            {
                Id = "../untrusted/path",
                StartedAt = new DateTime(2026, 10, 14, 10, 0, 0),
                FinishedAt = new DateTime(2026, 10, 14, 10, 0, 1),
                DeletedFiles = 3
            };
            var first = await service.SaveResultAsync(record);
            var dir = Path.Combine(root, "data", "cleanup-results");
            var firstPath = Path.Combine(dir, first);
            var original = await File.ReadAllTextAsync(firstPath);

            record.DeletedFiles = 99;
            var second = await service.SaveResultAsync(record);
            Assert.NotEqual(first, second);
            Assert.Equal(first, Path.GetFileName(first));
            Assert.DoesNotContain("/", first);
            Assert.DoesNotContain("..", first);
            Assert.Equal(original, await File.ReadAllTextAsync(firstPath));
            Assert.Contains("\"deletedFiles\": 99",
                await File.ReadAllTextAsync(Path.Combine(dir, second)));
            Assert.Equal(2, Directory.GetFiles(dir, "cleanup-result-*.json").Length);
            Assert.Empty(Directory.GetFiles(dir, ".cattech-cleanup-*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    [Fact]
    public async Task FolderCleanup_VanishedSyntheticLocation_MustReportFailure()
    {
        var missing = Path.Combine(Path.GetTempPath(),
            "cattech-missing-folder-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(missing));
        var target = new TempCleanupTarget { Id = "USER_TEMP", Path = missing };
        var summary = new TargetResult { TargetId = target.Id };
        var result = new TempCleanupResult();

        // This internal test path is nonexistent; no files are touched.
        await new TempCleanupService().CleanFolderAsync(target, summary, result);

        Assert.False(summary.IsSuccess);
        Assert.Equal(0, result.DeletedFiles);
        Assert.Equal(0, result.DeletedBytes);
        var error = Assert.Single(result.Errors);
        Assert.Equal("MissingTarget", error.ErrorType);
        Assert.Equal("USER_TEMP", error.FilePath);
        Assert.DoesNotContain(missing, error.Message);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ThumbnailCleanup_VanishedSyntheticLocation_MustReportFailure()
    {
        var missing = Path.Combine(Path.GetTempPath(),
            "cattech-missing-thumbs-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(missing));
        var target = new TempCleanupTarget { Id = "THUMBNAILS", Path = missing };
        var summary = new TargetResult { TargetId = target.Id };
        var result = new TempCleanupResult();

        // Not the real Windows thumbnail location; it never existed.
        TempCleanupService.CleanThumbnailsFolder(target, summary, result);

        Assert.False(summary.IsSuccess);
        Assert.Equal(0, result.DeletedFiles);
        Assert.Equal(0, result.DeletedBytes);
        var error = Assert.Single(result.Errors);
        Assert.Equal("MissingTarget", error.ErrorType);
        Assert.Equal("THUMBNAILS", error.FilePath);
        Assert.DoesNotContain(missing, error.Message);
    }

    [Fact]
    public async Task FolderCleanup_IsolatedSyntheticFile_AccountsForRemovedBytes()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-cleanup-synthetic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "old-synthetic-fixture.tmp");
        var bytes = new byte[733];
        await File.WriteAllBytesAsync(path, bytes);
        File.SetCreationTimeUtc(path, DateTime.UtcNow.AddDays(-8));
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-8));
        try
        {
            var target = new TempCleanupTarget { Id = "USER_TEMP", Path = root };
            var summary = new TargetResult { TargetId = target.Id };
            var result = new TempCleanupResult();

            // Explicitly isolated test fixture only. Not an actual USER_TEMP
            // selection or CleanupAsync call; never traverses OS temp roots.
            await new TempCleanupService(root).CleanFolderAsync(target, summary, result);

            Assert.False(File.Exists(path));
            Assert.Empty(result.Errors);
            Assert.Equal(1, summary.DeletedFiles);
            Assert.Equal(733L, summary.DeletedBytes);
            Assert.Equal(1, result.DeletedFiles);
            Assert.Equal(733L, result.DeletedBytes);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FolderCleanup_RecentSyntheticFile_IsNotRemovedOrCounted()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-recent-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "recent-synthetic.txt");
            File.WriteAllText(file, "PRESERVE-RECENT");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow);

            var target = new TempCleanupTarget { Id = "USER_TEMP", Path = root };
            var summary = new TargetResult { TargetId = target.Id };
            var result = new TempCleanupResult();

            await new TempCleanupService(root).CleanFolderAsync(target, summary, result);

            Assert.Equal("PRESERVE-RECENT", File.ReadAllText(file));
            Assert.Equal(0, summary.DeletedFiles);
            Assert.Equal(0L, summary.DeletedBytes);
            Assert.Equal(1, summary.SkippedFiles);
            Assert.Equal(0, result.DeletedFiles);
            Assert.Equal(1, result.SkippedFiles);
            Assert.Empty(result.Errors);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ThumbnailCleanup_OldSyntheticCache_UsesVerifiedHandle()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-thumbs-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "thumbcache_fixture.db");
            File.WriteAllBytes(file, new byte[461]);
            File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-8));
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));
            var target = new TempCleanupTarget { Id = "THUMBNAILS", Path = root };
            var summary = new TargetResult { TargetId = target.Id };
            var result = new TempCleanupResult();

            TempCleanupService.CleanThumbnailsFolder(target, summary, result);

            Assert.False(File.Exists(file));
            Assert.Equal(1, summary.DeletedFiles);
            Assert.Equal(461L, summary.DeletedBytes);
            Assert.Equal(1, result.DeletedFiles);
            Assert.Equal(461L, result.DeletedBytes);
            Assert.Empty(result.Errors);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Cleanup_CapturedScope_DoesNotFollowLaterCallerMutations()
    {
        // No cleanup is executed: verifies the trust boundary on an isolated
        // synthetic scope while a UI/client still owns its mutable objects.
        var original = new TempCleanupTarget
        {
            Id = "USER_TEMP",
            DisplayName = "Synthetic selection",
            Path = @"C:\synthetic\Temp",
            IsSelected = true,
            IsScanned = true,
            IsAccessible = true,
            EstimatedSizeBytes = 123
        };
        var notSelected = new TempCleanupTarget
        {
            Id = "THUMBNAILS",
            Path = @"C:\synthetic\Thumbnails",
            IsSelected = false
        };

        var captured = TempCleanupService.CaptureSelectedTargets(
            new[] { original, notSelected });
        var snapshot = Assert.Single(captured);
        Assert.NotSame(original, snapshot);

        original.Id = "WINDOWS_TEMP";
        original.Path = @"C:\synthetic\system-redirect";
        original.DisplayName = "Changed after approval";
        original.IsScanned = false;
        original.IsAccessible = false;
        original.IsSelected = false;
        original.EstimatedSizeBytes = 999;

        Assert.Equal("USER_TEMP", snapshot.Id);
        Assert.Equal(@"C:\synthetic\Temp", snapshot.Path);
        Assert.Equal("Synthetic selection", snapshot.DisplayName);
        Assert.True(snapshot.IsScanned);
        Assert.True(snapshot.IsAccessible);
        Assert.True(snapshot.IsSelected);
        Assert.Equal(123L, snapshot.EstimatedSizeBytes);
        Assert.DoesNotContain(captured, target => target.Id == "THUMBNAILS");
    }

    [Fact]
    public async Task Cleanup_DuplicateSelection_FailsBeforeAnyMutation()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-duplicate-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sentinel = Path.Combine(root, "keep.txt");
        await File.WriteAllTextAsync(sentinel, "SYNTHETIC-KEEP");
        try
        {
            // Deliberately forge duplicate allowed IDs and paths; direct
            // CleanupAsync should abort before calling a deletion routine.
            var duplicates = Enumerable.Range(0, 2).Select(i =>
                new TempCleanupTarget
                {
                    Id = "USER_TEMP",
                    DisplayName = $"Synthetic-{i}",
                    Path = root,
                    IsSelected = true,
                    IsScanned = true,
                    IsAccessible = true
                }).ToArray();

            var result = await new TempCleanupService(root).CleanupAsync(duplicates);

            Assert.False(result.IsSuccess);
            Assert.Equal(0, result.DeletedFiles);
            Assert.Equal(0L, result.DeletedBytes);
            Assert.Equal(2, result.Errors.Count);
            Assert.All(result.Errors, error =>
            {
                Assert.Equal("UnsafeTarget", error.ErrorType);
                Assert.Contains("duplicada", error.Message);
            });
            Assert.Equal(2, result.TargetResults.Count);
            Assert.All(result.TargetResults, item => Assert.False(item.IsSuccess));
            Assert.Equal("SYNTHETIC-KEEP", await File.ReadAllTextAsync(sentinel));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BoundedEnumeration_RefusesPartialCandidateLists()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-bounded-enum-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            for (var i = 0; i < 3; i++)
                File.WriteAllText(Path.Combine(root, $"synthetic-{i}.tmp"), "KEEP");

            Assert.False(TempCleanupService.TryEnumerateBounded(
                root, "*", recursive: true, maxFiles: 2,
                maxDuration: TimeSpan.FromSeconds(3), out var partial));
            Assert.Empty(partial);

            Assert.True(TempCleanupService.TryEnumerateBounded(
                root, "*", recursive: true, maxFiles: 3,
                maxDuration: TimeSpan.FromSeconds(3), out var complete));
            Assert.Equal(3, complete.Count);
            Assert.All(complete, path => Assert.StartsWith(root, path));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task FolderCleanup_OverBudget_RejectsBeforeRemovingAnything()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-enum-overflow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var files = Enumerable.Range(0, 3)
                .Select(i => Path.Combine(root, $"file-{i}.tmp")).ToArray();
            foreach (var file in files)
            {
                File.WriteAllText(file, "SYNTHETIC-KEEP");
                File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-8));
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));
            }

            var target = new TempCleanupTarget { Id = "USER_TEMP", Path = root };
            var details = new TargetResult { TargetId = target.Id };
            var result = new TempCleanupResult();
            await new TempCleanupService(root).CleanFolderAsync(
                target, details, result, maxCandidates: 2);

            Assert.Equal(0, result.DeletedFiles);
            Assert.Equal(0L, result.DeletedBytes);
            Assert.Equal("EnumerationError", Assert.Single(result.Errors).ErrorType);
            Assert.False(details.IsSuccess);
            Assert.All(files, file => Assert.Equal("SYNTHETIC-KEEP", File.ReadAllText(file)));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ThumbnailCleanup_OverBudget_RejectsBeforeRemovingAnything()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-thumb-overflow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var files = Enumerable.Range(0, 3)
                .Select(i => Path.Combine(root, $"thumbcache_{i}.db")).ToArray();
            foreach (var file in files)
            {
                File.WriteAllText(file, "SYNTHETIC-KEEP");
                File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-8));
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));
            }

            var target = new TempCleanupTarget { Id = "THUMBNAILS", Path = root };
            var details = new TargetResult { TargetId = target.Id };
            var result = new TempCleanupResult();
            TempCleanupService.CleanThumbnailsFolder(
                target, details, result, maxCandidates: 2);

            Assert.Equal(0, result.DeletedFiles);
            Assert.Equal(0L, result.DeletedBytes);
            Assert.Equal("EnumerationError", Assert.Single(result.Errors).ErrorType);
            Assert.False(details.IsSuccess);
            Assert.All(files, file => Assert.Equal("SYNTHETIC-KEEP", File.ReadAllText(file)));
        }
        finally { Directory.Delete(root, recursive: true); }
    }


    [Fact]
    public async Task Cleanup_NestedOldFile_IsNotInDeletionScope()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-direct-only-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var nested = Path.Combine(root, "nested");
            Directory.CreateDirectory(nested);
            var nestedFile = Path.Combine(nested, "old-user-data.txt");
            File.WriteAllText(nestedFile, "SYNTHETIC-KEEP");
            File.SetCreationTimeUtc(nestedFile, DateTime.UtcNow.AddDays(-8));
                File.SetLastWriteTimeUtc(nestedFile, DateTime.UtcNow.AddDays(-8));

            Assert.True(TempCleanupService.TryEnumerateBounded(
                root, "*", recursive: false, maxFiles: 5,
                maxDuration: TimeSpan.FromSeconds(3), out var candidates));
            Assert.Empty(candidates);

            var target = new TempCleanupTarget { Id = "USER_TEMP", Path = root };
            var details = new TargetResult { TargetId = target.Id };
            var result = new TempCleanupResult();
            await new TempCleanupService(root).CleanFolderAsync(
                target, details, result);

            Assert.Equal(0, details.DeletedFiles);
            Assert.Equal(0L, details.DeletedBytes);
            Assert.Empty(result.Errors);
            Assert.Equal("SYNTHETIC-KEEP", File.ReadAllText(nestedFile));
        }
        finally { Directory.Delete(root, recursive: true); }
    }


    [Fact]
    public void ScanFolder_OnlyEstimatesFilesOlderThanRetention()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-age-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var eligible = Path.Combine(root, "older-than-seven-days.tmp");
            var justCopied = Path.Combine(root, "copied-with-old-modified.tmp");
            var editedToday = Path.Combine(root, "recently-updated.tmp");
            File.WriteAllBytes(eligible, new byte[719]);
            File.WriteAllBytes(justCopied, new byte[481]);
            File.WriteAllBytes(editedToday, new byte[347]);

            File.SetCreationTimeUtc(eligible, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(eligible, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(justCopied, DateTime.UtcNow.AddDays(-40));
            File.SetCreationTimeUtc(editedToday, DateTime.UtcNow.AddDays(-40));
            File.SetLastWriteTimeUtc(editedToday, DateTime.UtcNow);

            var target = new TempCleanupTarget
            {
                Id = "USER_TEMP",
                Path = root,
                IsSelected = true
            };
            TempCleanupService.ScanFolder(target);
            Assert.True(target.IsAccessible);
            Assert.True(target.IsScanned);
            Assert.True(target.IsSelected);
            Assert.Equal(1, target.EstimatedFileCount);
            Assert.Equal(719L, target.EstimatedSizeBytes);
            Assert.True(File.Exists(eligible));
            Assert.True(File.Exists(justCopied));
            Assert.True(File.Exists(editedToday));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void TempCleanupTarget_DefaultValues_AreValid()
    {
        var target = new TempCleanupTarget();

        Assert.NotNull(target.Id);
        Assert.NotEmpty(target.Id);
        Assert.Equal(8, target.Id.Length);
        Assert.False(target.IsSelected);
        Assert.False(target.IsSystemLocation);
        Assert.False(target.IsScanned);
        Assert.Equal(0, target.EstimatedSizeBytes);
        Assert.Equal(0, target.EstimatedFileCount);
    }

    [Fact]
    public void TempCleanupTarget_SizeMB_CalculatesCorrectly()
    {
        var target = new TempCleanupTarget
        {
            EstimatedSizeBytes = 500L * 1024 * 1024 // 500 MB
        };

        Assert.Equal(500, target.EstimatedSizeMB);
    }

    [Fact]
    public void CleanupTargets_GetDefaultTargets_ReturnsValidTargets()
    {
        var targets = CleanupTargets.GetDefaultTargets();

        Assert.NotEmpty(targets);
        Assert.Contains(targets, t => t.Id == "USER_TEMP");
        Assert.Contains(targets, t => t.Id == "WINDOWS_TEMP");
        Assert.Contains(targets, t => t.Id == "RECYCLE_BIN");
        Assert.Contains(targets, t => t.Id == "THUMBNAILS");
    }

    [Fact]
    public void CleanupTargets_UserTemp_AutoSelectsOnlyTrustedKnownFolder()
    {
        var userTemp = CleanupTargets.GetDefaultTargets().Single(t => t.Id == "USER_TEMP");

        Assert.Equal(CleanupTargets.IsTrustedUserTempPath(userTemp.Path),
            userTemp.IsSelected);
        Assert.False(userTemp.IsSystemLocation);
        Assert.Equal(CleanupRiskLevel.Low, userTemp.RiskLevel);
        if (!userTemp.IsSelected)
            Assert.Contains("No disponible", userTemp.ScanNotes);
    }

    [Fact]
    public void CleanupTargets_RejectsRedirectedUserTempAndSubdirectories()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(local))
            return;

        var knownTemp = Path.Combine(local, "Temp");
        Assert.True(CleanupTargets.IsTrustedUserTempPath(knownTemp));
        Assert.False(CleanupTargets.IsTrustedUserTempPath(
            Path.Combine(local, "Documents")));
        Assert.False(CleanupTargets.IsTrustedUserTempPath(
            Path.Combine(knownTemp, "redirected")));
        Assert.False(CleanupTargets.IsTrustedUserTempPath(
            Path.GetPathRoot(knownTemp)));
    }

    [Fact]
    public void CleanupTargets_SystemTemp_FollowsActualWindowsInstallation()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(windows))
            return;

        var target = CleanupTargets.GetDefaultTargets().Single(t => t.Id == "WINDOWS_TEMP");
        Assert.Equal(Path.Combine(windows, "Temp"), target.Path);
        Assert.True(CleanupTargets.IsTrustedWindowsTempPath(target.Path));
        Assert.False(CleanupTargets.IsTrustedWindowsTempPath(
            Path.Combine(windows, "System32")));
    }

    [Fact]
    public void CleanupTargets_WindowsTemp_IsNotSelectedByDefault()
    {
        var targets = CleanupTargets.GetDefaultTargets();
        var winTemp = targets.First(t => t.Id == "WINDOWS_TEMP");

        Assert.False(winTemp.IsSelected);
        Assert.True(winTemp.IsSystemLocation);
    }

    [Fact]
    public void CleanupTargets_RecycleBin_IsOptional()
    {
        var targets = CleanupTargets.GetDefaultTargets();
        var recycleBin = targets.First(t => t.Id == "RECYCLE_BIN");

        Assert.True(recycleBin.IsOptional);
        Assert.False(recycleBin.IsSelected);
        Assert.Equal(CleanupRiskLevel.Medium, recycleBin.RiskLevel);
    }

    [Fact]
    public void CleanupTargets_NoProhibitedLocations()
    {
        var targets = CleanupTargets.GetDefaultTargets();
        var allPaths = string.Join(" ", targets.Select(t => t.Path.ToLowerInvariant()));

        // No debe incluir Descargas, Documentos, Escritorio
        Assert.DoesNotContain("downloads", allPaths);
        Assert.DoesNotContain("documents", allPaths);
        Assert.DoesNotContain("desktop", allPaths);
    }

    [Fact]
    public void CleanupRiskLevel_HasAllValues()
    {
        Assert.Equal(0, (int)CleanupRiskLevel.Low);
        Assert.Equal(1, (int)CleanupRiskLevel.Medium);
    }

    [Fact]
    public void TempCleanupResult_DefaultValues_AreValid()
    {
        var result = new TempCleanupResult();

        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
        Assert.Equal(8, result.Id.Length);
        Assert.True(result.StartedAt <= DateTime.Now);
        Assert.Equal(0, result.DeletedFiles);
        Assert.Equal(0, result.SkippedFiles);
        Assert.Equal(0, result.FailedFiles);
        Assert.True(result.Errors != null && result.Errors.Count == 0);
    }

    [Fact]
    public void TempCleanupResult_IsSuccess_WhenNoErrors()
    {
        var result = new TempCleanupResult();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TempCleanupResult_HasWarnings_WhenSkipped()
    {
        var result = new TempCleanupResult { SkippedFiles = 5 };
        Assert.True(result.HasWarnings);
    }

    [Fact]
    public void TempCleanupResult_FailedFilesWithoutErrorDetails_IsNotSuccess()
    {
        var result = new TempCleanupResult { FailedFiles = 1 };
        Assert.Empty(result.Errors);
        Assert.False(result.IsSuccess);
        Assert.True(result.HasWarnings);
    }

    [Fact]
    public void TempCleanupResult_SkippedOnly_IsWarningButNotFailure()
    {
        var result = new TempCleanupResult { SkippedFiles = 2 };
        Assert.True(result.IsSuccess);
        Assert.True(result.HasWarnings);
    }

    [Fact]
    public void TempCleanupResult_HasWarnings_WhenFailed()
    {
        var result = new TempCleanupResult { FailedFiles = 2 };
        Assert.True(result.HasWarnings);
    }

    [Fact]
    public void TempCleanupResult_Duration_CalculatesCorrectly()
    {
        var result = new TempCleanupResult
        {
            StartedAt = new DateTime(2024, 1, 1, 10, 0, 0),
            FinishedAt = new DateTime(2024, 1, 1, 10, 0, 30)
        };

        Assert.Equal(30, result.Duration.TotalSeconds);
    }

    [Fact]
    public void TempCleanupResult_SerializeToJson_ProducesValidJson()
    {
        var result = new TempCleanupResult
        {
            DeletedFiles = 100,
            DeletedBytes = 500L * 1024 * 1024,
            SkippedFiles = 5,
            FailedFiles = 2
        };

        result.Errors.Add(new CleanupError
        {
            FilePath = @"C:\temp\locked.lock",
            Message = "Archivo bloqueado",
            ErrorType = "Locked"
        });

        var json = JsonSerializer.Serialize(result, SerializerOptions);

        Assert.Contains("deletedFiles", json);
        Assert.Contains("skippedFiles", json);
        Assert.Contains("Archivo bloqueado", json);
    }

    [Fact]
    public void CleanupError_DefaultValues_AreValid()
    {
        var error = new CleanupError();

        Assert.Equal(string.Empty, error.FilePath);
        Assert.Equal(string.Empty, error.Message);
        Assert.Equal(string.Empty, error.ErrorType);
    }

    [Fact]
    public void TargetResult_DefaultValues_AreValid()
    {
        var targetResult = new TargetResult();

        Assert.Equal(string.Empty, targetResult.TargetId);
        Assert.Equal(0, targetResult.DeletedFiles);
        Assert.Equal(0L, targetResult.DeletedBytes);
        Assert.Equal(0, targetResult.SkippedFiles);
        Assert.Equal(0, targetResult.FailedFiles);
    }

    [Fact]
    public void TempCleanupResultSummary_DefaultValues_AreValid()
    {
        var summary = new TempCleanupResultSummary();

        Assert.Equal(string.Empty, summary.Id);
        Assert.Equal(0, summary.DeletedMB);
        Assert.Equal(0, summary.DeletedFiles);
        Assert.False(summary.HasWarnings);
    }
}
