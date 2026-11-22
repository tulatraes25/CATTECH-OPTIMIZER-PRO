using System.Runtime.CompilerServices;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Core.Models.Hardware;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Infrastructure.Data;
using Cattech.Optimizer.Pro.UI;
using Cattech.Optimizer.Pro.UI.ViewModels;
using Cattech.Optimizer.Pro.UI.Views;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Tests del shell (barra de estado, atajos, editor de smartctl) y de la
/// configuración de herramientas externas. No tocan hardware real, ni el
/// config/herramientas.json del ejecutable (siempre usan directorios temporales),
/// ni ejecutan operaciones destructivas.
/// </summary>
public sealed class ShellUxTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _toolsConfigPath;

    public ShellUxTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cattech_shell_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _toolsConfigPath = Path.Combine(_tempDir, "config", "herramientas.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    // --- Atajos de navegación (Ctrl+0..9) ---

    [Theory]
    [InlineData("0", "Home")]
    [InlineData("1", "ClientEquipment")]
    [InlineData("2", "SmartDisk")]
    [InlineData("3", "Hardware")]
    [InlineData("4", "Diagnostics")]
    [InlineData("5", "DecisionCenterDemo")]
    [InlineData("6", "BlueScreens")]
    [InlineData("7", "RepairCenter")]
    [InlineData("8", "Startup")]
    [InlineData("9", "TempCleanup")]
    public void ShortcutMap_MapsDigitsToSections(string key, string expectedSection)
    {
        Assert.Equal(expectedSection, MainViewModel.ResolveShortcutSection(key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("10")]
    [InlineData("Ctrl+1")]
    public void ShortcutMap_UnknownKey_ReturnsNull(string? key)
    {
        Assert.Null(MainViewModel.ResolveShortcutSection(key));
    }

    // --- Versión y modo de instalación ---

    [Fact]
    public void FormatAppVersion_UsesMajorMinorBuild()
    {
        Assert.Equal("v0.3.1", MainViewModel.FormatAppVersion(new Version(0, 3, 1, 0)));
        Assert.Equal("v2.7", MainViewModel.FormatAppVersion(new Version(2, 7)));
    }

    [Fact]
    public void ResolveInstallationMode_BuildOutput_ReportsDevelopment()
    {
        var path = Path.Combine(_tempDir, "bin", "Release", "net10.0-windows");

        Assert.Equal("Desarrollo (salida de compilación)", MainViewModel.ResolveInstallationMode(path));
    }

    // --- Indicador compacto de smartctl (solo lectura del JSON) ---

    [Fact]
    public void SmartctlIndicator_MissingConfig_ReportsNotConfigured()
    {
        var status = MainViewModel.ResolveSmartctlStatus(_tempDir);

        Assert.Equal("smartctl: sin configurar", status.Text);
        Assert.Equal(SmartctlStatusKind.NotConfigured, status.Kind);
        Assert.Contains("herramientas.json", status.Detail);
    }

    [Fact]
    public void SmartctlIndicator_ExistingConfiguredPath_ReportsOk()
    {
        var smartctlPath = Path.Combine(_tempDir, "tools", "smartctl.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(smartctlPath)!);
        File.WriteAllText(smartctlPath, string.Empty);
        WriteToolsConfig(smartctlPath, autoDetect: false);

        var status = MainViewModel.ResolveSmartctlStatus(_tempDir);

        Assert.Equal("smartctl: OK", status.Text);
        Assert.Equal(SmartctlStatusKind.Available, status.Kind);
        Assert.Contains(smartctlPath, status.Detail);
        Assert.Contains("autodetect: no", status.Detail);
    }

    [Fact]
    public void SmartctlIndicator_ConfiguredPathMissing_ReportsUnavailable()
    {
        WriteToolsConfig(Path.Combine(_tempDir, "no-existe", "smartctl.exe"), autoDetect: true);

        var status = MainViewModel.ResolveSmartctlStatus(_tempDir);

        Assert.Equal("smartctl: no disponible", status.Text);
        Assert.Equal(SmartctlStatusKind.InvalidPath, status.Kind);
    }

    [Fact]
    public void SmartctlIndicator_AutoDetectWithoutPath_ReportsAutoDetect()
    {
        WriteToolsConfig(string.Empty, autoDetect: true);

        var status = MainViewModel.ResolveSmartctlStatus(_tempDir);

        Assert.Equal("smartctl: autodetect", status.Text);
        Assert.Equal(SmartctlStatusKind.AutoDetect, status.Kind);
    }

    [Fact]
    public void SmartctlIndicator_PathEmptyAndAutoDetectOff_ReportsNotConfigured()
    {
        WriteToolsConfig(string.Empty, autoDetect: false);

        var status = MainViewModel.ResolveSmartctlStatus(_tempDir);

        Assert.Equal("smartctl: sin configurar", status.Text);
        Assert.Equal(SmartctlStatusKind.NotConfigured, status.Kind);
    }

    [Fact]
    public void SmartctlIndicator_InvalidJson_ReportsInvalidConfig()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_toolsConfigPath)!);
        File.WriteAllText(_toolsConfigPath, "{ roto");

        var status = MainViewModel.ResolveSmartctlStatus(_tempDir);

        Assert.Equal("smartctl: config inválida", status.Text);
        Assert.Equal(SmartctlStatusKind.InvalidPath, status.Kind);
    }

    // --- Editor de herramientas (CompanySettingsViewModel) ---

    [Fact]
    public async Task LoadTools_PopulatesFormFromDisk()
    {
        var smartctlPath = Path.Combine(_tempDir, "smartctl.exe");
        File.WriteAllText(smartctlPath, string.Empty);
        WriteToolsConfig(smartctlPath, autoDetect: false);
        var vm = CreateSettingsViewModel();

        await vm.LoadAsync();

        Assert.Equal(smartctlPath, vm.SmartctlPath);
        Assert.False(vm.SmartctlAutoDetect);
        Assert.False(vm.HasToolsChanges);
        Assert.Equal(_toolsConfigPath, vm.ToolsConfigFilePath);
    }

    [Fact]
    public async Task SaveTools_EmptyPathAndAutoDetectOff_ShowsErrorAndDoesNotWrite()
    {
        var vm = CreateSettingsViewModel();
        await vm.LoadAsync();

        vm.SmartctlPath = string.Empty;
        vm.SmartctlAutoDetect = false;

        await vm.SaveToolsCommand.ExecuteAsync(null);

        Assert.True(vm.ToolsIsError);
        Assert.False(vm.ToolsIsSuccess);
        Assert.False(File.Exists(_toolsConfigPath));
    }

    [Fact]
    public async Task SaveTools_NonexistentPath_ShowsWarningButPersists()
    {
        var vm = CreateSettingsViewModel();
        await vm.LoadAsync();
        var missing = Path.Combine(_tempDir, "no-existe", "smartctl.exe");

        vm.SmartctlPath = missing;
        vm.SmartctlAutoDetect = true;

        await vm.SaveToolsCommand.ExecuteAsync(null);

        Assert.True(vm.ToolsIsWarning);
        Assert.False(vm.ToolsIsError);
        Assert.True(File.Exists(_toolsConfigPath));

        var persisted = await new ToolsConfigService(_toolsConfigPath).LoadAsync();
        Assert.Equal(missing, persisted.SmartctlPath);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(_toolsConfigPath));
        Assert.Equal(missing, document.RootElement.GetProperty("smartctlPath").GetString());
        Assert.True(document.RootElement.GetProperty("smartctlAutoDetect").GetBoolean());
    }

    [Fact]
    public async Task TestSmartctl_Available_ShowsProbeResult()
    {
        var runner = new FakeSmartctlRunner
        {
            Result = new SmartctlAvailability
            {
                IsAvailable = true,
                SmartctlPath = @"C:\tools\smartmontools\smartctl.exe",
                Version = "7.4",
                SupportsJson = true,
                CheckedAt = new DateTime(2026, 1, 2, 3, 4, 5)
            }
        };
        var vm = CreateSettingsViewModel(runner);
        await vm.LoadAsync();

        await vm.TestSmartctlCommand.ExecuteAsync(null);

        Assert.True(vm.HasProbeResult);
        Assert.True(vm.ProbeIsAvailable);
        Assert.Equal("smartctl: OK", vm.ProbeStatusText);
        Assert.Equal("7.4", vm.ProbeVersion);
        Assert.Equal("sí", vm.ProbeSupportsJsonText);
        Assert.Equal(@"C:\tools\smartmontools\smartctl.exe", vm.ProbePath);
        Assert.Equal("03:04:05", vm.ProbeCheckedAt);
        Assert.Equal(1, runner.CheckCalls);
    }

    [Fact]
    public async Task TestSmartctl_Unavailable_ShowsProbeError()
    {
        var runner = new FakeSmartctlRunner
        {
            Result = new SmartctlAvailability
            {
                IsAvailable = false,
                ErrorMessage = "smartctl.exe no encontrado"
            }
        };
        var vm = CreateSettingsViewModel(runner);
        await vm.LoadAsync();

        await vm.TestSmartctlCommand.ExecuteAsync(null);

        Assert.True(vm.HasProbeResult);
        Assert.False(vm.ProbeIsAvailable);
        Assert.Equal("smartctl: no disponible", vm.ProbeStatusText);
        Assert.Equal("N/D", vm.ProbeVersion);
        Assert.Contains("no encontrado", vm.ProbeDetail);
    }

    [Fact]
    public async Task TestSmartctl_WithMissingPath_WarnsWithoutRunningSmartctl()
    {
        var runner = new FakeSmartctlRunner();
        var vm = CreateSettingsViewModel(runner);
        await vm.LoadAsync();

        vm.SmartctlPath = Path.Combine(_tempDir, "no-existe", "smartctl.exe");
        await vm.TestSmartctlCommand.ExecuteAsync(null);

        Assert.True(vm.ToolsIsWarning);
        Assert.False(vm.HasProbeResult);
        Assert.Equal(0, runner.CheckCalls);
    }

    // --- Shell: carga real de XAML ---

    [Fact]
    public void MainWindow_LoadsOnSta_WithRealShellViewModel()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow();

            Assert.IsType<MainViewModel>(window.DataContext);
            var vm = (MainViewModel)window.DataContext;
            Assert.Equal("Home", vm.CurrentSection);
            Assert.Equal(14, vm.NavigationItems.Count);
            Assert.False(string.IsNullOrWhiteSpace(vm.SmartctlStatusText));
        });
    }

    [Fact]
    public void CompanySettingsView_LoadsOnSta_WithToolsSectionDataContext()
    {
        RunOnSta(() =>
        {
            var vm = new CompanySettingsViewModel(
                new FakeSettingsService(),
                new ToolsConfigService(_toolsConfigPath),
                _tempDir);

            var view = new CompanySettingsView { DataContext = vm };

            Assert.NotNull(view.Content);
            Assert.Equal(_toolsConfigPath, vm.ToolsConfigFilePath);
        });
    }

    // --- Shell: navegación por atajo y F5 ---

    [Fact]
    public void MainViewModel_ShortcutNavigationAndF5_DoNotThrow()
    {
        RunOnSta(() =>
        {
            var vm = new MainViewModel(new FakeHardwareSensorService(), _tempDir);

            Assert.Equal("Home", vm.CurrentSection);
            Assert.Equal(14, vm.NavigationItems.Count);
            Assert.Contains(vm.NavigationItems,
                item => item.Section == "DecisionCenterDemo" && item.Label == "Decisiones");
            Assert.Contains(vm.NavigationItems,
                item => item.Section == "Hardware" && item.Shortcut == "Ctrl+3");
            Assert.False(string.IsNullOrWhiteSpace(vm.OsLine));
            Assert.StartsWith("v", vm.AppVersionText);
            Assert.False(string.IsNullOrWhiteSpace(vm.AdminText));
            Assert.True(vm.NavigationItems[0].IsActive);

            vm.NavigateByShortcutCommand.Execute("3");

            Assert.Equal("Hardware", vm.CurrentSection);
            Assert.True(vm.NavigationItems.Single(item => item.Section == "Hardware").IsActive);
            Assert.False(vm.NavigationItems[0].IsActive);

            // F5 no debe lanzar: sección con acción y sección Home sin acción principal.
            vm.RefreshCurrentSectionCommand.Execute(null);
            vm.NavigateByShortcutCommand.Execute("0");
            vm.RefreshCurrentSectionCommand.Execute(null);

            Assert.Equal("Home", vm.CurrentSection);
        });
    }

    // --- Helpers ---

    private CompanySettingsViewModel CreateSettingsViewModel(FakeSmartctlRunner? runner = null)
    {
        var effectiveRunner = runner ?? new FakeSmartctlRunner();
        return new CompanySettingsViewModel(
            new FakeSettingsService(),
            new ToolsConfigService(_toolsConfigPath),
            _tempDir,
            _ => effectiveRunner);
    }

    private void WriteToolsConfig(string smartctlPath, bool autoDetect)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_toolsConfigPath)!);
        var payload = new
        {
            smartctlPath,
            smartctlAutoDetect = autoDetect
        };
        File.WriteAllText(_toolsConfigPath, JsonSerializer.Serialize(payload));
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "La prueba STA no terminó en 30 segundos.");

        if (failure is not null)
        {
            throw failure;
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; } = new();

        public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(CurrentSettings);

        public Task SaveSettingsAsync(AppSettings settings) => Task.CompletedTask;

        public event EventHandler<AppSettings>? SettingsChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class FakeSmartctlRunner : ISmartctlRunner
    {
        public SmartctlAvailability Result { get; set; } = new();

        public int CheckCalls { get; private set; }

        public Task<SmartctlAvailability> CheckAvailabilityAsync()
        {
            CheckCalls++;
            return Task.FromResult(Result);
        }

        public Task<SmartctlCommandResult> RunAsync(string arguments, TimeSpan timeout) =>
            Task.FromResult(new SmartctlCommandResult());

        public Task<IReadOnlyList<SmartDiskDevice>> ListDevicesAsync() =>
            Task.FromResult<IReadOnlyList<SmartDiskDevice>>(Array.Empty<SmartDiskDevice>());
    }

    private sealed class FakeHardwareSensorService : IHardwareSensorService
    {
        public Task<HardwareTemperatureSnapshot> GetTemperatureSnapshotAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new HardwareTemperatureSnapshot());

        public Task<HardwareLiveSnapshot> GetLiveSnapshotAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new HardwareLiveSnapshot());

        public async IAsyncEnumerable<HardwareTemperatureSnapshot> WatchTemperatureSnapshotsAsync(
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield break;
        }

        public async IAsyncEnumerable<HardwareLiveSnapshot> WatchLiveSnapshotsAsync(
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield break;
        }
    }
}
