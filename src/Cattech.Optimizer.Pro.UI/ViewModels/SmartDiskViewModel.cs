using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Microsoft.Win32;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de Discos SMART.
/// </summary>
public partial class SmartDiskViewModel : ObservableObject
{
    private readonly ISmartctlRunner _smartctlRunner;
    private readonly ISmartDiskService _smartDiskService;
    private readonly ISmartTestService _smartTestService;
    private readonly string _reportsDirectory;
    private readonly string _configDirectory;
    private readonly Action<string> _openDirectory;
    private List<SmartDiskDevice> _allDevices = [];
    private List<SmartDiskReport> _allReports = [];
    private SmartAnalysisResult? _lastAnalysisResult;
    private string? _lastSavedFileName;

    // --- Estado del test SMART ---

    [ObservableProperty]
    private bool _canStartTest;

    [ObservableProperty]
    private bool _canStartExtendedTest;

    [ObservableProperty]
    private bool _isTestInProgress;

    [ObservableProperty]
    private SmartTestSession? _currentTestSession;

    [ObservableProperty]
    private string _testStatusText = string.Empty;

    [ObservableProperty]
    private string _testResultMessage = string.Empty;

    [ObservableProperty]
    private string _currentTestTypeText = string.Empty;

    public SmartDiskViewModel(
        ISmartctlRunner smartctlRunner,
        ISmartDiskService smartDiskService,
        ISmartTestService smartTestService,
        string? baseDirectory = null,
        Action<string>? openDirectory = null)
    {
        _smartctlRunner = smartctlRunner;
        _smartDiskService = smartDiskService;
        _smartTestService = smartTestService;

        var root = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : baseDirectory;
        _reportsDirectory = Path.Combine(root, "data", "smart-reports");
        _configDirectory = Path.Combine(root, "config");
        _openDirectory = openDirectory ?? OpenDirectoryWithShell;
    }

    /// <summary>
    /// Hook de apertura de carpetas para tests: nunca lanza el Explorador real.
    /// </summary>
    internal Action<string>? OpenDirectoryOverride { get; set; }

    /// <summary>
    /// Hook de portapapeles para tests: evita depender de una sesión STA/UI.
    /// </summary>
    internal Func<string, bool>? CopyTextOverride { get; set; }

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

    /// <summary>Estado compacto de smartctl para la barra técnica.</summary>
    [ObservableProperty]
    private string _smartctlStatusText = "Sin verificar";

    /// <summary>Línea técnica con versión, ruta y soporte JSON.</summary>
    [ObservableProperty]
    private string _smartctlSummaryLine = "smartctl: sin verificar";

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

    /// <summary>Encabezado técnico del disco seleccionado para la pestaña Atributos.</summary>
    [ObservableProperty]
    private string _selectedReportHeader = string.Empty;

    // --- Atributos crudos del disco seleccionado ---

    public ObservableCollection<SmartAttributeRow> SelectedAttributeRows { get; } = new();

    [ObservableProperty]
    private bool _hasSelectedAttributes;

    [ObservableProperty]
    private string _attributeEmptyMessage =
        "Seleccioná un disco en la pestaña Discos para ver sus atributos SMART.";

    // --- Sesiones de self-test persistidas ---

    public ObservableCollection<SmartTestSessionRow> TestSessions { get; } = new();

    [ObservableProperty]
    private bool _hasSessions;

    [ObservableProperty]
    private string _sessionsStatusText = "Sin sesiones cargadas";

    [ObservableProperty]
    private SmartTestSessionRow? _selectedSession;

    [ObservableProperty]
    private bool _isLoadingSessions;

    // --- Listas ---

    public ObservableCollection<SmartDiskDevice> Devices { get; } = new();
    public ObservableCollection<SmartDiskReport> Reports { get; } = new();
    public ObservableCollection<SmartAttribute> SelectedAttributes { get; } = new();

    // --- Disponibilidad de acciones ---
    // Combinan el estado lógico de la pantalla con IsRunning para que ningún
    // botón permita una segunda ejecución mientras hay una operación en curso.

    public bool CanCheckSmartctl => !IsRunning;

    public bool CanDetectDevices => !IsRunning && SmartctlAvailable;

    public bool CanAnalyzeSmart => !IsRunning && SmartctlAvailable;

    public bool CanSaveAnalysis => !IsRunning && HasResults;

    public bool CanRunShortTest => !IsRunning && CanStartTest;

    public bool CanRunExtendedTest => !IsRunning && CanStartExtendedTest;

    /// <summary>
    /// Permite consultar el estado de la sesión cargada (en ejecución o
    /// persistida); la consulta es de solo lectura sobre smartctl.
    /// </summary>
    public bool CanCheckTestStatus => !IsRunning && CurrentTestSession != null;

    partial void OnIsRunningChanged(bool value) => NotifyActionAvailability();

    partial void OnCurrentTestSessionChanged(SmartTestSession? value) => NotifyActionAvailability();

    partial void OnSmartctlAvailableChanged(bool value) => NotifyActionAvailability();

    partial void OnHasResultsChanged(bool value) => NotifyActionAvailability();

