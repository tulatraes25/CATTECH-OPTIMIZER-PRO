using System.Diagnostics;
using System.Runtime.CompilerServices;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Hardware;
using Cattech.Optimizer.Pro.UI.Converters;
using Cattech.Optimizer.Pro.UI.ViewModels;
using Cattech.Optimizer.Pro.UI.Views;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class HardwareViewModelTests
{
    // =====================
    // Fake de IHardwareSensorService (nunca toca hardware real)
    // =====================

    private sealed class FakeHardwareSensorService : IHardwareSensorService
    {
        public HardwareTemperatureSnapshot NextSnapshot { get; set; } = new();
        public HardwareLiveSnapshot? LiveSnapshot { get; set; }
        public Exception? ThrowOnGetSnapshot { get; set; }
        public List<HardwareLiveSnapshot> StreamLiveSnapshots { get; } = new();
        public bool StreamEndsImmediately { get; set; }
        public Exception? ThrowOnStream { get; set; }
        public int StreamCalls { get; private set; }
        public int WatchTemperatureCalls { get; private set; }
        public int GetTemperatureCalls { get; private set; }
        public int GetLiveCalls { get; private set; }
        public TimeSpan? LastInterval { get; private set; }
        public CancellationToken LastStreamToken { get; private set; }

        public Task<HardwareTemperatureSnapshot> GetTemperatureSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            GetTemperatureCalls++;
            if (ThrowOnGetSnapshot != null)
            {
                throw ThrowOnGetSnapshot;
            }

            return Task.FromResult(NextSnapshot);
        }

        public Task<HardwareLiveSnapshot> GetLiveSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            GetLiveCalls++;
            if (ThrowOnGetSnapshot != null)
            {
                throw ThrowOnGetSnapshot;
            }

            if (LiveSnapshot != null)
            {
                return Task.FromResult(LiveSnapshot);
            }

            return Task.FromResult(new HardwareLiveSnapshot
            {
                CapturedAt = NextSnapshot.CapturedAt,
                IsAvailable = NextSnapshot.IsAvailable,
                IsElevated = NextSnapshot.IsElevated,
                TemperatureSensors = NextSnapshot.Sensors,
                Warnings = NextSnapshot.Warnings,
                Errors = NextSnapshot.Errors
            });
        }

        public async IAsyncEnumerable<HardwareTemperatureSnapshot> WatchTemperatureSnapshotsAsync(
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            WatchTemperatureCalls++;
            yield break;
        }

        public async IAsyncEnumerable<HardwareLiveSnapshot> WatchLiveSnapshotsAsync(
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamCalls++;
            LastInterval = interval;
            LastStreamToken = cancellationToken;

            if (ThrowOnStream != null)
            {
                throw ThrowOnStream;
            }

            foreach (var snapshot in StreamLiveSnapshots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return snapshot;
            }

            if (StreamEndsImmediately)
            {
                yield break;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10, cancellationToken);
            }
        }
    }

    private sealed class PendingSnapshotService : IHardwareSensorService
    {
        public TaskCompletionSource<HardwareLiveSnapshot> Tcs { get; } = new();

        public Task<HardwareLiveSnapshot> GetLiveSnapshotAsync(
            CancellationToken cancellationToken = default) => Tcs.Task;

        public Task<HardwareTemperatureSnapshot> GetTemperatureSnapshotAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new HardwareTemperatureSnapshot());

        public async IAsyncEnumerable<HardwareTemperatureSnapshot> WatchTemperatureSnapshotsAsync(
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield break;
        }

        public async IAsyncEnumerable<HardwareLiveSnapshot> WatchLiveSnapshotsAsync(
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield break;
        }
    }

    // =====================
    // Helpers
    // =====================

    private static HardwareTemperatureSensor Sensor(string type, string hardware, string name,
        double? value = null, double? min = null, double? max = null)
    {
        return new HardwareTemperatureSensor
        {
            HardwareType = type,
            HardwareName = hardware,
            SensorName = name,
            SensorIdentifier = $"{hardware}/{name}",
            ValueCelsius = value,
            MinCelsius = min,
            MaxCelsius = max
        };
    }

    private static HardwarePerformanceSensor Perf(string type, string hardware, string name,
        HardwarePerformanceMetricType metric, double? value = null)
    {
        return new HardwarePerformanceSensor
        {
            HardwareType = type,
            HardwareName = hardware,
            SensorName = name,
            SensorIdentifier = $"{hardware}/{name}",
            MetricType = metric,
            Value = value,
            Unit = metric == HardwarePerformanceMetricType.Load ? "%" : "MHz"
        };
    }

    private static HardwareGpuMemorySensor GpuMem(string hardware, string name, double? value = null)
    {
        return new HardwareGpuMemorySensor
        {
            HardwareName = hardware,
            HardwareType = "GPU",
            SensorName = name,
            SensorIdentifier = $"{hardware}/{name}",
            ValueMB = value
        };
    }

    private static HardwareBatterySensor Battery(string hardware, string name,
        HardwareBatteryMetricType metric, double? value = null)
    {
        return new HardwareBatterySensor
        {
            HardwareName = hardware,
            HardwareType = "Batería",
            SensorName = name,
            SensorIdentifier = $"{hardware}/{name}",
            MetricType = metric,
            Value = value,
            Unit = metric switch
            {
                HardwareBatteryMetricType.Level => "%",
                HardwareBatteryMetricType.Energy => "mWh",
                HardwareBatteryMetricType.Voltage => "V",
                HardwareBatteryMetricType.Current => "A",
                HardwareBatteryMetricType.Power => "W",
                _ => "s"
            }
        };
    }

    private static HardwareMemoryTimingSensor Timing(string hardware, string name, double? value = null)
    {
        return new HardwareMemoryTimingSensor
        {
            HardwareName = hardware,
            HardwareIdentifier = "/mem/dimm/0",
            HardwareType = "Memoria",
            SensorName = name,
            SensorIdentifier = $"{hardware}/{name}",
            ValueNanoseconds = value,
            Unit = "ns"
        };
    }

    private static HardwareLiveSnapshot FullLiveSnapshot()
    {
        return new HardwareLiveSnapshot
        {
            IsAvailable = true,
            IsElevated = true,
            TemperatureSensors = [Sensor("CPU", "AMD Ryzen 7 5700X", "Package", 48.2)],
            PerformanceSensors =
            [
                Perf("CPU", "AMD Ryzen 7 5700X", "CPU Total", HardwarePerformanceMetricType.Load, 35.5),
                Perf("CPU", "AMD Ryzen 7 5700X", "CPU Clock", HardwarePerformanceMetricType.Clock, 3700)
            ],
            GpuMemorySensors = [GpuMem("NVIDIA RTX 4070", "GPU Memory Used", 6144)],
            BatterySensors = [Battery("Standard Battery", "Charge Level", HardwareBatteryMetricType.Level, 85)],
            MemoryTimingSensors = [Timing("DDR4-3200 DIMM", "tAA (CAS Latency Time)", 14.0)]
        };
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
            {
                throw new TimeoutException("La condición no se cumplió a tiempo.");
            }

            await Task.Delay(20);
        }
    }

    private static HardwareViewModel CreateViewModel(FakeHardwareSensorService service) => new(service);

    // =====================
    // Estado inicial
    // =====================

    [Fact]
    public void InitialState_NotMonitoring()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService());

        Assert.False(vm.IsMonitoring);
        Assert.False(vm.IsBusy);
        Assert.False(vm.IsAvailable);
        Assert.False(vm.HasSensors);
        Assert.Equal("Sin lectura", vm.StatusText);
        Assert.Null(vm.LastUpdatedAt);
    }

    [Fact]
    public void InitialState_NoInventedSensors()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService());

        Assert.Empty(vm.TemperatureSensors);
        Assert.Empty(vm.PerformanceSensors);
        Assert.Empty(vm.GpuMemorySensors);
        Assert.Empty(vm.BatterySensors);
        Assert.Empty(vm.MemoryTimingSensors);
        Assert.Equal(0, vm.ValidSensorCount);
        Assert.Equal("0", vm.SensorCountText);
        Assert.False(vm.HasLiveData);
    }

    // =====================
    // Migración a LiveSnapshot (B.4.1)
    // =====================

    [Fact]
    public async Task Refresh_UsesGetLiveSnapshotAsync()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = FullLiveSnapshot()
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(1, service.GetLiveCalls);
        Assert.Equal(0, service.GetTemperatureCalls);
    }

    [Fact]
    public async Task Start_UsesWatchLiveSnapshotsAsync()
    {
        var service = new FakeHardwareSensorService
        {
            StreamLiveSnapshots = { FullLiveSnapshot() }
        };
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.StreamCalls == 1);

        Assert.Equal(1, service.StreamCalls);
        Assert.Equal(0, service.WatchTemperatureCalls);

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    // =====================
    // Actualización única
    // =====================

    [Fact]
    public async Task Refresh_LoadsAvailableSnapshot()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = true, IsElevated = true }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.IsAvailable);
        Assert.True(vm.IsElevated);
        Assert.Equal("Disponible", vm.ProviderStatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Refresh_LoadsAllFamilies_FromSameSnapshot()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = FullLiveSnapshot()
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Single(vm.TemperatureSensors);
        Assert.Equal(2, vm.PerformanceSensors.Count);
        Assert.Single(vm.GpuMemorySensors);
        Assert.Single(vm.BatterySensors);
        Assert.Single(vm.MemoryTimingSensors);
        Assert.True(vm.HasLiveData);
        Assert.Equal("Lectura disponible", vm.StatusText);
    }

    [Fact]
    public async Task Refresh_OrdersTemperaturesStable()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                TemperatureSensors =
                [
                    Sensor("Almacenamiento", "Samsung SSD 980 PRO", "Temperature", 37.0),
                    Sensor("CPU", "AMD Ryzen 7 5700X", "Core (Tctl/Tdie)", 48.2),
                    Sensor("CPU", "AMD Ryzen 7 5700X", "Package", 50.1),
                    Sensor("GPU", "NVIDIA RTX 4070", "GPU Core", 45.0)
                ]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        var names = vm.TemperatureSensors.Select(s => $"{s.HardwareType}|{s.HardwareName}|{s.SensorName}").ToList();
        Assert.Equal(
        [
            "Almacenamiento|Samsung SSD 980 PRO|Temperature",
            "CPU|AMD Ryzen 7 5700X|Core (Tctl/Tdie)",
            "CPU|AMD Ryzen 7 5700X|Package",
            "GPU|NVIDIA RTX 4070|GPU Core"
        ], names);
    }

    [Fact]
    public async Task Refresh_OrdersPerformanceStable()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                PerformanceSensors =
                [
                    Perf("GPU", "NVIDIA RTX 4070", "GPU Core", HardwarePerformanceMetricType.Load, 42.0),
                    Perf("CPU", "AMD Ryzen 7 5700X", "CPU Clock", HardwarePerformanceMetricType.Clock, 3700),
                    Perf("CPU", "AMD Ryzen 7 5700X", "CPU Total", HardwarePerformanceMetricType.Load, 35.5)
                ]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        var names = vm.PerformanceSensors
            .Select(s => $"{s.HardwareType}|{s.HardwareName}|{s.MetricType}|{s.SensorName}")
            .ToList();
        Assert.Equal(
        [
            "CPU|AMD Ryzen 7 5700X|Load|CPU Total",
            "CPU|AMD Ryzen 7 5700X|Clock|CPU Clock",
            "GPU|NVIDIA RTX 4070|Load|GPU Core"
        ], names);
    }

    [Fact]
    public async Task Refresh_OrdersGpuMemoryStable()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                GpuMemorySensors =
                [
                    GpuMem("AMD Radeon RX 7800 XT", "GPU Memory Used", 8192),
                    GpuMem("NVIDIA RTX 4070", "GPU Memory Total", 12288),
                    GpuMem("NVIDIA RTX 4070", "GPU Memory Used", 6144)
                ]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        var names = vm.GpuMemorySensors.Select(s => $"{s.HardwareName}|{s.SensorName}").ToList();
        Assert.Equal(
        [
            "AMD Radeon RX 7800 XT|GPU Memory Used",
            "NVIDIA RTX 4070|GPU Memory Total",
            "NVIDIA RTX 4070|GPU Memory Used"
        ], names);
    }

    [Fact]
    public async Task Refresh_OrdersBatteryStable()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                BatterySensors =
                [
                    Battery("Battery B", "Charge Level", HardwareBatteryMetricType.Level, 60),
                    Battery("Battery A", "Voltage", HardwareBatteryMetricType.Voltage, 11.4),
                    Battery("Battery A", "Charge Level", HardwareBatteryMetricType.Level, 80)
                ]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        var names = vm.BatterySensors
            .Select(s => $"{s.HardwareName}|{s.MetricType}|{s.SensorName}")
            .ToList();
        Assert.Equal(
        [
            "Battery A|Level|Charge Level",
            "Battery A|Voltage|Voltage",
            "Battery B|Level|Charge Level"
        ], names);
    }

    [Fact]
    public async Task Refresh_OrdersTimingStable()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                MemoryTimingSensors =
                [
                    Timing("DDR4-3200 DIMM", "tRCD (RAS to CAS Delay Time)", 16.0),
                    Timing("DDR4-3200 DIMM", "tAA (CAS Latency Time)", 14.0)
                ]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        var names = vm.MemoryTimingSensors.Select(s => $"{s.HardwareName}|{s.SensorName}").ToList();
        Assert.Equal(
        [
            "DDR4-3200 DIMM|tAA (CAS Latency Time)",
            "DDR4-3200 DIMM|tRCD (RAS to CAS Delay Time)"
        ], names);
    }

    [Fact]
    public async Task NextSnapshot_ClearsAllPreviousFamilies()
    {
        var service = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.HasLiveData);

        service.LiveSnapshot = new HardwareLiveSnapshot
        {
            IsAvailable = true,
            TemperatureSensors = [Sensor("CPU", "AMD Ryzen 7 5700X", "Package", 51.0)]
        };
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Single(vm.TemperatureSensors);
        Assert.Empty(vm.PerformanceSensors);
        Assert.Empty(vm.GpuMemorySensors);
        Assert.Empty(vm.BatterySensors);
        Assert.Empty(vm.MemoryTimingSensors);
    }

    [Fact]
    public async Task UnavailableSnapshot_LeavesAllFiveEmpty()
    {
        var service = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        service.LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = false, Errors = ["fallo"] };
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.IsAvailable);
        Assert.Empty(vm.TemperatureSensors);
        Assert.Empty(vm.PerformanceSensors);
        Assert.Empty(vm.GpuMemorySensors);
        Assert.Empty(vm.BatterySensors);
        Assert.Empty(vm.MemoryTimingSensors);
        Assert.False(vm.HasLiveData);
    }

    [Fact]
    public async Task AvailableAfterUnavailable_RecoversAll()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = false, Errors = ["fallo"] }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.False(vm.IsAvailable);

        service.LiveSnapshot = FullLiveSnapshot();
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.IsAvailable);
        Assert.True(vm.HasLiveData);
        Assert.Single(vm.TemperatureSensors);
        Assert.Equal(2, vm.PerformanceSensors.Count);
        Assert.Single(vm.GpuMemorySensors);
        Assert.Single(vm.BatterySensors);
        Assert.Single(vm.MemoryTimingSensors);
    }

    // =====================
    // Estado global y contadores
    // =====================

    [Fact]
    public async Task HasSensors_StillTemperatureSpecific()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                PerformanceSensors = [Perf("CPU", "AMD Ryzen 7 5700X", "CPU Total", HardwarePerformanceMetricType.Load, 35.5)]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.HasSensors);
        Assert.Equal(0, vm.ValidSensorCount);
        Assert.True(vm.HasLiveData);
    }

    [Fact]
    public async Task HasLiveData_True_OnlyPerformance()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                PerformanceSensors = [Perf("CPU", "AMD Ryzen 7 5700X", "CPU Total", HardwarePerformanceMetricType.Load, 35.5)]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.HasLiveData);
    }

    [Fact]
    public async Task HasLiveData_True_OnlyBattery()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                BatterySensors = [Battery("Standard Battery", "Charge Level", HardwareBatteryMetricType.Level, 85)]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.HasLiveData);
    }

    [Fact]
    public async Task HasLiveData_True_OnlyTiming()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                MemoryTimingSensors = [Timing("DDR4-3200 DIMM", "tAA (CAS Latency Time)", 14.0)]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.HasLiveData);
    }

    [Fact]
    public async Task HasLiveData_False_AllEmpty()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = true }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.HasLiveData);
        Assert.Equal("Sin sensores disponibles", vm.StatusText);
    }

    [Fact]
    public async Task Counters_PerFamily_Correct()
    {
        var service = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.PerformanceSensorCount);
        Assert.Equal(2, vm.ValidPerformanceSensorCount);
        Assert.Equal(1, vm.GpuMemorySensorCount);
        Assert.Equal(1, vm.ValidGpuMemorySensorCount);
        Assert.Equal(1, vm.BatterySensorCount);
        Assert.Equal(1, vm.ValidBatterySensorCount);
        Assert.Equal(1, vm.MemoryTimingSensorCount);
        Assert.Equal(1, vm.ValidMemoryTimingSensorCount);
    }

    [Fact]
    public async Task Counters_WithNullValues_ValidCountsZero()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                PerformanceSensors = [Perf("CPU", "AMD Ryzen 7 5700X", "CPU Total", HardwarePerformanceMetricType.Load, null)],
                GpuMemorySensors = [GpuMem("NVIDIA RTX 4070", "GPU Memory Used", null)],
                BatterySensors = [Battery("Standard Battery", "Charge Level", HardwareBatteryMetricType.Level, null)],
                MemoryTimingSensors = [Timing("DDR4-3200 DIMM", "tAA (CAS Latency Time)", null)]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.PerformanceSensorCount);
        Assert.Equal(0, vm.ValidPerformanceSensorCount);
        Assert.Equal(0, vm.ValidGpuMemorySensorCount);
        Assert.Equal(0, vm.ValidBatterySensorCount);
        Assert.Equal(0, vm.ValidMemoryTimingSensorCount);
    }

    [Fact]
    public async Task Warnings_ReplacedByNewSnapshot()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                Warnings = ["Advertencia 1"]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Single(vm.Warnings);

        service.LiveSnapshot = new HardwareLiveSnapshot
        {
            IsAvailable = true,
            Warnings = ["Advertencia 2", "Advertencia 3"]
        };
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Warnings.Count);
        Assert.DoesNotContain(vm.Warnings, w => w == "Advertencia 1");
    }

    [Fact]
    public async Task Errors_ReplacedByNewSnapshot()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = false, Errors = ["Error 1"] }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Single(vm.Errors);

        service.LiveSnapshot = FullLiveSnapshot();
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(vm.Errors);
        Assert.True(vm.IsAvailable);
    }

    [Fact]
    public async Task EmptyBatteryAndTiming_NoError()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                PerformanceSensors = [Perf("CPU", "AMD Ryzen 7 5700X", "CPU Total", HardwarePerformanceMetricType.Load, 35.5)]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(vm.BatterySensors);
        Assert.Empty(vm.MemoryTimingSensors);
        Assert.Empty(vm.Errors);
        Assert.Equal("Lectura disponible", vm.StatusText);
    }

    [Fact]
    public async Task EmptySensors_NeutralMessage()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = true }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("Sin sensores disponibles", vm.StatusText);
        Assert.DoesNotContain("correctas", vm.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sano", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TimeSpanBattery_StillInSeconds()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                BatterySensors = [Battery("Standard Battery", "Remaining Time (Estimated)", HardwareBatteryMetricType.TimeSpan, 7200)]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(7200.0, vm.BatterySensors[0].Value);
        Assert.Equal("s", vm.BatterySensors[0].Unit);
    }

    [Fact]
    public async Task Timing_StillInNanoseconds()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot
            {
                IsAvailable = true,
                MemoryTimingSensors = [Timing("DDR4-3200 DIMM", "tAA (CAS Latency Time)", 14.0)]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(14.0, vm.MemoryTimingSensors[0].ValueNanoseconds);
        Assert.Equal("ns", vm.MemoryTimingSensors[0].Unit);
        Assert.Equal("tAA (CAS Latency Time)", vm.MemoryTimingSensors[0].SensorName);
    }

    // =====================
    // Monitoreo en tiempo real
    // =====================

    [Fact]
    public async Task StartMonitoring_UsesTwoSecondInterval()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.LastInterval.HasValue);

        Assert.Equal(TimeSpan.FromSeconds(2), service.LastInterval);

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task StartMonitoring_AppliesMultipleSnapshots()
    {
        var service = new FakeHardwareSensorService();
        service.StreamLiveSnapshots.Add(FullLiveSnapshot());
        service.StreamLiveSnapshots.Add(new HardwareLiveSnapshot
        {
            IsAvailable = true,
            TemperatureSensors = [Sensor("CPU", "AMD Ryzen 7 5700X", "Package", 51.0)]
        });
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => vm.TemperatureSensors.Count == 1 && vm.TemperatureSensors[0].ValueCelsius == 51.0);

        Assert.Equal(51.0, vm.TemperatureSensors[0].ValueCelsius);
        Assert.Empty(vm.PerformanceSensors);
        Assert.Equal("Lectura disponible", vm.StatusText);

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task StartMonitoring_NoSecondStream()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.StreamCalls == 1);

        vm.StartMonitoringCommand.Execute(null);
        await Task.Delay(100);

        Assert.Equal(1, service.StreamCalls);

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task StopMonitoring_CancelsStream()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.LastStreamToken != default);

        vm.StopMonitoringCommand.Execute(null);

        await WaitUntilAsync(() => service.LastStreamToken.IsCancellationRequested);
    }

    [Fact]
    public async Task StopMonitoring_Idempotent()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.LastStreamToken != default);

        vm.StopMonitoringCommand.Execute(null);
        vm.StopMonitoringCommand.Execute(null);
        vm.StopMonitoringCommand.Execute(null);

        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task NormalCancellation_NoErrorAdded()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.LastStreamToken != default);
        vm.StopMonitoringCommand.Execute(null);

        await WaitUntilAsync(() => !vm.IsMonitoring);

        Assert.Empty(vm.Errors);
        Assert.NotEqual("Error de monitoreo", vm.StatusText);
    }

    [Fact]
    public async Task UnexpectedException_ControlledError()
    {
        var service = new FakeHardwareSensorService
        {
            ThrowOnStream = new InvalidOperationException("fallo simulado")
        };
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => vm.Errors.Count == 1);

        Assert.Equal("Error de monitoreo", vm.StatusText);
        Assert.Contains("fallo simulado", vm.Errors[0], StringComparison.OrdinalIgnoreCase);

        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task IsMonitoring_ReturnsFalseAfterStreamEnds()
    {
        var service = new FakeHardwareSensorService
        {
            StreamEndsImmediately = true
        };
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);

        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task RefreshBlocked_WhileMonitoring()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => vm.IsMonitoring);

        Assert.False(vm.RefreshCommand.CanExecute(null));

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
        Assert.True(vm.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task StartBlocked_WhileRefreshActive()
    {
        var service = new PendingSnapshotService();
        var vm = new HardwareViewModel(service);

        var refreshTask = vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.IsBusy);
        Assert.False(vm.StartMonitoringCommand.CanExecute(null));
        Assert.False(vm.RefreshCommand.CanExecute(null));

        service.Tcs.SetResult(FullLiveSnapshot());
        await refreshTask;

        Assert.False(vm.IsBusy);
        Assert.True(vm.StartMonitoringCommand.CanExecute(null));
    }

    // =====================
    // Sin interpretación de salud/rendimiento
    // =====================

    [Fact]
    public void ViewModel_HasNoHealthStateProperties()
    {
        var propertyNames = typeof(HardwareViewModel)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(propertyNames, n => n is "IsHot" or "IsCritical" or "HealthStatus" or
            "Severity" or "Recommendation" or "IsOverloaded" or "IsSlow" or "CL" or "VramUsagePercent" or
            "BatteryHealthPercent");
    }

    [Fact]
    public void ViewModel_ExposesAllLiveCollections()
    {
        var propertyNames = typeof(HardwareViewModel)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.Contains("TemperatureSensors", propertyNames);
        Assert.Contains("PerformanceSensors", propertyNames);
        Assert.Contains("GpuMemorySensors", propertyNames);
        Assert.Contains("BatterySensors", propertyNames);
        Assert.Contains("MemoryTimingSensors", propertyNames);
    }

    [Fact]
    public async Task UsesInjectedService_NoRealHardware()
    {
        // Si el ViewModel usara LibreHardwareSensorService real, estos datos simulados
        // no serían posibles sin tocar el hardware.
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = FullLiveSnapshot()
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("AMD Ryzen 7 5700X", Assert.Single(vm.TemperatureSensors).HardwareName);
        Assert.Equal(48.2, vm.TemperatureSensors[0].ValueCelsius);
    }

    // =====================
    // Navegación: salir de Hardware cancela el monitoreo
    // =====================

    [Fact]
    public async Task LeavingHardwareSection_StopsMonitoring()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                RunNavigationTest().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(15));

        if (failure != null)
        {
            throw failure;
        }
    }

    private static async Task RunNavigationTest()
    {
        var service = new FakeHardwareSensorService();
        var mainVm = new MainViewModel(service);

        mainVm.NavigateCommand.Execute("Hardware");
        var hardwareVm = ((HardwareView)mainVm.CurrentView).DataContext as HardwareViewModel;
        Assert.NotNull(hardwareVm);

        hardwareVm!.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.LastStreamToken != default);
        Assert.True(hardwareVm.IsMonitoring);

        mainVm.NavigateCommand.Execute("Reports");

        await WaitUntilAsync(() => !hardwareVm.IsMonitoring);
        Assert.Equal("Reports", mainVm.CurrentSection);
        Assert.False(hardwareVm.IsMonitoring);
    }

    // =====================
    // Tests de converters (B.4.1)
    // =====================

    private static readonly System.Globalization.CultureInfo EsAr =
        new("es-AR");

    [Fact]
    public void NullableNumberConverter_NullToND()
    {
        var converter = new NullableNumberConverter();

        Assert.Equal("N/D", converter.Convert(null, typeof(string), null, EsAr));
    }

    [Fact]
    public void NullableNumberConverter_ZeroToZero()
    {
        var converter = new NullableNumberConverter();

        Assert.Equal("0", converter.Convert(0.0, typeof(string), null, EsAr));
    }

    [Fact]
    public void NullableNumberConverter_WholeNumber()
    {
        var converter = new NullableNumberConverter();

        Assert.Equal("14", converter.Convert(14.0, typeof(string), null, EsAr));
    }

    [Fact]
    public void NullableNumberConverter_DecimalFormatByCulture()
    {
        var converter = new NullableNumberConverter();

        Assert.Equal("48,25", converter.Convert(48.25, typeof(string), null, EsAr));
    }

    [Fact]
    public void NullableNumberConverter_NaNToND()
    {
        var converter = new NullableNumberConverter();

        Assert.Equal("N/D", converter.Convert(double.NaN, typeof(string), null, EsAr));
    }

    [Fact]
    public void NullableNumberConverter_InfinityToND()
    {
        var converter = new NullableNumberConverter();

        Assert.Equal("N/D", converter.Convert(double.PositiveInfinity, typeof(string), null, EsAr));
    }

    [Fact]
    public void PerformanceMetricTypeTextConverter_AllValues()
    {
        var converter = new PerformanceMetricTypeTextConverter();

        Assert.Equal("Carga", converter.Convert(HardwarePerformanceMetricType.Load, typeof(string), null, EsAr));
        Assert.Equal("Frecuencia", converter.Convert(HardwarePerformanceMetricType.Clock, typeof(string), null, EsAr));
    }

    [Fact]
    public void BatteryMetricTypeTextConverter_AllValues()
    {
        var converter = new BatteryMetricTypeTextConverter();

        Assert.Equal("Nivel", converter.Convert(HardwareBatteryMetricType.Level, typeof(string), null, EsAr));
        Assert.Equal("Energía", converter.Convert(HardwareBatteryMetricType.Energy, typeof(string), null, EsAr));
        Assert.Equal("Voltaje", converter.Convert(HardwareBatteryMetricType.Voltage, typeof(string), null, EsAr));
        Assert.Equal("Corriente", converter.Convert(HardwareBatteryMetricType.Current, typeof(string), null, EsAr));
        Assert.Equal("Potencia", converter.Convert(HardwareBatteryMetricType.Power, typeof(string), null, EsAr));
        Assert.Equal("Tiempo", converter.Convert(HardwareBatteryMetricType.TimeSpan, typeof(string), null, EsAr));
    }

    [Fact]
    public void NullableNumberConverter_DoesNotInferBySensorName()
    {
        // El converter genérico no agrega unidades ni interpreta nombres.
        var converter = new NullableNumberConverter();

        Assert.Equal("6144", converter.Convert(6144.0, typeof(string), null, EsAr));
        Assert.DoesNotContain("MB", (string)converter.Convert(6144.0, typeof(string), null, EsAr));
    }
}
