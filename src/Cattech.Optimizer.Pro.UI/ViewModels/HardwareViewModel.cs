using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Hardware;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel de la pantalla Hardware: presenta hardware live en tiempo real
/// (temperaturas, Load/Clock CPU-GPU, memoria GPU, batería, timings SPD)
/// a partir de UN único HardwareLiveSnapshot por captura o muestra.
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

    // --- Estado ---

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartMonitoringCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMonitoringCommand))]
    private bool _isMonitoring;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartMonitoringCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private bool _isElevated;

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
            StatusText = "Lectura no disponible";
            Errors.Add($"Error de lectura: {ex.Message}");
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
            var result = await Task.Run(ReadInventory);
            ApplyInventory(result);
        }
        catch (Exception ex)
        {
            InventoryStatusText = "Inventario no disponible";
            InventoryErrors.Add($"Error de inventario: {ex.Message}");
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
    private HardwareInventoryResult ReadInventory()
    {
        var result = new HardwareInventoryResult();

        try
        {
            result.Cpu = _hardwareService.GetCpuInfoAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"CPU: {ex.Message}");
        }

        try
        {
            result.Gpus = _hardwareService.GetGpuInfoAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"GPU: {ex.Message}");
        }

        try
        {
            result.Memory = _hardwareService.GetMemoryInfoAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"RAM: {ex.Message}");
        }

        try
        {
            result.Motherboard = _hardwareService.GetMotherboardInfoAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Placa madre: {ex.Message}");
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

        InventoryErrors.Clear();
        foreach (var error in result.Errors)
        {
            InventoryErrors.Add(error);
        }

        HasInventoryErrors = result.Errors.Count > 0;
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
            // Cancelación normal por StopMonitoring o por salir de la sección.
        }
        catch (Exception ex)
        {
            StatusText = "Error de monitoreo";
            Errors.Add($"Error de monitoreo: {ex.Message}");
        }
        finally
        {
            IsMonitoring = false;
            if (StatusText == "Monitoreando")
            {
                StatusText = "Lectura disponible";
            }

            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Aplica un snapshot live al estado de la UI. Cada muestra representa el estado ACTUAL:
    /// todas las familias anteriores se descartan, nunca se conservan como actuales.
    /// </summary>
    private void ApplyLiveSnapshot(HardwareLiveSnapshot snapshot)
    {
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

        Warnings.Clear();
        foreach (var warning in snapshot.Warnings)
        {
            Warnings.Add(warning);
        }

        Errors.Clear();
        foreach (var error in snapshot.Errors)
        {
            Errors.Add(error);
        }

        HasWarnings = snapshot.Warnings.Count > 0;
        HasErrors = snapshot.Errors.Count > 0;

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

        ProviderStatusText = snapshot.IsAvailable ? "Disponible" : "No disponible";
        AdminText = snapshot.IsElevated ? "Sí" : "No";

        StatusText = snapshot.IsAvailable
            ? HasLiveData ? "Lectura disponible" : "Sin sensores disponibles"
            : "Lectura no disponible";
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
