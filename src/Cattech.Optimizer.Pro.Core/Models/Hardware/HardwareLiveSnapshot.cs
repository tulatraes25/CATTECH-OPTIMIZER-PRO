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
/// Captura única de la sesión de hardware: temperaturas + métricas de rendimiento
/// de un MISMO Refresh (coherentes temporalmente).
/// Recolecta datos: no interpreta rendimiento ni salud térmica.
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
    /// Sensores de temperatura con valor válido (no null).
    /// </summary>
    public int ValidTemperatureSensorCount => TemperatureSensors.Count(s => s.ValueCelsius.HasValue);

    /// <summary>
    /// Sensores de rendimiento con valor válido (no null).
    /// </summary>
    public int ValidPerformanceSensorCount => PerformanceSensors.Count(s => s.Value.HasValue);
}
