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
            if (!_release.Wait(TimeSpan.FromSeconds(5)))
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
            if (!_release.Wait(TimeSpan.FromSeconds(5)))
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

    [Fact]
    public async Task MemoryWmiProbe_DoesNotBlockCallingThread()
    {
        using var reader = new BlockingMemoryReader();
        var service = new WmiHardwareService(reader);
        var callingThread = Environment.CurrentManagedThreadId;

        // If GetMemoryInfoAsync executes WMI synchronously, this invocation
        // blocks until the synthetic source times out and the test fails.
        var task = service.GetMemoryInfoAsync();
        try
        {
            await reader.Entered.WaitAsync(TimeSpan.FromSeconds(4));
            Assert.False(task.IsCompleted, "Slow WMI must run on a worker.");
            Assert.NotEqual(callingThread, reader.ReadingThreadId);
        }
        finally
        {
            reader.Release();
        }

        var result = await task.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.True(reader.Released);
        Assert.Equal(8d, result.TotalGB);
    }

    [Fact]
    public async Task PhysicalDiskTopologyProbe_DoesNotBlockCallingThread()
    {
        Assert.True(OperatingSystem.IsWindows(), "Target app uses Windows WMI.");
        using var memory = new BlockingMemoryReader();
        using var disks = new BlockingDiskReader();
        var service = new WmiHardwareService(memory, disks);
        var callingThread = Environment.CurrentManagedThreadId;

        var task = service.GetDiskInfoAsync();
        try
        {
            await disks.Entered.WaitAsync(TimeSpan.FromSeconds(4));
            Assert.False(task.IsCompleted, "Slow disk topology must not block WPF.");
            Assert.NotEqual(callingThread, disks.ReadingThreadId);
        }
        finally
        {
            disks.Release();
        }

        var result = await task.WaitAsync(TimeSpan.FromSeconds(4));
        var disk = Assert.Single(result);
        Assert.Equal("Synthetic NVMe", disk.Name);
        Assert.Equal("SSD", disk.MediaType);
    }
}
