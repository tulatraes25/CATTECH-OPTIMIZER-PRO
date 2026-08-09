namespace Cattech.Optimizer.Pro.Core.Models.Hardware;

/// <summary>
/// Tipo de métrica de rendimiento CATTECH (no expone enums de LibreHardwareMonitor).
/// </summary>
public enum HardwarePerformanceMetricType
{
    /// <summary>Carga (porcentaje).</summary>
    Load,
    /// <summary>Frecuencia (MHz).</summary>
    Clock
}

/// <summary>
/// Sensor de métrica dinámica (Load/Clock) capturado del hardware.
/// Modelo CATTECH: no depende de LibreHardwareMonitorLib.
/// </summary>
public class HardwarePerformanceSensor
{
    /// <summary>
    /// Nombre del hardware padre (ej: "AMD Ryzen 7 5700X").
    /// </summary>
    public string HardwareName { get; set; } = string.Empty;

    /// <summary>
    /// Tipo del hardware padre como valor CATTECH (ej: "CPU", "GPU").
    /// </summary>
    public string HardwareType { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del sensor (ej: "CPU Total", "GPU Core").
    /// </summary>
    public string SensorName { get; set; } = string.Empty;

    /// <summary>
    /// Identificador estable del sensor.
    /// </summary>
    public string SensorIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de métrica (Load o Clock).
    /// </summary>
    public HardwarePerformanceMetricType MetricType { get; set; }

    /// <summary>
    /// Valor actual. Null si el sensor no informó valor. Nunca se convierte null en 0.
    /// </summary>
    public double? Value { get; set; }

    /// <summary>
    /// Valor mínimo observado. Null si no está disponible.
    /// </summary>
    public double? Min { get; set; }

    /// <summary>
    /// Valor máximo observado. Null si no está disponible.
    /// </summary>
    public double? Max { get; set; }

    /// <summary>
    /// Unidad de la métrica: "%" para Load, "MHz" para Clock.
    /// </summary>
    public string Unit { get; set; } = string.Empty;
}

/// <summary>
/// Sensor de memoria de GPU (SensorType.SmallData) capturado del hardware.
/// CATTECH conserva lo que informa el proveedor sin interpretar Used/Free/Total por nombre.
/// </summary>
public class HardwareGpuMemorySensor
{
    /// <summary>
    /// Nombre del hardware padre (ej: "NVIDIA GeForce RTX 4070").
    /// </summary>
    public string HardwareName { get; set; } = string.Empty;

    /// <summary>
    /// Tipo del hardware padre (siempre "GPU" en esta fase).
    /// </summary>
    public string HardwareType { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del sensor tal como lo informa el proveedor (ej: "GPU Memory Used").
    /// No se interpreta por nombre.
    /// </summary>
    public string SensorName { get; set; } = string.Empty;

    /// <summary>
    /// Identificador estable del sensor.
    /// </summary>
    public string SensorIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Valor en MB. Null si el sensor no informó valor. Nunca se convierte null en 0.
    /// </summary>
    public double? ValueMB { get; set; }

    /// <summary>
    /// Valor mínimo en MB. Null si no está disponible.
    /// </summary>
    public double? MinMB { get; set; }

    /// <summary>
    /// Valor máximo en MB. Null si no está disponible.
    /// </summary>
    public double? MaxMB { get; set; }

    /// <summary>
    /// Unidad de la métrica.
    /// </summary>
    public string Unit => "MB";
}

/// <summary>
/// Tipo de métrica de batería CATTECH (no expone enums de LibreHardwareMonitor).
/// </summary>
public enum HardwareBatteryMetricType
{
    /// <summary>Nivel (porcentaje).</summary>
    Level,
    /// <summary>Energía (mWh).</summary>
    Energy,
    /// <summary>Voltaje (V).</summary>
    Voltage,
    /// <summary>Corriente (A).</summary>
    Current,
    /// <summary>Potencia (W).</summary>
    Power,
    /// <summary>Tiempo (segundos).</summary>
    TimeSpan
}

/// <summary>
/// Sensor de telemetría de batería capturado del hardware.
/// CATTECH conserva lo que informa el proveedor: no interpreta carga/descarga,
/// salud ni degradación.
/// </summary>
public class HardwareBatterySensor
{
    /// <summary>
    /// Nombre del hardware padre (ej: "Standard Battery").
    /// </summary>
    public string HardwareName { get; set; } = string.Empty;

    /// <summary>
    /// Tipo del hardware padre (siempre "Batería" en esta fase).
    /// </summary>
    public string HardwareType { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del sensor tal como lo informa el proveedor (ej: "Charge Level").
    /// No se interpreta por nombre.
    /// </summary>
    public string SensorName { get; set; } = string.Empty;

    /// <summary>
    /// Identificador estable del sensor.
    /// </summary>
    public string SensorIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de métrica (Level, Energy, Voltage, Current, Power, TimeSpan).
    /// </summary>
    public HardwareBatteryMetricType MetricType { get; set; }

    /// <summary>
    /// Valor actual. Null si el sensor no informó valor. Nunca se convierte null en 0.
    /// </summary>
    public double? Value { get; set; }

    /// <summary>
    /// Valor mínimo observado. Null si no está disponible.
    /// </summary>
    public double? Min { get; set; }

    /// <summary>
    /// Valor máximo observado. Null si no está disponible.
    /// </summary>
    public double? Max { get; set; }

    /// <summary>
    /// Unidad de la métrica ("%", "mWh", "V", "A", "W" o "s").
    /// </summary>
    public string Unit { get; set; } = string.Empty;
}

/// <summary>
/// Timing SPD de memoria (SensorType.Timing) capturado del hardware.
/// El valor es en nanosegundos tal como lo informa el proveedor: CATTECH
/// no lo convierte a ciclos ni calcula CL.
/// </summary>
public class HardwareMemoryTimingSensor
{
    /// <summary>
    /// Nombre del hardware padre (ej: "DDR4-3200 DIMM").
    /// </summary>
    public string HardwareName { get; set; } = string.Empty;

