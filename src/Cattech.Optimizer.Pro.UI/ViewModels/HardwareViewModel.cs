using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Hardware;
using Microsoft.Win32;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel de la pantalla Hardware: presenta hardware live en tiempo real
/// (temperaturas, Load/Clock CPU-GPU, memoria GPU, batería, timings SPD)
/// a partir de UN único HardwareLiveSnapshot por captura o muestra,
/// más un inventario estático WMI/SMBIOS consultado manualmente.
/// Presenta datos únicamente: no interpreta salud, rendimiento ni aplica thresholds.
/// </summary>
public partial class HardwareViewModel : ObservableObject
{
    /// <summary>
    /// Cadencia visual del monitoreo. No es un threshold ni una regla técnica.
    /// </summary>
    public static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(2);

    private readonly IHardwareSensorService _hardwareSensorService;
    private readonly IHardwareService _hardwareService;
    private CancellationTokenSource? _cts;

    public ObservableCollection<HardwareTemperatureSensor> TemperatureSensors { get; } = new();
    public ObservableCollection<HardwarePerformanceSensor> PerformanceSensors { get; } = new();
    public ObservableCollection<HardwareGpuMemorySensor> GpuMemorySensors { get; } = new();
    public ObservableCollection<HardwareBatterySensor> BatterySensors { get; } = new();
    public ObservableCollection<HardwareMemoryTimingSensor> MemoryTimingSensors { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();
    public ObservableCollection<string> Errors { get; } = new();

    // --- Inventario estático (WMI/SMBIOS) ---

    public ObservableCollection<GpuInfo> Gpus { get; } = new();
    public ObservableCollection<MemoryModuleInfo> MemoryModules { get; } = new();
    public ObservableCollection<string> InventoryErrors { get; } = new();

    public HardwareViewModel(
        IHardwareSensorService hardwareSensorService,
        IHardwareService hardwareService)
    {
        _hardwareSensorService = hardwareSensorService;
        _hardwareService = hardwareService;
    }

    // --- Estado live ---

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartMonitoringCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMonitoringCommand))]
    [NotifyPropertyChangedFor(nameof(ShowInitialLiveHint))]
    private bool _isMonitoring;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartMonitoringCommand))]
    [NotifyPropertyChangedFor(nameof(ShowInitialLiveHint))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private bool _isElevated;

    /// <summary>
    /// Si ya se recibió al menos un snapshot live (inclusive no disponible).
    /// Distingue "sin leer" de "leído vacío" y "lectura no disponible".
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLiveHint))]
    [NotifyPropertyChangedFor(nameof(ShowSummaryCards))]
    private bool _hasLiveReading;

    // HasSensors/ValidSensorCount siguen siendo específicos de TEMPERATURA (compatibilidad B.1).
    [ObservableProperty]
    private bool _hasSensors;

    [ObservableProperty]
    private int _validSensorCount;

    [ObservableProperty]
    private bool _hasLiveData;

    [ObservableProperty]
    private DateTime? _lastUpdatedAt;

    [ObservableProperty]
    private string _statusText = "Sin lectura";

    [ObservableProperty]
    private string _lastUpdatedText = string.Empty;

    [ObservableProperty]
    private string _sensorCountText = "0";

    [ObservableProperty]
    private string _validSensorCountText = "0";

    [ObservableProperty]
    private string _providerStatusText = "Sin lectura";

    [ObservableProperty]
    private string _adminText = "No";

    [ObservableProperty]
    private bool _hasWarnings;

    [ObservableProperty]
    private bool _hasErrors;

    // --- Contadores por familia ---

    [ObservableProperty]
    private int _performanceSensorCount;

    [ObservableProperty]
    private int _validPerformanceSensorCount;

    [ObservableProperty]
    private int _gpuMemorySensorCount;

    [ObservableProperty]
    private int _validGpuMemorySensorCount;

    [ObservableProperty]
    private int _batterySensorCount;

    [ObservableProperty]
    private int _validBatterySensorCount;

    [ObservableProperty]
    private int _memoryTimingSensorCount;

    [ObservableProperty]
    private int _validMemoryTimingSensorCount;

    // --- Hints de pestañas vacías: solo en lectura disponible con colección vacía ---

    [ObservableProperty]
    private bool _showEmptyTemperatureHint;

    [ObservableProperty]
    private bool _showEmptyPerformanceHint;

    [ObservableProperty]
    private bool _showEmptyGpuMemoryHint;

    [ObservableProperty]
    private bool _showEmptyBatteryHint;

    [ObservableProperty]
    private bool _showEmptyTimingHint;

    /// <summary>
    /// Hint inicial: solo antes de la primera lectura y sin operación en curso.
    /// </summary>
    public bool ShowInitialLiveHint => !HasLiveReading && !IsMonitoring && !IsBusy;

    /// <summary>
    /// Las tarjetas de resumen se muestran solo después de la primera lectura real.
    /// </summary>
    public bool ShowSummaryCards => HasLiveReading;

    // --- Estado de inventario ---

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshInventoryCommand))]
    private bool _isInventoryBusy;

    [ObservableProperty]
    private bool _isInventoryLoaded;

    [ObservableProperty]
    private string _inventoryStatusText = "Sin consultar";

    [ObservableProperty]
    private DateTime? _inventoryLastUpdatedAt;

    [ObservableProperty]
    private string _inventoryLastUpdatedText = string.Empty;

    [ObservableProperty]
    private bool _hasInventoryErrors;

    [ObservableProperty]
    private CpuInfo _cpu = new();

    [ObservableProperty]
    private MemoryInfo _memory = new();

    [ObservableProperty]
    private MotherboardInfo _motherboard = new();

    private bool CanRefreshInventory => !IsInventoryBusy;

    private bool CanRefresh => !IsMonitoring && !IsBusy;

    private bool CanStartMonitoring => !IsMonitoring && !IsBusy;

    private bool CanStopMonitoring => IsMonitoring;

    /// <summary>
    /// Lee una vez el estado live completo del hardware (una sola captura).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusText = "Leyendo...";
        try
        {
            var snapshot = await _hardwareSensorService.GetLiveSnapshotAsync();
            ApplyLiveSnapshot(snapshot);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ApplyLiveFailure($"Error de lectura: {ex.Message}", isStreamError: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Inicia el monitoreo periódico. No permite un segundo stream simultáneo.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartMonitoring))]
    private void StartMonitoring()
    {
        if (IsMonitoring || IsBusy)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        IsMonitoring = true;
        StatusText = "Monitoreando";
        _ = RunMonitoringAsync(_cts.Token);
    }

    /// <summary>
    /// Detiene el monitoreo. Idempotente: puede llamarse varias veces.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopMonitoring))]
    private void StopMonitoring()
    {
        _cts?.Cancel();
    }

    private async Task RunMonitoringAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var snapshot in _hardwareSensorService
                               .WatchLiveSnapshotsAsync(MonitorInterval, cancellationToken))
            {
                ApplyLiveSnapshot(snapshot);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelación normal por StopMonitoring o por salir de la sección:
            // conserva la última muestra; el estado final se deriva al terminar.
        }
        catch (Exception ex)
        {
            ApplyLiveFailure($"Error de monitoreo: {ex.Message}", isStreamError: true);
        }
        finally
        {
            // El estado final se calcula ANTES de marcar IsMonitoring = false:
            // con el orden inverso, un observador podía ver IsMonitoring=false
            // y el StatusText viejo ("Monitoreando"), lo que producía tests
            // intermitentes y un parpadeo de estado en la UI.
            if (StatusText == "Monitoreando")
            {
                UpdateIdleLiveStatus();
            }

            IsMonitoring = false;

            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Aplica un snapshot live al estado de la UI. Cada muestra representa el estado ACTUAL:
    /// todas las familias anteriores se descartan, nunca se conservan como actuales.
    /// Mientras el stream está activo, StatusText permanece "Monitoreando".
    /// </summary>
    private void ApplyLiveSnapshot(HardwareLiveSnapshot snapshot)
    {
        HasLiveReading = true;
        IsAvailable = snapshot.IsAvailable;
        IsElevated = snapshot.IsElevated;
        LastUpdatedAt = snapshot.CapturedAt;
        LastUpdatedText = $"Última actualización: {snapshot.CapturedAt:dd/MM/yyyy HH:mm:ss}";

        TemperatureSensors.Clear();
        foreach (var sensor in snapshot.TemperatureSensors
                     .OrderBy(s => s.HardwareType)
                     .ThenBy(s => s.HardwareName)
                     .ThenBy(s => s.SensorName))
        {
            TemperatureSensors.Add(sensor);
        }

        PerformanceSensors.Clear();
        foreach (var sensor in snapshot.PerformanceSensors
                     .OrderBy(s => s.HardwareType)
                     .ThenBy(s => s.HardwareName)
                     .ThenBy(s => s.MetricType)
                     .ThenBy(s => s.SensorName))
        {
            PerformanceSensors.Add(sensor);
        }

        GpuMemorySensors.Clear();
        foreach (var sensor in snapshot.GpuMemorySensors
                     .OrderBy(s => s.HardwareName)
                     .ThenBy(s => s.SensorName))
        {
            GpuMemorySensors.Add(sensor);
        }

        BatterySensors.Clear();
        foreach (var sensor in snapshot.BatterySensors
                     .OrderBy(s => s.HardwareName)
                     .ThenBy(s => s.MetricType)
                     .ThenBy(s => s.SensorName))
        {
            BatterySensors.Add(sensor);
        }

        MemoryTimingSensors.Clear();
        foreach (var sensor in snapshot.MemoryTimingSensors
                     .OrderBy(s => s.HardwareName)
                     .ThenBy(s => s.SensorName))
        {
            MemoryTimingSensors.Add(sensor);
        }

        ReplaceWarnings(snapshot.Warnings);
        ReplaceErrors(snapshot.Errors);

        HasSensors = snapshot.HasTemperatureSensors;
        ValidSensorCount = snapshot.ValidTemperatureSensorCount;
        SensorCountText = snapshot.TemperatureSensors.Count.ToString();
        ValidSensorCountText = snapshot.ValidTemperatureSensorCount.ToString();

        PerformanceSensorCount = snapshot.PerformanceSensors.Count;
        ValidPerformanceSensorCount = snapshot.ValidPerformanceSensorCount;
        GpuMemorySensorCount = snapshot.GpuMemorySensors.Count;
        ValidGpuMemorySensorCount = snapshot.ValidGpuMemorySensorCount;
        BatterySensorCount = snapshot.BatterySensors.Count;
        ValidBatterySensorCount = snapshot.ValidBatterySensorCount;
        MemoryTimingSensorCount = snapshot.MemoryTimingSensors.Count;
        ValidMemoryTimingSensorCount = snapshot.ValidMemoryTimingSensorCount;

        HasLiveData = snapshot.TemperatureSensors.Count > 0 ||
                      snapshot.PerformanceSensors.Count > 0 ||
                      snapshot.GpuMemorySensors.Count > 0 ||
                      snapshot.BatterySensors.Count > 0 ||
                      snapshot.MemoryTimingSensors.Count > 0;

        UpdateEmptyHints();
        ProviderStatusText = snapshot.IsAvailable ? "Disponible" : "No disponible";
        AdminText = snapshot.IsElevated ? "Sí" : "No";

        if (!IsMonitoring)
        {
            UpdateIdleLiveStatus();
        }
    }

    /// <summary>
    /// Aplica un fallo live controlado ante excepción inesperada:
    /// limpia todas las familias (nunca deja datos stale) y refleja el error actual.
    /// </summary>
    private void ApplyLiveFailure(string message, bool isStreamError)
    {
        HasLiveReading = true;
        IsAvailable = false;
        HasLiveData = false;

        TemperatureSensors.Clear();
        PerformanceSensors.Clear();
        GpuMemorySensors.Clear();
        BatterySensors.Clear();
        MemoryTimingSensors.Clear();

        HasSensors = false;
        ValidSensorCount = 0;
        SensorCountText = "0";
        ValidSensorCountText = "0";
        PerformanceSensorCount = 0;
        ValidPerformanceSensorCount = 0;
        GpuMemorySensorCount = 0;
        ValidGpuMemorySensorCount = 0;
        BatterySensorCount = 0;
        ValidBatterySensorCount = 0;
        MemoryTimingSensorCount = 0;
        ValidMemoryTimingSensorCount = 0;

        ReplaceErrors([message]);
        Warnings.Clear();
        HasWarnings = false;

        UpdateEmptyHints();
        ProviderStatusText = "No disponible";

        StatusText = isStreamError ? "Error de monitoreo" : "Lectura no disponible";
    }

    /// <summary>
    /// Deriva el estado idle real desde la última lectura (o ausencia de lectura).
    /// </summary>
    private void UpdateIdleLiveStatus()
    {
        if (!HasLiveReading)
        {
            StatusText = "Sin lectura";
            return;
        }

        if (!IsAvailable)
        {
            StatusText = "Lectura no disponible";
            return;
        }

        StatusText = HasLiveData ? "Lectura disponible" : "Sin sensores disponibles";
    }

    private void UpdateEmptyHints()
    {
        var readingAvailable = HasLiveReading && IsAvailable;
        ShowEmptyTemperatureHint = readingAvailable && TemperatureSensors.Count == 0;
        ShowEmptyPerformanceHint = readingAvailable && PerformanceSensors.Count == 0;
        ShowEmptyGpuMemoryHint = readingAvailable && GpuMemorySensors.Count == 0;
        ShowEmptyBatteryHint = readingAvailable && BatterySensors.Count == 0;
        ShowEmptyTimingHint = readingAvailable && MemoryTimingSensors.Count == 0;
    }

    private void ReplaceWarnings(IReadOnlyList<string> warnings)
    {
        Warnings.Clear();
        foreach (var warning in warnings)
        {
            Warnings.Add(warning);
        }

        HasWarnings = Warnings.Count > 0;
    }

    private void ReplaceErrors(IReadOnlyList<string> errors)
    {
        Errors.Clear();
        foreach (var error in errors)
        {
            Errors.Add(error);
        }

        HasErrors = Errors.Count > 0;
    }

    // --- Exportación/copia de datos crudos (solo snapshot actual + inventario) ---

    [ObservableProperty]
    private string _exportStatusText = string.Empty;

    [ObservableProperty]
    private bool _hasExportStatus;

    /// <summary>
    /// Copia al portapapeles la tabla cruda actual (TSV: live + inventario +
    /// diagnóstico técnico). Solo datos presentes en las colecciones.
    /// </summary>
    [RelayCommand]
    private void CopyTable()
    {
        try
        {
            Clipboard.SetText(BuildTableText());
            ExportStatusText = "Tabla copiada al portapapeles.";
            HasExportStatus = true;
        }
        catch (Exception)
        {
            ExportStatusText = "No se pudo copiar la tabla al portapapeles.";
            HasExportStatus = true;
        }
    }

    /// <summary>
    /// Exporta a CSV (separador ';') la tabla cruda actual y el inventario WMI.
    /// </summary>
    [RelayCommand]
    private void ExportCsv()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Exportar hardware a CSV",
                Filter = "CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*",
                DefaultExt = ".csv",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = $"hardware-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            File.WriteAllText(dialog.FileName, BuildCsv(), new UTF8Encoding(false));
            // Solo el nombre de archivo: no se exponen rutas privadas en la UI.
            ExportStatusText = $"CSV exportado: {Path.GetFileName(dialog.FileName)}";
            HasExportStatus = true;
        }
        catch (Exception)
        {
            ExportStatusText = "No se pudo exportar el CSV de hardware.";
            HasExportStatus = true;
        }
    }

    /// <summary>
    /// Tabla cruda actual en TSV, pensada para copiar a un editor o planilla.
    /// </summary>
    public string BuildTableText() => BuildHardwareText('\t', includeSeparatorDirective: false);

    /// <summary>
    /// Tabla cruda actual en CSV con separador ';' y directiva sep= para Excel.
    /// </summary>
    public string BuildCsv() => BuildHardwareText(';', includeSeparatorDirective: true);

    /// <summary>
    /// Panel técnico en texto: proveedor, admin, estado, conteos válidos/total,
    /// warnings y errors. Solo datos ya presentes en el ViewModel.
    /// </summary>
    public string BuildDiagnosticsText()
    {
        var validTemperature = TemperatureSensors.Count(s => s.ValueCelsius.HasValue);
        var validPerformance = PerformanceSensors.Count(s => s.Value.HasValue);
        var validGpuMemory = GpuMemorySensors.Count(s => s.ValueMB.HasValue);
        var validBattery = BatterySensors.Count(s => s.Value.HasValue);
        var validTimings = MemoryTimingSensors.Count(s => s.ValueNanoseconds.HasValue);

        var sb = new StringBuilder();
        sb.AppendLine("PROVEEDOR: " + Dash(ProviderStatusText));
        sb.AppendLine("ADMIN: " + Dash(AdminText));
        sb.AppendLine("ESTADO: " + Dash(StatusText));
        sb.AppendLine($"TEMPERATURA: {validTemperature}/{TemperatureSensors.Count} válidos");
        sb.AppendLine($"PERFORMANCE: {validPerformance}/{PerformanceSensors.Count} válidos");
        sb.AppendLine($"GPU MEMORIA: {validGpuMemory}/{GpuMemorySensors.Count} válidos");
        sb.AppendLine($"BATERÍA: {validBattery}/{BatterySensors.Count} válidos");
        sb.AppendLine($"TIMINGS SPD: {validTimings}/{MemoryTimingSensors.Count} válidos");
        sb.AppendLine($"INVENTARIO: {Dash(InventoryStatusText)}");

        sb.AppendLine($"WARNINGS ({Warnings.Count})");
        AppendLines(sb, Warnings);

        sb.AppendLine($"ERRORS ({Errors.Count})");
        AppendLines(sb, Errors);

        sb.AppendLine($"INVENTORY_ERRORS ({InventoryErrors.Count})");
        AppendLines(sb, InventoryErrors);

        return sb.ToString();
    }

    private string BuildHardwareText(char separator, bool includeSeparatorDirective)
    {
        var sb = new StringBuilder();

        if (includeSeparatorDirective)
        {
            sb.AppendLine($"sep={separator}");
        }

        sb.AppendLine("# CATTECH Optimizer Pro — Hardware");
        sb.AppendLine($"# capturado{separator}{LastUpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "N/D"}");
        sb.AppendLine($"# proveedor{separator}{Cell(ProviderStatusText, separator)}");
        sb.AppendLine($"# admin{separator}{Cell(AdminText, separator)}");
        sb.AppendLine($"# estado{separator}{Cell(StatusText, separator)}");
        sb.AppendLine();

        // --- Snapshot live ---
        sb.AppendLine("# LIVE");
        sb.AppendLine(
            $"familia{separator}hardware{separator}tipo{separator}sensor{separator}" +
            $"metrica{separator}valor{separator}unidad{separator}min{separator}max");

        foreach (var sensor in TemperatureSensors)
        {
            AppendRow(sb, separator,
                "temperatura", sensor.HardwareName, sensor.HardwareType, sensor.SensorName,
                string.Empty, Num(sensor.ValueCelsius), "°C",
                Num(sensor.MinCelsius), Num(sensor.MaxCelsius));
        }

        foreach (var sensor in PerformanceSensors)
        {
            AppendRow(sb, separator,
                "performance", sensor.HardwareName, sensor.HardwareType, sensor.SensorName,
                sensor.MetricType.ToString(), Num(sensor.Value), sensor.Unit,
                Num(sensor.Min), Num(sensor.Max));
        }

        foreach (var sensor in GpuMemorySensors)
        {
            AppendRow(sb, separator,
                "gpu_memoria", sensor.HardwareName, sensor.HardwareType, sensor.SensorName,
                string.Empty, Num(sensor.ValueMB), sensor.Unit,
                Num(sensor.MinMB), Num(sensor.MaxMB));
        }

        foreach (var sensor in BatterySensors)
        {
            AppendRow(sb, separator,
                "bateria", sensor.HardwareName, sensor.HardwareType, sensor.SensorName,
                sensor.MetricType.ToString(), Num(sensor.Value), sensor.Unit,
                Num(sensor.Min), Num(sensor.Max));
        }

        foreach (var sensor in MemoryTimingSensors)
        {
            AppendRow(sb, separator,
                "spd", sensor.HardwareName, sensor.HardwareType, sensor.SensorName,
                string.Empty, Num(sensor.ValueNanoseconds), "ns",
                Num(sensor.MinNanoseconds), Num(sensor.MaxNanoseconds));
        }

        if (TemperatureSensors.Count + PerformanceSensors.Count + GpuMemorySensors.Count +
            BatterySensors.Count + MemoryTimingSensors.Count == 0)
        {
            sb.AppendLine($"(sin sensores live){separator}{separator}{separator}{separator}{separator}{separator}{separator}{separator}");
        }

        // --- Inventario estático WMI ---
        sb.AppendLine();
        sb.AppendLine("# INVENTARIO");
        sb.AppendLine($"seccion{separator}campo{separator}valor");

        AppendRow(sb, separator, "cpu", "nombre", Cpu.Name);
        AppendRow(sb, separator, "cpu", "fabricante", Cpu.Manufacturer);
        AppendRow(sb, separator, "cpu", "nucleos", Cpu.Cores.ToString(CultureInfo.InvariantCulture));
        AppendRow(sb, separator, "cpu", "hilos", Cpu.Threads.ToString(CultureInfo.InvariantCulture));
        AppendRow(sb, separator, "cpu", "velocidad_base_ghz", Num(Cpu.BaseSpeedGHz));

        AppendRow(sb, separator, "ram", "total_gb", Num(Memory.TotalGB));
        AppendRow(sb, separator, "ram", "disponible_gb", Num(Memory.AvailableGB));
        AppendRow(sb, separator, "ram", "en_uso_gb", Num(Memory.UsedGB));
        AppendRow(sb, separator, "ram", "uso_porcentaje", Num(Memory.UsagePercent));
        AppendRow(sb, separator, "ram", "tipo", Memory.Type);
        AppendRow(sb, separator, "ram", "velocidad_mhz", Memory.SpeedMHz.ToString(CultureInfo.InvariantCulture));
        AppendRow(sb, separator, "ram", "slots", $"{Memory.SlotsUsed}/{Memory.SlotsTotal}");

        foreach (var gpu in Gpus)
        {
            AppendRow(sb, separator, "gpu", "nombre", gpu.Name);
            AppendRow(sb, separator, "gpu", "fabricante", gpu.Manufacturer);
            AppendRow(sb, separator, "gpu", "memoria_gb", Num(gpu.MemoryGB));
            AppendRow(sb, separator, "gpu", "tipo_memoria", gpu.MemoryType);
        }

        foreach (var module in MemoryModules)
        {
            var slot = string.IsNullOrWhiteSpace(module.DeviceLocator) ? "N/D" : module.DeviceLocator;
            AppendRow(sb, separator, "ram_modulo", $"{slot}:capacidad_gb", Num(module.CapacityGB));
            AppendRow(sb, separator, "ram_modulo", $"{slot}:tipo", module.MemoryType);
            AppendRow(sb, separator, "ram_modulo", $"{slot}:velocidad_mhz",
                module.ConfiguredClockSpeedMHz?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendRow(sb, separator, "ram_modulo", $"{slot}:fabricante", module.Manufacturer);
            AppendRow(sb, separator, "ram_modulo", $"{slot}:part_number", module.PartNumber);
            AppendRow(sb, separator, "ram_modulo", $"{slot}:serie", module.SerialNumber);
            AppendRow(sb, separator, "ram_modulo", $"{slot}:data_width_bits",
                module.DataWidthBits?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendRow(sb, separator, "ram_modulo", $"{slot}:rank",
                module.Rank?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        }

        AppendRow(sb, separator, "placa_madre", "fabricante", Motherboard.Manufacturer);
        AppendRow(sb, separator, "placa_madre", "modelo", Motherboard.Model);
        AppendRow(sb, separator, "placa_madre", "bios", Motherboard.BiosVersion);
        AppendRow(sb, separator, "placa_madre", "bios_fecha",
            Motherboard.BiosDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty);

        // --- Diagnóstico técnico ---
        sb.AppendLine();
        sb.AppendLine("# DIAGNOSTICO");
        sb.AppendLine($"seccion{separator}campo{separator}valor");
        AppendRow(sb, separator, "diagnostico", "proveedor", ProviderStatusText);
        AppendRow(sb, separator, "diagnostico", "admin", AdminText);
        AppendRow(sb, separator, "diagnostico", "estado", StatusText);
        AppendRow(sb, separator, "diagnostico", "temperatura_validos",
            $"{TemperatureSensors.Count(s => s.ValueCelsius.HasValue)}/{TemperatureSensors.Count}");
        AppendRow(sb, separator, "diagnostico", "performance_validos",
            $"{PerformanceSensors.Count(s => s.Value.HasValue)}/{PerformanceSensors.Count}");
        AppendRow(sb, separator, "diagnostico", "gpu_memoria_validos",
            $"{GpuMemorySensors.Count(s => s.ValueMB.HasValue)}/{GpuMemorySensors.Count}");
        AppendRow(sb, separator, "diagnostico", "bateria_validos",
            $"{BatterySensors.Count(s => s.Value.HasValue)}/{BatterySensors.Count}");
        AppendRow(sb, separator, "diagnostico", "spd_validos",
            $"{MemoryTimingSensors.Count(s => s.ValueNanoseconds.HasValue)}/{MemoryTimingSensors.Count}");
        AppendRow(sb, separator, "diagnostico", "inventario", InventoryStatusText);

        foreach (var warning in Warnings)
        {
            AppendRow(sb, separator, "warning", warning);
        }

        foreach (var error in Errors)
        {
            AppendRow(sb, separator, "error", error);
        }

        foreach (var error in InventoryErrors)
        {
            AppendRow(sb, separator, "inventario_error", error);
        }

        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, char separator, params string[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(separator);
            }

            sb.Append(Cell(cells[i], separator));
        }

        sb.AppendLine();
    }

    private static string Cell(string? value, char separator)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r", " ").Replace('\n', ' ');
        if (normalized.IndexOf(separator) < 0 && normalized.IndexOf('"') < 0)
        {
            return normalized;
        }

        return '"' + normalized.Replace("\"", "\"\"") + '"';
    }

    private static void AppendLines(StringBuilder sb, IEnumerable<string> lines)
    {
        var any = false;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            sb.AppendLine("- " + line.Replace("\r", " ").Replace('\n', ' ').Trim());
            any = true;
        }

        if (!any)
        {
            sb.AppendLine("- (sin registros)");
        }
    }

    private static string Dash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "N/D" : value.Trim();

    private static string Num(double? value)
        => value.HasValue
            ? value.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : string.Empty;

    private static string Num(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Consulta manual el inventario estático (CPU/GPU/RAM/placa madre) vía WMI/SMBIOS.
    /// Se ejecuta en background y es independiente del monitoreo live.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefreshInventory))]
    private async Task RefreshInventoryAsync()
    {
        if (IsInventoryBusy)
        {
            return;
        }

        IsInventoryBusy = true;
        InventoryStatusText = "Leyendo inventario...";
        try
        {
            var result = await Task.Run(ReadInventoryAsync);
            ApplyInventory(result);
        }
        catch (Exception)
        {
            // Hardware providers can include local paths and identifiers in
            // exception messages; do not send them to bound UI collections.
            ReplaceInventoryErrors(["Error de inventario: no se pudo completar la lectura."]);
            InventoryStatusText = "Inventario no disponible";
        }
        finally
        {
            IsInventoryBusy = false;
        }
    }

    /// <summary>
    /// Ejecuta las consultas estáticas de forma secuencial en background.
    /// Cada sección está protegida individualmente: un fallo no cancela el resto.
    /// </summary>
    private async Task<HardwareInventoryResult> ReadInventoryAsync()
    {
        var result = new HardwareInventoryResult();

        try
        {
            result.Cpu = await _hardwareService.GetCpuInfoAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            result.Errors.Add("CPU: no se pudo consultar el hardware.");
        }

        try
        {
            result.Gpus = await _hardwareService.GetGpuInfoAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            result.Errors.Add("GPU: no se pudo consultar el hardware.");
        }

        try
        {
            result.Memory = await _hardwareService.GetMemoryInfoAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            result.Errors.Add("RAM: no se pudo consultar el hardware.");
        }

        try
        {
            result.Motherboard = await _hardwareService.GetMotherboardInfoAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            result.Errors.Add("Placa madre: no se pudo consultar el hardware.");
        }

        return result;
    }

    private void ApplyInventory(HardwareInventoryResult result)
    {
        Cpu = result.Cpu;
        Memory = result.Memory;
        Motherboard = result.Motherboard;

        Gpus.Clear();
        foreach (var gpu in result.Gpus.OrderBy(g => g.Name).ThenBy(g => g.Manufacturer))
        {
            Gpus.Add(gpu);
        }

        MemoryModules.Clear();
        foreach (var module in result.Memory.Modules
                     .OrderBy(m => m.DeviceLocator)
                     .ThenBy(m => m.BankLabel)
                     .ThenBy(m => m.Manufacturer)
                     .ThenBy(m => m.PartNumber))
        {
            MemoryModules.Add(module);
        }

        ReplaceInventoryErrors(result.Errors);
        IsInventoryLoaded = true;
        InventoryLastUpdatedAt = DateTime.Now;
        InventoryLastUpdatedText = $"Última actualización: {InventoryLastUpdatedAt:dd/MM/yyyy HH:mm:ss}";

        InventoryStatusText = result.Errors.Count switch
        {
            0 => "Inventario actualizado",
            _ when result.Errors.Count >= 4 => "Inventario no disponible",
            _ => "Inventario parcial"
        };
    }

    private void ReplaceInventoryErrors(IReadOnlyList<string> errors)
    {
        InventoryErrors.Clear();
        foreach (var error in errors)
        {
            InventoryErrors.Add(error);
        }

        HasInventoryErrors = InventoryErrors.Count > 0;
    }

    /// <summary>
    /// Resultado interno de la consulta estática. Detalle de presentación/orquestación.
    /// </summary>
    private sealed class HardwareInventoryResult
    {
        public CpuInfo Cpu { get; set; } = new();
        public List<GpuInfo> Gpus { get; set; } = new();
        public MemoryInfo Memory { get; set; } = new();
        public MotherboardInfo Motherboard { get; set; } = new();
        public List<string> Errors { get; } = new();
    }
}
