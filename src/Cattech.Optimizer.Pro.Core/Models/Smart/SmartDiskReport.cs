namespace Cattech.Optimizer.Pro.Core.Models.Smart;

/// <summary>
/// Estado de salud general de un disco.
/// </summary>
public enum SmartHealthStatus
{
    /// <summary>Disco en buen estado.</summary>
    Good,
    /// <summary>Advertencia: revisar pronto.</summary>
    Warning,
    /// <summary>Crítico: backup inmediato recomendado.</summary>
    Critical,
    /// <summary>SMART no disponible o no soportado.</summary>
    NotAvailable,
    /// <summary>Estado no determinado.</summary>
    Unknown
}

/// <summary>
/// Severidad de un atributo SMART individual.
/// </summary>
public enum SmartSeverity
{
    Info,
    Warning,
    Critical,
    Unknown
}

/// <summary>
/// Atributo SMART individual de un disco.
/// </summary>
public class SmartAttribute
{
    /// <summary>ID del atributo (ej: 5, 197, 198).</summary>
    public int Id { get; set; }

    /// <summary>Nombre del atributo (ej: Reallocated_Sector_Ct).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Valor bruto actual.</summary>
    public long RawValue { get; set; }

    /// <summary>Valor normalizado actual (0-253).</summary>
    public int Value { get; set; }

    /// <summary>Peor valor registrado.</summary>
    public int Worst { get; set; }

    /// <summary>Umbral de fallo.</summary>
    public int Threshold { get; set; }

    /// <summary>Cuándo falló (si aplica).</summary>
    public string WhenFailed { get; set; } = string.Empty;

    /// <summary>Flags del atributo.</summary>
    public string Flags { get; set; } = string.Empty;

    /// <summary>Severidad calculada.</summary>
    public SmartSeverity Severity { get; set; }

    /// <summary>Descripción legible del atributo.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Reporte SMART de un disco individual.
/// </summary>
public class SmartDiskReport
{
    /// <summary>ID único del reporte.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>Fecha/hora del análisis.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // --- Información del dispositivo ---

    /// <summary>Nombre del dispositivo (ej: /dev/sda).</summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>Nombre para mostrar.</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>Tipo de disco (HDD, SSD, NVMe, USB).</summary>
    public string DeviceType { get; set; } = string.Empty;

    /// <summary>Protocolo (SATA, NVMe, etc.).</summary>
    public string Protocol { get; set; } = string.Empty;

    /// <summary>Nombre del modelo.</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Número de serie.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>Versión de firmware.</summary>
    public string FirmwareVersion { get; set; } = string.Empty;

    /// <summary>Capacidad en bytes.</summary>
    public long CapacityBytes { get; set; }

    /// <summary>Capacidad en GB.</summary>
    public double CapacityGB => Math.Round((double)CapacityBytes / (1024 * 1024 * 1024), 2);

    // --- Datos SMART ---

    /// <summary>Temperatura actual en °C.</summary>
    public int TemperatureCelsius { get; set; }

    /// <summary>Horas encendido.</summary>
    public long PowerOnHours { get; set; }

    /// <summary>Ciclos de encendido/apagado.</summary>
    public long PowerCycleCount { get; set; }

    /// <summary>Si el self-assessment de salud pasó.</summary>
    public bool OverallHealthPassed { get; set; }

    /// <summary>Estado de salud general.</summary>
    public SmartHealthStatus HealthStatus { get; set; }

    /// <summary>Resumen de estado en texto legible.</summary>
    public string HealthSummary { get; set; } = string.Empty;

    /// <summary>Atributos SMART relevantes.</summary>
    public List<SmartAttribute> ImportantAttributes { get; set; } = new();

    /// <summary>Advertencias generadas.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Errores encontrados.</summary>
    public List<string> Errors { get; set; } = new();

    // --- Metadata ---

    /// <summary>Versión de smartctl usada.</summary>
    public string SmartctlVersion { get; set; } = string.Empty;

    /// <summary>Si se recomienda backup inmediato.</summary>
    public bool RequiresBackupRecommendation { get; set; }

    /// <summary>Si el análisis fue exitoso.</summary>
    public bool IsAnalysisSuccessful { get; set; }

    /// <summary>Mensaje de error si falló.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Resultado del análisis de todos los discos.
/// Se persiste en data/smart-reports/
/// </summary>
public class SmartAnalysisResult
{
    /// <summary>ID único del análisis.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>Inicio del análisis.</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Fin del análisis.</summary>
    public DateTime FinishedAt { get; set; }

    /// <summary>Si smartctl está disponible.</summary>
    public bool SmartctlAvailable { get; set; }

    /// <summary>Versión de smartctl.</summary>
    public string SmartctlVersion { get; set; } = string.Empty;

    /// <summary>Cantidad de discos analizados.</summary>
    public int DevicesAnalyzed { get; set; }

    /// <summary>Reportes por disco.</summary>
    public List<SmartDiskReport> Reports { get; set; } = new();

    /// <summary>Errores generales.</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Advertencias generales.</summary>
    public List<string> Warnings { get; set; } = new();
}
