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

    // Keep the dedicated WPF-like caller thread ALIVE while the WMI worker
    // is blocked. ManagedThreadId may be reused after a thread terminates:
    // comparing a dead caller's ID to a later worker's ID is flaky and can
    // report a false inline dispatch on busy GitHub Windows runners.
    // Returning the Task before the fake Read() is released remains required.
    private static Task<(int CallerThreadId, Task<T> Probe)> InvokeOnDedicatedCaller<T>(
        Func<Task<T>> startProbe, ManualResetEventSlim callerRelease,
        out Task callerCompletion)
    {
        var returned = new TaskCompletionSource<(int, Task<T>)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        callerCompletion = Task.Factory.StartNew(() =>
        {
            try
            {
                var threadId = Environment.CurrentManagedThreadId;
                var probe = startProbe(); // Must return without executing blocking Read inline.
                returned.TrySetResult((threadId, probe));
            }
            catch (Exception ex)
            {
                returned.TrySetException(ex);
            }

            // While the fake source blocks, the caller ID remains live and
            // cannot be recycled for a worker. Always release in test finally.
            if (!callerRelease.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Synthetic caller release was missed.");
        }, CancellationToken.None,
        TaskCreationOptions.LongRunning, TaskScheduler.Default);
        return returned.Task;
    }

    [Fact]
    public async Task MemoryWmiProbe_DoesNotBlockCallingThread()
    {
        using var reader = new BlockingMemoryReader();
        var service = new WmiHardwareService(reader);

        using var callerRelease = new ManualResetEventSlim(false);
        var invocation = InvokeOnDedicatedCaller(service.GetMemoryInfoAsync,
            callerRelease, out var callerCompletion);
        try
        {
            // An accidental inline Read() hangs this dedicated caller until
            // finally releases the fake; scheduling its worker is a separate gate.
            var (callerThreadId, probe) =
                await invocation.WaitAsync(TimeSpan.FromSeconds(5));

            await reader.Entered.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(probe.IsCompleted, "Slow WMI must run on a worker.");
            Assert.False(callerCompletion.IsCompleted,
                "Caller remains alive until WMI source is explicitly released.");
            Assert.NotEqual(callerThreadId, reader.ReadingThreadId);

            reader.Release();
            var result = await probe.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(reader.Released);
            Assert.Equal(8d, result.TotalGB);
        }
        finally
        {
            reader.Release();
            callerRelease.Set();
            await callerCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task PhysicalDiskTopologyProbe_DoesNotBlockCallingThread()
    {
        Assert.True(OperatingSystem.IsWindows(), "Target app uses Windows WMI.");
        using var memory = new BlockingMemoryReader();
        using var disks = new BlockingDiskReader();
        var service = new WmiHardwareService(memory, disks);

        using var callerRelease = new ManualResetEventSlim(false);
        var invocation = InvokeOnDedicatedCaller(service.GetDiskInfoAsync,
            callerRelease, out var callerCompletion);
        try
        {
            var (callerThreadId, probe) =
                await invocation.WaitAsync(TimeSpan.FromSeconds(5));

            await disks.Entered.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(probe.IsCompleted, "Slow disk topology must not block WPF.");
            Assert.False(callerCompletion.IsCompleted,
                "Caller remains alive until disk reader is explicitly released.");
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
            callerRelease.Set();
            await callerCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
