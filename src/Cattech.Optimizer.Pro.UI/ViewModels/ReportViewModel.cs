using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Startup;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Core.Models.VisualOptimization;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de informes técnicos.
/// </summary>
public partial class ReportViewModel : ObservableObject
{
    private readonly IReportGenerationService _reportService;
    private readonly ISettingsService _settingsService;
    private readonly IServiceReportService _serviceReportService;
    private readonly IDiagnosticService _diagnosticService;
    private readonly IStartupService _startupService;
    private readonly ITempCleanupService _cleanupService;
    private readonly IVisualOptimizationService _visualOptimizationService;
    private readonly IRestorePointService _restorePointService;

    // --- Estado de la UI ---

    [ObservableProperty]
    private string _statusText = "Sin datos";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _hasGeneratedReport;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // --- Datos cargados ---

    [ObservableProperty]
    private bool _hasCompanyData;

    [ObservableProperty]
    private bool _hasClientData;

    [ObservableProperty]
    private bool _hasDiagnosticData;

    [ObservableProperty]
    private bool _hasStartupData;

    [ObservableProperty]
    private bool _hasCleanupData;

    [ObservableProperty]
    private bool _hasVisualOptimizationData;

    [ObservableProperty]
    private bool _hasRestorePointData;

    // --- Secciones seleccionadas ---

    [ObservableProperty]
    private bool _includeCompany = true;

    [ObservableProperty]
    private bool _includeClient = true;

    [ObservableProperty]
    private bool _includeDiagnostic = true;

    [ObservableProperty]
    private bool _includeStartup = true;

    [ObservableProperty]
    private bool _includeCleanup = true;

    [ObservableProperty]
    private bool _includeVisualOptimization = true;

    [ObservableProperty]
    private bool _includeRestorePoint = true;

    [ObservableProperty]
    private bool _includeRecommendations = true;

    // --- Observaciones ---

    [ObservableProperty]
    private string _finalObservations = string.Empty;

    // --- Datos seleccionados ---

    [ObservableProperty]
    private ServiceReport? _selectedServiceReport;

    [ObservableProperty]
    private DiagnosticReport? _selectedDiagnosticReport;

    [ObservableProperty]
    private StartupAnalysis? _selectedStartupAnalysis;

    [ObservableProperty]
    private TempCleanupResult? _selectedCleanupResult;

    [ObservableProperty]
    private VisualOptimizationResult? _selectedVisualOptimizationResult;

    [ObservableProperty]
    private RestorePointResult? _selectedRestorePointResult;

    // --- Listas de datos disponibles ---

    public ObservableCollection<ServiceReport> AvailableServiceReports { get; } = new();
    public ObservableCollection<DiagnosticReport> AvailableDiagnosticReports { get; } = new();
    public ObservableCollection<StartupAnalysis> AvailableStartupAnalyses { get; } = new();
    public ObservableCollection<TempCleanupResult> AvailableCleanupResults { get; } = new();
    public ObservableCollection<VisualOptimizationResult> AvailableVisualOptResults { get; } = new();
    public ObservableCollection<RestorePointResult> AvailableRestorePointResults { get; } = new();

    // --- Último informe generado ---

    [ObservableProperty]
    private string _lastReportPath = string.Empty;

    public ReportViewModel(
        IReportGenerationService reportService,
        ISettingsService settingsService,
        IServiceReportService serviceReportService,
        IDiagnosticService diagnosticService,
        IStartupService startupService,
        ITempCleanupService cleanupService,
        IVisualOptimizationService visualOptimizationService,
        IRestorePointService restorePointService)
    {
        _reportService = reportService;
        _settingsService = settingsService;
        _serviceReportService = serviceReportService;
        _diagnosticService = diagnosticService;
        _startupService = startupService;
        _cleanupService = cleanupService;
        _visualOptimizationService = visualOptimizationService;
        _restorePointService = restorePointService;
    }

