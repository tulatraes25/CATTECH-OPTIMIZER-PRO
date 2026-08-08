namespace Cattech.Optimizer.Pro.Core.Models.Smart;

/// <summary>
/// Tipo de test SMART.
/// </summary>
public enum SmartTestType
{
    Short,
    Extended
}

/// <summary>
/// Estado de una sesión de test SMART.
/// </summary>
public enum SmartTestStatus
{
    NotStarted,
    Starting,
    InProgress,
    CompletedWithoutError,
    CompletedWithError,
    Aborted,
    Interrupted,
    Unsupported,
    FailedToStart,
    Unknown
}

/// <summary>
/// Sesión de test SMART de un disco.
/// El test ocurre internamente en el firmware del disco; CATTECH solo consulta.
/// Se persiste en data/smart-tests/
/// </summary>
public class SmartTestSession
{
    /// <summary>ID único de la sesión.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>Nombre del dispositivo (ej: /dev/sda).</summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>Nombre del modelo.</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Número de serie.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>Tipo de test.</summary>
    public SmartTestType TestType { get; set; }

    /// <summary>Estado actual de la sesión.</summary>
    public SmartTestStatus Status { get; set; }

    /// <summary>Fecha/hora de la solicitud.</summary>
    public DateTime RequestedAt { get; set; } = DateTime.Now;

    /// <summary>Fecha/hora de inicio del test (si se conoce).</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>Duración estimada en minutos (null si no se puede determinar).</summary>
    public int? EstimatedDurationMinutes { get; set; }

    /// <summary>Fecha/hora estimada de finalización.</summary>
    public DateTime? EstimatedCompletionAt { get; set; }

    /// <summary>Fecha/hora de la última consulta de estado.</summary>
    public DateTime? LastCheckedAt { get; set; }

    /// <summary>Fecha/hora de finalización del test.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Progreso porcentual (null si no está disponible).</summary>
    public int? ProgressPercent { get; set; }

    /// <summary>Mensaje legible del resultado.</summary>
    public string ResultMessage { get; set; } = string.Empty;

    /// <summary>Código de salida de smartctl al iniciar.</summary>
    public int SmartctlExitCode { get; set; }

    /// <summary>Errores encontrados.</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Advertencias.</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Resultado final de un test SMART.
/// </summary>
public class SmartTestResult
{
    /// <summary>Nombre del dispositivo.</summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>Tipo de test.</summary>
    public SmartTestType TestType { get; set; }

    /// <summary>Estado final.</summary>
    public SmartTestStatus Status { get; set; }

    /// <summary>Fecha/hora de finalización.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Horas de vida del disco al momento del test.</summary>
    public long LifetimeHours { get; set; }

    /// <summary>LBA del primer error (null si no hay errores).</summary>
    public long? LbaOfFirstError { get; set; }

    /// <summary>Mensaje legible del resultado.</summary>
    public string ResultMessage { get; set; } = string.Empty;

    /// <summary>Estado crudo según smartctl (JSON crudo o texto).</summary>
    public string RawStatus { get; set; } = string.Empty;

    /// <summary>Errores encontrados.</summary>
    public List<string> Errors { get; set; } = new();
}