    partial void OnCanStartTestChanged(bool value) => NotifyActionAvailability();

    partial void OnCanStartExtendedTestChanged(bool value) => NotifyActionAvailability();

    partial void OnIsTestInProgressChanged(bool value) => NotifyActionAvailability();

    private void NotifyActionAvailability()
    {
        OnPropertyChanged(nameof(CanCheckSmartctl));
        OnPropertyChanged(nameof(CanDetectDevices));
        OnPropertyChanged(nameof(CanAnalyzeSmart));
        OnPropertyChanged(nameof(CanSaveAnalysis));
        OnPropertyChanged(nameof(CanRunShortTest));
        OnPropertyChanged(nameof(CanRunExtendedTest));
        OnPropertyChanged(nameof(CanCheckTestStatus));
    }

    /// <summary>
    /// Verificación inicial al abrir la sección: si no se ejecuta, los botones
    /// de detección y análisis quedan deshabilitados en frío y la pantalla
    /// parece no responder. No repite la verificación si ya hay una operación.
    /// También restaura las sesiones de self-test persistidas: NO inicia tests,
    /// solo las carga para poder consultar el estado de una corrida anterior.
    /// </summary>
    public async Task EnsureInitializedAsync()
    {
        if (IsRunning)
            return;

        await CheckSmartctlAsync();
        await RefreshSessionsAsync();
    }

