using LibreHardwareMonitor.Hardware;

namespace Cattech.Optimizer.Pro.Infrastructure.Hardware.SensorProvider;

/// <summary>
/// Fábrica real que abre LibreHardwareMonitorLib contra el hardware del equipo.
/// Solo lectura: no modifica valores, no controla ventiladores, no escribe configuraciones.
/// </summary>
internal sealed class LibreHardwareMonitorFactory : IHardwareMonitorFactory
{
    private static readonly HardwareUpdateVisitor Visitor = new();

    public IHardwareMonitorSession Create()
    {
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            IsControllerEnabled = true
        };

        try
        {
            computer.Open();

            var hardware = computer.Hardware
                .Select(h => (IHardwareNode)new HardwareNodeAdapter(h))
                .ToList();

            return new LibreHardwareMonitorSession(computer, hardware, Visitor);
        }
        catch
        {
            // No dejar el Computer abierto si la construcción de la sesión falla.
            try
            {
                computer.Close();
            }
            catch
            {
                // Conservar la excepción original.
            }

            throw;
        }
    }
}

/// <summary>
/// Sesión real sobre Computer de LibreHardwareMonitor. Libera el recurso al cerrarse.
/// </summary>
internal sealed class LibreHardwareMonitorSession : IHardwareMonitorSession
{
    private readonly Computer _computer;
    private readonly HardwareUpdateVisitor _visitor;

    public IReadOnlyList<IHardwareNode> Hardware { get; }

    public LibreHardwareMonitorSession(Computer computer, IReadOnlyList<IHardwareNode> hardware, HardwareUpdateVisitor visitor)
    {
        _computer = computer;
        Hardware = hardware;
        _visitor = visitor;
    }

    public void Refresh() => _computer.Accept(_visitor);

    public void Dispose() => _computer.Close();
}

/// <summary>
/// Adaptador de IHardware de LibreHardwareMonitor a IHardwareNode interno.
/// </summary>
internal sealed class HardwareNodeAdapter : IHardwareNode
{
    private readonly IHardware _hardware;

    public string Name => _hardware.Name;

    public string Identifier => _hardware.Identifier.ToString();

    public InternalHardwareType HardwareType => MapType(_hardware.HardwareType);

    public IReadOnlyList<IHardwareNode> SubHardware =>
        _hardware.SubHardware.Select(h => (IHardwareNode)new HardwareNodeAdapter(h)).ToList();

    public IReadOnlyList<ISensorNode> Sensors =>
        _hardware.Sensors.Select(s => (ISensorNode)new SensorNodeAdapter(s)).ToList();

    public HardwareNodeAdapter(IHardware hardware)
    {
        _hardware = hardware;
    }

    private static InternalHardwareType MapType(LibreHardwareMonitor.Hardware.HardwareType type) => type switch
    {
        LibreHardwareMonitor.Hardware.HardwareType.Cpu => InternalHardwareType.Cpu,
        LibreHardwareMonitor.Hardware.HardwareType.GpuNvidia or
        LibreHardwareMonitor.Hardware.HardwareType.GpuAmd or
        LibreHardwareMonitor.Hardware.HardwareType.GpuIntel => InternalHardwareType.Gpu,
        LibreHardwareMonitor.Hardware.HardwareType.Memory => InternalHardwareType.Memory,
        LibreHardwareMonitor.Hardware.HardwareType.Motherboard => InternalHardwareType.Motherboard,
        LibreHardwareMonitor.Hardware.HardwareType.Storage => InternalHardwareType.Storage,
        _ => InternalHardwareType.Other
    };
}

/// <summary>
/// Visitor que actualiza valores de hardware y subhardware tras Open().
/// UpdateVisitor no es público en LibreHardwareMonitorLib 0.9.6; se implementa IVisitor.
/// </summary>
internal sealed class HardwareUpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();

        foreach (var sub in hardware.SubHardware)
        {
            sub.Accept(this);
        }
    }

    public void VisitSensor(ISensor sensor)
    {
    }

    public void VisitParameter(IParameter parameter)
    {
    }
}

/// <summary>
/// Adaptador de ISensor de LibreHardwareMonitor a ISensorNode interno.
/// </summary>
internal sealed class SensorNodeAdapter : ISensorNode
{
    private readonly ISensor _sensor;

    public string Name => _sensor.Name;

    public string Identifier => _sensor.Identifier.ToString();

    public InternalSensorType SensorType => _sensor.SensorType switch
    {
        LibreHardwareMonitor.Hardware.SensorType.Temperature => InternalSensorType.Temperature,
        LibreHardwareMonitor.Hardware.SensorType.Load => InternalSensorType.Load,
        LibreHardwareMonitor.Hardware.SensorType.Clock => InternalSensorType.Clock,
        _ => InternalSensorType.Other
    };

    public float? Value => _sensor.Value;

    public float? Min => _sensor.Min;

    public float? Max => _sensor.Max;

    public SensorNodeAdapter(ISensor sensor)
    {
        _sensor = sensor;
    }
}
