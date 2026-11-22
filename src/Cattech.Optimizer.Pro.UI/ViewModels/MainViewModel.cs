using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Infrastructure.Cleanup;
using Cattech.Optimizer.Pro.Infrastructure.Data;
using Cattech.Optimizer.Pro.Infrastructure.Diagnostics;
using Cattech.Optimizer.Pro.Infrastructure.Hardware;
using Cattech.Optimizer.Pro.Infrastructure.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Repair;
using Cattech.Optimizer.Pro.Infrastructure.RestorePoint;
using Cattech.Optimizer.Pro.Infrastructure.Smart;
using Cattech.Optimizer.Pro.Infrastructure.Startup;
using Cattech.Optimizer.Pro.Infrastructure.VisualOptimization;
using Cattech.Optimizer.Pro.UI.Views;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// Estado compacto del indicador de smartctl de la barra de estado.
/// Se resuelve solo por lectura de config/herramientas.json (no ejecuta smartctl).
/// </summary>
internal enum SmartctlStatusKind
{
    /// <summary>Sin archivo de configuración o sin ruta y autodetección desactivada.</summary>
    NotConfigured,

    /// <summary>Sin ruta fija, autodetección activa.</summary>
    AutoDetect,

    /// <summary>Ruta configurada y existente en disco.</summary>
    Available,

    /// <summary>Ruta configurada inexistente o JSON inválido.</summary>
    InvalidPath
}

