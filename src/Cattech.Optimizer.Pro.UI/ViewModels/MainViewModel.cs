using System.Windows;
using System.Windows.Controls;
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
/// ViewModel principal para la ventana de la aplicación.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // Estilos para botones del sidebar
    public static string HomeButtonStyle { get; set; } = "SidebarButton";
    public static string HardwareButtonStyle { get; set; } = "SidebarButton";
    public static string DiagnosticsButtonStyle { get; set; } = "SidebarButton";
    public static string OptimizationButtonStyle { get; set; } = "SidebarButton";
    public static string ReportsButtonStyle { get; set; } = "SidebarButton";
    public static string SettingsButtonStyle { get; set; } = "SidebarButton";

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentSection = "Home";

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

    public MainViewModel()
        : this(new LibreHardwareSensorService())
    {
    }

    internal MainViewModel(IHardwareSensorService hardwareSensorService)
    {
        // TODO: Reemplazar con Dependency Injection cuando se configure
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
        NavigateTo("Home");
    }

    [RelayCommand]
    private void Navigate(string? section)
    {
        if (!string.IsNullOrEmpty(section))
        {
            NavigateTo(section);
        }
    }

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
            "Home" => CreatePlaceholder("Inicio - Panel principal"),
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
            _ => CreatePlaceholder("Sección no encontrada")
        };
    }

    private object CreateCompanySettingsView()
    {
        _companySettingsViewModel ??= new CompanySettingsViewModel(_settingsService);

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

        return new RestorePointView
        {
            DataContext = _restorePointViewModel
        };
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

        return new ReportView
        {
            DataContext = _reportViewModel
        };
    }

    private object CreateSmartDiskView()
    {
        _smartDiskViewModel ??= new SmartDiskViewModel(
            _smartctlRunner,
            _smartDiskService,
            _smartTestService);

        return new SmartDiskView
        {
            DataContext = _smartDiskViewModel
        };
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

    private static ContentControl CreatePlaceholder(string text)
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

        var title = new TextBlock
        {
            Text = text,
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)),
            Margin = new Thickness(0, 20, 0, 10)
        };

        var subtitle = new TextBlock
        {
            Text = "Panel principal en desarrollo.",
            FontSize = 14,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x66, 0x66, 0x66)),
            Margin = new Thickness(0, 0, 0, 20)
        };

        var version = new TextBlock
        {
            Text = "CATTECH OPTIMIZER PRO v0.3.0 (candidata)",
            FontSize = 12,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x99, 0x99, 0x99))
        };

        stackPanel.Children.Add(title);
        stackPanel.Children.Add(subtitle);
        stackPanel.Children.Add(version);

        border.Child = stackPanel;

        return new ContentControl { Content = border };
    }
}
