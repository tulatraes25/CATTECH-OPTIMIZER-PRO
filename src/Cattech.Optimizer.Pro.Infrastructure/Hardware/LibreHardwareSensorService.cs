using System.Runtime.CompilerServices;
using System.Security.Principal;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Hardware;
using Cattech.Optimizer.Pro.Infrastructure.Hardware.SensorProvider;

namespace Cattech.Optimizer.Pro.Infrastructure.Hardware;

/// <summary>
/// Servicio read-only de sensores de hardware mediante LibreHardwareMonitorLib.
/// Independiente de WmiHardwareService: recolecta datos dinámicos sin interpretar salud térmica
/// ni rendimiento. Temperatura + Load + Clock se capturan con UN solo Refresh por muestra.
/// </summary>
public class LibreHardwareSensorService : IHardwareSensorService
{
    private readonly IHardwareMonitorFactory _factory;
    private readonly IHardwareMonitorDelay _delay;
    private readonly bool _isElevated;

    public LibreHardwareSensorService()
        : this(new LibreHardwareMonitorFactory(), new TaskDelay(), IsProcessElevated())
    {
    }

    internal LibreHardwareSensorService(IHardwareMonitorFactory factory, bool isElevated)
        : this(factory, new TaskDelay(), isElevated)
    {
    }

    internal LibreHardwareSensorService(IHardwareMonitorFactory factory, IHardwareMonitorDelay delay, bool isElevated)
    {
        _factory = factory;
        _delay = delay;
        _isElevated = isElevated;
    }

