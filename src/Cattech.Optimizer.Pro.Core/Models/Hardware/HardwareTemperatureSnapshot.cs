namespace Cattech.Optimizer.Pro.Core.Models.Hardware;

/// <summary>
/// Sensor de temperatura individual capturado del hardware.
/// Modelo CATTECH: no depende de LibreHardwareMonitorLib.
/// </summary>
public class HardwareTemperatureSensor
{
    /// <summary>
    /// Nombre del hardware padre (ej: "Intel Core i7-13700K").
    /// </summary>
    public string HardwareName { get; set; } = string.Empty;

    /// <summary>
    /// Tipo del hardware padre como valor CATTECH (ej: "CPU", "GPU", "Memoria", "Placa Madre", "Almacenamiento", "Controlador").
    /// </summary>
    public string HardwareType { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del sensor (ej: "Core Max", "Package").
    /// </summary>
    public string SensorName { get; set; } = string.Empty;

    /// <summary>
    /// Identificador estable del sensor (derivado del Identifier expuesto por el proveedor).
    /// </summary>
    public string SensorIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Temperatura actual en °C. Null si el sensor no informó valor.
    /// Nunca se convierte null en 0 °C.
    /// </summary>
    public double? ValueCelsius { get; set; }

    /// <summary>
    /// Temperatura mínima observada en °C. Null si no está disponible.
    /// </summary>
    public double? MinCelsius { get; set; }

    /// <summary>
    /// Temperatura máxima observada en °C. Null si no está disponible.
    /// </summary>
    public double? MaxCelsius { get; set; }
}

/// <summary>
/// Snapshot de sensores de temperatura del sistema.
/// Recolecta datos: no interpreta salud térmica.
/// </summary>
public class HardwareTemperatureSnapshot
{
    /// <summary>
    /// Fecha/hora de captura.
    /// </summary>
    public DateTime CapturedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Si el proveedor de hardware pudo inicializarse correctamente.
    /// True aunque no se encuentren sensores de temperatura.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Si el proceso se ejecuta con permisos elevados.
    /// La falta de elevación no es un error fatal.
    /// </summary>
    public bool IsElevated { get; set; }

    /// <summary>
    /// Sensores de temperatura capturados (deduplicados por SensorIdentifier).
    /// </summary>
    public List<HardwareTemperatureSensor> Sensors { get; set; } = new();

    /// <summary>
    /// Advertencias controladas (permisos, sin sensores, etc.).
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Errores de lectura (sin propagar excepciones a la UI).
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Si se capturó al menos un sensor de temperatura.
    /// </summary>
    public bool HasSensors => Sensors.Count > 0;

    /// <summary>
    /// Cantidad de sensores con valor de temperatura válido (no null).
    /// </summary>
    public int ValidSensorCount => Sensors.Count(s => s.ValueCelsius.HasValue);
}
