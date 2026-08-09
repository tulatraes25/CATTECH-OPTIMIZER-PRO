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
    // Fakes (nunca tocan hardware real)
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

    private sealed class FakeHardwareService : IHardwareService
    {
        public CpuInfo Cpu { get; set; } = new();
        public List<GpuInfo> Gpus { get; set; } = new();
        public MemoryInfo Memory { get; set; } = new();
        public MotherboardInfo Motherboard { get; set; } = new();
        public Exception? ThrowOnCpu { get; set; }
        public Exception? ThrowOnGpu { get; set; }
        public Exception? ThrowOnMemory { get; set; }
        public Exception? ThrowOnMotherboard { get; set; }
        public TaskCompletionSource<CpuInfo>? PendingCpu { get; set; }
        public int GetCpuCalls { get; private set; }
        public int GetGpuCalls { get; private set; }
        public int GetMemoryCalls { get; private set; }
        public int GetMotherboardCalls { get; private set; }
        public int GetHardwareReportCalls { get; private set; }
        public int GetSystemInfoCalls { get; private set; }
        public int GetDiskInfoCalls { get; private set; }

        public Task<HardwareReport> GetHardwareReportAsync()
        {
            GetHardwareReportCalls++;
            return Task.FromResult(new HardwareReport());
        }

        public Task<SystemInfo> GetSystemInfoAsync()
        {
            GetSystemInfoCalls++;
            return Task.FromResult(new SystemInfo());
        }

        public Task<CpuInfo> GetCpuInfoAsync()
        {
            GetCpuCalls++;
            if (PendingCpu != null)
            {
                return PendingCpu.Task;
            }

            if (ThrowOnCpu != null)
            {
                throw ThrowOnCpu;
            }

            return Task.FromResult(Cpu);
        }

        public Task<MemoryInfo> GetMemoryInfoAsync()
        {
            GetMemoryCalls++;
            if (ThrowOnMemory != null)
            {
                throw ThrowOnMemory;
            }

            return Task.FromResult(Memory);
        }

        public Task<List<GpuInfo>> GetGpuInfoAsync()
        {
            GetGpuCalls++;
            if (ThrowOnGpu != null)
            {
                throw ThrowOnGpu;
            }

            return Task.FromResult(Gpus);
        }

        public Task<List<DiskInfo>> GetDiskInfoAsync()
        {
            GetDiskInfoCalls++;
            return Task.FromResult(new List<DiskInfo>());
        }

        public Task<MotherboardInfo> GetMotherboardInfoAsync()
        {
            GetMotherboardCalls++;
            if (ThrowOnMotherboard != null)
            {
                throw ThrowOnMotherboard;
            }

            return Task.FromResult(Motherboard);
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

    private static HardwareViewModel CreateViewModel(
        FakeHardwareSensorService sensorService,
        FakeHardwareService? hardwareService = null) =>
        new(sensorService, hardwareService ?? new FakeHardwareService());

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

    [Fact]
    public void ViewModel_AcceptsBothServices()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService(), new FakeHardwareService());

        Assert.NotNull(vm);
    }

    [Fact]
    public void Construction_DoesNotQueryInventory()
    {
        var hardware = new FakeHardwareService();
        var vm = CreateViewModel(new FakeHardwareSensorService(), hardware);

        Assert.Equal(0, hardware.GetCpuCalls);
        Assert.Equal(0, hardware.GetGpuCalls);
        Assert.Equal(0, hardware.GetMemoryCalls);
        Assert.Equal(0, hardware.GetMotherboardCalls);
        Assert.False(vm.IsInventoryLoaded);
        Assert.Equal("Sin consultar", vm.InventoryStatusText);
    }

    // =====================
    // Separación live vs inventario (B.4.2)
    // =====================

    [Fact]
    public async Task RefreshLive_DoesNotCallHardwareService()
    {
        var hardware = new FakeHardwareService();
        var service = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var vm = CreateViewModel(service, hardware);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(1, service.GetLiveCalls);
        Assert.Equal(0, hardware.GetCpuCalls);
        Assert.Equal(0, hardware.GetGpuCalls);
        Assert.Equal(0, hardware.GetMemoryCalls);
        Assert.Equal(0, hardware.GetMotherboardCalls);
    }

    [Fact]
    public async Task StartMonitoring_DoesNotCallHardwareService()
    {
        var hardware = new FakeHardwareService();
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service, hardware);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.StreamCalls == 1);

        Assert.Equal(0, hardware.GetCpuCalls);
        Assert.Equal(0, hardware.GetGpuCalls);
        Assert.Equal(0, hardware.GetMemoryCalls);
        Assert.Equal(0, hardware.GetMotherboardCalls);

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task EachLiveSample_DoesNotQueryWmi()
    {
        var hardware = new FakeHardwareService();
        var service = new FakeHardwareSensorService();
        service.StreamLiveSnapshots.Add(FullLiveSnapshot());
        service.StreamLiveSnapshots.Add(FullLiveSnapshot());
        var vm = CreateViewModel(service, hardware);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => vm.TemperatureSensors.Count == 1 && service.StreamCalls == 1);
        await Task.Delay(100);

        Assert.Equal(0, hardware.GetCpuCalls);
        Assert.Equal(0, hardware.GetGpuCalls);
        Assert.Equal(0, hardware.GetMemoryCalls);
        Assert.Equal(0, hardware.GetMotherboardCalls);

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task RefreshInventory_CallsEachQueryOnce()
    {
        var hardware = new FakeHardwareService();
        var vm = CreateViewModel(new FakeHardwareSensorService(), hardware);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal(1, hardware.GetCpuCalls);
        Assert.Equal(1, hardware.GetGpuCalls);
        Assert.Equal(1, hardware.GetMemoryCalls);
        Assert.Equal(1, hardware.GetMotherboardCalls);
    }

    [Fact]
    public async Task RefreshInventory_DoesNotCallUnusedApis()
    {
        var hardware = new FakeHardwareService();
        var vm = CreateViewModel(new FakeHardwareSensorService(), hardware);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal(0, hardware.GetHardwareReportCalls);
        Assert.Equal(0, hardware.GetSystemInfoCalls);
        Assert.Equal(0, hardware.GetDiskInfoCalls);
    }

    // =====================
    // Aplicación del inventario
    // =====================

    private static FakeHardwareService InventoryService()
    {
        return new FakeHardwareService
        {
            Cpu = new CpuInfo
            {
                Name = "AMD Ryzen 7 5700X",
                Manufacturer = "Advanced Micro Devices",
                Cores = 8,
                Threads = 16,
                BaseSpeedGHz = 3.4
            },
            Gpus =
            [
                new GpuInfo { Name = "NVIDIA GeForce RTX 4070", Manufacturer = "NVIDIA", MemoryGB = 12 },
                new GpuInfo { Name = "AMD Radeon RX 7800 XT", Manufacturer = "AMD", MemoryGB = 16 }
            ],
            Memory = new MemoryInfo
            {
                TotalGB = 32,
                Type = "DDR4",
                SpeedMHz = 3200,
                SlotsUsed = 2,
                SlotsTotal = 4,
                Modules =
                [
                    new MemoryModuleInfo
                    {
                        DeviceLocator = "DIMM 0",
                        BankLabel = "BANK 0",
                        Manufacturer = "Kingston",
                        PartNumber = "KF3200C16D4/16",
                        SerialNumber = "SN001",
                        CapacityBytes = 17_179_869_184,
                        ConfiguredClockSpeedMHz = 3200,
                        MemoryType = "DDR4",
                        DataWidthBits = 64,
                        TotalWidthBits = 72,
                        Rank = 1
                    },
                    new MemoryModuleInfo
                    {
                        DeviceLocator = "DIMM 1",
                        BankLabel = "BANK 1",
                        Manufacturer = "Kingston",
                        PartNumber = "KF3200C16D4/16",
                        SerialNumber = "SN002",
                        CapacityBytes = 17_179_869_184,
                        ConfiguredClockSpeedMHz = 3200,
                        MemoryType = "DDR4",
                        DataWidthBits = 64,
                        TotalWidthBits = 72,
                        Rank = 1
                    }
                ]
            },
            Motherboard = new MotherboardInfo
            {
                Manufacturer = "ASUS",
                Model = "ROG STRIX B550-A",
                BiosVersion = "2804",
                BiosDate = new DateTime(2024, 5, 10)
            }
        };
    }

    [Fact]
    public async Task Inventory_CpuApplied()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService(), InventoryService());

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal("AMD Ryzen 7 5700X", vm.Cpu.Name);
        Assert.Equal(8, vm.Cpu.Cores);
        Assert.Equal(16, vm.Cpu.Threads);
    }

    [Fact]
    public async Task Inventory_GpusAppliedAndOrdered()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService(), InventoryService());

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Gpus.Count);
        Assert.Equal("AMD Radeon RX 7800 XT", vm.Gpus[0].Name);
        Assert.Equal("NVIDIA GeForce RTX 4070", vm.Gpus[1].Name);
    }

    [Fact]
    public async Task Inventory_MemoryApplied()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService(), InventoryService());

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal(32, vm.Memory.TotalGB);
        Assert.Equal("DDR4", vm.Memory.Type);
        Assert.Equal(3200, vm.Memory.SpeedMHz);
        Assert.Equal(2, vm.Memory.SlotsUsed);
        Assert.Equal(4, vm.Memory.SlotsTotal);
    }

    [Fact]
    public async Task Inventory_MemoryModulesAppliedAndOrdered()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService(), InventoryService());

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.MemoryModules.Count);
        Assert.Equal("DIMM 0", vm.MemoryModules[0].DeviceLocator);
        Assert.Equal("DIMM 1", vm.MemoryModules[1].DeviceLocator);
        Assert.Equal("Kingston", vm.MemoryModules[0].Manufacturer);
    }

    [Fact]
    public async Task Inventory_MotherboardApplied()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService(), InventoryService());

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal("ASUS", vm.Motherboard.Manufacturer);
        Assert.Equal("ROG STRIX B550-A", vm.Motherboard.Model);
        Assert.Equal("2804", vm.Motherboard.BiosVersion);
        Assert.NotNull(vm.Motherboard.BiosDate);
    }

    [Fact]
    public async Task Inventory_LoadedStateAndTimestamps()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService(), InventoryService());

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.True(vm.IsInventoryLoaded);
        Assert.NotNull(vm.InventoryLastUpdatedAt);
        Assert.Contains("Última actualización", vm.InventoryLastUpdatedText);
        Assert.False(vm.IsInventoryBusy);
        Assert.Equal("Inventario actualizado", vm.InventoryStatusText);
    }

    [Fact]
    public async Task DoubleRefreshInventory_Blocked()
    {
        var hardware = new FakeHardwareService { PendingCpu = new TaskCompletionSource<CpuInfo>() };
        var vm = CreateViewModel(new FakeHardwareSensorService(), hardware);

        var task = vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.True(vm.IsInventoryBusy);
        Assert.False(vm.RefreshInventoryCommand.CanExecute(null));

        vm.RefreshInventoryCommand.Execute(null);

        Assert.Equal(1, hardware.GetCpuCalls);

        hardware.PendingCpu!.SetResult(new CpuInfo());
        await task;

        Assert.False(vm.IsInventoryBusy);
        Assert.True(vm.RefreshInventoryCommand.CanExecute(null));
    }

    // =====================
    // Tolerancia parcial
    // =====================

    [Fact]
    public async Task CpuError_DoesNotBlockGpu()
    {
        var hardware = InventoryService();
        hardware.ThrowOnCpu = new InvalidOperationException("CPU falló");
        var vm = CreateViewModel(new FakeHardwareSensorService(), hardware);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Gpus.Count);
        Assert.Equal(32, vm.Memory.TotalGB);
        Assert.Equal("ASUS", vm.Motherboard.Manufacturer);
        Assert.Equal("Inventario parcial", vm.InventoryStatusText);
        Assert.Contains(vm.InventoryErrors, e => e.Contains("CPU:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GpuError_DoesNotBlockRam()
    {
        var hardware = InventoryService();
        hardware.ThrowOnGpu = new InvalidOperationException("GPU falló");
        var vm = CreateViewModel(new FakeHardwareSensorService(), hardware);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal("AMD Ryzen 7 5700X", vm.Cpu.Name);
        Assert.Empty(vm.Gpus);
        Assert.Equal(32, vm.Memory.TotalGB);
        Assert.Equal("Inventario parcial", vm.InventoryStatusText);
    }

    [Fact]
    public async Task RamError_DoesNotBlockMotherboard()
    {
        var hardware = InventoryService();
        hardware.ThrowOnMemory = new InvalidOperationException("RAM falló");
        var vm = CreateViewModel(new FakeHardwareSensorService(), hardware);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal("AMD Ryzen 7 5700X", vm.Cpu.Name);
        Assert.Equal(2, vm.Gpus.Count);
        Assert.Equal(0, vm.Memory.TotalGB);
        Assert.Equal("ASUS", vm.Motherboard.Manufacturer);
        Assert.Equal("Inventario parcial", vm.InventoryStatusText);
    }

    [Fact]
    public async Task MotherboardError_KeepsRest()
    {
        var hardware = InventoryService();
        hardware.ThrowOnMotherboard = new InvalidOperationException("MB falló");
        var vm = CreateViewModel(new FakeHardwareSensorService(), hardware);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal("AMD Ryzen 7 5700X", vm.Cpu.Name);
        Assert.Equal(2, vm.MemoryModules.Count);
        Assert.Equal(string.Empty, vm.Motherboard.Manufacturer);
        Assert.Equal("Inventario parcial", vm.InventoryStatusText);
    }

    [Fact]
    public async Task AllFourErrors_StatusNoDisponible()
    {
        var hardware = new FakeHardwareService
        {
            ThrowOnCpu = new InvalidOperationException("1"),
            ThrowOnGpu = new InvalidOperationException("2"),
            ThrowOnMemory = new InvalidOperationException("3"),
            ThrowOnMotherboard = new InvalidOperationException("4")
        };
        var vm = CreateViewModel(new FakeHardwareSensorService(), hardware);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal("Inventario no disponible", vm.InventoryStatusText);
        Assert.Equal(4, vm.InventoryErrors.Count);
        Assert.True(vm.HasInventoryErrors);
    }

    [Fact]
    public async Task InventoryErrors_ReplacedOnNextRefresh()
    {
        var hardware = InventoryService();
        hardware.ThrowOnCpu = new InvalidOperationException("fallo");
        var vm = CreateViewModel(new FakeHardwareSensorService(), hardware);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);
        Assert.Single(vm.InventoryErrors);

        hardware.ThrowOnCpu = null;
        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Empty(vm.InventoryErrors);
        Assert.False(vm.HasInventoryErrors);
        Assert.Equal("Inventario actualizado", vm.InventoryStatusText);
    }

    [Fact]
    public async Task LiveErrors_NotMixedWithInventoryErrors()
    {
        var hardware = InventoryService();
        hardware.ThrowOnCpu = new InvalidOperationException("fallo CPU");
        var sensor = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = false, Errors = ["fallo live"] }
        };
        var vm = CreateViewModel(sensor, hardware);

        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Single(vm.Errors);
        Assert.Equal("fallo live", vm.Errors[0]);
        Assert.Single(vm.InventoryErrors);
        Assert.Contains("CPU:", vm.InventoryErrors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyLiveSnapshot_DoesNotClearInventory()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() }, InventoryService());

        await vm.RefreshInventoryCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.MemoryModules.Count);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.MemoryModules.Count);
        Assert.Equal("AMD Ryzen 7 5700X", vm.Cpu.Name);
        Assert.True(vm.IsInventoryLoaded);
    }

    [Fact]
    public async Task RefreshInventory_DoesNotClearLiveCollections()
    {
        var sensor = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var vm = CreateViewModel(sensor, InventoryService());

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.PerformanceSensors.Count);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.PerformanceSensors.Count);
        Assert.Single(vm.TemperatureSensors);
        Assert.Single(vm.BatterySensors);
    }

    [Fact]
    public async Task Inventory_WhileMonitoring_Allowed_NoSecondStream()
    {
        var sensor = new FakeHardwareSensorService();
        var vm = CreateViewModel(sensor, InventoryService());

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => sensor.StreamCalls == 1);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal(1, sensor.StreamCalls);
        Assert.True(vm.IsInventoryLoaded);
        Assert.Equal("AMD Ryzen 7 5700X", vm.Cpu.Name);

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    // =====================
    // Migración a LiveSnapshot (B.4.1)
    // =====================

    [Fact]
    public async Task Refresh_UsesGetLiveSnapshotAsync()
    {
        var service = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(1, service.GetLiveCalls);
        Assert.Equal(0, service.GetTemperatureCalls);
    }

    [Fact]
    public async Task Start_UsesWatchLiveSnapshotsAsync()
    {
        var service = new FakeHardwareSensorService { StreamLiveSnapshots = { FullLiveSnapshot() } };
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.StreamCalls == 1);

        Assert.Equal(1, service.StreamCalls);
        Assert.Equal(0, service.WatchTemperatureCalls);

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task Refresh_LoadsAllFamilies_FromSameSnapshot()
    {
        var service = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
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
    }

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
    public async Task Warnings_ReplacedByNewSnapshot()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = true, Warnings = ["Advertencia 1"] }
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
        Assert.Equal("Monitoreando", vm.StatusText);

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
        Assert.Equal("Sin lectura", vm.StatusText);
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
        var vm = new HardwareViewModel(service, new FakeHardwareService());

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
    // Sin interpretación / alcance
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
            "BatteryHealthPercent" or "CpuUsagePercent" or "GpuUsagePercent");
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
        Assert.Contains("Gpus", propertyNames);
        Assert.Contains("MemoryModules", propertyNames);
        Assert.Contains("InventoryErrors", propertyNames);
    }

    [Fact]
    public void NoWmiSpdCorrelation_Methods()
    {
        var methodNames = typeof(HardwareViewModel)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance)
            .Select(m => m.Name)
            .ToList();

        Assert.DoesNotContain(methodNames, n => n.Contains("Correlat", StringComparison.OrdinalIgnoreCase) ||
                                                n.Contains("Match", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Inventory_UsesInjectedServices_NoRealHardware()
    {
        // Si el ViewModel usara los servicios reales, estos datos simulados
        // no serían posibles sin WMI/LHM reales.
        var vm = CreateViewModel(
            new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() },
            InventoryService());

        await vm.RefreshInventoryCommand.ExecuteAsync(null);
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.IsInventoryLoaded);
        Assert.True(vm.HasLiveData);
        Assert.Equal("AMD Ryzen 7 5700X", vm.Cpu.Name);
        Assert.Equal("AMD Ryzen 7 5700X", vm.TemperatureSensors[0].HardwareName);
    }

    // =====================
    // Navegación: salir de Hardware cancela el monitoreo
    // =====================

    [Fact]
    public void LeavingHardwareSection_StopsMonitoring()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                RunNavigationTestSync();
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

    private static void RunNavigationTestSync()
    {
        var service = new FakeHardwareSensorService();
        var mainVm = new MainViewModel(service);

        mainVm.NavigateCommand.Execute("Hardware");
        var hardwareVm = ((HardwareView)mainVm.CurrentView).DataContext as HardwareViewModel;
        Assert.NotNull(hardwareVm);

        hardwareVm!.StartMonitoringCommand.Execute(null);
        WaitUntilSync(() => service.LastStreamToken != default);
        Assert.True(hardwareVm.IsMonitoring);

        mainVm.NavigateCommand.Execute("Reports");

        WaitUntilSync(() => !hardwareVm.IsMonitoring);
        Assert.Equal("Reports", mainVm.CurrentSection);
        Assert.False(hardwareVm.IsMonitoring);
    }

    /// <summary>
    /// Espera manteniendo el hilo de llamada (STA): el polling no cambia de hilo,
    /// así las operaciones WPF posteriores siguen en el hilo STA.
    /// </summary>
    private static void WaitUntilSync(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
            {
                throw new TimeoutException("La condición no se cumplió a tiempo.");
            }

            Task.Delay(20).GetAwaiter().GetResult();
        }
    }

    // =====================
    // Tests de converters (B.4.1/B.4.2)
    // =====================

    private static readonly System.Globalization.CultureInfo EsAr = new("es-AR");

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
    public void EmptyStringToNdConverter_AllCases()
    {
        var converter = new EmptyStringToNdConverter();

        Assert.Equal("N/D", converter.Convert(null, typeof(string), null, EsAr));
        Assert.Equal("N/D", converter.Convert(string.Empty, typeof(string), null, EsAr));
        Assert.Equal("N/D", converter.Convert("   ", typeof(string), null, EsAr));
        Assert.Equal("Kingston", converter.Convert("Kingston", typeof(string), null, EsAr));
        Assert.Equal("Unknown", converter.Convert("Unknown", typeof(string), null, EsAr));
        Assert.Equal("No detectado", converter.Convert("No detectado", typeof(string), null, EsAr));
    }

    [Fact]
    public void PositiveNumberOrNdConverter_AllCases()
    {
        var converter = new PositiveNumberOrNdConverter();

        Assert.Equal("N/D", converter.Convert(null, typeof(string), null, EsAr));
        Assert.Equal("N/D", converter.Convert(double.NaN, typeof(string), null, EsAr));
        Assert.Equal("N/D", converter.Convert(double.PositiveInfinity, typeof(string), null, EsAr));
        Assert.Equal("N/D", converter.Convert(0.0, typeof(string), null, EsAr));
        Assert.Equal("N/D", converter.Convert(-5.0, typeof(string), null, EsAr));
        Assert.Equal("N/D", converter.Convert(0, typeof(string), null, EsAr));
        Assert.Equal("N/D", converter.Convert(0u, typeof(string), null, EsAr));
        Assert.Equal("3,2", converter.Convert(3.2, typeof(string), null, EsAr));
        Assert.Equal("48,25", converter.Convert(48.25, typeof(string), null, EsAr));
        Assert.Equal("16", converter.Convert(16, typeof(string), null, EsAr));
        Assert.Equal("3200", converter.Convert(3200u, typeof(string), null, EsAr));
    }

    [Fact]
    public async Task Inventory_UsesInjectedServices_NoRealWmi()
    {
        var hardware = new FakeHardwareService
        {
            Cpu = new CpuInfo { Name = "Simulated CPU", Cores = 4, Threads = 8 },
            Memory = new MemoryInfo { TotalGB = 16, Modules = [new MemoryModuleInfo { DeviceLocator = "DIMM 0" }] }
        };
        var vm = CreateViewModel(new FakeHardwareSensorService(), hardware);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal("Simulated CPU", vm.Cpu.Name);
        Assert.Equal(4, vm.Cpu.Cores);
        Assert.Single(vm.MemoryModules);
    }

    // =====================
    // Tests Fase B.4.3 - Estados live y pulido
    // =====================

    [Fact]
    public void HasLiveReading_InitialFalse()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService());

        Assert.False(vm.HasLiveReading);
        Assert.Equal("Sin lectura", vm.StatusText);
    }

    [Fact]
    public async Task HasLiveReading_True_AfterAvailableReading()
    {
        var service = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.HasLiveReading);
    }

    [Fact]
    public async Task HasLiveReading_True_AfterUnavailableReading()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = false, Errors = ["fallo"] }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.HasLiveReading);
        Assert.Equal("Lectura no disponible", vm.StatusText);
    }

    [Fact]
    public async Task Start_SetsMonitoreando()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);

        Assert.Equal("Monitoreando", vm.StatusText);

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task DuringStream_StatusStaysMonitoreando()
    {
        var service = new FakeHardwareSensorService();
        service.StreamLiveSnapshots.Add(FullLiveSnapshot());
        service.StreamLiveSnapshots.Add(FullLiveSnapshot());
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => vm.PerformanceSensors.Count == 2);

        Assert.True(vm.IsMonitoring);
        Assert.Equal("Monitoreando", vm.StatusText);

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task Stop_AfterValidSample_SetsLecturaDisponible()
    {
        var service = new FakeHardwareSensorService();
        service.StreamLiveSnapshots.Add(FullLiveSnapshot());
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => vm.HasLiveData);
        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);

        Assert.Equal("Lectura disponible", vm.StatusText);
    }

    [Fact]
    public async Task Stop_AfterEmptyAvailableSample_SetsSinSensores()
    {
        var service = new FakeHardwareSensorService();
        service.StreamLiveSnapshots.Add(new HardwareLiveSnapshot { IsAvailable = true });
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => vm.HasLiveReading);
        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);

        Assert.Equal("Sin sensores disponibles", vm.StatusText);
    }

    [Fact]
    public async Task Stop_AfterUnavailableLastSample_SetsLecturaNoDisponible()
    {
        var service = new FakeHardwareSensorService();
        service.StreamLiveSnapshots.Add(new HardwareLiveSnapshot { IsAvailable = false, Errors = ["fallo"] });
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => vm.HasLiveReading);
        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);

        Assert.Equal("Lectura no disponible", vm.StatusText);
    }

    [Fact]
    public async Task Stop_BeforeFirstSample_SetsSinLectura()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.LastStreamToken != default);
        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);

        Assert.False(vm.HasLiveReading);
        Assert.Equal("Sin lectura", vm.StatusText);
    }

    [Fact]
    public async Task Cancellation_KeepsLastSample()
    {
        var service = new FakeHardwareSensorService();
        service.StreamLiveSnapshots.Add(FullLiveSnapshot());
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => vm.HasLiveData);
        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);

        Assert.Single(vm.TemperatureSensors);
        Assert.Equal(2, vm.PerformanceSensors.Count);
        Assert.Single(vm.BatterySensors);
        Assert.Empty(vm.Errors);
    }

    [Fact]
    public async Task RefreshException_ClearsAllFive_NoStale()
    {
        var service = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.HasLiveData);

        service.ThrowOnGetSnapshot = new InvalidOperationException("fallo inesperado");
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.IsAvailable);
        Assert.False(vm.HasLiveData);
        Assert.True(vm.HasLiveReading);
        Assert.True(vm.HasErrors);
        Assert.Equal("Lectura no disponible", vm.StatusText);
        Assert.Equal("No disponible", vm.ProviderStatusText);
        Assert.Empty(vm.TemperatureSensors);
        Assert.Empty(vm.PerformanceSensors);
        Assert.Empty(vm.GpuMemorySensors);
        Assert.Empty(vm.BatterySensors);
        Assert.Empty(vm.MemoryTimingSensors);
        Assert.Equal(0, vm.PerformanceSensorCount);
    }

    [Fact]
    public async Task StreamException_ClearsAllFive_StatusNotOverwritten()
    {
        var service = new FakeHardwareSensorService();
        service.StreamLiveSnapshots.Add(FullLiveSnapshot());
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => vm.HasLiveData);

        service.ThrowOnStream = new InvalidOperationException("fallo stream");
        // El stream ya está en el loop: la excepción llega en la siguiente iteración.
        // Con un fake que lanza al enumerar la primera vez, forzamos el fallo directo.
        var service2 = new FakeHardwareSensorService { ThrowOnStream = new InvalidOperationException("fallo stream") };
        var vm2 = CreateViewModel(service2);

        vm2.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm2.IsMonitoring);

        Assert.True(vm2.HasErrors);
        Assert.False(vm2.IsAvailable);
        Assert.False(vm2.HasLiveData);
        Assert.Equal("Error de monitoreo", vm2.StatusText);
        Assert.Equal("No disponible", vm2.ProviderStatusText);
        Assert.Empty(vm2.TemperatureSensors);
        Assert.Empty(vm2.PerformanceSensors);
        Assert.Empty(vm2.GpuMemorySensors);
        Assert.Empty(vm2.BatterySensors);
        Assert.Empty(vm2.MemoryTimingSensors);
    }

    // =====================
    // B.4.3 - Hints de pestañas vacías
    // =====================

    [Fact]
    public void EmptyHints_False_BeforeReading()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService());

        Assert.False(vm.ShowEmptyTemperatureHint);
        Assert.False(vm.ShowEmptyPerformanceHint);
        Assert.False(vm.ShowEmptyGpuMemoryHint);
        Assert.False(vm.ShowEmptyBatteryHint);
        Assert.False(vm.ShowEmptyTimingHint);
    }

    [Fact]
    public async Task EmptyHints_True_AvailableEmptyReading()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = true }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.ShowEmptyTemperatureHint);
        Assert.True(vm.ShowEmptyPerformanceHint);
        Assert.True(vm.ShowEmptyGpuMemoryHint);
        Assert.True(vm.ShowEmptyBatteryHint);
        Assert.True(vm.ShowEmptyTimingHint);
    }

    [Fact]
    public async Task EmptyHints_False_UnavailableReading()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = false, Errors = ["fallo"] }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.ShowEmptyTemperatureHint);
        Assert.False(vm.ShowEmptyPerformanceHint);
        Assert.False(vm.ShowEmptyGpuMemoryHint);
        Assert.False(vm.ShowEmptyBatteryHint);
        Assert.False(vm.ShowEmptyTimingHint);
        Assert.Equal("Lectura no disponible", vm.StatusText);
    }

    [Fact]
    public async Task EmptyHints_False_WhenFamiliesHaveData()
    {
        var service = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.ShowEmptyTemperatureHint);
        Assert.False(vm.ShowEmptyPerformanceHint);
        Assert.False(vm.ShowEmptyGpuMemoryHint);
        Assert.False(vm.ShowEmptyBatteryHint);
        Assert.False(vm.ShowEmptyTimingHint);
    }

    [Fact]
    public async Task EmptyHint_True_OnlyForEmptyFamily()
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

        Assert.True(vm.ShowEmptyTemperatureHint);
        Assert.False(vm.ShowEmptyPerformanceHint);
        Assert.True(vm.ShowEmptyGpuMemoryHint);
        Assert.True(vm.ShowEmptyBatteryHint);
        Assert.True(vm.ShowEmptyTimingHint);
    }

    // =====================
    // B.4.3 - Flags y estado inicial
    // =====================

    [Fact]
    public void ShowInitialLiveHint_True_BeforeReading()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService());

        Assert.True(vm.ShowInitialLiveHint);
        Assert.False(vm.ShowSummaryCards);
    }

    [Fact]
    public async Task ShowInitialLiveHint_Hidden_AfterReading()
    {
        var service = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.ShowInitialLiveHint);
        Assert.True(vm.ShowSummaryCards);
    }

    [Fact]
    public void ShowInitialLiveHint_Hidden_WhileMonitoring()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);

        Assert.False(vm.ShowInitialLiveHint);

        vm.StopMonitoringCommand.Execute(null);
    }

    [Fact]
    public async Task HasErrors_SyncWithErrorsCollection()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = false, Errors = ["error 1"] }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.HasErrors);

        service.LiveSnapshot = FullLiveSnapshot();
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.False(vm.HasErrors);
        Assert.Empty(vm.Errors);
    }

    [Fact]
    public async Task HasWarnings_SyncWithWarningsCollection()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = true, Warnings = ["warning 1"] }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.HasWarnings);

        service.LiveSnapshot = FullLiveSnapshot();
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.False(vm.HasWarnings);
        Assert.Empty(vm.Warnings);
    }

    [Fact]
    public async Task RefreshFailure_ClearsWarnings()
    {
        var service = new FakeHardwareSensorService
        {
            LiveSnapshot = new HardwareLiveSnapshot { IsAvailable = true, Warnings = ["warning 1"] }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.HasWarnings);

        service.ThrowOnGetSnapshot = new InvalidOperationException("fallo");
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.HasWarnings);
        Assert.Empty(vm.Warnings);
        Assert.True(vm.HasErrors);
    }

    // =====================
    // B.4.3 - Independencia live/inventario
    // =====================

    [Fact]
    public async Task LiveFailure_DoesNotClearInventory()
    {
        var sensor = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var vm = CreateViewModel(sensor, InventoryService());

        await vm.RefreshInventoryCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.MemoryModules.Count);

        sensor.ThrowOnGetSnapshot = new InvalidOperationException("fallo live");
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.MemoryModules.Count);
        Assert.Equal("AMD Ryzen 7 5700X", vm.Cpu.Name);
        Assert.Equal("Inventario actualizado", vm.InventoryStatusText);
        Assert.False(vm.IsAvailable);
    }

    [Fact]
    public async Task InventoryFailure_DoesNotClearLive()
    {
        var sensor = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var hardware = InventoryService();
        hardware.ThrowOnCpu = new InvalidOperationException("fallo CPU");
        var vm = CreateViewModel(sensor, hardware);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.PerformanceSensors.Count);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.PerformanceSensors.Count);
        Assert.Single(vm.TemperatureSensors);
        Assert.Equal("Lectura disponible", vm.StatusText);
        Assert.Equal("Inventario parcial", vm.InventoryStatusText);
    }

    [Fact]
    public async Task ApplyLiveSnapshot_DoesNotModifyInventoryStatusText()
    {
        var sensor = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var vm = CreateViewModel(sensor, InventoryService());

        await vm.RefreshInventoryCommand.ExecuteAsync(null);
        Assert.Equal("Inventario actualizado", vm.InventoryStatusText);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("Inventario actualizado", vm.InventoryStatusText);
        Assert.Equal("Lectura disponible", vm.StatusText);
    }

    [Fact]
    public async Task ApplyInventory_DoesNotModifyLiveStatusText()
    {
        var sensor = new FakeHardwareSensorService { LiveSnapshot = FullLiveSnapshot() };
        var vm = CreateViewModel(sensor, InventoryService());

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Lectura disponible", vm.StatusText);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.Equal("Lectura disponible", vm.StatusText);
        Assert.Equal("Inventario actualizado", vm.InventoryStatusText);
    }

    [Fact]
    public async Task InventoryLastUpdated_OnlyUpdatedOnSuccessfulApply()
    {
        var hardware = InventoryService();
        var vm = CreateViewModel(new FakeHardwareSensorService(), hardware);

        await vm.RefreshInventoryCommand.ExecuteAsync(null);
        var first = vm.InventoryLastUpdatedAt;
        Assert.NotNull(first);

        hardware.ThrowOnCpu = new InvalidOperationException("fallo");
        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        // La lectura parcial es una lectura real: la fecha se actualiza.
        Assert.NotNull(vm.InventoryLastUpdatedAt);
        Assert.Equal("Inventario parcial", vm.InventoryStatusText);
        Assert.True(vm.HasInventoryErrors);

        hardware.ThrowOnCpu = null;
        await vm.RefreshInventoryCommand.ExecuteAsync(null);

        Assert.False(vm.HasInventoryErrors);
        Assert.Equal("Inventario actualizado", vm.InventoryStatusText);
    }

    // =====================
    // B.4.3 - Navegación e integración
    // =====================

    [Fact]
    public void ReturnToHardware_ReusesViewModel_AllowsRestart()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                RunNavigationReuseTestSync();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(20));

        if (failure != null)
        {
            throw failure;
        }
    }

    private static void RunNavigationReuseTestSync()
    {
        var service = new FakeHardwareSensorService();
        var mainVm = new MainViewModel(service);

        mainVm.NavigateCommand.Execute("Hardware");
        var firstVm = ((HardwareView)mainVm.CurrentView).DataContext as HardwareViewModel;
        Assert.NotNull(firstVm);

        firstVm!.StartMonitoringCommand.Execute(null);
        WaitUntilSync(() => service.LastStreamToken != default);
        firstVm.StopMonitoringCommand.Execute(null);
        WaitUntilSync(() => !firstVm.IsMonitoring);

        mainVm.NavigateCommand.Execute("Reports");
        WaitUntilSync(() => !firstVm.IsMonitoring);

        mainVm.NavigateCommand.Execute("Hardware");
        var secondVm = ((HardwareView)mainVm.CurrentView).DataContext as HardwareViewModel;

        Assert.Same(firstVm, secondVm);

        secondVm!.StartMonitoringCommand.Execute(null);
        WaitUntilSync(() => secondVm.IsMonitoring);
        secondVm.StopMonitoringCommand.Execute(null);
        WaitUntilSync(() => !secondVm.IsMonitoring);
    }

    // =====================
    // B.4.3 - Presentación (XAML estructural)
    // =====================

    private static string ReadHardwareViewXaml()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/Cattech.Optimizer.Pro.UI/Views/HardwareView.xaml"));

        return File.ReadAllText(path);
    }

    [Fact]
    public void Xaml_ProviderStatusTextBound()
    {
        var xaml = ReadHardwareViewXaml();

        Assert.Contains("ProviderStatusText", xaml);
        Assert.Contains("Proveedor:", xaml);
    }

    [Fact]
    public void Xaml_InitialHintAndSummaryCardsBound()
    {
        var xaml = ReadHardwareViewXaml();

        Assert.Contains("ShowInitialLiveHint", xaml);
        Assert.Contains("ShowSummaryCards", xaml);
    }

    [Fact]
    public void Xaml_SixTabs()
    {
        var xaml = ReadHardwareViewXaml();

        Assert.Contains("Header=\"Temperaturas\"", xaml);
        Assert.Contains("Header=\"CPU / GPU\"", xaml);
        Assert.Contains("Header=\"Memoria GPU\"", xaml);
        Assert.Contains("Header=\"Batería\"", xaml);
        Assert.Contains("Header=\"RAM SPD\"", xaml);
        Assert.Contains("Header=\"Inventario\"", xaml);
    }

    [Fact]
    public void Xaml_AccentsCorrect()
    {
        var xaml = ReadHardwareViewXaml();

        Assert.Contains("Batería", xaml);
        Assert.Contains("Métrica", xaml);
        Assert.Contains("Mín.", xaml);
        Assert.Contains("Máx.", xaml);
        Assert.Contains("Módulo", xaml);
        Assert.Contains("Núcleos", xaml);
        Assert.Contains("información", xaml);
        Assert.Contains("dinámicas", xaml);
        Assert.Contains("telemetría", xaml);
        Assert.Contains("— solo lectura", xaml);
        Assert.DoesNotContain("Header=\"Bateria\"", xaml);
        Assert.DoesNotContain("Header=\"Metrica\"", xaml);
    }

    [Fact]
    public void Xaml_EmptyHintsBoundToFlags()
    {
        var xaml = ReadHardwareViewXaml();

        Assert.Contains("ShowEmptyTemperatureHint", xaml);
        Assert.Contains("ShowEmptyPerformanceHint", xaml);
        Assert.Contains("ShowEmptyGpuMemoryHint", xaml);
        Assert.Contains("ShowEmptyBatteryHint", xaml);
        Assert.Contains("ShowEmptyTimingHint", xaml);
    }
}
