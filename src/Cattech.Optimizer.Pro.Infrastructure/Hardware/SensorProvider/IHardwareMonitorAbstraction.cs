namespace Cattech.Optimizer.Pro.Infrastructure.Hardware.SensorProvider;

/// <summary>
/// Tipos de hardware internos de CATTECH (no expone el enum de LibreHardwareMonitor).
/// </summary>
internal enum InternalHardwareType
{
    Cpu,
    Gpu,
    Memory,
    Motherboard,
    Storage,
    Controller,
    Battery,
    Other
}

/// <summary>
/// Tipos de sensor internos de CATTECH.
/// </summary>
internal enum InternalSensorType
{
    Temperature,
    Load,
    Clock,
    SmallData,
    Level,
    Energy,
    Voltage,
    Current,
    Power,
    TimeSpan,
    Other
}

/// <summary>
/// Sesión abierta de monitoreo de hardware. Debe liberarse con Dispose.
/// </summary>
internal interface IHardwareMonitorSession : IDisposable
{
    /// <summary>
    /// Hardware raíz detectado.
    /// </summary>
    IReadOnlyList<IHardwareNode> Hardware { get; }

    /// <summary>
    /// Actualiza los valores actuales de hardware y subhardware.
    /// No recrea ni reabre la sesión.
    /// </summary>
    void Refresh();
}

/// <summary>
/// Nodo de hardware (hardware o subhardware).
/// </summary>
internal interface IHardwareNode
{
    /// <summary>
    /// Nombre del hardware (ej: "Intel Core i7-13700K").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Identificador estable del hardware.
    /// </summary>
    string Identifier { get; }

    /// <summary>
    /// Tipo de hardware.
    /// </summary>
    InternalHardwareType HardwareType { get; }

    /// <summary>
    /// Subhardware (recorrido recursivo).
    /// </summary>
    IReadOnlyList<IHardwareNode> SubHardware { get; }

    /// <summary>
    /// Sensores expuestos por este nodo.
    /// </summary>
    IReadOnlyList<ISensorNode> Sensors { get; }
}

/// <summary>
/// Sensor individual expuesto por un nodo de hardware.
/// </summary>
internal interface ISensorNode
{
    /// <summary>
    /// Nombre del sensor (ej: "Core Max").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Identificador estable del sensor.
    /// </summary>
    string Identifier { get; }

    /// <summary>
    /// Tipo de sensor.
    /// </summary>
    InternalSensorType SensorType { get; }

    /// <summary>
    /// Valor actual. Null si no informado.
    /// </summary>
    float? Value { get; }

    /// <summary>
    /// Valor mínimo observado. Null si no disponible.
    /// </summary>
    float? Min { get; }

    /// <summary>
    /// Valor máximo observado. Null si no disponible.
    /// </summary>
    float? Max { get; }
}

/// <summary>
/// Fábrica de sesiones de monitoreo. Permite simular hardware en tests.
/// Create() abre la sesión pero NO la refresca: Refresh() es responsabilidad del llamador.
/// </summary>
internal interface IHardwareMonitorFactory
{
    /// <summary>
    /// Abre el monitoreo de hardware.
    /// Lanza excepción si no puede inicializarse.
    /// </summary>
    IHardwareMonitorSession Create();
}

/// <summary>
/// Abstracción del delay entre muestras para poder testear sin esperas reales.
/// </summary>
internal interface IHardwareMonitorDelay
{
    /// <summary>
    /// Espera el intervalo especificado o hasta cancelación.
    /// </summary>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

/// <summary>
/// Delay real basado en Task.Delay.
/// </summary>
internal sealed class TaskDelay : IHardwareMonitorDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        => Task.Delay(delay, cancellationToken);
}