/// <summary>
/// ViewModel principal para la ventana de la aplicación.
/// Compone manualmente los servicios (sin contenedor DI), crea las vistas de
/// cada sección y expone el estado técnico del shell para la barra inferior.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly string _appBaseDirectory;

    // Servicios (temporal: sin DI, se crean directamente)
    private readonly ISettingsService _settingsService;
    private readonly IServiceReportService _reportService;
    private readonly IHardwareService _hardwareService;
    private readonly IDiagnosticService _diagnosticService;
    private readonly IStartupService _startupService;
    private readonly ITempCleanupService _tempCleanupService;
    private readonly IVisualOptimizationService _visualOptimizationService;
    private readonly IRestorePointService _restorePointService;
    private readonly IReportGenerationService _reportGenerationService;
    private readonly IPdfExportService _pdfExportService;
    private readonly ISmartctlRunner _smartctlRunner;
    private readonly ISmartDiskService _smartDiskService;
    private readonly ISmartTestService _smartTestService;
    private readonly IHardwareSensorService _hardwareSensorService;
    private readonly ICrashDumpDiscoveryService _crashDumpDiscoveryService;
    private readonly ICrashEventDiscoveryService _crashEventDiscoveryService;
    private readonly IBugCheckDecoder _bugCheckDecoder;
    private readonly IDumpCheckService _dumpCheckService;
    private readonly IWindowsUpdateReadinessService _windowsUpdateReadinessService;
    private readonly ISystemRepairPlanService _systemRepairPlanService;
    private readonly ISystemRepairPreflightService _systemRepairPreflightService;

    // ViewModels de secciones
    private CompanySettingsViewModel? _companySettingsViewModel;
    private ClientEquipmentViewModel? _clientEquipmentViewModel;
    private QuickDiagnosticViewModel? _quickDiagnosticViewModel;
    private StartupAnalysisViewModel? _startupAnalysisViewModel;
    private TempCleanupViewModel? _tempCleanupViewModel;
    private VisualOptimizationViewModel? _visualOptimizationViewModel;
    private RestorePointViewModel? _restorePointViewModel;
    private ReportViewModel? _reportViewModel;
    private SmartDiskViewModel? _smartDiskViewModel;
    private HardwareViewModel? _hardwareViewModel;
    private CrashEvidenceViewModel? _crashEvidenceViewModel;
    private RepairCenterViewModel? _repairCenterViewModel;

    private static readonly Brush SmartctlOkBrush = CreateFrozenBrush(0x2E, 0x7D, 0x32);
    private static readonly Brush SmartctlWarningBrush = CreateFrozenBrush(0xB2, 0x6A, 0x00);
    private static readonly Brush SmartctlErrorBrush = CreateFrozenBrush(0xC6, 0x28, 0x28);
    private static readonly Brush SmartctlNeutralBrush = CreateFrozenBrush(0x66, 0x66, 0x66);
    private static readonly Brush HomeTitleBrush = CreateFrozenBrush(0x22, 0x22, 0x22);
    private static readonly Brush HomeMutedBrush = CreateFrozenBrush(0x66, 0x66, 0x66);
    private static readonly Brush HomeCardBorderBrush = CreateFrozenBrush(0xDD, 0xDD, 0xDD);
    private static readonly Brush HomeCardBackgroundBrush = CreateFrozenBrush(0xFA, 0xFA, 0xFA);

    private readonly Dictionary<string, TextBlock> _homeCountBlocks = new(StringComparer.Ordinal);
    private bool _homeRefreshRunning;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentSection = "Home";

    // --- Estado técnico de la barra inferior (se calcula una vez al iniciar) ---

    /// <summary>Si el proceso corre elevado (token de administrador).</summary>
    public bool IsAdministrator { get; }

    /// <summary>Texto compacto del estado de elevación ("Admin: sí/no").</summary>
    public string AdminText { get; }

    /// <summary>Windows build, arquitectura del SO y runtime de .NET.</summary>
    public string OsLine { get; }

    /// <summary>Versión de la aplicación derivada del ensamblado de UI.</summary>
    public string AppVersionText { get; }

    /// <summary>Directorio de datos de la aplicación (BaseDirectory normalizado).</summary>
    public string AppBaseDirectory { get; }

    /// <summary>Modo de ejecución detectado: portable, instalado o compilación.</summary>
    public string InstallationMode { get; }

    /// <summary>Ruta de config/ dentro del directorio de la aplicación.</summary>
    public string ConfigDirectory { get; }

    /// <summary>Ruta de data/ dentro del directorio de la aplicación.</summary>
    public string DataDirectory { get; }

    /// <summary>Ruta de backups/ dentro del directorio de la aplicación.</summary>
    public string BackupsDirectory { get; }

    /// <summary>Ruta de reports/ dentro del directorio de la aplicación.</summary>
    public string ReportsDirectory { get; }

    /// <summary>Ruta de logs/ dentro del directorio de la aplicación.</summary>
    public string LogsDirectory { get; }

    /// <summary>Si existe el directorio de logs (la app no lo crea por sí sola).</summary>
    public bool LogsDirectoryExists { get; }

    /// <summary>Atajos y secciones del menú lateral (Ctrl+0..9).</summary>
    public IReadOnlyList<NavigationItem> NavigationItems { get; }

    [ObservableProperty]
    private string _smartctlStatusText = "smartctl: sin configurar";

    [ObservableProperty]
    private string _smartctlStatusDetail = string.Empty;

    [ObservableProperty]
    private Brush _smartctlStatusBrush = SmartctlNeutralBrush;

    public MainViewModel()
        : this(new LibreHardwareSensorService())
    {
    }

    internal MainViewModel(IHardwareSensorService hardwareSensorService, string? appBaseDirectory = null)
    {
        _appBaseDirectory = NormalizeDirectory(appBaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory);

        // Composición manual de servicios: el arranque no depende de un contenedor DI,
        // lo que mantiene la aplicación simple de publicar y de probar.
        _settingsService = new JsonSettingsService();
        _reportService = new JsonServiceReportService();
        _hardwareService = new WmiHardwareService();
        _diagnosticService = new DiagnosticService(_hardwareService);
        _startupService = new StartupService();
        _tempCleanupService = new TempCleanupService();
        _visualOptimizationService = new VisualOptimizationService();
        _restorePointService = new RestorePointService();
        _reportGenerationService = new HtmlReportService();
        _pdfExportService = new PdfExportService();
        _smartctlRunner = new SmartctlRunner();
        _smartDiskService = new SmartDiskService(_smartctlRunner);
        _smartTestService = new SmartTestService(_smartctlRunner);
        _hardwareSensorService = hardwareSensorService;
        _crashDumpDiscoveryService = new CrashDumpDiscoveryService();
        _crashEventDiscoveryService = new CrashEventDiscoveryService();
        _bugCheckDecoder = new BugCheckDecoder();
        _dumpCheckService = new DumpCheckService();
        _windowsUpdateReadinessService = new WindowsUpdateReadinessService();
        _systemRepairPlanService = new SystemRepairPlanService();
        _systemRepairPreflightService = new SystemRepairPreflightService();

        NavigationItems = CreateNavigationItems();

        AppBaseDirectory = _appBaseDirectory;
        ConfigDirectory = Path.Combine(_appBaseDirectory, "config");
        DataDirectory = Path.Combine(_appBaseDirectory, "data");
        BackupsDirectory = Path.Combine(_appBaseDirectory, "backups");
        ReportsDirectory = Path.Combine(_appBaseDirectory, "reports");
        LogsDirectory = Path.Combine(_appBaseDirectory, "logs");
        LogsDirectoryExists = Directory.Exists(LogsDirectory);

        IsAdministrator = Environment.IsPrivilegedProcess;
        AdminText = IsAdministrator ? "Admin: sí" : "Admin: no";
        AppVersionText = FormatAppVersion(typeof(MainViewModel).Assembly.GetName().Version);
        OsLine = BuildOsLine();
        InstallationMode = ResolveInstallationMode(_appBaseDirectory);

        NavigateTo("Home");
    }

    // --- Comandos de shell ---

    [RelayCommand]
    private void Navigate(string? section)
    {
        if (!string.IsNullOrEmpty(section))
        {
            NavigateTo(section);
        }
    }

    /// <summary>
    /// Navegación por atajo: Ctrl+0 = Inicio, Ctrl+1..Ctrl+9 = primeras 9 secciones
    /// después de Inicio. Un atajo desconocido no hace nada (el shell no falla).
    /// </summary>
    [RelayCommand]
    private void NavigateByShortcut(string? key)
    {
        var section = ResolveShortcutSection(key);
        if (section is not null)
        {
            NavigateTo(section);
        }
    }

    /// <summary>
    /// F5: ejecuta la acción principal de la sección visible usando el ViewModel ya
    /// instanciado. En Inicio actualiza el panel técnico; en secciones sin acción
    /// principal (Decisiones) es un no-op deliberado.
    /// </summary>
    [RelayCommand]
    private void RefreshCurrentSection()
    {
        if (string.Equals(CurrentSection, "Home", StringComparison.Ordinal))
        {
            _ = RefreshHomeDashboardAsync();
            return;
        }

        var command = ResolveSectionCommand(CurrentSection);
        if (command is null || !command.CanExecute(null))
        {
            return;
        }

        try
        {
            if (command is IAsyncRelayCommand asyncCommand)
            {
                _ = ExecuteSectionCommandAsync(asyncCommand);
            }
            else
            {
                command.Execute(null);
            }
        }
        catch (Exception)
        {
            // La acción de la sección no debe tumbar el shell: la propia sección
            // expone su estado/error al operador.
        }
    }

    /// <summary>Abre el Explorador en el directorio de datos de la aplicación.</summary>
    [RelayCommand]
    private void OpenAppDataFolder()
    {
        TryOpenFolder(
            AppBaseDirectory,
            "No se pudo abrir la carpeta de datos. Verificá que exista y que el Explorador de Windows esté disponible.");
    }

    /// <summary>Copia una ruta o valor crudo al portapapeles (sin recortes).</summary>
    [RelayCommand]
    private void CopyToClipboard(string? value)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                Clipboard.SetText(value);
            }
        }
        catch (Exception)
        {
            MessageBox.Show(
                "No se pudo copiar al portapapeles. Reintentá la operación.",
                "CATTECH — Portapapeles",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Información del entorno: versión, modo de ejecución, paths de la app,
    /// configuración y logs (solo si existen).
    /// </summary>
    [RelayCommand]
    private void ShowAppInfo()
    {
        var lines = new List<string>
        {
            $"CATTECH OPTIMIZER PRO {AppVersionText}",
            string.Empty,
            $"Modo: {InstallationMode}",
            $"Administrador: {(IsAdministrator ? "sí" : "no")}",
            $"Windows: {Environment.OSVersion.Version} ({Environment.OSVersion.VersionString})",
            $"Arquitectura: {DescribeArchitecture(RuntimeInformation.OSArchitecture)}",
            $"Runtime: {RuntimeInformation.FrameworkDescription} (CLR {Environment.Version})",
            string.Empty,
            $"Ejecutable: {AppBaseDirectory}",
            $"Config: {ConfigDirectory}",
            $"Datos: {DataDirectory}",
            $"Backups: {BackupsDirectory}",
            $"Informes: {ReportsDirectory}"
        };

        if (LogsDirectoryExists)
        {
            lines.Add($"Logs: {LogsDirectory}");
        }

        lines.Add($"smartctl: {SmartctlStatusText}");
        lines.Add($"config/herramientas.json: {Path.Combine(ConfigDirectory, "herramientas.json")}");

        MessageBox.Show(
            string.Join(Environment.NewLine, lines),
            "CATTECH — Información del sistema",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // --- Navegación ---

    private void NavigateTo(string section)
    {
        // No dejar el monitoreo de Hardware activo al navegar a otra sección.
        if (CurrentSection == "Hardware" && section != "Hardware")
        {
            _hardwareViewModel?.StopMonitoringCommand.Execute(null);
        }

        CurrentSection = section;

        CurrentView = section switch
        {
            "Home" => CreateHomeView(),
            "Hardware" => CreateHardwareView(),
            "SmartDisk" => CreateSmartDiskView(),
            "Diagnostics" => CreateQuickDiagnosticView(),
            "DecisionCenterDemo" => CreateDecisionCenterDemoView(),
            "BlueScreens" => CreateCrashEvidenceView(),
            "RepairCenter" => CreateRepairCenterView(),
            "TempCleanup" => CreateTempCleanupView(),
            "Optimization" => CreateVisualOptimizationView(),
            "RestorePoint" => CreateRestorePointView(),
            "Reports" => CreateReportView(),
            "Settings" => CreateCompanySettingsView(),
            "ClientEquipment" => CreateClientEquipmentView(),
            "Startup" => CreateStartupAnalysisView(),
            _ => CreateNotFoundView()
        };

        UpdateActiveNavigation();
        RefreshSmartctlIndicator();

        // El panel técnico de Inicio se completa en segundo plano.
        if (string.Equals(CurrentSection, "Home", StringComparison.Ordinal))
        {
            _ = RefreshHomeDashboardAsync();
        }
    }

    private void UpdateActiveNavigation()
    {
        foreach (var item in NavigationItems)
        {
            item.IsActive = string.Equals(item.Section, CurrentSection, StringComparison.Ordinal);
        }
    }

    partial void OnCurrentSectionChanged(string value) => UpdateActiveNavigation();

    private ICommand? ResolveSectionCommand(string section) => section switch
    {
        "ClientEquipment" => _clientEquipmentViewModel?.DetectHardwareCommand,
        "SmartDisk" => _smartDiskViewModel?.CheckSmartctlCommand,
        "Hardware" => _hardwareViewModel?.RefreshCommand,
        "Diagnostics" => _quickDiagnosticViewModel?.RunDiagnosticCommand,
        "BlueScreens" => _crashEvidenceViewModel?.RefreshCommand,
        "RepairCenter" => _repairCenterViewModel?.InspectWindowsUpdateCommand,
        "Startup" => _startupAnalysisViewModel?.AnalyzeCommand,
        "TempCleanup" => _tempCleanupViewModel?.ScanCommand,
        "Optimization" => _visualOptimizationViewModel?.AnalyzeCommand,
        "RestorePoint" => _restorePointViewModel?.CheckStatusCommand,
        "Reports" => _reportViewModel?.LoadDataCommand,
        "Settings" => _companySettingsViewModel?.ReloadCommand,
        _ => null
    };

    private static async Task ExecuteSectionCommandAsync(IAsyncRelayCommand command)
    {
        try
        {
            await command.ExecuteAsync(null);
        }
        catch (Exception)
        {
            // Igual que en RefreshCurrentSection: la sección muestra su resultado.
        }
    }

    // --- Barra de estado ---

    private void RefreshSmartctlIndicator()
    {
        var (text, detail, kind) = ResolveSmartctlStatus(_appBaseDirectory);
        SmartctlStatusText = text;
        SmartctlStatusDetail = detail;
        SmartctlStatusBrush = kind switch
        {
            SmartctlStatusKind.Available => SmartctlOkBrush,
            SmartctlStatusKind.InvalidPath => SmartctlErrorBrush,
            SmartctlStatusKind.AutoDetect => SmartctlNeutralBrush,
            _ => SmartctlWarningBrush
        };
    }

    /// <summary>
    /// Resuelve el indicador compacto de smartctl leyendo SOLO
    /// <c>config/herramientas.json</c>. No ejecuta smartctl (arranque rápido).
    /// </summary>
    internal static (string Text, string Detail, SmartctlStatusKind Kind) ResolveSmartctlStatus(
        string appBaseDirectory)
    {
        var root = NormalizeDirectory(appBaseDirectory);
        var configPath = Path.Combine(root, "config", "herramientas.json");
        var nl = Environment.NewLine;

        if (!File.Exists(configPath))
        {
            return (
                "smartctl: sin configurar",
                $"Archivo ausente: {configPath}{nl}Se resolverá por autodetección al usar Discos SMART.",
                SmartctlStatusKind.NotConfigured);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            var rootElement = document.RootElement;

            var configuredPath = rootElement.ValueKind == JsonValueKind.Object &&
                                 rootElement.TryGetProperty("smartctlPath", out var pathElement) &&
                                 pathElement.ValueKind == JsonValueKind.String
                ? (pathElement.GetString() ?? string.Empty).Trim()
                : string.Empty;

            var autoDetect = rootElement.ValueKind != JsonValueKind.Object ||
                             !rootElement.TryGetProperty("smartctlAutoDetect", out var autoElement) ||
                             autoElement.ValueKind != JsonValueKind.False;

            if (configuredPath.Length > 0)
            {
                var smartctlExists = File.Exists(configuredPath);
                var detail =
                    $"config: {configPath}{nl}smartctlPath: {configuredPath}{nl}autodetect: {(autoDetect ? "sí" : "no")}";

                return smartctlExists
                    ? ("smartctl: OK", detail, SmartctlStatusKind.Available)
                    : ("smartctl: no disponible", detail + $"{nl}la ruta configurada no existe",
                        SmartctlStatusKind.InvalidPath);
            }

            return autoDetect
                ? ("smartctl: autodetect",
                    $"config: {configPath}{nl}sin ruta fija; autodetección activa",
                    SmartctlStatusKind.AutoDetect)
                : ("smartctl: sin configurar",
                    $"config: {configPath}{nl}sin ruta y autodetección desactivada",
                    SmartctlStatusKind.NotConfigured);
        }
        catch (JsonException)
        {
            return ("smartctl: config inválida",
                $"config: {configPath}{nl}JSON inválido; smartctl se resolverá por autodetección.",
                SmartctlStatusKind.InvalidPath);
        }
        catch (IOException)
        {
            return ("smartctl: config inválida",
                $"config: {configPath}{nl}no se pudo leer el archivo.",
                SmartctlStatusKind.InvalidPath);
        }
        catch (UnauthorizedAccessException)
        {
            return ("smartctl: config inválida",
                $"config: {configPath}{nl}sin permisos para leer el archivo.",
                SmartctlStatusKind.InvalidPath);
        }
    }

    /// <summary>
    /// Mapa de atajos de navegación. Ctrl+0 = Inicio; Ctrl+1..9 = primeras 9
    /// secciones después de Inicio. Devuelve <c>null</c> para atajos desconocidos.
    /// </summary>
    internal static string? ResolveShortcutSection(string? key) => key switch
    {
        "0" => "Home",
        "1" => "ClientEquipment",
        "2" => "SmartDisk",
        "3" => "Hardware",
        "4" => "Diagnostics",
        "5" => "DecisionCenterDemo",
        "6" => "BlueScreens",
        "7" => "RepairCenter",
        "8" => "Startup",
        "9" => "TempCleanup",
        _ => null
    };

    internal static string FormatAppVersion(Version? version)
    {
        if (version is null)
        {
            return "v0.0.0";
        }

        return version.Build >= 0
            ? $"v{version.Major}.{version.Minor}.{version.Build}"
            : $"v{version.Major}.{version.Minor}";
    }

    internal static string BuildOsLine() =>
        $"Win {Environment.OSVersion.Version} · {DescribeArchitecture(RuntimeInformation.OSArchitecture)} · {RuntimeInformation.FrameworkDescription}";

    private static string DescribeArchitecture(Architecture architecture) => architecture switch
    {
        Architecture.X64 => "x64",
        Architecture.X86 => "x86",
        Architecture.Arm64 => "ARM64",
        Architecture.Arm => "ARM",
        _ => architecture.ToString()
    };

    internal static string ResolveInstallationMode(string appBaseDirectory)
    {
        try
        {
            var root = NormalizeDirectory(appBaseDirectory);

            if (IsUnder(root, Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")))
            {
                return "Instalado (LocalAppData)";
            }

            if (IsUnder(root, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)))
            {
                return "Instalado (Program Files x86)";
            }

            if (IsUnder(root, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)))
            {
                return "Instalado (Program Files)";
            }

            var releaseMarker = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}";
            var debugMarker = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}";
            if (root.Contains(releaseMarker, StringComparison.OrdinalIgnoreCase) ||
                root.Contains(debugMarker, StringComparison.OrdinalIgnoreCase))
            {
                return "Desarrollo (salida de compilación)";
            }

            return "Portable";
        }
        catch (Exception)
        {
            return "Portable";
        }
    }

    private static bool IsUnder(string path, string? root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var normalizedRoot = NormalizeDirectory(root);
        return string.Equals(path, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectory(string path) =>
        string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static Brush CreateFrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static void TryOpenFolder(string folder, string errorMessage)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                throw new DirectoryNotFoundException();
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folder}\"",
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Nunca exponer la ruta absoluta del equipo en el mensaje de error.
            MessageBox.Show(
                errorMessage,
                "CATTECH — Abrir carpeta",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static IReadOnlyList<NavigationItem> CreateNavigationItems() => new List<NavigationItem>
    {
        new("Home", "Inicio", "Ctrl+0", "Panorama y estado del shell."),
        new("ClientEquipment", "Cliente y equipo", "Ctrl+1", "Ficha del cliente/equipo y detección de hardware."),
        new("SmartDisk", "Discos SMART", "Ctrl+2", "smartctl: disponibilidad, discos y análisis SMART."),
        new("Hardware", "Hardware", "Ctrl+3", "Inventario WMI y monitoreo en vivo de sensores."),
        new("Diagnostics", "Diagnóstico", "Ctrl+4", "Diagnóstico determinístico del equipo."),
        new("DecisionCenterDemo", "Decisiones", "Ctrl+5",
            "Caso de demostración del motor de decisiones (no diagnostica hardware real)"),
        new("BlueScreens", "Pantallas azules", "Ctrl+6", "Evidencia de fallas: dumps, eventos y BugCheck."),
        new("RepairCenter", "Reparaciones", "Ctrl+7",
            "Vista previa de reparaciones de Windows Update (sin ejecutar cambios)."),
        new("Startup", "Programas de inicio", "Ctrl+8",
            "Análisis y desactivación con respaldo de entradas de inicio."),
        new("TempCleanup", "Limpieza", "Ctrl+9", "Escaneo de temporales; limpieza solo con confirmación."),
        new("Optimization", "Optimización", string.Empty,
            "Optimización visual con respaldo y restauración."),
        new("RestorePoint", "Punto de restauración", string.Empty,
            "Estado y creación de puntos de restauración."),
        new("Reports", "Informes", string.Empty, "Informes HTML/PDF con datos recolectados."),
        new("Settings", "Configuración", string.Empty,
            "Datos de empresa, herramientas externas y rutas de la app.")
    };

    // --- Creación de vistas por sección ---

    private object CreateCompanySettingsView()
    {
        _companySettingsViewModel ??= new CompanySettingsViewModel(
            _settingsService,
            new ToolsConfigService(Path.Combine(_appBaseDirectory, "config", "herramientas.json")),
            _appBaseDirectory);

        var view = new CompanySettingsView
        {
            DataContext = _companySettingsViewModel
        };

        _ = _companySettingsViewModel.LoadAsync();
        return view;
    }

    private object CreateClientEquipmentView()
    {
        _clientEquipmentViewModel ??= new ClientEquipmentViewModel(
            _reportService, _settingsService, _hardwareService);

        return new ClientEquipmentView
        {
            DataContext = _clientEquipmentViewModel
        };
    }

    private object CreateQuickDiagnosticView()
    {
        _quickDiagnosticViewModel ??= new QuickDiagnosticViewModel(_diagnosticService);

        return new QuickDiagnosticView
        {
            DataContext = _quickDiagnosticViewModel
        };
    }

    // Isolated demonstration: never receives real hardware providers,
    // client records or services and never initiates system changes.
    private static object CreateDecisionCenterDemoView() =>
        new DecisionCenterDemoView
        {
            DataContext = new DecisionCenterDemoViewModel()
        };

    private object CreateCrashEvidenceView()
    {
        _crashEvidenceViewModel ??= new CrashEvidenceViewModel(
            _crashDumpDiscoveryService, _crashEventDiscoveryService, _bugCheckDecoder,
            _dumpCheckService, fileName => MessageBox.Show(
                "Se abrirá localmente el archivo de volcado seleccionado mediante DumpChk " +
                "(Windows Debugging Tools) para comprobar su legibilidad y código BugCheck.\n\n" +
                $"Archivo: {fileName}\n\n" +
                "Puede contener datos sensibles; no se subirá ni copiará automáticamente. " +
                "No se descargarán símbolos ni se reparará Windows.\n\n" +
                "¿Confirmás expresamente esta comprobación?",
                "CATTECH — Confirmar DumpChk",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes);

        return new CrashEvidenceView
        {
            DataContext = _crashEvidenceViewModel
        };
    }

    private object CreateRepairCenterView()
    {
        _repairCenterViewModel ??= new RepairCenterViewModel(
            _windowsUpdateReadinessService, _systemRepairPlanService,
            _systemRepairPreflightService);

        return new RepairCenterView
        {
            DataContext = _repairCenterViewModel
        };
    }

    private object CreateStartupAnalysisView()
    {
        _startupAnalysisViewModel ??= new StartupAnalysisViewModel(_startupService);

        return new StartupAnalysisView
        {
            DataContext = _startupAnalysisViewModel
        };
    }

    private object CreateTempCleanupView()
    {
        _tempCleanupViewModel ??= new TempCleanupViewModel(_tempCleanupService);

        return new TempCleanupView
        {
            DataContext = _tempCleanupViewModel
        };
    }

    private object CreateVisualOptimizationView()
    {
        _visualOptimizationViewModel ??= new VisualOptimizationViewModel(_visualOptimizationService);

        return new VisualOptimizationView
        {
            DataContext = _visualOptimizationViewModel
        };
    }

    private object CreateRestorePointView()
    {
        _restorePointViewModel ??= new RestorePointViewModel(_restorePointService);

        var view = new RestorePointView
        {
            DataContext = _restorePointViewModel
        };

        // Auto-verificación al abrir la sección: el botón de crear ya no depende de una sonda
        // obsoleta, pero conviene mostrar el estado real sin exigir un clic extra. La verificación
        // es asíncrona y no bloquea el hilo de UI; la propia VM evita la reentrancia.
        _ = _restorePointViewModel.CheckStatusCommand.ExecuteAsync(null);

        return view;
    }

    private object CreateReportView()
    {
        _reportViewModel ??= new ReportViewModel(
            _reportGenerationService,
            _pdfExportService,
            _settingsService,
            _reportService,
            _diagnosticService,
            _startupService,
            _tempCleanupService,
            _visualOptimizationService,
            _restorePointService,
            _smartDiskService,
            _smartTestService,
            new ReportSourceReviewCoordinator(_reportService));

        var view = new ReportView
        {
            DataContext = _reportViewModel
        };

        // Auto-carga al abrir la sección: sin esto "Generar informe" y
        // "Exportar PDF" fallan porque la pantalla nunca cargó datos.
        _ = _reportViewModel.LoadDataCommand.ExecuteAsync(null);
        return view;
    }

    private object CreateSmartDiskView()
    {
        _smartDiskViewModel ??= new SmartDiskViewModel(
            _smartctlRunner,
            _smartDiskService,
            _smartTestService);

        var view = new SmartDiskView
        {
            DataContext = _smartDiskViewModel
        };

        // Verificación inicial al abrir la sección: habilita "Detectar discos"
        // y "Analizar SMART" sin requerir un clic previo en "Verificar smartctl".
        _ = _smartDiskViewModel.EnsureInitializedAsync();
        return view;
    }

    private object CreateHardwareView()
    {
        _hardwareViewModel ??= new HardwareViewModel(
            _hardwareSensorService,
            _hardwareService);

        return new HardwareView
        {
            DataContext = _hardwareViewModel
        };
    }

    private object CreateHomeView()
    {
        _homeCountBlocks.Clear();

        var page = new Border
        {
            Background = System.Windows.Media.Brushes.White,
            Padding = new Thickness(24)
        };

        var root = new StackPanel { VerticalAlignment = VerticalAlignment.Top };

        root.Children.Add(new TextBlock
        {
            Text = "PANEL TÉCNICO",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = HomeTitleBrush
        });
        root.Children.Add(new TextBlock
        {
            Text = "Estado del equipo y del almacenamiento local. F5 actualiza este panel.",
            FontSize = 11,
            Foreground = HomeMutedBrush,
            Margin = new Thickness(0, 2, 0, 14)
        });

        var columns = new Grid();
        for (var index = 0; index < 3; index++)
        {
            columns.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
        }
        root.Children.Add(columns);

        // --- Columna 1: sistema ---
        var systemRows = new List<(string Label, string Value)>
        {
            ("Equipo", Environment.MachineName),
            ("Usuario", Environment.UserName),
            ("Administrador", Environment.IsPrivilegedProcess ? "SÍ" : "no"),
            ("Windows", Environment.OSVersion.Version.ToString()),
            ("Arquitectura", RuntimeInformation.OSArchitecture.ToString()),
            ("Runtime", RuntimeInformation.FrameworkDescription),
            ("smartctl", SmartctlStatusText.Replace("smartctl: ", string.Empty)),
            ("Datos", DataDirectory),
            ("Informes", ReportsDirectory),
            ("Backups", BackupsDirectory)
        };
        AddHomeCard(columns, 0, "SISTEMA",
            systemRows.Select(row => CreateHomeValueRow(
                row.Label, row.Value, monospace: row.Label is "Datos" or "Informes" or "Backups")));

        // --- Columna 2: almacenamiento local ---
        var storageRows = new List<(string Key, string Label)>
        {
            ("reports", "Informes de cliente"),
            ("html", "Informes HTML"),
            ("diagnostics", "Diagnósticos"),
            ("startup", "Análisis de inicio"),
            ("startup-backups", "Backups de inicio"),
            ("cleanup", "Limpiezas registradas"),
            ("visual", "Optimizaciones registradas"),
            ("restore", "Puntos de restauración")
        };
        var storageElements = new List<FrameworkElement>();
        foreach (var row in storageRows)
        {
            var valueBlock = new TextBlock
            {
                Text = "…",
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.SemiBold,
                Foreground = HomeMutedBrush,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _homeCountBlocks[row.Key] = valueBlock;
            storageElements.Add(CreateHomeValueRow(row.Label, valueBlock));
        }
        AddHomeCard(columns, 1, "ALMACENAMIENTO LOCAL", storageElements);

        // --- Columna 3: acciones rápidas ---
        var actions = new List<FrameworkElement>
        {
            CreateHomeAction("Ejecutar diagnóstico", () => NavigateTo("Diagnostics")),
            CreateHomeAction("Escanear temporales", () => NavigateTo("TempCleanup")),
            CreateHomeAction("Analizar hardware", () => NavigateTo("Hardware")),
            CreateHomeAction("Programas de inicio", () => NavigateTo("Startup")),
            CreateHomeAction("Configurar smartctl", () => NavigateTo("Settings")),
            CreateHomeAction("Abrir carpeta de datos", () => OpenAppDataFolderCommand.Execute(null)),
            CreateHomeAction("Abrir carpeta de informes", () => TryOpenFolder(
                ReportsDirectory, "No se pudo abrir la carpeta de informes.")),
            CreateHomeAction("Copiar resumen del sistema", CopyHomeSystemSummary)
        };
        AddHomeCard(columns, 2, "ACCIONES RÁPIDAS", actions);

        root.Children.Add(new TextBlock
        {
            Text = $"CATTECH OPTIMIZER PRO v0.3.1 · {DataDirectory}",
            FontSize = 10,
            FontFamily = new FontFamily("Consolas"),
            Foreground = HomeMutedBrush,
            Margin = new Thickness(0, 14, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        page.Child = root;
        return new ContentControl { Content = page };
    }

    private static void AddHomeCard(Grid columns, int column, string title,
        IEnumerable<FrameworkElement> rows)
    {
        var card = new Border
        {
            Background = HomeCardBackgroundBrush,
            BorderBrush = HomeCardBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(column == 0 ? 0 : 8, 0, column == 2 ? 0 : 8, 0)
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = HomeMutedBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });
        foreach (var row in rows)
            stack.Children.Add(row);

        card.Child = stack;
        Grid.SetColumn(card, column);
        columns.Children.Add(card);
    }

    private static FrameworkElement CreateHomeValueRow(string label, string value, bool monospace = false)
    {
        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = 11,
            Foreground = HomeTitleBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 220
        };
        if (monospace)
            valueBlock.FontFamily = new FontFamily("Consolas");
        return CreateHomeValueRow(label, valueBlock);
    }

    private static FrameworkElement CreateHomeValueRow(string label, FrameworkElement value)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = HomeMutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(labelBlock, 0);
        Grid.SetColumn(value, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(value);
        return grid;
    }

    private static Button CreateHomeAction(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            FontSize = 11,
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = HomeCardBorderBrush,
            BorderThickness = new Thickness(1)
        };
        button.Click += (_, _) =>
        {
            try { action(); } catch { /* Una acción rápida no debe tumbar el shell. */ }
        };
        return button;
    }

    /// <summary>
    /// Carga los contadores del panel técnico sin bloquear la UI. Cada fuente se
    /// consulta por separado: un almacenamiento ilegible no oculta los demás.
    /// </summary>
    private async Task RefreshHomeDashboardAsync()
    {
        if (_homeRefreshRunning)
            return;

        _homeRefreshRunning = true;
        try
        {
            await SetHomeCountAsync("reports", async () =>
                (await _reportService.ListReportsAsync(1000)).Count);
            await SetHomeCountAsync("html", () => Task.Run(() =>
                Directory.Exists(ReportsDirectory)
                    ? Directory.GetFiles(ReportsDirectory, "*.html").Length
                    : 0));
            await SetHomeCountAsync("diagnostics", async () =>
                (await _diagnosticService.ListDiagnosticsAsync(1000)).Count);
            await SetHomeCountAsync("startup", async () =>
                (await _startupService.ListAnalysesAsync(1000)).Count);
            await SetHomeCountAsync("startup-backups", async () =>
                (await _startupService.ListBackupsAsync()).Count);
            await SetHomeCountAsync("cleanup", async () =>
                (await _tempCleanupService.ListResultsAsync(1000)).Count);
            await SetHomeCountAsync("visual", async () =>
                (await _visualOptimizationService.ListResultsAsync(1000)).Count);
            await SetHomeCountAsync("restore", async () =>
                (await _restorePointService.ListResultsAsync(1000)).Count);
        }
        finally
        {
            _homeRefreshRunning = false;
        }
    }

    private async Task SetHomeCountAsync(string key, Func<Task<int>> load)
    {
        if (!_homeCountBlocks.TryGetValue(key, out var block))
            return;

        try
        {
            ApplyHomeCount(block, (await load()).ToString("N0"), HomeTitleBrush);
        }
        catch
        {
            ApplyHomeCount(block, "n/d", HomeMutedBrush);
        }
    }

    /// <summary>
    /// WPF exige actualizar controles desde el hilo de UI. En hosts sin
    /// dispatcher activo (tests) el valor se encola sin bloquear.
    /// </summary>
    private static void ApplyHomeCount(TextBlock block, string text, Brush brush)
    {
        void Update()
        {
            block.Text = text;
            block.Foreground = brush;
        }

        if (block.CheckAccess())
        {
            Update();
            return;
        }

        try
        {
            block.Dispatcher.BeginInvoke(Update);
        }
        catch
        {
            // Sin dispatcher (tests): el contador queda pendiente, sin romper.
        }
    }

    private void CopyHomeSystemSummary()
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("CATTECH OPTIMIZER PRO - resumen del sistema");
        builder.AppendLine($"Equipo: {Environment.MachineName} | Usuario: {Environment.UserName}");
        builder.AppendLine($"Administrador: {(Environment.IsPrivilegedProcess ? "sí" : "no")} | " +
            $"Windows {Environment.OSVersion.Version} | {RuntimeInformation.OSArchitecture} | " +
            RuntimeInformation.FrameworkDescription);
        builder.AppendLine($"Datos: {DataDirectory}");
        builder.AppendLine($"Informes: {ReportsDirectory}");
        builder.AppendLine($"Backups: {BackupsDirectory}");
        builder.AppendLine($"smartctl: {SmartctlStatusText.Replace("smartctl: ", string.Empty)}");
        foreach (var entry in _homeCountBlocks)
            builder.AppendLine($"{entry.Key}: {entry.Value.Text}");
        Clipboard.SetText(builder.ToString());
    }

    private static object CreateNotFoundView()
    {
        return CreateMessageView(
            "Sección no encontrada",
            "La sección solicitada no existe en esta versión de la aplicación.",
            showVersion: false);
    }

    private static object CreateMessageView(string title, string subtitle, bool showVersion)
    {
        var border = new Border
        {
            Background = System.Windows.Media.Brushes.White,
            Padding = new Thickness(30)
        };

        var stackPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Top
        };

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)),
            Margin = new Thickness(0, 20, 0, 10)
        };

        var subtitleBlock = new TextBlock
        {
            Text = subtitle,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x66, 0x66, 0x66)),
            Margin = new Thickness(0, 0, 0, 20)
        };

        stackPanel.Children.Add(titleBlock);
        stackPanel.Children.Add(subtitleBlock);

        if (showVersion)
        {
            stackPanel.Children.Add(new TextBlock
            {
                Text = "CATTECH OPTIMIZER PRO v0.3.1",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x99, 0x99, 0x99))
            });
        }

        border.Child = stackPanel;

        return new ContentControl { Content = border };
    }

    /// <summary>
    /// Entrada del menú lateral. Mantiene el atajo visible y el estado activo
    /// sin converters adicionales en XAML.
    /// </summary>
    public sealed class NavigationItem : ObservableObject
    {
        private bool _isActive;

        public NavigationItem(string section, string label, string shortcut, string toolTip)
        {
            Section = section;
            Label = label;
            Shortcut = shortcut;
            ToolTip = toolTip;
        }

        public string Section { get; }

        public string Label { get; }

        public string Shortcut { get; }

        public string ToolTip { get; }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }
    }
}
