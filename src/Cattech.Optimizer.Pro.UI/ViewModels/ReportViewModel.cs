using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
        AvailableSmartTestSessions.CollectionChanged += OnSmartTestCollectionChanged;
    }

    /// <summary>
    /// SMART tests are checkboxes on child view-models rather than a scalar
    /// property of this report view-model. Each change invalidates the
    /// previous "current" HTML/PDF without deleting historical files.
    /// </summary>
    private void OnSmartTestCollectionChanged(
        object? sender, NotifyCollectionChangedEventArgs args)
    {
        // Reconcile all subscriptions after Add/Remove/Replace/Reset.
        // In particular, Reset has no OldItems in ObservableCollection.
        var current = AvailableSmartTestSessions.ToHashSet();
        foreach (var old in _observedSmartTestSelections.Except(current).ToArray())
        {
            old.PropertyChanged -= OnSmartTestSelectionPropertyChanged;
            _observedSmartTestSelections.Remove(old);
        }

        foreach (var item in current.Except(_observedSmartTestSelections))
        {
            item.PropertyChanged += OnSmartTestSelectionPropertyChanged;
            _observedSmartTestSelections.Add(item);
        }

        InvalidateGeneratedArtifacts();
    }

    private void OnSmartTestSelectionPropertyChanged(
        object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SmartTestSessionSelectionItem.IsSelected))
            InvalidateGeneratedArtifacts();
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
            // The store inventory is the total; the customer selector is a
            // 10-entry preview, NOT the full archive. Never imply that reports
            // beyond the visible list were lost or deleted.
            ReportInventoryMessage = health.UniqueEntries > 10
                ? $"Inventario revisado: {health.UniqueEntries} informes sin conflictos detectados. " +
                  "La pantalla muestra como máximo los 10 más recientes; los demás siguen guardados."
                : $"Inventario revisado: {health.UniqueEntries} informes sin conflictos detectados.";
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

    // Selecting another report or changing the customer checkbox invalidates
    // an in-flight export, even if the operator later restores the same choice.
    // WPF updates these properties on its dispatcher; no global storage writes.
    private long _customerSelectionRevision;
    private long _artifactRevision;
    private long _activeExportAttempt;
    private readonly HashSet<SmartTestSessionSelectionItem> _observedSmartTestSelections = new();

    partial void OnSelectedServiceReportChanged(ServiceReport? value)
    {
        _customerSelectionRevision++;
        InvalidateGeneratedArtifacts();
    }

    partial void OnIncludeClientChanged(bool value)
    {
        _customerSelectionRevision++;
        InvalidateGeneratedArtifacts();
    }

    // An artifact represents the previously selected options, not the current
    // form. On an edit, revoke its "latest report" buttons. Historical files
    // stay untouched and are still accessible through the report history.
    partial void OnIncludeCompanyChanged(bool value) => InvalidateGeneratedArtifacts();
    partial void OnIncludeDiagnosticChanged(bool value) => InvalidateGeneratedArtifacts();
    partial void OnIncludeStartupChanged(bool value) => InvalidateGeneratedArtifacts();
    partial void OnIncludeCleanupChanged(bool value) => InvalidateGeneratedArtifacts();
    partial void OnIncludeVisualOptimizationChanged(bool value) => InvalidateGeneratedArtifacts();
    partial void OnIncludeRestorePointChanged(bool value) => InvalidateGeneratedArtifacts();
    partial void OnIncludeSmartChanged(bool value) => InvalidateGeneratedArtifacts();
    partial void OnIncludeSmartTestsChanged(bool value) => InvalidateGeneratedArtifacts();
    partial void OnIncludeRecommendationsChanged(bool value) => InvalidateGeneratedArtifacts();
    partial void OnSelectedDiagnosticReportChanged(DiagnosticReport? value) => InvalidateGeneratedArtifacts();
    partial void OnSelectedStartupAnalysisChanged(StartupAnalysis? value) => InvalidateGeneratedArtifacts();
    partial void OnSelectedCleanupResultChanged(TempCleanupResult? value) => InvalidateGeneratedArtifacts();
    partial void OnSelectedVisualOptimizationResultChanged(VisualOptimizationResult? value) => InvalidateGeneratedArtifacts();
    partial void OnSelectedRestorePointResultChanged(RestorePointResult? value) => InvalidateGeneratedArtifacts();
    partial void OnSelectedSmartAnalysisChanged(SmartAnalysisResult? value) => InvalidateGeneratedArtifacts();
    partial void OnFinalObservationsChanged(string value) => InvalidateGeneratedArtifacts();

    /// <summary>
    /// Compare the EXACT client report captured for this export, not whatever
    /// happens to be selected after asynchronous storage/inventory reads.
    /// Serialize only in memory and detach the validated payload from mutable
    /// WPF state. This remains advisory: file identity can change after read.
    /// </summary>
    private async Task<bool> VerifySelectedClientBeforeOutputAsync(
        ReportGenerationOptions options)
    {
        var selected = options.ServiceReport;
        if (selected is null)
            return false;

        var capturedRevision = _customerSelectionRevision;

        bool SelectionStillCurrent() =>
            _customerSelectionRevision == capturedRevision
            && ReferenceEquals(SelectedServiceReport, selected)
            && IncludeClient && options.IncludeClient;

        bool RejectChangedSelection()
        {
            InvalidateGeneratedArtifacts();
            ShowError("La selección o los datos del cliente cambiaron mientras se preparaba el informe. Volvé a generar con la selección actual.");
            return false;
        }

        if (!SelectionStillCurrent())
            return RejectChangedSelection();

        string selectedSnapshot;
        try
        {
            selectedSnapshot = JsonSerializer.Serialize(selected);
        }
        catch (Exception)
        {
            RevokeCustomerSelection("Los datos seleccionados no pudieron verificarse.");
            return false;
        }

        if (!await CheckReportInventoryAsync())
        {
            RevokeCustomerSelection("La integridad de los informes cambió. " +
                "Se canceló la generación con datos de clientes.");
            return false;
        }

        if (!SelectionStillCurrent())
            return RejectChangedSelection();

        try
        {
            var live = await _serviceReportService.LoadReportAsync(selected.Id);

            // Snapshot mutation and ABA selection changes both fail closed.
            if (!SelectionStillCurrent()
                || !string.Equals(JsonSerializer.Serialize(selected),
                    selectedSnapshot, StringComparison.Ordinal))
                return RejectChangedSelection();

            if (live is not null
                && string.Equals(JsonSerializer.Serialize(live),
                    selectedSnapshot, StringComparison.Ordinal))
            {
                // The exporter receives its own detached customer snapshot.
                // Later changes to the WPF form cannot rewrite this payload.
                options.ServiceReport = JsonSerializer.Deserialize<ServiceReport>(
                    selectedSnapshot) ?? throw new InvalidDataException(
                        "El reporte de cliente no pudo reconstruirse.");
                return true;
            }
        }
        catch (Exception)
        {
            // Do not show private filesystem paths or serialized client data.
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
    /// <summary>
    /// Immediately revoke ALL prior report-view case bindings on refresh.
    /// Data sources can fail after any awaited I/O; partially reloaded
    /// diagnostics or SMART sessions never inherit an earlier case's data.
    /// Explicit refresh also discards unsaved report-only observations.
    /// This never deletes stored reports or actual diagnostic evidence.
    /// </summary>
    private void RevokeAllReportSelections()
    {
        HasCompanyData = false;
        IncludeCompany = false;

        AvailableServiceReports.Clear();
        SelectedServiceReport = null;
        HasClientData = false;
        IncludeClient = false;

        AvailableDiagnosticReports.Clear();
        SelectedDiagnosticReport = null;
        HasDiagnosticData = false;
        IncludeDiagnostic = false;

        AvailableStartupAnalyses.Clear();
        SelectedStartupAnalysis = null;
        HasStartupData = false;
        IncludeStartup = false;

        AvailableCleanupResults.Clear();
        SelectedCleanupResult = null;
        HasCleanupData = false;
        IncludeCleanup = false;

        AvailableVisualOptResults.Clear();
        SelectedVisualOptimizationResult = null;
        HasVisualOptimizationData = false;
        IncludeVisualOptimization = false;

        AvailableRestorePointResults.Clear();
        SelectedRestorePointResult = null;
        HasRestorePointData = false;
        IncludeRestorePoint = false;

        AvailableSmartAnalyses.Clear();
        SelectedSmartAnalysis = null;
        HasSmartData = false;
        IncludeSmart = false;

        AvailableSmartTestSessions.Clear();
        HasSmartTestData = false;
        IncludeSmartTests = false;

        FinalObservations = string.Empty;
        InvalidateGeneratedArtifacts();
    }

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
        RevokeAllReportSelections();

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

            // History entries are SUMMARIES, not selectable full results.
            // No audited LoadResultAsync exists; never invent missing details.
            var cleanupSummaries = await _cleanupService.ListResultsAsync(5);
            AvailableCleanupResults.Clear();
            SelectedCleanupResult = null;
            HasCleanupData = false;
            IncludeCleanup = false;

            // Optimización visual
            HasVisualOptimizationData = false;
            IncludeVisualOptimization = false;

            // Restore history is likewise summary-only. A future full
            // loader needs stable IDs, schema validation and permission gates.
            var restoreSummaries = await _restorePointService.ListResultsAsync(5);
            AvailableRestorePointResults.Clear();
            SelectedRestorePointResult = null;
            HasRestorePointData = false;
            IncludeRestorePoint = false;

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
                ? "Informes requieren revisión"
                : cleanupSummaries.Count > 0 || restoreSummaries.Count > 0
                    ? "Datos cargados; historiales de limpieza/restauración no exportables"
                    : "Datos cargados";

            // Invalidar artifacts generados previamente para forzar regeneración
            // con los datos actualmente cargados (fix SMOKE-B1R-006)
            InvalidateGeneratedArtifacts();
        }
        catch (Exception)
        {
            // Fail closed if loading stops after customer records were selected.
            // Exception messages may include local paths or personal data.
            StatusText = "Error al cargar";
            HasReportInventoryWarning = true;
            ReportInventoryMessage = "La carga quedó incompleta. Volvé a revisar el almacenamiento de informes.";
            RevokeAllReportSelections();
            ShowError("No se pudieron cargar todos los datos. Revisá el almacenamiento y volvé a intentar.");
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
        var attempt = BeginExportAttempt();
        var revision = _artifactRevision;
        IsRunning = true;
        StatusText = "Generando informe...";

        try
        {
            var options = await BuildReportOptionsAsync();
            if (!EnsureExportStillCurrent(attempt, revision))
                return;

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
            if (options.IncludeClient && !await VerifySelectedClientBeforeOutputAsync(options))
                return;
            if (!EnsureExportStillCurrent(attempt, revision))
                return;

            var filePath = await _reportService.GenerateHtmlReportAsync(options);
            // The HTML may now exist on disk; a changed form must not give
            // the old attempt a "current report" button or a success claim.
            if (!EnsureExportStillCurrent(attempt, revision))
                return;

            var info = CreateGeneratedReportInfo(options, filePath);

            await _reportService.SaveReportInfoAsync(info);
            if (!EnsureExportStillCurrent(attempt, revision))
                return;

            LastReportPath = filePath;
            HasGeneratedReport = true;
            StatusText = "Informe generado";
            ShowSuccess($"Informe guardado: {Path.GetFileName(filePath)}");
        }
        catch (Exception)
        {
            if (attempt == _activeExportAttempt)
            {
                StatusText = "Error al generar";
                // Output may have been written before the index failed.
                ShowError("No se pudo completar el informe. Revisá el almacenamiento y el historial antes de reintentar.");
            }
        }
        finally
        {
            if (attempt == _activeExportAttempt)
                IsRunning = false;
        }
    }

    /// <summary>
    /// Abre el último informe generado.
    /// </summary>
    [RelayCommand]
    private async Task OpenReportAsync()
    {
        if (HasGeneratedReport && !string.IsNullOrEmpty(LastReportPath))
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
        ClearMessages();
        var attempt = BeginExportAttempt();
        var revision = _artifactRevision;
        // Siempre regenerar HTML con el snapshot actual de datos (fix SMOKE-B1R-006)
        // Esto asegura que el PDF refleje las selecciones actuales, no un HTML stale.
        StatusText = "Generando HTML primero...";
        ReportGenerationOptions options;
        // Keep the generated HTML identity stable even if another UI action
        // changes the 'last report' field while the PDF renderer is awaiting.
        string htmlPath;
        try
        {
            options = await BuildReportOptionsAsync();
            if (!EnsureExportStillCurrent(attempt, revision))
                return;

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
            if (options.IncludeClient && !await VerifySelectedClientBeforeOutputAsync(options))
                return;
            if (!EnsureExportStillCurrent(attempt, revision))
                return;

            htmlPath = await _reportService.GenerateHtmlReportAsync(options);
            if (!EnsureExportStillCurrent(attempt, revision))
                return;
            LastReportPath = htmlPath;
            HasGeneratedReport = true;
        }
        catch (Exception)
        {
            if (attempt == _activeExportAttempt)
                ShowError("No se pudo preparar el HTML para el PDF. Revisá el almacenamiento y volvé a intentar.");
            return;
        }

        ClearMessages();
        IsExportingPdf = true;
        PdfStatusText = "Exportando PDF...";

        try
        {
            var pdfPath = _pdfExportService.GetPdfOutputPath(htmlPath);

            // Asegurar que el directorio existe
            var pdfDir = Path.GetDirectoryName(pdfPath);
            if (!string.IsNullOrEmpty(pdfDir) && !Directory.Exists(pdfDir))
            {
                Directory.CreateDirectory(pdfDir);
            }

            var success = await _pdfExportService.ExportHtmlToPdfAsync(htmlPath, pdfPath);

            if (success && File.Exists(pdfPath))
            {
                // Record accurate artifact metadata first. If the form changed
                // while Edge ran, never present its old PDF as the current one.
                var info = CreateGeneratedReportInfo(options, htmlPath, pdfPath);
                await _reportService.SaveReportInfoAsync(info);
                if (!EnsureExportStillCurrent(attempt, revision))
                    return;

                LastPdfPath = pdfPath;
                HasGeneratedPdf = true;
                PdfStatusText = "PDF generado";
                ShowSuccess($"PDF guardado: {Path.GetFileName(pdfPath)}");
            }
            else if (attempt == _activeExportAttempt)
            {
                PdfStatusText = "Error al exportar";
                ShowError("No se pudo generar el PDF. Verifique que Microsoft Edge esté instalado.");
            }
        }
        catch (Exception)
        {
            if (attempt == _activeExportAttempt)
            {
                PdfStatusText = "Error al exportar";
                // Include neither renderer command lines nor filesystem paths.
                ShowError("No se pudo completar el PDF. Revisá el almacenamiento y el historial antes de reintentar.");
            }
        }
        finally
        {
            if (attempt == _activeExportAttempt)
                IsExportingPdf = false;
        }
    }

    /// <summary>
    /// Abre el último PDF generado.
    /// </summary>
    [RelayCommand]
    private async Task OpenPdfAsync()
    {
        if (HasGeneratedPdf && !string.IsNullOrEmpty(LastPdfPath))
        {
            await _pdfExportService.OpenPdfAsync(LastPdfPath);
        }
    }

    /// <summary>
    /// Export attempts are monotonically numbered. Mutations of any report
    /// selection call InvalidateGeneratedArtifacts, advancing the revision.
    /// A superseded older attempt must never overwrite newer UI feedback.
    /// </summary>
    private long BeginExportAttempt()
    {
        _activeExportAttempt++;
        InvalidateGeneratedArtifacts();
        // Superseded operations no longer own the current WPF busy indicators.
        IsRunning = false;
        IsExportingPdf = false;
        return _activeExportAttempt;
    }

    private bool EnsureExportStillCurrent(long attempt, long revision)
    {
        if (attempt != _activeExportAttempt)
            return false; // Newer export owns the UI. No old error message.

        if (revision == _artifactRevision)
            return true;

        // The file may already have been written. No rollback claims.
        ShowError("La selección o los datos del informe cambiaron durante la generación. Revisá la carpeta y el historial; generá nuevamente el documento actual.");
        return false;
    }

    /// <summary>
    /// HTML and PDF history use one immutable export-options view. Reading
    /// live WPF properties after awaits could label another customer's PDF or
    /// omit included sections from history.
    /// </summary>
    private static GeneratedReportInfo CreateGeneratedReportInfo(
        ReportGenerationOptions options, string htmlPath, string? pdfPath = null)
    {
        var info = new GeneratedReportInfo
        {
            ClientName = options.IncludeClient
                ? options.ServiceReport?.Client?.Name ?? "Sin cliente" : "Sin cliente",
            EquipmentName = options.IncludeClient
                ? $"{options.ServiceReport?.Equipment?.Brand} {options.ServiceReport?.Equipment?.Model}"
                : string.Empty,
            HtmlPath = htmlPath,
            PdfPath = pdfPath ?? string.Empty,
            Notes = options.FinalObservations
        };

        if (options.IncludeCompany) info.IncludedSections.Add("Empresa");
        if (options.IncludeClient) info.IncludedSections.Add("Cliente");
        if (options.IncludeDiagnostic) info.IncludedSections.Add("Diagnóstico");
        if (options.IncludeStartup) info.IncludedSections.Add("Inicio");
        if (options.IncludeCleanup) info.IncludedSections.Add("Limpieza");
        if (options.IncludeVisualOptimization) info.IncludedSections.Add("Optimización");
        if (options.IncludeRestorePoint) info.IncludedSections.Add("Restauración");
        if (options.IncludeSmart && options.SmartAnalysis is not null)
            info.IncludedSections.Add("SMART");
        if (options.IncludeSmartTests && options.SmartTestSessions.Count > 0)
            info.IncludedSections.Add("SMART Self-Tests");
        if (options.IncludeRecommendations)
            info.IncludedSections.Add("Recomendaciones");
        return info;
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

        // Checkbox state alone cannot certify a report section. Require
        // that a selected FULL result belongs to the available inventory.
        var selectedCleanup = IncludeCleanup && HasCleanupData
            && SelectedCleanupResult is { } cleanup
            && AvailableCleanupResults.Contains(cleanup) ? cleanup : null;
        var selectedRestore = IncludeRestorePoint && HasRestorePointData
            && SelectedRestorePointResult is { } restore
            && AvailableRestorePointResults.Contains(restore) ? restore : null;

        return new ReportGenerationOptions
        {
            Settings = IncludeCompany
                ? await _settingsService.LoadSettingsAsync()
                : new AppSettings(),
            // Unchecked customer sections must not carry data into exports.
            ServiceReport = IncludeClient ? SelectedServiceReport : null,
            DiagnosticReport = IncludeDiagnostic ? SelectedDiagnosticReport : null,
            StartupAnalysis = IncludeStartup ? SelectedStartupAnalysis : null,
            CleanupResult = selectedCleanup,
            VisualOptimizationResult = IncludeVisualOptimization
                ? SelectedVisualOptimizationResult : null,
            RestorePointResult = selectedRestore,
            SmartAnalysis = IncludeSmart ? SelectedSmartAnalysis : null,
            SmartTestSessions = IncludeSmartTests ? selectedTests : new(),
            FinalObservations = FinalObservations,
            IncludeCompany = IncludeCompany,
            IncludeClient = IncludeClient,
            IncludeDiagnostic = IncludeDiagnostic,
            IncludeStartup = IncludeStartup,
            IncludeCleanup = selectedCleanup is not null,
            IncludeVisualOptimization = IncludeVisualOptimization,
            IncludeRestorePoint = selectedRestore is not null,
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
        _artifactRevision++;
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
