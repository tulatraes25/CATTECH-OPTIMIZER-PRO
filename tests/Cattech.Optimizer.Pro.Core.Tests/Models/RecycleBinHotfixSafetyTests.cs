using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Infrastructure.Cleanup;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// P0 release hotfix safety regressions. No real cleanup is executed.
/// </summary>
public sealed class RecycleBinHotfixSafetyTests
{
    [Fact]
    public void ScanningPolicy_AlwaysDisablesRecycleBinWithoutFilesystemAccess()
    {
        var target = new TempCleanupTarget
        {
            Id = "RECYCLE_BIN", IsSelected = true, IsAccessible = true,
            EstimatedFileCount = 99, EstimatedSizeBytes = 12345
        };

        TempCleanupService.MarkRecycleBinUnsupported(target);

        Assert.True(target.IsScanned);
        Assert.False(target.IsAccessible);
        Assert.False(target.IsSelected);
        Assert.Equal(0, target.EstimatedFileCount);
        Assert.Equal(0, target.EstimatedSizeBytes);
        Assert.Contains("deshabilitada", target.ScanNotes);
    }

    [Fact]
    public async Task DirectSelectedRecycleBin_RejectsBeforeAnyProcessOrDiskWrite()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-p0-reject-" + Guid.NewGuid().ToString("N"));
        var service = new TempCleanupService(root);
        var selected = new TempCleanupTarget
        {
            Id = "RECYCLE_BIN", Path = "synthetic-not-a-directory",
            DisplayName = "Synthetic Recycle Bin", IsSelected = true
        };

        var result = await service.CleanupAsync(new[] { selected });

        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.DeletedFiles);
        Assert.Equal(0, result.DeletedBytes);
        Assert.Single(result.Errors);
        Assert.Equal("UnsupportedOperation", result.Errors[0].ErrorType);
        var item = Assert.Single(result.TargetResults);
        Assert.False(item.IsSuccess);
        Assert.Equal(0, item.DeletedFiles);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task MixedSelection_AbortsEntireBatch_AndPreservesSyntheticSentinel()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-p0-mixed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sentinel = Path.Combine(root, "keep-this-test-file.txt");
            await File.WriteAllTextAsync(sentinel, "NEVER DELETE");
            var targets = new[]
            {
                new TempCleanupTarget
                {
                    Id = "USER_TEMP", Path = root, IsSelected = true,
                    IsAccessible = true, IsScanned = true
                },
                new TempCleanupTarget
                {
                    Id = "RECYCLE_BIN", Path = "synthetic-unsupported",
                    IsSelected = true, IsAccessible = true
                }
            };

            var result = await new TempCleanupService(root).CleanupAsync(targets);

            Assert.False(result.IsSuccess);
            Assert.Equal(0, result.DeletedFiles);
            Assert.Equal(0, result.DeletedBytes);
            Assert.Single(result.Errors);
            Assert.Equal(2, result.TargetResults.Count);
            Assert.All(result.TargetResults, entry => Assert.False(entry.IsSuccess));
            Assert.Equal("NEVER DELETE", await File.ReadAllTextAsync(sentinel));
            Assert.Single(Directory.GetFiles(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FailedFileCount_AloneCannotBeReportedAsSuccessfulCleanup()
    {
        Assert.False(new TempCleanupResult { FailedFiles = 1 }.IsSuccess);
        Assert.True(new TempCleanupResult().IsSuccess);
    }

    [Fact]
    public void StableHotfixSource_HasNoGlobalRecycleDeletionCommand()
    {
        // Source assertion complements functional tests: the dangerous helper
        // must be absent from this narrow hotfix branch, not just unreachable.
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/Cattech.Optimizer.Pro.Infrastructure/Cleanup/TempCleanupService.cs"));
        var code = File.ReadAllText(path);
        Assert.DoesNotContain("cmd.exe", code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rd /s", code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Shell.Application", code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CleanRecycleBinAsync", code, StringComparison.OrdinalIgnoreCase);
    }
}
