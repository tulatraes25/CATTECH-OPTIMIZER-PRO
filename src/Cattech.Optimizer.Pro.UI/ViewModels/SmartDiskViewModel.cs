using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Infrastructure.Smart;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de Discos SMART.
/// </summary>
public partial class SmartDiskViewModel : ObservableObject
{
    private readonly ISmartctlRunner _smartctlRunner;
    private readonly ISmartDiskService _smartDiskService;
    private readonly ISmartTestService _smartTestService;
    private List<SmartDiskDevice> _allDevices = [];
    private List<SmartDiskReport> _allReports = [];
    private SmartAnalysisResult? _lastAnalysisResult;

    // --- Estado del test SMART ---

    [ObservableProperty]
    private bool _canStartTest;

    [ObservableProperty]
    private bool _isTestInProgress;

    [ObservableProperty]
    private SmartTestSession? _currentTestSession;

    [ObservableProperty]
    private string _testStatusText = string.Empty;

    [ObservableProperty]
    private string _testResultMessage = string.Empty;

    public SmartDiskViewModel(
        ISmartctlRunner smartctlRunner,
        ISmartDiskService smartDiskService,
        ISmartTestService smartTestService)
    {
        _smartctlRunner = smartctlRunner;
        _smartDiskService = smartDiskService;
        _smartTestService = smartTestService;
    }

    // --- Estado de la UI ---

    [ObservableProperty]
    private string _statusText = "Sin verificar";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // --- Estado de smartctl ---

    [ObservableProperty]
    private bool _smartctlChecked;

    [ObservableProperty]
    private bool _smartctlAvailable;

    [ObservableProperty]
    private string _smartctlPath = string.Empty;

    [ObservableProperty]
    private string _smartctlVersion = string.Empty;

    [ObservableProperty]
    private bool _smartctlSupportsJson;

    [ObservableProperty]
    private string _smartctlErrorMessage = string.Empty;

    // --- Discos ---

    [ObservableProperty]
    private bool _hasDevices;

    [ObservableProperty]
    private bool _hasResults;

    // --- Resumen ---

    [ObservableProperty]
    private string _summaryTotal = "0";

    [ObservableProperty]
    private string _summaryGood = "0";

    [ObservableProperty]
    private string _summaryWarning = "0";

    [ObservableProperty]
    private string _summaryCritical = "0";

    [ObservableProperty]
    private string _summaryNotAvailable = "0";

    [ObservableProperty]
    private string _summaryUnknown = "0";

    [ObservableProperty]
    private int _summaryWarningCount;

    [ObservableProperty]
    private int _summaryCriticalCount;

    // --- Reporte seleccionado ---

    [ObservableProperty]
    private SmartDiskReport? _selectedReport;

    [ObservableProperty]
    private bool _hasSelectedReport;

    // --- Listas ---

    public ObservableCollection<SmartDiskDevice> Devices { get; } = new();
    public ObservableCollection<SmartDiskReport> Reports { get; } = new();
    public ObservableCollection<SmartAttribute> SelectedAttributes { get; } = new();