    /// <summary>
    /// Identificador estable del hardware padre (ej: "/mem/dimm/0").
    /// Se preserva sin correlacionar con WMI.
    /// </summary>
    public string HardwareIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Tipo del hardware padre (siempre "Memoria" en esta fase).
    /// </summary>
    public string HardwareType { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del timing tal como lo informa el proveedor (ej: "tAA (CAS Latency Time)").
    /// No se parsea ni interpreta por nombre.
    /// </summary>
    public string SensorName { get; set; } = string.Empty;

    /// <summary>
    /// Identificador estable del sensor.
    /// </summary>
    public string SensorIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Valor en nanosegundos. Null si no informado. Nunca se convierte null en 0.
    /// </summary>
    public double? ValueNanoseconds { get; set; }

    /// <summary>
    /// Valor mínimo en nanosegundos. Null si no disponible.
    /// </summary>
    public double? MinNanoseconds { get; set; }

    /// <summary>
    /// Valor máximo en nanosegundos. Null si no disponible.
    /// </summary>
    public double? MaxNanoseconds { get; set; }

    /// <summary>
    /// Unidad de la métrica (siempre "ns").
    /// </summary>
    public string Unit { get; set; } = "ns";
}

/// <summary>
/// Captura única de la sesión de hardware: temperaturas + métricas de rendimiento
/// + memoria GPU + telemetría de batería + timings SPD de un MISMO Refresh
/// (coherentes temporalmente).
/// Recolecta datos: no interpreta rendimiento, memoria, batería, timings ni salud térmica.
/// </summary>
public class HardwareLiveSnapshot
{
    /// <summary>
    /// Fecha/hora de captura.
    /// </summary>
    public DateTime CapturedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Si el proveedor de hardware pudo inicializarse correctamente.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Si el proceso se ejecuta con permisos elevados.
    /// </summary>
    public bool IsElevated { get; set; }

    /// <summary>
    /// Sensores de temperatura capturados.
    /// </summary>
    public List<HardwareTemperatureSensor> TemperatureSensors { get; set; } = new();

    /// <summary>
    /// Sensores de rendimiento (Load/Clock de CPU/GPU) capturados.
    /// </summary>
    public List<HardwarePerformanceSensor> PerformanceSensors { get; set; } = new();

    /// <summary>
    /// Sensores de memoria de GPU (SmallData) capturados.
    /// </summary>
    public List<HardwareGpuMemorySensor> GpuMemorySensors { get; set; } = new();

    /// <summary>
    /// Sensores de telemetría de batería (no térmicos) capturados.
    /// </summary>
    public List<HardwareBatterySensor> BatterySensors { get; set; } = new();

    /// <summary>
    /// Timings SPD de memoria (SensorType.Timing) capturados.
    /// </summary>
    public List<HardwareMemoryTimingSensor> MemoryTimingSensors { get; set; } = new();

    /// <summary>
    /// Advertencias controladas.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Errores de lectura (sin propagar excepciones a la UI).
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Si se capturó al menos un sensor de temperatura.
    /// </summary>
    public bool HasTemperatureSensors => TemperatureSensors.Count > 0;

    /// <summary>
    /// Si se capturó al menos un sensor de rendimiento.
    /// </summary>
    public bool HasPerformanceSensors => PerformanceSensors.Count > 0;

    /// <summary>
    /// Si se capturó al menos un sensor de memoria de GPU.
    /// </summary>
    public bool HasGpuMemorySensors => GpuMemorySensors.Count > 0;

    /// <summary>
    /// Si se capturó al menos un sensor de batería.
    /// </summary>
    public bool HasBatterySensors => BatterySensors.Count > 0;

    /// <summary>
    /// Si se capturó al menos un timing SPD de memoria.
    /// </summary>
    public bool HasMemoryTimingSensors => MemoryTimingSensors.Count > 0;

    /// <summary>
    /// Sensores de temperatura con valor válido (no null).
    /// </summary>
    public int ValidTemperatureSensorCount => TemperatureSensors.Count(s => s.ValueCelsius.HasValue);

    /// <summary>
    /// Sensores de rendimiento con valor válido (no null).
    /// </summary>
    public int ValidPerformanceSensorCount => PerformanceSensors.Count(s => s.Value.HasValue);

    /// <summary>
    /// Sensores de memoria de GPU con valor válido (no null).
    /// </summary>
    public int ValidGpuMemorySensorCount => GpuMemorySensors.Count(s => s.ValueMB.HasValue);

    /// <summary>
    /// Sensores de batería con valor válido (no null).
    /// </summary>
    public int ValidBatterySensorCount => BatterySensors.Count(s => s.Value.HasValue);

    /// <summary>
    /// Timings SPD con valor válido (no null).
    /// </summary>
    public int ValidMemoryTimingSensorCount => MemoryTimingSensors.Count(s => s.ValueNanoseconds.HasValue);
}