    /// <summary>
    /// Verifica si smartctl está disponible.
    /// </summary>
    [RelayCommand]
    private async Task CheckSmartctlAsync()
    {
        if (RejectWhileRunning())
            return;

        ClearMessages();
        IsRunning = true;
        StatusText = "Verificando smartctl...";
        SmartctlStatusText = "Verificando...";

        try
        {
            var availability = await _smartctlRunner.CheckAvailabilityAsync();

            SmartctlChecked = true;
            SmartctlAvailable = availability.IsAvailable;
            SmartctlPath = availability.SmartctlPath;
            SmartctlVersion = availability.Version;
            SmartctlSupportsJson = availability.SupportsJson;
            SmartctlErrorMessage = availability.ErrorMessage;

            SmartctlStatusText = availability.IsAvailable ? "Disponible" : "No disponible";
            SmartctlSummaryLine = BuildSmartctlSummaryLine();
            StatusText = availability.IsAvailable ? "smartctl disponible" : "smartctl no disponible";
        }
        catch (Exception ex)
        {
            SmartctlChecked = true;
            SmartctlAvailable = false;
            SmartctlErrorMessage = ex.Message;
            SmartctlStatusText = "Error al verificar";
            SmartctlSummaryLine = BuildSmartctlSummaryLine();
            StatusText = "Error al verificar";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private string BuildSmartctlSummaryLine()
    {
        var version = string.IsNullOrWhiteSpace(SmartctlVersion) ? "versión N/D" : SmartctlVersion;
        var path = string.IsNullOrWhiteSpace(SmartctlPath) ? "ruta N/D" : SmartctlPath;
        var json = SmartctlSupportsJson ? "JSON: sí" : "JSON: no";
        return $"{version} · {path} · {json}";
    }

    /// <summary>
    /// Detecta discos disponibles.
    /// </summary>
    [RelayCommand]
    private async Task DetectDevicesAsync()
    {
        if (RejectWhileRunning())
            return;

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
        if (RejectWhileRunning())
            return;

        if (!SmartctlAvailable)
        {
            ShowError("smartctl no está disponible.");
            return;
        }

        ClearMessages();
        IsRunning = true;
        StatusText = "Analizando SMART...";
        _lastSavedFileName = null;

        try
        {
            // Conservar el resultado original completo (timestamps, errors, warnings, metadata)
            _lastAnalysisResult = await _smartDiskService.AnalyzeAllDisksAsync();
            var result = _lastAnalysisResult;

            _allReports = result.Reports.ToList();

            Reports.Clear();
            foreach (var report in _allReports)
                Reports.Add(report);

            // Errores generales del servicio (fallos de listado/análisis, timeout,
            // permisos). No deben quedar ocultos detrás de un mensaje de éxito.
            var serviceErrors = result.Errors
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToList();

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

            // Dejar un disco seleccionado: la pestaña Atributos y el detalle
            // técnico arrancan con datos reales sin un clic extra.
            if (SelectedReport is null && Reports.Count > 0)
                SelectedReport = Reports[0];

            // Ningún reporte significa que el análisis no produjo evidencia: se
            // informan los errores del servicio en lugar de un falso éxito.
            if (Reports.Count == 0)
            {
                StatusText = "Análisis sin resultados";
                var detail = serviceErrors.Count > 0
                    ? string.Join(" ", serviceErrors)
                    : "smartctl no devolvió ningún disco para analizar.";
                ShowError($"No se pudo completar el análisis SMART. {detail}");
                return;
            }

            // Reportes parciales junto a errores: se muestran los errores del
            // servicio; no se afirma que el análisis haya sido exitoso.
            if (serviceErrors.Count > 0)
            {
                StatusText = "Análisis completado con errores";
                var criticalPrefix = critical > 0
                    ? $"⚠️ {critical} disco(s) en estado CRÍTICO. "
                    : string.Empty;
                ShowError($"{criticalPrefix}El servicio reportó errores: {string.Join(" ", serviceErrors)}");
                return;
            }

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
        if (RejectWhileRunning())
            return;

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
            _lastSavedFileName = fileName;
            ShowSuccess($"Análisis guardado: {fileName}");
        }
        catch (Exception ex)
        {
            ShowError($"Error al guardar: {ex.Message}");
        }
    }

    /// <summary>
    /// Selecciona un reporte para ver detalle y atributos.
    /// </summary>
    [RelayCommand]
    private void SelectReport(SmartDiskReport? report)
    {
        SelectedReport = report;
    }

    partial void OnSelectedReportChanged(SmartDiskReport? value)
    {
        HasSelectedReport = value != null;
        RebuildAttributeRows(value);

        SelectedReportHeader = value is null
            ? string.Empty
            : $"{value.Device} · {value.ModelName} · {value.SerialNumber}";

        // Gestionar habilitación de tests según estado del disco
        if (value == null)
        {
            CanStartTest = false;
            CanStartExtendedTest = false;
            return;
        }

        switch (value.HealthStatus)
        {
            case SmartHealthStatus.Critical:
                CanStartTest = false;
                CanStartExtendedTest = false;
                TestStatusText = "Test bloqueado: disco en estado crítico. Realice backup antes de continuar.";
                break;

            case SmartHealthStatus.NotAvailable:
            case SmartHealthStatus.Unknown:
                CanStartTest = true;
                // Extended: solo si el soporte puede verificarse o es desconocido (el servicio resolverá)
                CanStartExtendedTest = value.SelfTestSupportKnown
                    ? value.SupportsExtendedSelfTest == true
                    : true;
                TestStatusText = "Estado del disco no determinado. Se verificará soporte de self-test al iniciar.";
                break;

            default:
                CanStartTest = true;
                // Good/Warning: permitir extendido; si soporte conocido y false → bloquear
                CanStartExtendedTest = value.SelfTestSupportKnown
                    ? value.SupportsExtendedSelfTest != false
                    : true;
                TestStatusText = string.Empty;
                break;
        }

        // Si ya hay un test en progreso para este disco, bloquear ambos
        if (IsTestInProgress && CurrentTestSession?.Device == value.Device)
        {
            CanStartTest = false;
            CanStartExtendedTest = false;
            TestStatusText = "Test en ejecución para este disco.";
        }
    }

    /// <summary>
    /// Inicia un test SMART corto sobre el disco seleccionado.
    /// </summary>
    [RelayCommand]
    private async Task StartShortTestAsync()
    {
        if (RejectWhileRunning())
            return;

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
                    Type = SelectedReport.SmartctlDeviceType,
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
    /// Inicia un test SMART extendido sobre el disco seleccionado.
    /// Requiere confirmación explícita del técnico.
    /// </summary>
    [RelayCommand]
    private async Task StartExtendedTestAsync()
    {
        if (RejectWhileRunning())
            return;

        if (SelectedReport == null)
        {
            ShowError("Seleccione un disco para ejecutar el test.");
            return;
        }

        // Bloquear si el disco es crítico
        if (SelectedReport.HealthStatus == SmartHealthStatus.Critical)
        {
            ShowError("Este disco presenta indicadores críticos. Realice backup antes de ejecutar pruebas adicionales.");
            return;
        }

        // Bloquear si el soporte extendido es conocido y false
        if (SelectedReport.SelfTestSupportKnown && SelectedReport.SupportsExtendedSelfTest == false)
        {
            ShowError("El dispositivo no soporta test SMART extendido.");
            return;
        }

        // Bloquear si ya hay un test en progreso para este disco (corto o extendido)
        if (IsTestInProgress && CurrentTestSession?.Device == SelectedReport.Device)
        {
            ShowError("Ya hay un test en ejecución para este disco.");
            return;
        }

        // Confirmación fuerte
        var confirmMessage = "Se iniciará una prueba SMART extendida sobre el disco seleccionado.\n\n" +
                             "Esta prueba puede tardar desde decenas de minutos hasta varias horas y genera actividad sostenida sobre la unidad.\n\n" +
                             "No apague ni desconecte el equipo durante la prueba.\n\n" +
                             "Si el disco contiene información importante, se recomienda realizar una copia de seguridad antes de continuar.";

        if (SelectedReport.HealthStatus == SmartHealthStatus.Warning)
        {
            confirmMessage += "\n\nEl disco ya presenta advertencias SMART. Se recomienda realizar backup antes de continuar.";
        }

        var confirmed = MessageBox.Show(
            confirmMessage,
            "Confirmar test SMART extendido",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (confirmed != MessageBoxResult.OK)
            return;

        ClearMessages();
        IsRunning = true;
        TestStatusText = "Iniciando test extendido...";

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
                    Type = SelectedReport.SmartctlDeviceType,
                    ApproximateDiskType = SelectedReport.DeviceType,
                    Protocol = SelectedReport.Protocol,
                    ModelName = SelectedReport.ModelName,
                    SerialNumber = SelectedReport.SerialNumber
                };
            }

            var session = await _smartTestService.StartExtendedTestAsync(device);

            CurrentTestSession = session;
            CurrentTestTypeText = "Extendido";

            switch (session.Status)
            {
                case SmartTestStatus.InProgress:
                    IsTestInProgress = true;
                    CanStartTest = false;
                    CanStartExtendedTest = false;
                    TestStatusText = "Test extendido en ejecución";

                    var durationText = session.EstimatedDurationMinutes.HasValue
                        ? $"{session.EstimatedDurationMinutes.Value} min"
                        : "no disponible";
                    var completionText = session.EstimatedCompletionAt.HasValue
                        ? session.EstimatedCompletionAt.Value.ToString("HH:mm:ss")
                        : "no disponible";

                    TestResultMessage = $"Test extendido iniciado a las {session.StartedAt:HH:mm:ss}. " +
                                        $"Duración estimada: {durationText}. " +
                                        $"Finalización estimada: {completionText}.";
                    ShowSuccess("Test SMART extendido iniciado correctamente.");
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
        if (RejectWhileRunning())
            return;

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

            // La consulta de estado puede fallar aunque el test siga existiendo:
            // en ese caso no se informa éxito con datos posiblemente obsoletos.
            if (!session.LastCheckSucceeded)
            {
                var detail = string.IsNullOrWhiteSpace(session.LastCheckError)
                    ? "smartctl no pudo leer el estado del test."
                    : session.LastCheckError;
                TestStatusText = "No se pudo consultar el estado";
                ShowError($"No se pudo consultar el estado del test: {detail}");
                return;
            }

            // Usar mensaje preparado por el servicio (extensión Core, no parser Infrastructure)
            TestResultMessage = session.Status.ToDisplayMessage();

            if (session.ProgressPercent.HasValue)
            {
                TestResultMessage += $" Progreso: {session.ProgressPercent.Value}%";
            }

            switch (session.Status)
            {
                case SmartTestStatus.CompletedWithoutError:
                    IsTestInProgress = false;
                    CanStartTest = true;
                    CanStartExtendedTest = true;
                    TestStatusText = "Test completado";
                    ShowSuccess("Prueba completada sin errores reportados.");
                    break;

                case SmartTestStatus.CompletedWithError:
                    IsTestInProgress = false;
                    CanStartTest = true;
                    CanStartExtendedTest = true;
                    TestStatusText = "Test completado con errores";
                    ShowError("La prueba detectó errores. Revisar SMART y realizar backup.");
                    break;

                case SmartTestStatus.InProgress:
                    IsTestInProgress = true;
                    CanStartTest = false;
                    CanStartExtendedTest = false;
                    TestStatusText = "Test en ejecución";
                    ShowSuccess("Test aún en ejecución.");
                    break;

                default:
                    IsTestInProgress = false;
                    CanStartTest = true;
                    CanStartExtendedTest = true;
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

    // =====================
    // ATRIBUTOS CRUDOS Y EXPORTACIÓN TÉCNICA
    // =====================

    /// <summary>
    /// Reconstruye las filas de atributos crudos del disco seleccionado.
    /// Si smartctl no expuso atributos, la lista queda vacía y la UI lo informa
    /// explícitamente ("sin atributos"); nunca se rellenan valores inventados.
    /// </summary>
    private void RebuildAttributeRows(SmartDiskReport? report)
    {
        SelectedAttributes.Clear();
        SelectedAttributeRows.Clear();

        if (report?.ImportantAttributes != null)
        {
            foreach (var attribute in report.ImportantAttributes)
                SelectedAttributes.Add(attribute);
        }

        foreach (var row in SmartAttributeRow.FromReport(report))
            SelectedAttributeRows.Add(row);

        HasSelectedAttributes = SelectedAttributeRows.Count > 0;
        AttributeEmptyMessage = report is null
            ? "Seleccioná un disco en la pestaña Discos para ver sus atributos SMART."
            : "Sin atributos SMART disponibles para este disco (smartctl no los expuso).";
    }

    /// <summary>
    /// Informe técnico TSV (tabulado) con discos, atributos crudos y sesiones.
    /// Función pura: no toca el portapapeles ni el disco.
    /// </summary>
    public string BuildSmartReportTsv()
    {
        var sb = new StringBuilder();

        sb.AppendLine("# CATTECH SMART — informe técnico");
        sb.AppendLine($"# Generado: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"# smartctl: {Column(SmartctlSummaryLine)}");
        sb.AppendLine($"# Estado smartctl: {Column(SmartctlStatusText)}");
        sb.AppendLine($"# Análisis guardado: {Column(_lastSavedFileName ?? "sin guardar")}");
        sb.AppendLine($"# Discos analizados: {Reports.Count.ToString(CultureInfo.InvariantCulture)}");

        if (Reports.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## DISCOS");
            sb.AppendLine(string.Join('\t',
                "Estado", "Dispositivo", "Modelo", "Serial", "Firmware", "Tipo", "Protocolo",
                "CapacidadGB", "TempC", "Horas", "Ciclos", "Reasignados", "Pendientes",
                "OfflineUncorrectable", "CRC_UDMA", "NVMe_UsadoPct", "NVMe_CriticalWarning",
                "NVMe_MediaErrors", "NVMe_UnsafeShutdowns", "SaludSMART", "AnalisisOK", "Resumen"));

            foreach (var report in Reports)
            {
                sb.AppendLine(string.Join('\t',
                    HealthText(report.HealthStatus),
                    Column(report.Device),
                    Column(report.ModelName),
                    Column(report.SerialNumber),
                    Column(report.FirmwareVersion),
                    Column(report.DeviceType),
                    Column(report.Protocol),
                    FormatNumber(report.CapacityGB),
                    report.TemperatureCelsius.ToString(CultureInfo.InvariantCulture),
                    report.PowerOnHours.ToString(CultureInfo.InvariantCulture),
                    report.PowerCycleCount.ToString(CultureInfo.InvariantCulture),
                    report.ReallocatedSectorCount.ToString(CultureInfo.InvariantCulture),
                    report.PendingSectorCount.ToString(CultureInfo.InvariantCulture),
                    report.OfflineUncorrectableCount.ToString(CultureInfo.InvariantCulture),
                    report.UDMACrcErrorCount.ToString(CultureInfo.InvariantCulture),
                    OptionalNumber(report.NvmePercentageUsed),
                    OptionalCriticalWarning(report.NvmeCriticalWarning),
                    OptionalNumber(report.NvmeMediaErrors),
                    OptionalNumber(report.NvmeUnsafeShutdowns),
                    OverallHealthText(report.OverallHealthPassed),
                    report.IsAnalysisSuccessful ? "sí" : "no",
                    Column(report.HealthSummary)));
            }
        }

        sb.AppendLine();
        sb.AppendLine("## ATRIBUTOS SMART");
        sb.AppendLine(string.Join('\t',
            "Dispositivo", "Fuente", "ID", "Nombre", "Val", "Peor", "Umbral", "Raw",
            "Flags", "Prefallo", "Severidad", "Descripcion"));

        foreach (var report in Reports)
        {
            foreach (var row in SmartAttributeRow.FromReport(report))
            {
                sb.AppendLine(string.Join('\t',
                    Column(report.Device),
                    Column(row.Source),
                    Column(row.IdDisplay),
                    Column(row.Name),
                    Column(row.ValueDisplay),
                    Column(row.WorstDisplay),
                    Column(row.ThresholdDisplay),
                    Column(row.RawDisplay),
                    Column(row.Flags),
                    row.IsPrefailure ? "sí" : "no",
                    SeverityText(row.Severity),
                    Column(row.Description)));
            }
        }

        sb.AppendLine();
        sb.AppendLine("## SESIONES SELF-TEST");
        sb.AppendLine(string.Join('\t',
            "ID", "Dispositivo", "Modelo", "Test", "Estado", "Solicitado", "Iniciado",
            "EstimadoMin", "ProgresoPct", "Completado", "UltimaConsulta", "UltimaConsultaOK", "Resultado"));

        foreach (var row in TestSessions)
        {
            sb.AppendLine(string.Join('\t',
                Column(row.Id),
                Column(row.Device),
                Column(row.ModelName),
                Column(row.TestTypeText),
                Column(row.StatusText),
                row.RequestedAtDisplay,
                row.StartedAtDisplay,
                row.EstimatedMinutesDisplay,
                row.ProgressDisplay,
                row.CompletedAtDisplay,
                row.LastCheckedDisplay,
                row.LastCheckText,
                Column(row.ResultMessage)));
        }

        if (Reports.Count == 0 && TestSessions.Count == 0)
            sb.AppendLine("(sin datos: ejecutá un análisis SMART para generar evidencia)");

        var errors = Reports.SelectMany(r => r.Errors)
            .Concat(_lastAnalysisResult is null ? Enumerable.Empty<string>() : _lastAnalysisResult.Errors)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .ToList();
        if (errors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## ERRORES");
            foreach (var error in errors)
                sb.AppendLine(Column(error));
        }

        var warnings = Reports.SelectMany(r => r.Warnings)
            .Concat(_lastAnalysisResult is null ? Enumerable.Empty<string>() : _lastAnalysisResult.Warnings)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToList();
        if (warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## ADVERTENCIAS");
            foreach (var warning in warnings)
                sb.AppendLine(Column(warning));
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// CSV técnico con secciones [DISCOS], [ATRIBUTOS] y [SESIONES]. Separador
    /// punto y coma (configuración regional española) y entrecomillado RFC 4180
    /// para celdas con separador, comillas o saltos de línea.
    /// Función pura: no toca el disco.
    /// </summary>
    public string BuildSmartReportCsv()
    {
        var sb = new StringBuilder();

        sb.AppendLine("# CATTECH SMART — exportación técnica CSV");
        sb.AppendLine($"# Generado;{Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))}");
        sb.AppendLine($"# smartctl;{Csv(SmartctlSummaryLine)}");
        sb.AppendLine($"# Estado smartctl;{Csv(SmartctlStatusText)}");
        sb.AppendLine($"# Analisis guardado;{Csv(_lastSavedFileName ?? "sin guardar")}");

        sb.AppendLine();
        sb.AppendLine("[DISCOS]");
        sb.AppendLine(string.Join(';',
            "Estado", "Dispositivo", "Modelo", "Serial", "Firmware", "Tipo", "Protocolo",
            "CapacidadGB", "TempC", "Horas", "Ciclos", "Reasignados", "Pendientes",
            "OfflineUncorrectable", "CRC_UDMA", "NVMe_UsadoPct", "NVMe_CriticalWarning",
            "NVMe_MediaErrors", "NVMe_UnsafeShutdowns", "SaludSMART", "AnalisisOK", "Resumen"));
        foreach (var report in Reports)
        {
            sb.AppendLine(string.Join(';',
                Csv(HealthText(report.HealthStatus)),
                Csv(report.Device),
                Csv(report.ModelName),
                Csv(report.SerialNumber),
                Csv(report.FirmwareVersion),
                Csv(report.DeviceType),
                Csv(report.Protocol),
                Csv(FormatNumber(report.CapacityGB)),
                Csv(report.TemperatureCelsius.ToString(CultureInfo.InvariantCulture)),
                Csv(report.PowerOnHours.ToString(CultureInfo.InvariantCulture)),
                Csv(report.PowerCycleCount.ToString(CultureInfo.InvariantCulture)),
                Csv(report.ReallocatedSectorCount.ToString(CultureInfo.InvariantCulture)),
                Csv(report.PendingSectorCount.ToString(CultureInfo.InvariantCulture)),
                Csv(report.OfflineUncorrectableCount.ToString(CultureInfo.InvariantCulture)),
                Csv(report.UDMACrcErrorCount.ToString(CultureInfo.InvariantCulture)),
                Csv(OptionalNumber(report.NvmePercentageUsed)),
                Csv(OptionalCriticalWarning(report.NvmeCriticalWarning)),
                Csv(OptionalNumber(report.NvmeMediaErrors)),
                Csv(OptionalNumber(report.NvmeUnsafeShutdowns)),
                Csv(OverallHealthText(report.OverallHealthPassed)),
                Csv(report.IsAnalysisSuccessful ? "sí" : "no"),
                Csv(report.HealthSummary)));
        }

        sb.AppendLine();
        sb.AppendLine("[ATRIBUTOS]");
        sb.AppendLine(string.Join(';',
            "Dispositivo", "Fuente", "ID", "Nombre", "Val", "Peor", "Umbral", "Raw",
            "Flags", "Prefallo", "Severidad", "Descripcion"));
        foreach (var report in Reports)
        {
            foreach (var row in SmartAttributeRow.FromReport(report))
            {
                sb.AppendLine(string.Join(';',
                    Csv(report.Device),
                    Csv(row.Source),
                    Csv(row.IdDisplay),
                    Csv(row.Name),
                    Csv(row.ValueDisplay),
                    Csv(row.WorstDisplay),
                    Csv(row.ThresholdDisplay),
                    Csv(row.RawDisplay),
                    Csv(row.Flags),
                    Csv(row.IsPrefailure ? "sí" : "no"),
                    Csv(SeverityText(row.Severity)),
                    Csv(row.Description)));
            }
        }

        sb.AppendLine();
        sb.AppendLine("[SESIONES]");
        sb.AppendLine(string.Join(';',
            "ID", "Dispositivo", "Modelo", "Test", "Estado", "Solicitado", "Iniciado",
            "EstimadoMin", "ProgresoPct", "Completado", "UltimaConsulta", "UltimaConsultaOK", "Resultado"));
        foreach (var row in TestSessions)
        {
            sb.AppendLine(string.Join(';',
                Csv(row.Id),
                Csv(row.Device),
                Csv(row.ModelName),
                Csv(row.TestTypeText),
                Csv(row.StatusText),
                Csv(row.RequestedAtDisplay),
                Csv(row.StartedAtDisplay),
                Csv(row.EstimatedMinutesDisplay),
                Csv(row.ProgressDisplay),
                Csv(row.CompletedAtDisplay),
                Csv(row.LastCheckedDisplay),
                Csv(row.LastCheckText),
                Csv(row.ResultMessage)));
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Escribe el CSV en disco con BOM UTF-8 (Excel) de forma síncrona.
    /// La UI lo ejecuta en un hilo de fondo y traduce los errores a un aviso.
    /// </summary>
    internal void WriteCsvFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllText(path, BuildSmartReportCsv(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>
    /// Copia el informe técnico TSV al portapapeles.
    /// </summary>
    [RelayCommand]
    private Task CopySmartReportAsync()
    {
        if (RejectWhileRunning())
            return Task.CompletedTask;

        if (Reports.Count == 0 && TestSessions.Count == 0)
        {
            ShowError("No hay informe SMART para copiar. Ejecutá un análisis primero.");
            return Task.CompletedTask;
        }

        if (!TryCopyText(BuildSmartReportTsv()))
        {
            ShowError("No se pudo copiar el informe al portapapeles.");
            return Task.CompletedTask;
        }

        ShowSuccess("Informe SMART copiado al portapapeles (TSV).");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Exporta el CSV a la ruta elegida por el técnico (SaveFileDialog).
    /// </summary>
    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (RejectWhileRunning())
            return;

        if (Reports.Count == 0 && TestSessions.Count == 0)
        {
            ShowError("No hay datos SMART para exportar. Ejecutá un análisis primero.");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Exportar informe SMART (CSV)",
            Filter = "CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*",
            DefaultExt = ".csv",
            FileName = $"smart-report-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            InitialDirectory = EnsureReportsDirectory()
        };

        if (dialog.ShowDialog() != true)
        {
            StatusText = "Exportación cancelada";
            return;
        }

        try
        {
            await Task.Run(() => WriteCsvFile(dialog.FileName));
            ShowSuccess($"CSV exportado: {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException
            or System.Security.SecurityException)
        {
            // Nunca se muestra la ruta elegida: puede contener datos del cliente.
            ShowError("No se pudo exportar el CSV. Verificá la ruta y los permisos de escritura.");
        }
    }

    /// <summary>
    /// Abre la carpeta de informes SMART persistidos (la crea si es la primera vez).
    /// </summary>
    [RelayCommand]
    private Task OpenReportsFolderAsync() => OpenFolderSafeAsync(
        _reportsDirectory,
        "carpeta de informes SMART",
        "Carpeta de informes SMART abierta.");

    /// <summary>
    /// Abre la carpeta de configuración de smartctl (herramientas.json).
    /// </summary>
    [RelayCommand]
    private Task OpenSmartctlConfigAsync() => OpenFolderSafeAsync(
        _configDirectory,
        "carpeta de configuración de smartctl",
        "Carpeta de configuración abierta.");

    private Task OpenFolderSafeAsync(string folder, string folderLabel, string successMessage)
    {
        if (RejectWhileRunning())
            return Task.CompletedTask;

        try
        {
            var directory = EnsureDirectory(folder);
            var opener = OpenDirectoryOverride ?? _openDirectory;
            opener(directory);
            ShowSuccess(successMessage);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException
            or System.Security.SecurityException
            or System.ComponentModel.Win32Exception)
        {
            // Aviso visible sin exponer rutas privadas.
            ShowError($"No se pudo abrir la {folderLabel}. Verificá los permisos e intentá nuevamente.");
        }

        return Task.CompletedTask;
    }

    // =====================
    // SESIONES DE SELF-TEST PERSISTIDAS
    // =====================

    /// <summary>
    /// Carga las sesiones de self-test guardadas por corridas anteriores.
    /// NO inicia pruebas: solo lista y deja disponible la sesión en ejecución
    /// para consultar su estado con un clic.
    /// </summary>
    [RelayCommand]
    private async Task RefreshSessionsAsync()
    {
        if (IsRunning)
            return;

        IsLoadingSessions = true;

        try
        {
            var sessions = await _smartTestService.ListSessionsAsync(20);

            TestSessions.Clear();
            foreach (var session in sessions)
            {
                if (session is null)
                    continue;
                TestSessions.Add(new SmartTestSessionRow(session));
            }

            HasSessions = TestSessions.Count > 0;
            SessionsStatusText = HasSessions
                ? $"{TestSessions.Count} sesión(es) persistida(s)"
                : "Sin sesiones de self-test registradas.";

            // Restaurar una sesión "en ejecución" de una corrida anterior. No se
            // inicia nada: si el test sigue corriendo en el firmware del disco,
            // "Consultar estado" actualiza el resultado real.
            var runningSession = TestSessions.FirstOrDefault(s => s.IsRunning);
            if (runningSession is not null && (CurrentTestSession is null || !IsTestInProgress))
            {
                CurrentTestSession = runningSession.Session;
                IsTestInProgress = true;
                SelectedSession = runningSession;
                CurrentTestTypeText = runningSession.TestTypeText;

                var matchingReport = Reports.FirstOrDefault(r =>
                    string.Equals(r.Device, runningSession.Device, StringComparison.OrdinalIgnoreCase));
                if (matchingReport is not null)
                    SelectedReport = matchingReport;

                TestStatusText = "Sesión persistida en ejecución. Consultá el estado para actualizar.";
                TestResultMessage = runningSession.StatusText + " Usá 'Consultar estado' para actualizar.";
            }
        }
        catch (Exception)
        {
            HasSessions = false;
            TestSessions.Clear();
            SessionsStatusText = "No se pudieron cargar las sesiones de self-test.";
        }
        finally
        {
            IsLoadingSessions = false;
        }
    }

    /// <summary>
    /// Carga una sesión persistida en el panel de test (sin iniciar pruebas)
    /// para poder consultar su estado con el servicio real.
    /// </summary>
    [RelayCommand]
    private void UseSelectedSession()
    {
        if (SelectedSession is null)
        {
            ShowError("Seleccioná una sesión de self-test de la lista.");
            return;
        }

        var row = SelectedSession;
        CurrentTestSession = row.Session;
        CurrentTestTypeText = row.TestTypeText;
        IsTestInProgress = row.IsRunning;
        TestStatusText = row.IsRunning
            ? "Sesión persistida en ejecución. Consultá el estado para actualizar."
            : "Sesión persistida finalizada.";
        TestResultMessage = row.StatusText;

        var matchingReport = Reports.FirstOrDefault(r =>
            string.Equals(r.Device, row.Device, StringComparison.OrdinalIgnoreCase));
        if (matchingReport is not null)
            SelectedReport = matchingReport;

        ShowSuccess("Sesión cargada. Usá 'Consultar estado' para consultar el firmware del disco.");
    }

    // =====================
    // HELPERS DE FORMATO
    // =====================

    private static string Column(string? value) => string.IsNullOrEmpty(value)
        ? string.Empty
        : value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.IndexOfAny([';', '"', '\r', '\n']) < 0)
            return text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private static string FormatNumber(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string OptionalNumber(int? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "-";

    private static string OptionalNumber(long? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "-";

    private static string OptionalCriticalWarning(int? value) =>
        value.HasValue ? "0x" + value.Value.ToString("X2", CultureInfo.InvariantCulture) : "-";

    private static string OverallHealthText(bool? passed) => passed switch
    {
        true => "PASSED",
        false => "FAILED",
        null => "N/D"
    };

    private static string HealthText(SmartHealthStatus status) => status switch
    {
        SmartHealthStatus.Good => "Bueno",
        SmartHealthStatus.Warning => "Precaución",
        SmartHealthStatus.Critical => "Crítico",
        SmartHealthStatus.NotAvailable => "No disponible",
        _ => "Desconocido"
    };

    private static string SeverityText(SmartSeverity severity) => severity switch
    {
        SmartSeverity.Info => "Info",
        SmartSeverity.Warning => "Advertencia",
        SmartSeverity.Critical => "Crítico",
        _ => "Desconocida"
    };

    private string EnsureReportsDirectory() => EnsureDirectory(_reportsDirectory);

    private static string EnsureDirectory(string folder)
    {
        Directory.CreateDirectory(folder);
        return Path.GetFullPath(folder);
    }

    private bool TryCopyText(string text)
    {
        try
        {
            if (CopyTextOverride is not null)
                return CopyTextOverride(text);

            Clipboard.SetText(text);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void OpenDirectoryWithShell(string directory)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Si ya hay una operación en curso, informa y rechaza la nueva ejecución.
    /// Evita que un doble clic dispare dos procesos smartctl en paralelo.
    /// </summary>
    private bool RejectWhileRunning()
    {
        if (!IsRunning)
            return false;

        ShowError("Hay una operación en curso. Esperá a que termine e intentá nuevamente.");
        return true;
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

/// <summary>
/// Fila de presentación de una sesión de self-test persistida.
/// Solo deriva texto desde el modelo: no ejecuta ni inicia pruebas.
/// </summary>
public sealed class SmartTestSessionRow
{
    public SmartTestSessionRow(SmartTestSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>Sesión original persistida.</summary>
    public SmartTestSession Session { get; }

    public string Id => Session.Id;

    public string Device => Session.Device;

    public string ModelName => Session.ModelName;

    /// <summary>La sesión sigue en curso (iniciando o en ejecución).</summary>
    public bool IsRunning =>
        Session.Status is SmartTestStatus.InProgress or SmartTestStatus.Starting;

    public string TestTypeText => Session.TestType == SmartTestType.Extended ? "Extendido" : "Corto";

    public string StatusText => Session.Status switch
    {
        SmartTestStatus.NotStarted => "No iniciado",
        SmartTestStatus.Starting => "Iniciando",
        SmartTestStatus.InProgress => "En ejecución",
        SmartTestStatus.CompletedWithoutError => "Completada sin errores",
        SmartTestStatus.CompletedWithError => "Completada con errores",
        SmartTestStatus.Aborted => "Abortada",
        SmartTestStatus.Interrupted => "Interrumpida",
        SmartTestStatus.Unsupported => "No soportada",
        SmartTestStatus.FailedToStart => "No se pudo iniciar",
        _ => "Desconocida"
    };

    public string RequestedAtDisplay =>
        Session.RequestedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string StartedAtDisplay => Session.StartedAt.HasValue
        ? Session.StartedAt.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
        : "-";

    public string CompletedAtDisplay => Session.CompletedAt.HasValue
        ? Session.CompletedAt.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
        : "-";

    public string LastCheckedDisplay => Session.LastCheckedAt.HasValue
        ? Session.LastCheckedAt.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
        : "-";

    public string EstimatedMinutesDisplay => Session.EstimatedDurationMinutes.HasValue
        ? Session.EstimatedDurationMinutes.Value.ToString(CultureInfo.InvariantCulture)
        : "-";

    public string ProgressDisplay => Session.ProgressPercent.HasValue
        ? $"{Session.ProgressPercent.Value.ToString(CultureInfo.InvariantCulture)}%"
        : "-";

    public string LastCheckText => Session.LastCheckSucceeded ? "OK" : "Fallida";

    public string ResultMessage => Session.ResultMessage;
}