    /// <summary>
    /// Verifica si smartctl está disponible.
    /// </summary>
    [RelayCommand]
    private async Task CheckSmartctlAsync()
    {
        ClearMessages();
        IsRunning = true;
        StatusText = "Verificando smartctl...";

        try
        {
            var availability = await _smartctlRunner.CheckAvailabilityAsync();

            SmartctlChecked = true;
            SmartctlAvailable = availability.IsAvailable;
            SmartctlPath = availability.SmartctlPath;
            SmartctlVersion = availability.Version;
            SmartctlSupportsJson = availability.SupportsJson;
            SmartctlErrorMessage = availability.ErrorMessage;

            StatusText = availability.IsAvailable ? "smartctl disponible" : "smartctl no disponible";
        }
        catch (Exception ex)
        {
            SmartctlChecked = true;
            SmartctlAvailable = false;
            SmartctlErrorMessage = ex.Message;
            StatusText = "Error al verificar";
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Detecta discos disponibles.
    /// </summary>
    [RelayCommand]
    private async Task DetectDevicesAsync()
    {
        if (!SmartctlAvailable)
        {
            ShowError("smartctl no está disponible. Verifique la configuración.");
            return;
        }

        ClearMessages();
        IsRunning = true;
        StatusText = "Detectando discos...";

        try
        {
            _allDevices = (await _smartctlRunner.ListDevicesAsync()).ToList();

            Devices.Clear();
            foreach (var device in _allDevices)
                Devices.Add(device);

            HasDevices = Devices.Count > 0;
            StatusText = Devices.Count > 0
                ? $"{Devices.Count} disco(s) detectado(s)"
                : "No se encontraron discos";

            if (Devices.Count == 0)
            {
                ShowError("No se encontraron dispositivos de almacenamiento. Verifique que smartctl tenga permisos.");
            }
        }
        catch (Exception ex)
        {
            StatusText = "Error al detectar";
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Ejecuta análisis SMART de todos los discos.
    /// </summary>
    [RelayCommand]
    private async Task AnalyzeSmartAsync()
    {
        if (!SmartctlAvailable)
        {
            ShowError("smartctl no está disponible.");
            return;
        }

        ClearMessages();
        IsRunning = true;
        StatusText = "Analizando SMART...";

        try
        {
            // Conservar el resultado original completo (timestamps, errors, warnings, metadata)
            _lastAnalysisResult = await _smartDiskService.AnalyzeAllDisksAsync();
            var result = _lastAnalysisResult;

            _allReports = result.Reports.ToList();

            Reports.Clear();
            foreach (var report in _allReports)
                Reports.Add(report);

            // Calcular resumen
            var good = _allReports.Count(r => r.HealthStatus == SmartHealthStatus.Good);
            var warning = _allReports.Count(r => r.HealthStatus == SmartHealthStatus.Warning);
            var critical = _allReports.Count(r => r.HealthStatus == SmartHealthStatus.Critical);
            var notAvailable = _allReports.Count(r => r.HealthStatus == SmartHealthStatus.NotAvailable);
            var unknown = _allReports.Count(r => r.HealthStatus == SmartHealthStatus.Unknown);

            SummaryTotal = result.DevicesAnalyzed.ToString();
            SummaryGood = good.ToString();
            SummaryWarning = warning.ToString();
            SummaryCritical = critical.ToString();
            SummaryNotAvailable = notAvailable.ToString();
            SummaryUnknown = unknown.ToString();
            SummaryWarningCount = warning;
            SummaryCriticalCount = critical;

            // HasResults: existen reportes SMART. HasDevices: existen dispositivos detectados.
            HasResults = Reports.Count > 0;

            // Lógica de estados corregida
            if (critical > 0)
            {
                StatusText = $"Análisis completado: {critical} disco(s) crítico(s)";
                ShowError($"⚠️ {critical} disco(s) en estado CRÍTICO. Backup inmediato recomendado.");
            }
            else if (warning > 0)
            {
                StatusText = $"Análisis completado: {warning} advertencia(s)";
                ShowSuccess($"Análisis completado. {warning} disco(s) con advertencias.");
            }
            else if (notAvailable > 0 || unknown > 0)
            {
                var indeterminate = notAvailable + unknown;
                StatusText = $"Análisis completado con estado no determinado en {indeterminate} disco(s)";
                ShowSuccess($"Análisis completado. Estado no determinado en {indeterminate} disco(s). " +
                           "SMART no disponible no significa que el disco esté sano.");
            }
            else if (Reports.Count > 0 && good == Reports.Count)
            {
                StatusText = "Análisis completado";
                ShowSuccess("Todos los discos en buen estado.");
            }
            else
            {
                StatusText = "Análisis completado";
                ShowSuccess("Análisis completado.");
            }
        }
        catch (Exception ex)
        {
            StatusText = "Error al analizar";
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Guarda el resultado del análisis.
    /// Preserva el SmartAnalysisResult original (timestamps, errors, warnings).
    /// </summary>
    [RelayCommand]
    private async Task SaveAnalysisAsync()
    {
        if (_lastAnalysisResult == null)
        {
            ShowError("No hay análisis para guardar. Ejecute un análisis primero.");
            return;
        }

        ClearMessages();

        try
        {
            // Guardar el resultado original completo, no reconstruirlo
            var fileName = await _smartDiskService.SaveResultAsync(_lastAnalysisResult);
            ShowSuccess($"Análisis guardado: {fileName}");
        }
        catch (Exception ex)
        {
            ShowError($"Error al guardar: {ex.Message}");
        }
    }

    /// <summary>
    /// Selecciona un reporte para ver detalle.
    /// </summary>
    [RelayCommand]
    private void SelectReport(SmartDiskReport? report)
    {
        SelectedReport = report;
        HasSelectedReport = report != null;

        SelectedAttributes.Clear();
        if (report?.ImportantAttributes != null)
        {
            foreach (var attr in report.ImportantAttributes)
                SelectedAttributes.Add(attr);
        }
    }

    partial void OnSelectedReportChanged(SmartDiskReport? value)
    {
        HasSelectedReport = value != null;

        // Gestionar habilitación del test según estado del disco
        if (value == null)
        {
            CanStartTest = false;
            return;
        }

        switch (value.HealthStatus)
        {
            case SmartHealthStatus.Critical:
                CanStartTest = false;
                TestStatusText = "Test bloqueado: disco en estado crítico. Realice backup antes de continuar.";
                break;

            case SmartHealthStatus.NotAvailable:
            case SmartHealthStatus.Unknown:
                CanStartTest = true;
                TestStatusText = "Estado del disco no determinado. Se verificará soporte de self-test al iniciar.";
                break;

            default:
                CanStartTest = true;
                TestStatusText = string.Empty;
                break;
        }

        // Si ya hay un test en progreso para este disco, no permitir otro
        if (IsTestInProgress && CurrentTestSession?.Device == value.Device)
        {
            CanStartTest = false;
            TestStatusText = "Test corto en ejecución para este disco.";
        }
    }

    /// <summary>
    /// Inicia un test SMART corto sobre el disco seleccionado.
    /// </summary>
    [RelayCommand]
    private async Task StartShortTestAsync()
    {
        if (SelectedReport == null)
        {
            ShowError("Seleccione un disco para ejecutar el test.");
            return;
        }

        // Bloquear si el disco es crítico
        if (SelectedReport.HealthStatus == SmartHealthStatus.Critical)
        {
            ShowError("Este disco presenta indicadores críticos. Se recomienda realizar backup antes de ejecutar pruebas adicionales.");
            return;
        }

        // Bloquear si ya hay un test en progreso para este disco
        if (IsTestInProgress && CurrentTestSession?.Device == SelectedReport.Device)
        {
            ShowError("Ya hay un test en ejecución para este disco.");
            return;
        }

        ClearMessages();
        IsRunning = true;
        TestStatusText = "Iniciando test corto...";

        try
        {
            // Buscar el dispositivo detectado que coincide con el reporte
            var device = _allDevices.FirstOrDefault(d => d.Name == SelectedReport.Device);
            if (device == null)
            {
                device = new SmartDiskDevice
                {
                    Name = SelectedReport.Device,
                    InfoName = SelectedReport.DeviceName,
                    ApproximateDiskType = SelectedReport.DeviceType,
                    Protocol = SelectedReport.Protocol,
                    ModelName = SelectedReport.ModelName,
                    SerialNumber = SelectedReport.SerialNumber
                };
            }

            var session = await _smartTestService.StartShortTestAsync(device);

            CurrentTestSession = session;

            switch (session.Status)
            {
                case SmartTestStatus.InProgress:
                    IsTestInProgress = true;
                    CanStartTest = false;
                    TestStatusText = "Test corto en ejecución";

                    var durationText = session.EstimatedDurationMinutes.HasValue
                        ? $"{session.EstimatedDurationMinutes.Value} min"
                        : "no disponible";
                    var completionText = session.EstimatedCompletionAt.HasValue
                        ? session.EstimatedCompletionAt.Value.ToString("HH:mm:ss")
                        : "no disponible";

                    TestResultMessage = $"Test iniciado a las {session.StartedAt:HH:mm:ss}. " +
                                        $"Duración estimada: {durationText}. " +
                                        $"Finalización estimada: {completionText}.";
                    ShowSuccess("Test SMART corto iniciado correctamente.");
                    break;

                case SmartTestStatus.Unsupported:
                    TestStatusText = "Test no soportado";
                    TestResultMessage = "El dispositivo no soporta esta prueba.";
                    ShowError(TestResultMessage);
                    break;

                case SmartTestStatus.FailedToStart:
                    TestStatusText = "Error al iniciar test";
                    TestResultMessage = session.ResultMessage;
                    ShowError($"No se pudo iniciar la prueba: {session.ResultMessage}");
                    break;

                default:
                    TestStatusText = "Estado desconocido";
                    TestResultMessage = session.ResultMessage;
                    ShowError(session.ResultMessage);
                    break;
            }
        }
        catch (Exception ex)
        {
            TestStatusText = "Error al iniciar test";
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Consulta el estado actual del test en ejecución.
    /// </summary>
    [RelayCommand]
    private async Task CheckTestStatusAsync()
    {
        if (CurrentTestSession == null)
        {
            ShowError("No hay test en ejecución.");
            return;
        }

        ClearMessages();
        IsRunning = true;
        TestStatusText = "Consultando estado del test...";

        try
        {
            var session = await _smartTestService.CheckStatusAsync(CurrentTestSession);
            CurrentTestSession = session;

            TestResultMessage = SmartctlParser.StatusToMessage(session.Status);

            if (session.ProgressPercent.HasValue)
            {
                TestResultMessage += $" Progreso: {session.ProgressPercent.Value}%";
            }

            switch (session.Status)
            {
                case SmartTestStatus.CompletedWithoutError:
                    IsTestInProgress = false;
                    CanStartTest = true;
                    TestStatusText = "Test completado";
                    ShowSuccess("Prueba completada sin errores reportados.");
                    break;

                case SmartTestStatus.CompletedWithError:
                    IsTestInProgress = false;
                    CanStartTest = true;
                    TestStatusText = "Test completado con errores";
                    ShowError("La prueba detectó errores. Revisar SMART y realizar backup.");
                    break;

                case SmartTestStatus.InProgress:
                    IsTestInProgress = true;
                    CanStartTest = false;
                    TestStatusText = "Test corto en ejecución";
                    ShowSuccess("Test aún en ejecución.");
                    break;

                default:
                    IsTestInProgress = false;
                    CanStartTest = true;
                    TestStatusText = "Test finalizado";
                    ShowSuccess(TestResultMessage);
                    break;
            }
        }
        catch (Exception ex)
        {
            TestStatusText = "Error al consultar estado";
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
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
