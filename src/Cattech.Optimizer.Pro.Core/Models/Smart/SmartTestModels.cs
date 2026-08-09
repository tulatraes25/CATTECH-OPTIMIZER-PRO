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

    /// <summary>
    /// Tipo smartctl del dispositivo (ej: scsi, nvme, sat, sntjmicron).
    /// Es el argumento -d TYPE usado al iniciar y al consultar la sesión.
    /// Vacío en sesiones legacy → smartctl autodetecta.
    /// </summary>
    public string SmartctlDeviceType { get; set; } = string.Empty;

    /// <summary>Errores encontrados.</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Advertencias.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Si la última consulta de estado fue exitosa.</summary>
    public bool LastCheckSucceeded { get; set; }

    /// <summary>Error de la última consulta de estado (si falló).</summary>
    public string LastCheckError { get; set; } = string.Empty;
}

/// <summary>
/// Resultado de un test SMART.
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

/// <summary>
/// Resultado tipado del parseo de inicio de un self-test SMART.
/// Se usa en lugar de depender de texto localizado.
/// </summary>
public class SmartTestStartParseResult
{
    /// <summary>Si el test se inició correctamente.</summary>
    public bool Started { get; set; }

    /// <summary>Estado resultante del intento de inicio.</summary>
    public SmartTestStatus Status { get; set; } = SmartTestStatus.Unknown;

    /// <summary>Duración estimada en minutos (null si no se puede determinar).</summary>
    public int? EstimatedDurationMinutes { get; set; }

    /// <summary>Mensaje descriptivo.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Código de salida de smartctl (exit_status.value).</summary>
    public int? SmartctlExitStatus { get; set; }

    /// <summary>Errores detectados.</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Advertencias.</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Extensión para obtener mensaje legible de un estado de test SMART.
/// Ubicado en Core para que la UI no dependa de Infrastructure.
/// </summary>
public static class SmartTestStatusExtensions
{
    /// <summary>
    /// Convierte SmartTestStatus a mensaje legible en español.
    /// </summary>
    public static string ToDisplayMessage(this SmartTestStatus status) => status switch
    {
        SmartTestStatus.NotStarted => "Test no iniciado",
        SmartTestStatus.Starting => "Test iniciándose",
        SmartTestStatus.InProgress => "Test en ejecución",
        SmartTestStatus.CompletedWithoutError => "Prueba completada sin errores reportados.",
        SmartTestStatus.CompletedWithError => "La prueba detectó errores. Revisar SMART y realizar backup.",
        SmartTestStatus.Aborted => "Prueba abortada.",
        SmartTestStatus.Interrupted => "Prueba interrumpida.",
        SmartTestStatus.Unsupported => "El dispositivo no soporta esta prueba.",
        SmartTestStatus.FailedToStart => "No se pudo iniciar la prueba.",
        SmartTestStatus.Unknown => "No se pudo determinar el resultado.",
        _ => "Estado desconocido"
    };
}