    /// <summary>
    /// Carga todos los datos disponibles.
    /// </summary>
    [RelayCommand]
    private async Task LoadDataAsync()
    {
        ClearMessages();
        IsRunning = true;
        StatusText = "Cargando datos...";

        try
        {
            // Empresa
            var settings = await _settingsService.LoadSettingsAsync();
            HasCompanyData = !string.IsNullOrWhiteSpace(settings.Company.Name);
            IncludeCompany = HasCompanyData;

            // Clientes
            var serviceReports = await _serviceReportService.ListReportsAsync(10);
            AvailableServiceReports.Clear();
            foreach (var sr in serviceReports)
            {
                var fullReport = await _serviceReportService.LoadReportAsync(sr.Id);
                if (fullReport != null) AvailableServiceReports.Add(fullReport);
            }
            HasClientData = AvailableServiceReports.Count > 0;
            if (HasClientData) SelectedServiceReport = AvailableServiceReports.First();
            IncludeClient = HasClientData;

            // Diagnósticos
            var diagnosticSummaries = await _diagnosticService.ListDiagnosticsAsync(10);
            AvailableDiagnosticReports.Clear();
            foreach (var ds in diagnosticSummaries)
            {
                var fullDiag = await _diagnosticService.LoadDiagnosticAsync(ds.Id);
                if (fullDiag != null) AvailableDiagnosticReports.Add(fullDiag);
            }
            HasDiagnosticData = AvailableDiagnosticReports.Count > 0;
            if (HasDiagnosticData) SelectedDiagnosticReport = AvailableDiagnosticReports.First();
            IncludeDiagnostic = HasDiagnosticData;

            // Inicio
            var startupAnalyses = await _startupService.ListAnalysesAsync(5);
            AvailableStartupAnalyses.Clear();
            foreach (var sa in startupAnalyses)
            {
                var fullSa = await _startupService.LoadAnalysisAsync(sa.Id);
                if (fullSa != null) AvailableStartupAnalyses.Add(fullSa);
            }
            HasStartupData = AvailableStartupAnalyses.Count > 0;
            if (HasStartupData) SelectedStartupAnalysis = AvailableStartupAnalyses.First();
            IncludeStartup = HasStartupData;

            // Limpieza
            var cleanupSummaries = await _cleanupService.ListResultsAsync(5);
            AvailableCleanupResults.Clear();
            HasCleanupData = cleanupSummaries.Count > 0;
            if (HasCleanupData)
            {
                // Los resultados de limpieza ya vienen completos en el listado
                // Usamos los summaries como placeholders - en producción se cargarían completos
                SelectedCleanupResult = null;
            }
            IncludeCleanup = HasCleanupData;

            // Optimización visual
            HasVisualOptimizationData = false;
            IncludeVisualOptimization = false;

            // Punto de restauración
            var restoreSummaries = await _restorePointService.ListResultsAsync(5);
            AvailableRestorePointResults.Clear();
            HasRestorePointData = restoreSummaries.Count > 0;
            if (HasRestorePointData)
            {
                // Los resultados de restauración ya vienen completos en el listado
                SelectedRestorePointResult = null;
            }
            IncludeRestorePoint = HasRestorePointData;

            StatusText = "Datos cargados";
        }
        catch (Exception ex)
        {
            StatusText = "Error al cargar";
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Genera el informe HTML.
    /// </summary>
    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        ClearMessages();
        IsRunning = true;
        StatusText = "Generando informe...";

        try
        {
            var options = new ReportGenerationOptions
            {
                Settings = await _settingsService.LoadSettingsAsync(),
                ServiceReport = SelectedServiceReport,
                DiagnosticReport = SelectedDiagnosticReport,
                StartupAnalysis = SelectedStartupAnalysis,
                CleanupResult = SelectedCleanupResult,
                VisualOptimizationResult = SelectedVisualOptimizationResult,
                RestorePointResult = SelectedRestorePointResult,
                FinalObservations = FinalObservations,
                IncludeCompany = IncludeCompany,
                IncludeClient = IncludeClient,
                IncludeDiagnostic = IncludeDiagnostic,
                IncludeStartup = IncludeStartup,
                IncludeCleanup = IncludeCleanup,
                IncludeVisualOptimization = IncludeVisualOptimization,
                IncludeRestorePoint = IncludeRestorePoint,
                IncludeRecommendations = IncludeRecommendations
            };

            var filePath = await _reportService.GenerateHtmlReportAsync(options);

            // Guardar info del informe
            var info = new GeneratedReportInfo
            {
                ClientName = SelectedServiceReport?.Client?.Name ?? "Sin cliente",
                EquipmentName = $"{SelectedServiceReport?.Equipment?.Brand} {SelectedServiceReport?.Equipment?.Model}",
                HtmlPath = filePath,
                Notes = FinalObservations
            };

            if (IncludeCompany) info.IncludedSections.Add("Empresa");
            if (IncludeClient) info.IncludedSections.Add("Cliente");
            if (IncludeDiagnostic) info.IncludedSections.Add("Diagnóstico");
            if (IncludeStartup) info.IncludedSections.Add("Inicio");
            if (IncludeCleanup) info.IncludedSections.Add("Limpieza");
            if (IncludeVisualOptimization) info.IncludedSections.Add("Optimización");
            if (IncludeRestorePoint) info.IncludedSections.Add("Restauración");
            if (IncludeRecommendations) info.IncludedSections.Add("Recomendaciones");

            await _reportService.SaveReportInfoAsync(info);

            LastReportPath = filePath;
            HasGeneratedReport = true;
            StatusText = "Informe generado";
            ShowSuccess($"Informe guardado: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            StatusText = "Error al generar";
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Abre el último informe generado.
    /// </summary>
    [RelayCommand]
    private async Task OpenReportAsync()
    {
        if (!string.IsNullOrEmpty(LastReportPath))
        {
            await _reportService.OpenReportAsync(LastReportPath);
        }
    }

    /// <summary>
    /// Abre la carpeta de informes.
    /// </summary>
    [RelayCommand]
    private async Task OpenReportsFolderAsync()
    {
        await _reportService.OpenReportsFolderAsync();
    }

    private void ShowSuccess(string message)
    {
        StatusMessage = message;
        IsSuccess = true;
        HasError = false;
        ErrorMessage = string.Empty;
    }

    private void ShowError(string message)
    {
        StatusMessage = string.Empty;
        IsSuccess = false;
        HasError = true;
        ErrorMessage = message;
    }

    private void ClearMessages()
    {
        StatusMessage = string.Empty;
        IsSuccess = false;
        HasError = false;
        ErrorMessage = string.Empty;
    }
}
