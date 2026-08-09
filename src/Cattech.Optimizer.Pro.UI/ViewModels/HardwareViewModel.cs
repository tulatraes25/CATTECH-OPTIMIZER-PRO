using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Hardware;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel de la pantalla Hardware: muestra sensores de temperatura en tiempo real.
/// Recolecta datos únicamente: no interpreta salud térmica ni aplica thresholds.
/// </summary>
public partial class HardwareViewModel : ObservableObject
{
    /// <summary>
    /// Cadencia visual del monitoreo. No es un threshold ni una regla técnica.
    /// </summary>
    public static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(2);

    private readonly IHardwareSensorService _hardwareSensorService;
    private CancellationTokenSource? _cts;

    public ObservableCollection<HardwareTemperatureSensor> TemperatureSensors { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();
    public ObservableCollection<string> Errors { get; } = new();

    public HardwareViewModel(IHardwareSensorService hardwareSensorService)
    {
        _hardwareSensorService = hardwareSensorService;
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

    [ObservableProperty]
    private bool _hasSensors;

    [ObservableProperty]
    private int _validSensorCount;

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

    private bool CanRefresh => !IsMonitoring && !IsBusy;

    private bool CanStartMonitoring => !IsMonitoring && !IsBusy;

    private bool CanStopMonitoring => IsMonitoring;

    /// <summary>
    /// Lee una vez el estado actual de los sensores.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusText = "Leyendo...";
        try
        {
            var snapshot = await _hardwareSensorService.GetTemperatureSnapshotAsync();
            ApplySnapshot(snapshot);
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

    private async Task RunMonitoringAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var snapshot in _hardwareSensorService
                               .WatchTemperatureSnapshotsAsync(MonitorInterval, cancellationToken))
            {
                ApplySnapshot(snapshot);
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
    /// Aplica un snapshot al estado de la UI. Cada muestra representa el estado ACTUAL:
    /// los sensores anteriores se descartan, nunca se conservan como actuales.
    /// </summary>
    private void ApplySnapshot(HardwareTemperatureSnapshot snapshot)
    {
        IsAvailable = snapshot.IsAvailable;
        IsElevated = snapshot.IsElevated;
        LastUpdatedAt = snapshot.CapturedAt;
        LastUpdatedText = $"Última actualización: {snapshot.CapturedAt:dd/MM/yyyy HH:mm:ss}";

        TemperatureSensors.Clear();
        foreach (var sensor in snapshot.Sensors
                     .OrderBy(s => s.HardwareType)
                     .ThenBy(s => s.HardwareName)
                     .ThenBy(s => s.SensorName))
        {
            TemperatureSensors.Add(sensor);
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

        HasSensors = snapshot.HasSensors;
        ValidSensorCount = snapshot.ValidSensorCount;
        SensorCountText = snapshot.Sensors.Count.ToString();
        ValidSensorCountText = snapshot.ValidSensorCount.ToString();
        ProviderStatusText = snapshot.IsAvailable ? "Disponible" : "No disponible";
        AdminText = snapshot.IsElevated ? "Sí" : "No";

        StatusText = snapshot.IsAvailable
            ? snapshot.HasSensors ? "Lectura disponible" : "Sin sensores disponibles"
            : "Lectura no disponible";
    }
}
