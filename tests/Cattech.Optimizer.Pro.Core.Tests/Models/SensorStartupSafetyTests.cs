using Cattech.Optimizer.Pro.Infrastructure.Hardware;
using Cattech.Optimizer.Pro.Infrastructure.Hardware.SensorProvider;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// No sensor hardware is opened. These tests check failure containment when a
/// third-party monitor factory returns null or leaks a local path in an error.
/// </summary>
public sealed class SensorStartupSafetyTests
{
    private const string SensitivePath = @"C:\Users\SyntheticPrivateUser\secret.log";

    private sealed class NullFactory : IHardwareMonitorFactory
    {
        public int Calls { get; private set; }
        public IHardwareMonitorSession Create()
        {
            Calls++;
            return null!;
        }
    }

    private sealed class ThrowingFactory : IHardwareMonitorFactory
    {
        public IHardwareMonitorSession Create()
            => throw new InvalidOperationException(SensitivePath);
    }

    [Fact]
    public async Task NullSessionFactory_SingleReadIsUnavailableWithSafeMessage()
    {
        var factory = new NullFactory();
        var service = new LibreHardwareSensorService(factory, isElevated: true);

        var result = await service.GetLiveSnapshotAsync();

        Assert.False(result.IsAvailable);
        Assert.Empty(result.TemperatureSensors);
        Assert.Single(result.Errors);
        Assert.Contains("No se pudo inicializar", result.Errors[0]);
        Assert.DoesNotContain("Object reference", result.Errors[0]);
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public async Task NullSessionFactory_StreamEndsAfterOneUnavailableSnapshot()
    {
        var factory = new NullFactory();
        var service = new LibreHardwareSensorService(factory, isElevated: true);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var samples = new List<Cattech.Optimizer.Pro.Core.Models.Hardware.HardwareLiveSnapshot>();

        await foreach (var item in service.WatchLiveSnapshotsAsync(
            TimeSpan.FromMilliseconds(1), cancellation.Token))
        {
            samples.Add(item);
            if (samples.Count > 1)
                break; // Fails, but never hangs if the original bug reappears.
        }

        Assert.Single(samples);
        Assert.False(samples[0].IsAvailable);
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public async Task ProviderError_DoesNotLeakPrivatePathInSnapshot()
    {
        var service = new LibreHardwareSensorService(
            new ThrowingFactory(), isElevated: false);

        var snapshot = await service.GetLiveSnapshotAsync();

        Assert.False(snapshot.IsAvailable);
        Assert.NotEmpty(snapshot.Errors);
        Assert.DoesNotContain("SyntheticPrivateUser", string.Join(" ", snapshot.Errors));
        Assert.DoesNotContain(SensitivePath, string.Join(" ", snapshot.Errors));
    }

    [Fact]
    public async Task ProviderError_DoesNotLeakPrivatePathInWatchStream()
    {
        var service = new LibreHardwareSensorService(
            new ThrowingFactory(), isElevated: true);
        var samples = new List<Cattech.Optimizer.Pro.Core.Models.Hardware.HardwareLiveSnapshot>();

        await foreach (var item in service.WatchLiveSnapshotsAsync(TimeSpan.FromMilliseconds(1)))
        {
            samples.Add(item);
            if (samples.Count > 1)
                break;
        }

        var item1 = Assert.Single(samples);
        Assert.False(item1.IsAvailable);
        Assert.DoesNotContain("SyntheticPrivateUser", string.Join(" ", item1.Errors));
        Assert.Single(item1.Errors);
    }
}
