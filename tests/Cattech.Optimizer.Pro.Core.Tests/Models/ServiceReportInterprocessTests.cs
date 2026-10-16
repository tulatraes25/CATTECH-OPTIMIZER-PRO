using System.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Data;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Synthetic report-store concurrency tests. Only disposable temp directories;
/// no real customer data, deletions or repairs are performed.
/// </summary>
public sealed class ServiceReportInterprocessTests
{
    private const string ChildRootVariable = "CATTECH_SERVICE_CHILD_ROOT";
    private const string ChildIdVariable = "CATTECH_SERVICE_CHILD_ID";
    private const int RecordsPerChild = 12;

    [Fact]
    public async Task HeldForeignLease_SerializesReadersInventoryAndWriter()
    {
        var root = NewRoot();
        try
        {
            var owner = new JsonServiceReportService(root);
            var original = new ServiceReport
            {
                Id = "SYNTHETIC-ORIGINAL",
                Client = new ClientInfo { Name = "Synthetic one" }
            };
            await owner.SaveReportAsync(original);

            var path = StoreMarkerPath(root);
            await using var held = new FileStream(
                path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            var reader = new JsonServiceReportService(root);
            var inventoryReader = new JsonServiceReportService(root);
            var writer = new JsonServiceReportService(root);
            var loadTask = reader.LoadReportAsync(original.Id);
            var inventoryTask = inventoryReader.InspectReportInventoryAsync();
            var writeTask = writer.SaveReportAsync(new ServiceReport
            {
                Id = "SYNTHETIC-WAITING",
                Client = new ClientInfo { Name = "Synthetic two" }
            });

            await Task.Delay(140);
            Assert.False(loadTask.IsCompleted, "Load bypassed the external lease.");
            Assert.False(inventoryTask.IsCompleted, "Inventory bypassed the external lease.");
            Assert.False(writeTask.IsCompleted, "Save bypassed the external lease.");

            await held.DisposeAsync();
            var loaded = await loadTask.WaitAsync(TimeSpan.FromSeconds(8));
            var inventory = await inventoryTask.WaitAsync(TimeSpan.FromSeconds(8));
            await writeTask.WaitAsync(TimeSpan.FromSeconds(8));
            Assert.NotNull(loaded);
            Assert.Equal(original.Id, loaded.Id);
            Assert.True(inventory.TotalFiles is 1 or 2);
            var final = await owner.InspectReportInventoryAsync();
            Assert.Equal(2, final.UniqueEntries);
            Assert.False(final.RequiresReview);
            Assert.True(File.Exists(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LeaseTimeout_LeavesSyntheticPublishedReportUntouched()
    {
        var root = NewRoot();
        try
        {
            var store = new JsonServiceReportService(root);
            var report = new ServiceReport { Id = "SYNTHETIC-PRESERVE" };
            var name = await store.SaveReportAsync(report);
            var file = Path.Combine(root, "data", "service-reports", name);
            var before = await File.ReadAllTextAsync(file);

            var marker = StoreMarkerPath(root);
            await using var lease = new FileStream(marker, FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            await Assert.ThrowsAsync<TimeoutException>(() =>
                JsonServiceReportService.AcquireStoreLeaseAsync(
                    marker, TimeSpan.FromMilliseconds(175)));
            Assert.Equal(before, await File.ReadAllTextAsync(file));
            await lease.DisposeAsync();

            Assert.NotNull(await store.LoadReportAsync(report.Id));
            Assert.Equal(before, await File.ReadAllTextAsync(file));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DistinctServiceInstances_ConcurrentWritesPreserveValidReportIds()
    {
        var root = NewRoot();
        try
        {
            var first = new JsonServiceReportService(root);
            var second = new JsonServiceReportService(root);
            var writes = Enumerable.Range(0, 24).Select(index =>
                (index % 2 == 0 ? first : second).SaveReportAsync(new ServiceReport
                {
                    Id = $"SYNTHETIC-ID-{index:D3}",
                    Client = new ClientInfo { Name = $"Synthetic {index:D3}" }
                }));
            var names = await Task.WhenAll(writes).WaitAsync(TimeSpan.FromSeconds(20));

            Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
            var health = await first.InspectReportInventoryAsync();
            Assert.Equal(24, health.TotalFiles);
            Assert.Equal(24, health.UniqueEntries);
            Assert.False(health.RequiresReview);
            for (var i = 0; i < 24; i++)
                Assert.NotNull(await second.LoadReportAsync($"SYNTHETIC-ID-{i:D3}"));

            Assert.Empty(Directory.GetFiles(
                Path.Combine(root, "data", "service-reports"), ".cattech-service-*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task IndependentProcesses_ConcurrentSavesRetainEverySyntheticRecord()
    {
        // Child testhosts only run their explicitly filtered worker method.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ChildRootVariable)))
            return;
        Assert.True(OperatingSystem.IsWindows(), "Interprocess test requires Windows CI.");

        var root = NewRoot();
        Process? first = null;
        Process? second = null;
        try
        {
            first = StartWorker(root, "A");
            second = StartWorker(root, "B");
            var outFirst = first.StandardOutput.ReadToEndAsync();
            var errFirst = first.StandardError.ReadToEndAsync();
            var outSecond = second.StandardOutput.ReadToEndAsync();
            var errSecond = second.StandardError.ReadToEndAsync();

            var watch = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(root, "ready-A"))
                || !File.Exists(Path.Combine(root, "ready-B")))
            {
                Assert.False(first.HasExited || second.HasExited,
                    "One child exited before both processes were ready.");
                if (watch.Elapsed > TimeSpan.FromSeconds(30))
                    throw new TimeoutException("Synthetic worker readiness timed out.");
                await Task.Delay(40);
            }
            await File.WriteAllTextAsync(Path.Combine(root, "start.signal"), "go");

            await Task.WhenAll(
                first.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90)),
                second.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90)));

            var firstStdout = await outFirst;
            var firstStderr = await errFirst;
            var secondStdout = await outSecond;
            var secondStderr = await errSecond;
            Assert.True(first.ExitCode == 0,
                $"Synthetic writer A failed: {first.ExitCode} {firstStdout} {firstStderr}");
            Assert.True(second.ExitCode == 0,
                $"Synthetic writer B failed: {second.ExitCode} {secondStdout} {secondStderr}");

            var service = new JsonServiceReportService(root);
            var inventory = await service.InspectReportInventoryAsync();
            Assert.Equal(RecordsPerChild * 2, inventory.TotalFiles);
            Assert.Equal(RecordsPerChild * 2, inventory.UniqueEntries);
            Assert.False(inventory.RequiresReview);
            var listed = await service.ListReportsAsync(RecordsPerChild * 2);
            Assert.Equal(RecordsPerChild * 2, listed.Count);
            for (var index = 0; index < RecordsPerChild; index++)
            {
                Assert.NotNull(await service.LoadReportAsync($"STORE-A-{index:D3}"));
                Assert.NotNull(await service.LoadReportAsync($"STORE-B-{index:D3}"));
            }
            Assert.Empty(Directory.GetFiles(
                Path.Combine(root, "data", "service-reports"), ".cattech-service-*.tmp"));
        }
        finally
        {
            foreach (var child in new[] { first, second })
            {
                if (child is null)
                    continue;
                try
                {
                    if (!child.HasExited)
                        child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (InvalidOperationException) { }
                catch (TimeoutException) { }
                finally { child.Dispose(); }
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChildWorker_SaveSyntheticServiceRecords_WhenActivated()
    {
        var root = Environment.GetEnvironmentVariable(ChildRootVariable);
        if (string.IsNullOrWhiteSpace(root))
            return;
        var worker = Environment.GetEnvironmentVariable(ChildIdVariable);
        Assert.True(worker is "A" or "B");
        await File.WriteAllTextAsync(Path.Combine(root, $"ready-{worker}"), "ready");

        var watch = Stopwatch.StartNew();
        while (!File.Exists(Path.Combine(root, "start.signal")))
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(30))
                throw new TimeoutException("Parent worker handshake timed out.");
            await Task.Delay(30);
        }

        var service = new JsonServiceReportService(root);
        for (var i = 0; i < RecordsPerChild; i++)
        {
            await service.SaveReportAsync(new ServiceReport
            {
                Id = $"STORE-{worker}-{i:D3}",
                Client = new ClientInfo { Name = $"Synthetic-{worker}-{i:D3}" }
            });
        }
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-store-process-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string StoreMarkerPath(string root) =>
        Path.Combine(root, "data", "service-reports", ".cattech-service-reports.lock");

    private static Process StartWorker(string root, string worker)
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
        start.ArgumentList.Add(typeof(ServiceReportInterprocessTests).Assembly.Location);
        start.ArgumentList.Add(
            "--TestCaseFilter:FullyQualifiedName~ChildWorker_SaveSyntheticServiceRecords_WhenActivated");
        start.Environment[ChildRootVariable] = root;
        start.Environment[ChildIdVariable] = worker;
        return Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start synthetic worker.");
    }
}