    /// <inheritdoc/>
    public async Task<HardwareTemperatureSnapshot> GetTemperatureSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var live = await GetLiveSnapshotAsync(cancellationToken);
        return ToTemperatureSnapshot(live);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<HardwareTemperatureSnapshot> WatchTemperatureSnapshotsAsync(
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var live in WatchLiveSnapshotsAsync(interval, cancellationToken))
        {
            yield return ToTemperatureSnapshot(live);
        }
    }

    /// <inheritdoc/>
    public Task<HardwareLiveSnapshot> GetLiveSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        // La lectura de LibreHardwareMonitor es sincrónica; se ejecuta en background
        // para no bloquear el hilo de la futura UI WPF.
        return Task.Run(() => ReadSingleSnapshot(cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<HardwareLiveSnapshot> WatchLiveSnapshotsAsync(
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval),
                "El intervalo entre muestras debe ser mayor que TimeSpan.Zero.");
        }

        IHardwareMonitorSession? session = null;
        try
        {
            session = TryCreateSession(out var createError);
            if (createError != null)
            {
                // Un fallo de apertura produce UNA muestra controlada y el stream termina.
                yield return CreateUnavailableSnapshot(createError);
                yield break;
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return await CaptureWithRefreshAsync(session, cancellationToken);
                await _delay.DelayAsync(interval, cancellationToken);
            }
        }
        finally
        {
            session?.Dispose();
        }
    }

    private HardwareLiveSnapshot ReadSingleSnapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IHardwareMonitorSession? session = null;
        try
        {
            session = _factory.Create();
            cancellationToken.ThrowIfCancellationRequested();
            session.Refresh();
            return BuildLiveSnapshot(session, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CreateUnavailableSnapshot($"No se pudo inicializar el monitoreo de hardware: {ex.Message}");
        }
        finally
        {
            session?.Dispose();
        }
    }

    private IHardwareMonitorSession? TryCreateSession(out string? error)
    {
        try
        {
            error = null;
            return _factory.Create();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            error = $"No se pudo inicializar el monitoreo de hardware: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Refresca y captura una muestra. Ejecución en background, estrictamente secuencial.
    /// Un Refresh fallido produce una muestra no disponible sin cerrar el monitor:
    /// el siguiente intento puede recuperarse.
    /// </summary>
    private async Task<HardwareLiveSnapshot> CaptureWithRefreshAsync(
        IHardwareMonitorSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() =>
            {
                session.Refresh();
                return BuildLiveSnapshot(session, cancellationToken);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CreateUnavailableSnapshot($"No se pudo actualizar la lectura de sensores: {ex.Message}");
        }
    }

    /// <summary>
    /// Construye la captura completa (temperaturas + rendimiento) recorriendo el
    /// hardware UNA sola vez. La deduplicación se reinicia por snapshot.
    /// </summary>
    private HardwareLiveSnapshot BuildLiveSnapshot(
        IHardwareMonitorSession session,
        CancellationToken cancellationToken)
    {
        var snapshot = new HardwareLiveSnapshot
        {
            CapturedAt = DateTime.Now,
            IsAvailable = true,
            IsElevated = _isElevated
        };

        if (!_isElevated)
        {
            snapshot.Warnings.Add("Algunos sensores pueden no estar disponibles sin permisos de administrador.");
        }

        var seen = new HashSet<string>();
        foreach (var node in session.Hardware)
        {
            CollectSensors(node, snapshot, seen, cancellationToken);
        }

        if (snapshot.TemperatureSensors.Count == 0)
        {
            snapshot.Warnings.Add("No se detectaron sensores de temperatura disponibles.");
        }

        return snapshot;
    }

    private static void CollectSensors(
        IHardwareNode node,
        HardwareLiveSnapshot snapshot,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Un fallo parcial (sensores o subhardware) no derriba el snapshot completo:
        // se registra como error y los demás nodos siguen leyéndose.
        try
        {
            foreach (var sensor in node.Sensors)
            {
                var identifier = ResolveIdentifier(node, sensor);
                if (!seen.Add(identifier))
                {
                    continue;
                }

                switch (sensor.SensorType)
                {
                    case InternalSensorType.Temperature:
                        snapshot.TemperatureSensors.Add(new HardwareTemperatureSensor
                        {
                            HardwareName = node.Name,
                            HardwareType = MapHardwareType(node.HardwareType),
                            SensorName = sensor.Name,
                            SensorIdentifier = identifier,
                            ValueCelsius = Normalize(sensor.Value),
                            MinCelsius = Normalize(sensor.Min),
                            MaxCelsius = Normalize(sensor.Max)
                        });
                        break;

                    case InternalSensorType.Load:
                    case InternalSensorType.Clock:
                        // Métricas de rendimiento: solo CPU/GPU en esta fase.
                        if (node.HardwareType is InternalHardwareType.Cpu or InternalHardwareType.Gpu)
                        {
                            var isLoad = sensor.SensorType == InternalSensorType.Load;
                            snapshot.PerformanceSensors.Add(new HardwarePerformanceSensor
                            {
                                HardwareName = node.Name,
                                HardwareType = MapHardwareType(node.HardwareType),
                                SensorName = sensor.Name,
                                SensorIdentifier = identifier,
                                MetricType = isLoad
                                    ? HardwarePerformanceMetricType.Load
                                    : HardwarePerformanceMetricType.Clock,
                                Value = Normalize(sensor.Value),
                                Min = Normalize(sensor.Min),
                                Max = Normalize(sensor.Max),
                                Unit = isLoad ? "%" : "MHz"
                            });
                        }

                        break;

                    case InternalSensorType.SmallData:
                        // Memoria de GPU: solo hardware GPU en esta fase.
                        if (node.HardwareType == InternalHardwareType.Gpu)
                        {
                            snapshot.GpuMemorySensors.Add(new HardwareGpuMemorySensor
                            {
                                HardwareName = node.Name,
                                HardwareType = MapHardwareType(node.HardwareType),
                                SensorName = sensor.Name,
                                SensorIdentifier = identifier,
                                ValueMB = Normalize(sensor.Value),
                                MinMB = Normalize(sensor.Min),
                                MaxMB = Normalize(sensor.Max)
                            });
                        }

                        break;

                    case InternalSensorType.Level:
                    case InternalSensorType.Energy:
                    case InternalSensorType.Voltage:
                    case InternalSensorType.Current:
                    case InternalSensorType.Power:
                    case InternalSensorType.TimeSpan:
                        // Telemetría de batería no térmica: solo hardware Battery.
                        if (node.HardwareType == InternalHardwareType.Battery)
                        {
                            var metricType = MapBatteryMetricType(sensor.SensorType);
                            snapshot.BatterySensors.Add(new HardwareBatterySensor
                            {
                                HardwareName = node.Name,
                                HardwareType = MapHardwareType(node.HardwareType),
                                SensorName = sensor.Name,
                                SensorIdentifier = identifier,
                                MetricType = metricType,
                                Value = Normalize(sensor.Value),
                                Min = Normalize(sensor.Min),
                                Max = Normalize(sensor.Max),
                                Unit = MapBatteryUnit(metricType)
                            });
                        }

                        break;
                    case InternalSensorType.Timing:
                        // Timings SPD: solo hardware Memory. Valores en ns, sin conversión a ciclos.
                        if (node.HardwareType == InternalHardwareType.Memory)
                        {
                            snapshot.MemoryTimingSensors.Add(new HardwareMemoryTimingSensor
                            {
                                HardwareName = node.Name,
                                HardwareIdentifier = node.Identifier,
                                HardwareType = MapHardwareType(node.HardwareType),
                                SensorName = sensor.Name,
                                SensorIdentifier = identifier,
                                ValueNanoseconds = Normalize(sensor.Value),
                                MinNanoseconds = Normalize(sensor.Min),
                                MaxNanoseconds = Normalize(sensor.Max),
                                Unit = "ns"
                            });
                        }

                        break;
                }
            }

            foreach (var sub in node.SubHardware)
            {
                CollectSensors(sub, snapshot, seen, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            snapshot.Errors.Add($"Error al leer sensores de {node.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Proyecta una captura live a HardwareTemperatureSnapshot con listas independientes.
    /// No modifica la captura live.
    /// </summary>
    private static HardwareTemperatureSnapshot ToTemperatureSnapshot(HardwareLiveSnapshot live)
    {
        return new HardwareTemperatureSnapshot
        {
            CapturedAt = live.CapturedAt,
            IsAvailable = live.IsAvailable,
            IsElevated = live.IsElevated,
            Sensors = live.TemperatureSensors.ToList(),
            Warnings = live.Warnings.ToList(),
            Errors = live.Errors.ToList()
        };
    }

    private HardwareLiveSnapshot CreateUnavailableSnapshot(string error)
    {
        var snapshot = new HardwareLiveSnapshot
        {
            CapturedAt = DateTime.Now,
            IsAvailable = false,
            IsElevated = _isElevated
        };
        snapshot.Errors.Add(error);
        return snapshot;
    }

    private static string ResolveIdentifier(IHardwareNode node, ISensorNode sensor)
    {
        if (!string.IsNullOrWhiteSpace(sensor.Identifier))
        {
            return sensor.Identifier;
        }

        return string.IsNullOrWhiteSpace(node.Identifier)
            ? $"{node.Name}/{sensor.Name}"
            : $"{node.Identifier}/{sensor.Name}";
    }

    private static string MapHardwareType(InternalHardwareType type) => type switch
    {
        InternalHardwareType.Cpu => "CPU",
        InternalHardwareType.Gpu => "GPU",
        InternalHardwareType.Memory => "Memoria",
        InternalHardwareType.Motherboard => "Placa Madre",
        InternalHardwareType.Storage => "Almacenamiento",
        InternalHardwareType.Controller => "Controlador",
        InternalHardwareType.Battery => "Batería",
        _ => "Otro"
    };

    private static HardwareBatteryMetricType MapBatteryMetricType(InternalSensorType type) => type switch
    {
        InternalSensorType.Level => HardwareBatteryMetricType.Level,
        InternalSensorType.Energy => HardwareBatteryMetricType.Energy,
        InternalSensorType.Voltage => HardwareBatteryMetricType.Voltage,
        InternalSensorType.Current => HardwareBatteryMetricType.Current,
        InternalSensorType.Power => HardwareBatteryMetricType.Power,
        InternalSensorType.TimeSpan => HardwareBatteryMetricType.TimeSpan,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static string MapBatteryUnit(HardwareBatteryMetricType type) => type switch
    {
        HardwareBatteryMetricType.Level => "%",
        HardwareBatteryMetricType.Energy => "mWh",
        HardwareBatteryMetricType.Voltage => "V",
        HardwareBatteryMetricType.Current => "A",
        HardwareBatteryMetricType.Power => "W",
        HardwareBatteryMetricType.TimeSpan => "s",
        _ => string.Empty
    };

    private static double? Normalize(float? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var v = value.Value;
        if (float.IsNaN(v) || float.IsInfinity(v))
        {
            return null;
        }

        return v;
    }

    private static bool IsProcessElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
