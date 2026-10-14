using System.Diagnostics;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Real multi-process Windows regression: each writer runs in its own VSTest
/// testhost and appends synthetic metadata to the same on-disk index.
/// </summary>
public sealed class ReportIndexInterprocessTests
{
    private const string ChildRootVariable = "CATTECH_INDEX_CHILD_ROOT";
    private const string ChildIdVariable = "CATTECH_INDEX_CHILD_ID";
    private const string ChildCountVariable = "CATTECH_INDEX_CHILD_COUNT";
    private const int RecordsPerChild = 20;

    [Fact]
    public async Task IndependentProcesses_ConcurrentSaves_RetainEveryRecord()
    {
        // Child workers never spawn grandchildren if a filter is ignored.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ChildRootVariable)))
            return;

        Assert.True(OperatingSystem.IsWindows(), "Multi-process regression runs on Windows CI.");

        var root = Path.Combine(Path.GetTempPath(),
            "cattech-interprocess-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Process? first = null;
        Process? second = null;
        Task<string>? out1 = null, err1 = null, out2 = null, err2 = null;
        try
        {
            first = StartChildWorker(root, "A");
            second = StartChildWorker(root, "B");
            out1 = first.StandardOutput.ReadToEndAsync();
            err1 = first.StandardError.ReadToEndAsync();
            out2 = second.StandardOutput.ReadToEndAsync();
            err2 = second.StandardError.ReadToEndAsync();

            var readiness = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(root, "ready-A"))
                || !File.Exists(Path.Combine(root, "ready-B")))
            {
                Assert.False(first.HasExited || second.HasExited,
                    "A child testhost exited before the concurrency gate was released.");
                if (readiness.Elapsed > TimeSpan.FromSeconds(30))
                    throw new TimeoutException(
                        "Two independent testhosts did not become ready.");
                await Task.Delay(40);
            }

            // Release both independent processes together.
            await File.WriteAllTextAsync(Path.Combine(root, "start.signal"), "go");

            await Task.WhenAll(
                first.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90)),
                second.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90)));

            var firstLog = await out1;
            var firstError = await err1;
            var secondLog = await out2;
            var secondError = await err2;
            Assert.True(first.ExitCode == 0,
                $"Writer A exited {first.ExitCode}: {firstLog} {firstError}");
            Assert.True(second.ExitCode == 0,
                $"Writer B exited {second.ExitCode}: {secondLog} {secondError}");

            var service = new HtmlReportService(root);
            var entries = await service.ListGeneratedReportsAsync(RecordsPerChild * 2);
            Assert.Equal(RecordsPerChild * 2, entries.Count);

            var uniqueIds = entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
            Assert.Equal(RecordsPerChild * 2, uniqueIds.Count);
            for (var i = 0; i < RecordsPerChild; i++)
            {
                Assert.Contains($"INDEX-A-{i:D3}", uniqueIds);
                Assert.Contains($"INDEX-B-{i:D3}", uniqueIds);
            }

            var indexDirectory = Path.Combine(root, "reports", "html");
            var raw = await File.ReadAllTextAsync(
                Path.Combine(indexDirectory, "reports-info.json"));
            using var document = JsonDocument.Parse(raw);
            Assert.Equal(RecordsPerChild * 2, document.RootElement.GetArrayLength());
            Assert.Empty(Directory.GetFiles(indexDirectory, ".cattech-index-*.tmp"));
        }
        finally
        {
            foreach (var worker in new[] { first, second })
            {
                if (worker is null)
                    continue;
                try
                {
                    if (!worker.HasExited)
                        worker.Kill(entireProcessTree: true);
                    await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (InvalidOperationException) { }
                catch (TimeoutException) { }
                finally { worker.Dispose(); }
            }

            // All child handles are released before temporary data is removed.
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChildWorker_AppendSyntheticMetadata_WhenActivated()
    {
        var root = Environment.GetEnvironmentVariable(ChildRootVariable);
        if (string.IsNullOrWhiteSpace(root))
            return;

        var worker = Environment.GetEnvironmentVariable(ChildIdVariable);
        Assert.True(worker is "A" or "B");
        var count = int.Parse(Environment.GetEnvironmentVariable(ChildCountVariable)!);

        await File.WriteAllTextAsync(Path.Combine(root, $"ready-{worker}"), "ready");
        var start = Stopwatch.StartNew();
        while (!File.Exists(Path.Combine(root, "start.signal")))
        {
            if (start.Elapsed > TimeSpan.FromSeconds(30))
                throw new TimeoutException("Parent did not release the worker gate.");
            await Task.Delay(30);
        }

        var service = new HtmlReportService(root);
        for (var i = 0; i < count; i++)
        {
            await service.SaveReportInfoAsync(new GeneratedReportInfo
            {
                Id = $"INDEX-{worker}-{i:D3}",
                ClientName = $"Synthetic-{worker}-{i:D3}",
                HtmlPath = $"synthetic-{worker}-{i:D3}.html"
            });
        }
    }

    [Fact]
    public async Task HeldFileLease_SerializesWaitingWriter_WithoutBlockingIndefinitely()
    {
        var root = NewRoot();
        try
        {
            var directory = Path.Combine(root, "reports", "html");
            Directory.CreateDirectory(directory);
            var leasePath = Path.Combine(directory, ".cattech-index.lock");

            await using var held = new FileStream(leasePath, FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            var service = new HtmlReportService(root);
            var save = service.SaveReportInfoAsync(new GeneratedReportInfo
            {
                Id = "AFTER-LEASE",
                ClientName = "Synthetic",
                HtmlPath = "synthetic.html"
            });

            await Task.Delay(125);
            Assert.False(save.IsCompleted, "Writer bypassed another owner's lease.");

            await held.DisposeAsync();
            await save.WaitAsync(TimeSpan.FromSeconds(5));

            var entries = await service.ListGeneratedReportsAsync();
            Assert.Single(entries);
            Assert.Equal("AFTER-LEASE", entries[0].Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LeaseTimeout_LeavesPublishedIndexUnmodified()
    {
        var root = NewRoot();
        try
        {
            var directory = Path.Combine(root, "reports", "html");
            Directory.CreateDirectory(directory);
            var indexPath = Path.Combine(directory, "reports-info.json");
            const string original = "[{\"id\":\"PRESERVE\"}]";
            await File.WriteAllTextAsync(indexPath, original);

            await using var held = new FileStream(
                Path.Combine(directory, ".cattech-index.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            await Assert.ThrowsAsync<TimeoutException>(
                () => HtmlReportService.AcquireIndexLeaseAsync(
                    Path.Combine(directory, ".cattech-index.lock"),
                    TimeSpan.FromMilliseconds(175)));

            Assert.Equal(original, await File.ReadAllTextAsync(indexPath));
            await held.DisposeAsync();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PersistentMarkerAfterLeaseRelease_DoesNotBlockAnotherProcess()
    {
        var root = NewRoot();
        try
        {
            var service = new HtmlReportService(root);
            await service.SaveReportInfoAsync(new GeneratedReportInfo
            {
                Id = "FIRST",
                ClientName = "Synthetic",
                HtmlPath = "first.html"
            });

            var marker = Path.Combine(root, "reports", "html", ".cattech-index.lock");
            Assert.True(File.Exists(marker));
            // A crash leaves the marker filename behind but closes its handle.
            var independent = new HtmlReportService(root);
            await independent.SaveReportInfoAsync(new GeneratedReportInfo
            {
                Id = "SECOND",
                ClientName = "Synthetic",
                HtmlPath = "second.html"
            });

            var records = await independent.ListGeneratedReportsAsync();
            Assert.Equal(2, records.Count);
            Assert.Contains(records, r => r.Id == "FIRST");
            Assert.Contains(records, r => r.Id == "SECOND");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-lease-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static Process StartChildWorker(string root, string id)
    {
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(typeof(ReportIndexInterprocessTests).Assembly.Location);
        start.ArgumentList.Add(
            "--TestCaseFilter:FullyQualifiedName~ChildWorker_AppendSyntheticMetadata_WhenActivated");
        start.Environment[ChildRootVariable] = root;
        start.Environment[ChildIdVariable] = id;
        start.Environment[ChildCountVariable] = RecordsPerChild.ToString();
        return Process.Start(start)
            ?? throw new InvalidOperationException("Unable to start synthetic child testhost.");
    }
}
