using Cattech.Optimizer.Pro.Infrastructure.Hardware;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Fake WMI sources block deliberately. They must never hold up the calling
/// (typically WPF dispatcher) thread before a Task is returned.
/// No actual WMI hardware or privileged operation is used.
/// </summary>
public sealed class WmiAsyncDispatchTests
{
    private sealed class BlockingMemoryReader : IWmiMemoryReader, IDisposable
    {
        private readonly ManualResetEventSlim _release = new(false);
        private readonly TaskCompletionSource<bool> _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;
        public int ReadingThreadId { get; private set; }
        public bool Released { get; private set; }

        public WmiMemorySnapshot Read()
        {
            ReadingThreadId = Environment.CurrentManagedThreadId;
            _entered.TrySetResult(true);
            if (!_release.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Synthetic WMI source not released by test.");
            return new WmiMemorySnapshot
            {
                TotalPhysicalMemoryBytes = 8UL * 1024 * 1024 * 1024
            };
        }

        public void Release()
        {
            Released = true;
            _release.Set();
        }

        public void Dispose() => _release.Dispose();
    }

    private sealed class BlockingDiskReader : IDiskTopologyReader, IDisposable
    {
        private readonly ManualResetEventSlim _release = new(false);
        private readonly TaskCompletionSource<bool> _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;
        public int ReadingThreadId { get; private set; }

        public IReadOnlyList<PhysicalDiskRecord> Read()
        {
            ReadingThreadId = Environment.CurrentManagedThreadId;
            _entered.TrySetResult(true);
            if (!_release.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Synthetic disk topology source not released by test.");

            return new[]
            {
                new PhysicalDiskRecord
                {
                    Name = "Synthetic NVMe",
                    Interface = "NVMe",
                    AssociationsAvailable = true
                }
            };
        }

        public void Release() => _release.Set();
        public void Dispose() => _release.Dispose();
    }

    // Give the UI-like caller its own thread: this checks that dispatch returns
    // before the fake WMI source is released, independently of how quickly
    // xUnit's shared worker pool schedules that source on a busy CI runner.
    private static Task<(int CallerThreadId, Task<T> Probe)> InvokeOnDedicatedCaller<T>(
        Func<Task<T>> startProbe)
    {
        return Task.Factory.StartNew(
            () => (Environment.CurrentManagedThreadId, startProbe()),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    [Fact]
    public async Task MemoryWmiProbe_DoesNotBlockCallingThread()
    {
        using var reader = new BlockingMemoryReader();
        var service = new WmiHardwareService(reader);

        var invocation = InvokeOnDedicatedCaller(service.GetMemoryInfoAsync);
        try
        {
            // An accidental inline Read() hangs this dedicated caller until
            // finally releases the fake; scheduling its worker is a separate gate.
            var (callerThreadId, probe) =
                await invocation.WaitAsync(TimeSpan.FromSeconds(5));

            await reader.Entered.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(probe.IsCompleted, "Slow WMI must run on a worker.");
            Assert.NotEqual(callerThreadId, reader.ReadingThreadId);

            reader.Release();
            var result = await probe.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(reader.Released);
            Assert.Equal(8d, result.TotalGB);
        }
        finally
        {
            reader.Release();
        }
    }

    [Fact]
    public async Task PhysicalDiskTopologyProbe_DoesNotBlockCallingThread()
    {
        Assert.True(OperatingSystem.IsWindows(), "Target app uses Windows WMI.");
        using var memory = new BlockingMemoryReader();
        using var disks = new BlockingDiskReader();
        var service = new WmiHardwareService(memory, disks);

        var invocation = InvokeOnDedicatedCaller(service.GetDiskInfoAsync);
        try
        {
            var (callerThreadId, probe) =
                await invocation.WaitAsync(TimeSpan.FromSeconds(5));

            await disks.Entered.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(probe.IsCompleted, "Slow disk topology must not block WPF.");
            Assert.NotEqual(callerThreadId, disks.ReadingThreadId);

            disks.Release();
            var result = await probe.WaitAsync(TimeSpan.FromSeconds(10));
            var disk = Assert.Single(result);
            Assert.Equal("Synthetic NVMe", disk.Name);
            Assert.Equal("SSD", disk.MediaType);
        }
        finally
        {
            disks.Release();
        }
    }
}
