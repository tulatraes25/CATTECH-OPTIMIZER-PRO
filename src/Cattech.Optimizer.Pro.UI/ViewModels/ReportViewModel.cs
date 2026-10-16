using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
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
using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de informes técnicos.
/// </summary>
public partial class ReportViewModel : ObservableObject
{
    private readonly IReportGenerationService _reportService;
    private readonly IPdfExportService _pdfExportService;
    private readonly ISettingsService _settingsService;
    private readonly IServiceReportService _serviceReportService;
    private readonly IDiagnosticService _diagnosticService;
    private readonly IStartupService _startupService;
    private readonly ITempCleanupService _cleanupService;
    private readonly IVisualOptimizationService _visualOptimizationService;
    private readonly IRestorePointService _restorePointService;
    private readonly ISmartDiskService _smartDiskService;
    private readonly ISmartTestService _smartTestService;

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

    [ObservableProperty]
    private bool _hasReportInventoryWarning;

    [ObservableProperty]
    private string _reportInventoryMessage = string.Empty;

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

    [ObservableProperty]
    private bool _hasSmartData;

    [ObservableProperty]
    private bool _hasSmartTestData;

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
    private bool _includeSmart = true;

    [ObservableProperty]
    private bool _includeSmartTests;

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

    [ObservableProperty]
    private SmartAnalysisResult? _selectedSmartAnalysis;

    // --- Listas de datos disponibles ---

    public ObservableCollection<ServiceReport> AvailableServiceReports { get; } = new();
    public ObservableCollection<DiagnosticReport> AvailableDiagnosticReports { get; } = new();
    public ObservableCollection<StartupAnalysis> AvailableStartupAnalyses { get; } = new();
    public ObservableCollection<TempCleanupResult> AvailableCleanupResults { get; } = new();
    public ObservableCollection<VisualOptimizationResult> AvailableVisualOptResults { get; } = new();
    public ObservableCollection<RestorePointResult> AvailableRestorePointResults { get; } = new();
    public ObservableCollection<SmartAnalysisResult> AvailableSmartAnalyses { get; } = new();
    public ObservableCollection<SmartTestSessionSelectionItem> AvailableSmartTestSessions { get; } = new();

    // --- Último informe generado ---

    [ObservableProperty]
    private string _lastReportPath = string.Empty;

    // --- PDF ---

    [ObservableProperty]
    private string _lastPdfPath = string.Empty;

    [ObservableProperty]
    private bool _hasGeneratedPdf;

    [ObservableProperty]
    private bool _isExportingPdf;

    [ObservableProperty]
    private string _pdfStatusText = string.Empty;

    public ReportViewModel(
        IReportGenerationService reportService,
        IPdfExportService pdfExportService,
        ISettingsService settingsService,
        IServiceReportService serviceReportService,
        IDiagnosticService diagnosticService,
        IStartupService startupService,
        ITempCleanupService cleanupService,
        IVisualOptimizationService visualOptimizationService,
        IRestorePointService restorePointService,
        ISmartDiskService smartDiskService,
        ISmartTestService smartTestService)
    {
        _reportService = reportService;
        _pdfExportService = pdfExportService;
        _settingsService = settingsService;
        _serviceReportService = serviceReportService;
        _diagnosticService = diagnosticService;
        _startupService = startupService;
        _cleanupService = cleanupService;
        _visualOptimizationService = visualOptimizationService;
        _restorePointService = restorePointService;
        _smartDiskService = smartDiskService;
        _smartTestService = smartTestService;
    }

    /// <summary>
    /// Advisory read-only check. Never use the snapshot as authorization to
    /// load, delete or export a specific file without revalidation.
    /// </summary>
    private async Task<bool> CheckReportInventoryAsync()
    {
        if (_serviceReportService is not IServiceReportInventoryReader reader)
        {
            HasReportInventoryWarning = true;
            ReportInventoryMessage = "La integridad de los informes no se puede comprobar. " +
                "No se cargarán clientes desde este almacenamiento.";
            return false;
        }

        try
        {
            var health = await reader.InspectReportInventoryAsync();
            if (health.ScanFailed)
            {
                HasReportInventoryWarning = true;
                ReportInventoryMessage = "No se pudo completar la comprobación de informes. " +
                    "No se cargarán datos de clientes hasta revisar el almacenamiento.";
                return false;
            }

            if (health.RequiresReview)
            {
                HasReportInventoryWarning = true;
                ReportInventoryMessage =
                    $"Almacenamiento a revisar: {health.InvalidEntries} archivos inválidos y " +
                    $"{health.AmbiguousEntries} registros con identificador duplicado. " +
                    "Se suspendió la selección de clientes; ningún archivo fue eliminado.";
                return false;
            }

            HasReportInventoryWarning = false;
            ReportInventoryMessage = $"Inventario revisado: {health.UniqueEntries} informes sin conflictos detectados.";
            return true;
        }
        catch (Exception)
        {
            // Never surface arbitrary file paths or exception text in the UI.
            HasReportInventoryWarning = true;
            ReportInventoryMessage = "No fue posible comprobar los informes de servicio. " +
                "Se suspendió la selección de clientes sin modificar archivos.";
            return false;
        }
    }

