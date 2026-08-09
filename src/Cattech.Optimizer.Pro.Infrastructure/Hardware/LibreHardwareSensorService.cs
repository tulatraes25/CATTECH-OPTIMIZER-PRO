using System.Runtime.CompilerServices;
using System.Security.Principal;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Hardware;
using Cattech.Optimizer.Pro.Infrastructure.Hardware.SensorProvider;

namespace Cattech.Optimizer.Pro.Infrastructure.Hardware;

/// <summary>
/// Servicio read-only de sensores de temperatura mediante LibreHardwareMonitorLib.
/// Independiente de WmiHardwareService: recolecta datos dinámicos sin interpretar salud térmica.
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
    public Task<HardwareTemperatureSnapshot> GetTemperatureSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        // La lectura de LibreHardwareMonitor es sincrónica; se ejecuta en background
        // para no bloquear el hilo de la futura UI WPF.
        return Task.Run(() => ReadSingleSnapshot(cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<HardwareTemperatureSnapshot> WatchTemperatureSnapshotsAsync(
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

    private HardwareTemperatureSnapshot ReadSingleSnapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IHardwareMonitorSession? session = null;
        try
        {
            session = _factory.Create();
            cancellationToken.ThrowIfCancellationRequested();
            session.Refresh();
            return BuildSnapshot(session, cancellationToken);
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

    /// <summary>
    /// Refresca y captura una muestra. Ejecución en background, estrictamente secuencial.
    /// Un Refresh fallido produce una muestra no disponible sin cerrar el monitor:
    /// el siguiente intento puede recuperarse.
    /// </summary>
    private async Task<HardwareTemperatureSnapshot> CaptureWithRefreshAsync(
        IHardwareMonitorSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() =>
            {
                session.Refresh();
                return BuildSnapshot(session, cancellationToken);
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

    private HardwareTemperatureSnapshot BuildSnapshot(
        IHardwareMonitorSession session,
        CancellationToken cancellationToken)
    {
        var snapshot = new HardwareTemperatureSnapshot
        {
            CapturedAt = DateTime.Now,
            IsAvailable = true,
            IsElevated = _isElevated
        };

        if (!_isElevated)
        {
            snapshot.Warnings.Add("Algunos sensores pueden no estar disponibles sin permisos de administrador.");
        }

        // La deduplicación se reinicia por snapshot: la misma sesión puede
        // producir muestras independientes.
        var seen = new HashSet<string>();
        foreach (var node in session.Hardware)
        {
            CollectTemperatureSensors(node, snapshot, seen, cancellationToken);
        }

        if (snapshot.Sensors.Count == 0)
        {
            snapshot.Warnings.Add("No se detectaron sensores de temperatura disponibles.");
        }

        return snapshot;
    }

    private static void CollectTemperatureSensors(
        IHardwareNode node,
        HardwareTemperatureSnapshot snapshot,
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
                if (sensor.SensorType != InternalSensorType.Temperature)
                {
                    continue;
                }

                var identifier = ResolveIdentifier(node, sensor);
                if (!seen.Add(identifier))
                {
                    continue;
                }

                snapshot.Sensors.Add(new HardwareTemperatureSensor
                {
                    HardwareName = node.Name,
                    HardwareType = MapHardwareType(node.HardwareType),
                    SensorName = sensor.Name,
                    SensorIdentifier = identifier,
                    ValueCelsius = Normalize(sensor.Value),
                    MinCelsius = Normalize(sensor.Min),
                    MaxCelsius = Normalize(sensor.Max)
                });
            }

            foreach (var sub in node.SubHardware)
            {
                CollectTemperatureSensors(sub, snapshot, seen, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            snapshot.Errors.Add($"Error al leer sensores de {node.Name}: {ex.Message}");
        }
    }

    private HardwareTemperatureSnapshot CreateUnavailableSnapshot(string error)
    {
        var snapshot = new HardwareTemperatureSnapshot
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
        _ => "Otro"
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
