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
    private readonly bool _isElevated;

    public LibreHardwareSensorService()
        : this(new LibreHardwareMonitorFactory(), IsProcessElevated())
    {
    }

    internal LibreHardwareSensorService(IHardwareMonitorFactory factory, bool isElevated)
    {
        _factory = factory;
        _isElevated = isElevated;
    }

    /// <inheritdoc/>
    public Task<HardwareTemperatureSnapshot> GetTemperatureSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        // La lectura de LibreHardwareMonitor es sincrónica; se ejecuta en background
        // para no bloquear el hilo de la futura UI WPF.
        return Task.Run(() => ReadSnapshot(cancellationToken), cancellationToken);
    }

    private HardwareTemperatureSnapshot ReadSnapshot(CancellationToken cancellationToken)
    {
        var snapshot = new HardwareTemperatureSnapshot
        {
            CapturedAt = DateTime.Now,
            IsElevated = _isElevated
        };

        if (!_isElevated)
        {
            snapshot.Warnings.Add("Algunos sensores pueden no estar disponibles sin permisos de administrador.");
        }

        IHardwareMonitorSession? session = null;
        try
        {
            session = _factory.Create();
            snapshot.IsAvailable = true;

            cancellationToken.ThrowIfCancellationRequested();

            var seen = new HashSet<string>();
            foreach (var node in session.Hardware)
            {
                CollectTemperatureSensors(node, snapshot, seen, cancellationToken);
            }

            if (snapshot.Sensors.Count == 0)
            {
                snapshot.Warnings.Add("No se detectaron sensores de temperatura disponibles.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            snapshot.IsAvailable = false;
            snapshot.Errors.Add($"No se pudo inicializar el monitoreo de hardware: {ex.Message}");
        }
        finally
        {
            session?.Dispose();
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

        // Un fallo en un hardware no pierde los sensores válidos de otros
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
        }
        catch (Exception ex)
        {
            snapshot.Errors.Add($"Error al leer sensores de {node.Name}: {ex.Message}");
        }

        foreach (var sub in node.SubHardware)
        {
            CollectTemperatureSensors(sub, snapshot, seen, cancellationToken);
        }
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