    /// <summary>
    /// Advisory pre-output revalidation. Does not guarantee file-handle identity
    /// or physical equipment identity; stored data may change after this check.
    /// </summary>
    private async Task<bool> VerifySelectedClientBeforeOutputAsync()
    {
        var selected = SelectedServiceReport;
        if (selected is null)
            return false;

        if (!await CheckReportInventoryAsync())
        {
            RevokeCustomerSelection("La integridad de los informes cambió. " +
                "Se canceló la generación con datos de clientes.");
            return false;
        }

        try
        {
            var live = await _serviceReportService.LoadReportAsync(selected.Id);
            // Compare the full semantic report, not only name/model/serial:
            // phone, service notes, actions and all exportable values matter.
            // Do not log or persist the serialized customer payload.
            // This reduces stale data, but cannot pin on-disk identity or
            // prevent external writes immediately after this check.
            if (live is not null
                && string.Equals(JsonSerializer.Serialize(live),
                    JsonSerializer.Serialize(selected), StringComparison.Ordinal))
                return true;
        }
        catch (Exception)
        {
            // Revalidation failed. No raw file names or exception details shown.
        }

        RevokeCustomerSelection("El informe seleccionado cambió o dejó de estar disponible. " +
            "Se suspendió la generación con datos de clientes.");
        return false;
    }

