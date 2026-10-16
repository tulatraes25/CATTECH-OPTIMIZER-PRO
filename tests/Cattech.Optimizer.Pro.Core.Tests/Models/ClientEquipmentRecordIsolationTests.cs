using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Core.Models.Hardware;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class ClientEquipmentRecordIsolationTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;

    private sealed class CapturingReportService : IServiceReportService
    {
        public List<ServiceReport> Saved { get; } = new();
        public bool ThrowOnSave { get; set; }
        public string SavedFileName { get; set; } = "fictitious-qa.json";

        public Task<string> SaveReportAsync(ServiceReport report)
        {
            if (ThrowOnSave)
                throw new IOException(@"Synthetic PRIVATE C:\Users\PRIVATE\service.json");
            Saved.Add(report);
            return Task.FromResult(SavedFileName);
        }

        public Task<ServiceReport?> LoadReportAsync(string reportId)
            => Task.FromResult<ServiceReport?>(null);

        public Task<List<ServiceReportSummary>> ListReportsAsync(int maxResults = 50)
            => Task.FromResult(new List<ServiceReportSummary>());

        public Task<bool> DeleteReportAsync(string reportId)
            => Task.FromResult(false);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; private set; } = new();

        public event EventHandler<AppSettings>? SettingsChanged;

        public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(CurrentSettings);

        public Task SaveSettingsAsync(AppSettings settings)
        {
            CurrentSettings = settings;
            SettingsChanged?.Invoke(this, settings);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHardwareService : IHardwareService
    {
        public HardwareReport Latest { get; set; } = CreateHardware();
        public TaskCompletionSource<HardwareReport>? Pending { get; set; }

        public Task<HardwareReport> GetHardwareReportAsync() =>
            Pending?.Task ?? Task.FromResult(Latest);

        public Task<SystemInfo> GetSystemInfoAsync() => Task.FromResult(new SystemInfo());
        public Task<CpuInfo> GetCpuInfoAsync() => Task.FromResult(new CpuInfo());
        public Task<MemoryInfo> GetMemoryInfoAsync() => Task.FromResult(new MemoryInfo());
        public Task<List<GpuInfo>> GetGpuInfoAsync() => Task.FromResult(new List<GpuInfo>());
        public Task<List<DiskInfo>> GetDiskInfoAsync() => Task.FromResult(new List<DiskInfo>());
        public Task<MotherboardInfo> GetMotherboardInfoAsync()
            => Task.FromResult(new MotherboardInfo());
    }

    private static HardwareReport CreateHardware() => new()
    {
        System = new SystemInfo
        {
            OsName = "Synthetic OS A",
            BuildNumber = "QA-123"
        },
        Disks = new()
        {
            new DiskInfo
            {
                Name = "Synthetic A NVMe",
                MediaType = "SSD",
                VolumeAssociationsVerified = true,
                Volumes = new()
                {
                    new DiskVolumeInfo
                    {
                        DriveLetter = "C:",
                        CapacityBytes = 240 * GiB,
                        FreeBytes = 120 * GiB,
                        IsSystemVolume = true
                    }
                }
            }
        }
    };

    private static ClientEquipmentViewModel NewViewModel(
        CapturingReportService storage, FakeHardwareService hardware)
        => new(storage, new FakeSettingsService(), hardware);

    private static void PopulateRequiredFields(ClientEquipmentViewModel vm, string name)
    {
        vm.ClientName = name;
        vm.ServiceReason = "QA regression";
        vm.EquipmentType = "Notebook";
    }

    [Fact]
    public async Task SaveFailure_HidesPrivateStoragePathsAndDoesNotClaimAbsentWrite()
    {
        var storage = new CapturingReportService { ThrowOnSave = true };
        var vm = NewViewModel(storage, new FakeHardwareService());
        PopulateRequiredFields(vm, "Synthetic Client");

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.IsSuccess);
        Assert.Contains("No se pudo confirmar", vm.ErrorMessage);
        Assert.Contains("Revisá Informes", vm.ErrorMessage);
        Assert.DoesNotContain("PRIVATE", vm.ErrorMessage);
        Assert.DoesNotContain("Users", vm.ErrorMessage);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task SaveSuccess_DoesNotEchoUntrustedStorageFileName()
    {
        var storage = new CapturingReportService
        {
            SavedFileName = @"C:\Users\PRIVATE\hidden-client-file.json"
        };
        var vm = NewViewModel(storage, new FakeHardwareService());
        PopulateRequiredFields(vm, "Synthetic Client");

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.True(vm.IsSuccess);
        Assert.Contains("Registro guardado correctamente", vm.StatusMessage);
        Assert.DoesNotContain("PRIVATE", vm.StatusMessage);
        Assert.DoesNotContain("hidden-client-file", vm.StatusMessage);
        Assert.Single(storage.Saved);
    }

    [Fact]
    public async Task NewRecord_RemovesHiddenDiskMetricsBeforeSavingDifferentClient()
    {
        var storage = new CapturingReportService();
        var vm = NewViewModel(storage, new FakeHardwareService());

        PopulateRequiredFields(vm, "Fictitious Client A");
        await vm.DetectHardwareCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        var savedA = Assert.Single(storage.Saved);
        Assert.True(savedA.Equipment.DiskFreeSpaceKnown);
        Assert.Equal(240, savedA.Equipment.DiskCapacityGB);
        Assert.Equal(120, savedA.Equipment.DiskFreeGB);
        Assert.Contains("Synthetic OS A", savedA.Equipment.WindowsVersion);

        vm.NewRecordCommand.Execute(null);
        Assert.False(vm.HasDetectedData);
        Assert.False(vm.IsDetecting);

        PopulateRequiredFields(vm, "Fictitious Client B");
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(2, storage.Saved.Count);
        var savedB = storage.Saved[1];
        Assert.Equal("Fictitious Client B", savedB.Client.Name);
        Assert.False(savedB.Equipment.DiskFreeSpaceKnown);
        Assert.Equal(0, savedB.Equipment.DiskCapacityGB);
        Assert.Equal(0, savedB.Equipment.DiskFreeGB);
        Assert.Equal("", savedB.Equipment.WindowsVersion);
        Assert.Equal("", savedB.Equipment.PrimaryDisk);
        Assert.True(savedA.Equipment.DiskFreeSpaceKnown);
    }

    [Fact]
    public async Task InFlightDetectionOfClientA_CannotPopulateClearedClientB()
    {
        var storage = new CapturingReportService();
        var hardware = new FakeHardwareService
        {
            Pending = new TaskCompletionSource<HardwareReport>(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var vm = NewViewModel(storage, hardware);

        PopulateRequiredFields(vm, "Fictitious Client A");
        var pending = vm.DetectHardwareCommand.ExecuteAsync(null);
        Assert.True(vm.IsDetecting);

        vm.NewRecordCommand.Execute(null);
        PopulateRequiredFields(vm, "Fictitious Client B");

        hardware.Pending.SetResult(CreateHardware());
        await pending;

        Assert.False(vm.IsDetecting);
        Assert.False(vm.HasDetectedData);
        Assert.Equal("", vm.DetectedDisk);
        Assert.Equal("", vm.DetectedOS);
        Assert.Equal("Sin detectar", vm.DetectionStatus);
        Assert.Equal("Fictitious Client B", vm.ClientName);

        await vm.SaveCommand.ExecuteAsync(null);
        var savedB = Assert.Single(storage.Saved);
        Assert.False(savedB.Equipment.DiskFreeSpaceKnown);
        Assert.Equal(0, savedB.Equipment.DiskCapacityGB);
        Assert.Equal("", savedB.Equipment.WindowsVersion);
    }

    [Fact]
    public async Task LateDetectionFailure_DoesNotLeakErrorIntoNewClient()
    {
        var hardware = new FakeHardwareService
        {
            Pending = new TaskCompletionSource<HardwareReport>(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var vm = NewViewModel(new CapturingReportService(), hardware);

        var pending = vm.DetectHardwareCommand.ExecuteAsync(null);
        vm.NewRecordCommand.Execute(null);
        hardware.Pending.SetException(new IOException(
            @"C:\Users\SyntheticPrivateUser\hidden.txt"));

        await pending;

        Assert.False(vm.IsDetecting);
        Assert.False(vm.HasError);
        Assert.Equal("Sin detectar", vm.DetectionStatus);
        Assert.DoesNotContain("SyntheticPrivateUser", vm.ErrorMessage);
    }
    [Fact]
    public async Task ActiveDetectionFailure_IsRedactedForCurrentClient()
    {
        var hardware = new FakeHardwareService
        {
            Pending = new TaskCompletionSource<HardwareReport>(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var vm = NewViewModel(new CapturingReportService(), hardware);

        var pending = vm.DetectHardwareCommand.ExecuteAsync(null);
        hardware.Pending.SetException(new IOException(
            @"C:\Users\SyntheticPrivateUser\hidden.txt"));
        await pending;

        Assert.True(vm.HasError);
        Assert.False(vm.IsDetecting);
        Assert.Equal("Error al detectar", vm.DetectionStatus);
        Assert.Contains("No se pudo completar", vm.ErrorMessage);
        Assert.DoesNotContain("SyntheticPrivateUser", vm.ErrorMessage);
    }

}