    private void RevokeCustomerSelection(string message)
    {
        HasReportInventoryWarning = true;
        ReportInventoryMessage = message;
        AvailableServiceReports.Clear();
        SelectedServiceReport = null;
        HasClientData = false;
        IncludeClient = false;
        InvalidateGeneratedArtifacts();
        ShowError("No se generó un informe con datos de clientes. Revisá el almacenamiento y volvé a cargar.");
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
        // Fail closed immediately: stale customer selections must not survive
        // an unsuccessful refresh or a newly corrupted report store.
        HasReportInventoryWarning = true;
        ReportInventoryMessage = "Comprobando integridad de informes...";
        AvailableServiceReports.Clear();
        SelectedServiceReport = null;
        HasClientData = false;
        IncludeClient = false;
        InvalidateGeneratedArtifacts();

        try
        {
            // Empresa
            var settings = await _settingsService.LoadSettingsAsync();
            HasCompanyData = !string.IsNullOrWhiteSpace(settings.Company.Name);
            IncludeCompany = HasCompanyData;

            // Clientes: advertise problems before presenting any customer choice.
            if (await CheckReportInventoryAsync())
            {
                try
                {
                    var serviceReports = await _serviceReportService.ListReportsAsync(10);
                    foreach (var sr in serviceReports)
                    {
                        var fullReport = await _serviceReportService.LoadReportAsync(sr.Id);
                        if (fullReport is null)
                        {
                            // The store may have changed since the read-only scan.
                            HasReportInventoryWarning = true;
                            ReportInventoryMessage = "Un informe cambió o no pudo verificarse. " +
                                "Se suspendió la selección de clientes; no se modificaron archivos.";
                            AvailableServiceReports.Clear();
                            break;
                        }
                        AvailableServiceReports.Add(fullReport);
                    }
                }
                catch (Exception)
                {
                    HasReportInventoryWarning = true;
                    ReportInventoryMessage = "No se pudo completar la carga de informes. " +
                        "Se suspendió la selección de clientes sin modificar archivos.";
                    AvailableServiceReports.Clear();
                }
            }

            HasClientData = !HasReportInventoryWarning && AvailableServiceReports.Count > 0;
            SelectedServiceReport = HasClientData ? AvailableServiceReports.First() : null;
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

            // SMART (solo datos persistidos, NO se ejecuta smartctl)
            var smartAnalyses = await _smartDiskService.ListResultsAsync(10);
            AvailableSmartAnalyses.Clear();
            foreach (var smart in smartAnalyses)
                AvailableSmartAnalyses.Add(smart);
            HasSmartData = AvailableSmartAnalyses.Count > 0;
            if (HasSmartData)
            {
                SelectedSmartAnalysis = AvailableSmartAnalyses.First();
                IncludeSmart = true;
            }
            else
            {
                SelectedSmartAnalysis = null;
                IncludeSmart = false;
            }

            // Self-tests SMART persistidos (solo lectura, NO se consulta estado)
            var smartTests = await _smartTestService.ListSessionsAsync(20);
            AvailableSmartTestSessions.Clear();
            foreach (var test in smartTests)
            {
                AvailableSmartTestSessions.Add(new SmartTestSessionSelectionItem
                {
                    Session = test,
                    IsSelected = false
                });
            }
            HasSmartTestData = AvailableSmartTestSessions.Count > 0;
            IncludeSmartTests = false; // Selección manual intencional

            StatusText = HasReportInventoryWarning
                ? "Informes requieren revisión" : "Datos cargados";

            // Invalidar artifacts generados previamente para forzar regeneración
            // con los datos actualmente cargados (fix SMOKE-B1R-006)
            InvalidateGeneratedArtifacts();
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
            var options = await BuildReportOptionsAsync();

            // Validar selecciones obligatorias antes de generar
            if (options.IncludeClient && options.ServiceReport == null)
            {
                ShowError("Seleccioná un cliente/equipo antes de generar el informe.");
                return;
            }
            if (options.IncludeDiagnostic && options.DiagnosticReport == null)
            {
                ShowError("Seleccioná un diagnóstico antes de generar el informe.");
                return;
            }
            if (options.IncludeClient && !await VerifySelectedClientBeforeOutputAsync())
                return;

            var filePath = await _reportService.GenerateHtmlReportAsync(options);

            // Guardar info del informe
            var info = new GeneratedReportInfo
            {
                ClientName = options.IncludeClient
                    ? options.ServiceReport?.Client?.Name ?? "Sin cliente" : "Sin cliente",
                EquipmentName = options.IncludeClient
                    ? $"{options.ServiceReport?.Equipment?.Brand} {options.ServiceReport?.Equipment?.Model}"
                    : string.Empty,
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
            if (IncludeSmart && SelectedSmartAnalysis != null) info.IncludedSections.Add("SMART");
            if (IncludeSmartTests && AvailableSmartTestSessions.Any(x => x.IsSelected)) info.IncludedSections.Add("SMART Self-Tests");
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

    /// <summary>
    /// Exporta el informe a PDF.
    /// </summary>
    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        // Siempre regenerar HTML con el snapshot actual de datos (fix SMOKE-B1R-006)
        // Esto asegura que el PDF refleje las selecciones actuales, no un HTML stale.
        StatusText = "Generando HTML primero...";
        ReportGenerationOptions options;
        try
        {
            options = await BuildReportOptionsAsync();

            // Validar selecciones obligatorias antes de generar
            if (options.IncludeClient && options.ServiceReport == null)
            {
                ShowError("Seleccioná un cliente/equipo antes de generar el informe.");
                return;
            }
            if (options.IncludeDiagnostic && options.DiagnosticReport == null)
            {
                ShowError("Seleccioná un diagnóstico antes de generar el informe.");
                return;
            }
            if (options.IncludeClient && !await VerifySelectedClientBeforeOutputAsync())
                return;

            LastReportPath = await _reportService.GenerateHtmlReportAsync(options);
            HasGeneratedReport = true;
        }
        catch (Exception ex)
        {
            ShowError($"Error al generar HTML: {ex.Message}");
            return;
        }

        ClearMessages();
        IsExportingPdf = true;
        PdfStatusText = "Exportando PDF...";

        try
        {
            var pdfPath = _pdfExportService.GetPdfOutputPath(LastReportPath);

            // Asegurar que el directorio existe
            var pdfDir = Path.GetDirectoryName(pdfPath);
            if (!string.IsNullOrEmpty(pdfDir) && !Directory.Exists(pdfDir))
            {
                Directory.CreateDirectory(pdfDir);
            }

            var success = await _pdfExportService.ExportHtmlToPdfAsync(LastReportPath, pdfPath);

            if (success && File.Exists(pdfPath))
            {
                LastPdfPath = pdfPath;
                HasGeneratedPdf = true;
                PdfStatusText = "PDF generado";
                ShowSuccess($"PDF guardado: {Path.GetFileName(pdfPath)}");

                // Actualizar info del informe
                var info = new GeneratedReportInfo
                {
                    ClientName = options.IncludeClient
                    ? options.ServiceReport?.Client?.Name ?? "Sin cliente" : "Sin cliente",
                    EquipmentName = options.IncludeClient
                    ? $"{options.ServiceReport?.Equipment?.Brand} {options.ServiceReport?.Equipment?.Model}"
                    : string.Empty,
                    HtmlPath = LastReportPath,
                    PdfPath = pdfPath,
                    Notes = FinalObservations
                };
                await _reportService.SaveReportInfoAsync(info);
            }
            else
            {
                PdfStatusText = "Error al exportar";
                ShowError("No se pudo generar el PDF. Verifique que Microsoft Edge esté instalado.");
            }
        }
        catch (Exception ex)
        {
            PdfStatusText = "Error al exportar";
            ShowError($"Error al exportar PDF: {ex.Message}");
        }
        finally
        {
            IsExportingPdf = false;
        }
    }

    /// <summary>
    /// Abre el último PDF generado.
    /// </summary>
    [RelayCommand]
    private async Task OpenPdfAsync()
    {
        if (!string.IsNullOrEmpty(LastPdfPath))
        {
            await _pdfExportService.OpenPdfAsync(LastPdfPath);
        }
    }

    /// <summary>
    /// Construye las opciones del informe desde los datos actuales.
    /// Única fuente lógica para generar informes (HTML y PDF).
    /// </summary>
    private async Task<ReportGenerationOptions> BuildReportOptionsAsync()
    {
        // Solo sesiones seleccionadas manualmente
        var selectedTests = AvailableSmartTestSessions
            .Where(x => x.IsSelected)
            .Select(x => x.Session)
            .ToList();

        return new ReportGenerationOptions
        {
            Settings = await _settingsService.LoadSettingsAsync(),
            // Unchecked customer sections must not carry data into exports.
            ServiceReport = IncludeClient ? SelectedServiceReport : null,
            DiagnosticReport = SelectedDiagnosticReport,
            StartupAnalysis = SelectedStartupAnalysis,
            CleanupResult = SelectedCleanupResult,
            VisualOptimizationResult = SelectedVisualOptimizationResult,
            RestorePointResult = SelectedRestorePointResult,
            SmartAnalysis = SelectedSmartAnalysis,
            SmartTestSessions = selectedTests,
            FinalObservations = FinalObservations,
            IncludeCompany = IncludeCompany,
            IncludeClient = IncludeClient,
            IncludeDiagnostic = IncludeDiagnostic,
            IncludeStartup = IncludeStartup,
            IncludeCleanup = IncludeCleanup,
            IncludeVisualOptimization = IncludeVisualOptimization,
            IncludeRestorePoint = IncludeRestorePoint,
            IncludeSmart = IncludeSmart,
            IncludeSmartTests = IncludeSmartTests && selectedTests.Count > 0,
            IncludeRecommendations = IncludeRecommendations
        };
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

    /// <summary>
    /// Invalida los artifacts generados previamente (HTML/PDF) para forzar
    /// regeneración con los datos actualmente cargados.
    /// No borra físicamente informes históricos, solo limpia el estado actual.
    /// </summary>
    private void InvalidateGeneratedArtifacts()
    {
        LastReportPath = string.Empty;
        HasGeneratedReport = false;
        LastPdfPath = string.Empty;
        HasGeneratedPdf = false;
        PdfStatusText = string.Empty;
    }
}

/// <summary>
/// Wrapper de UI para selección manual de sesiones de self-test SMART.
/// No se persiste.
/// </summary>
public partial class SmartTestSessionSelectionItem : ObservableObject
{
    /// <summary>Sesión SMART persistida.</summary>
    public required SmartTestSession Session { get; init; }

    /// <summary>Si la sesión fue seleccionada para el informe.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Nombre de presentación: fecha - tipo - modelo/dispositivo - estado.
    /// </summary>
    public string DisplayName
    {
        get
        {
            var date = Session.RequestedAt.ToString("dd/MM/yyyy HH:mm");
            var type = Session.TestType == SmartTestType.Extended ? "Extendido" : "Corto";
            var model = !string.IsNullOrEmpty(Session.ModelName) ? Session.ModelName : Session.Device;
            var status = Session.Status.ToDisplayMessage();
            return $"{date} - {type} - {model} - {status}";
        }
    }
}
