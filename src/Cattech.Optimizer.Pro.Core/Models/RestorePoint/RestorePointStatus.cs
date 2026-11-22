namespace Cattech.Optimizer.Pro.Core.Models.RestorePoint;

/// <summary>
/// Estado (advisory) de la protección del sistema.
/// No es una puerta de bloqueo: Windows es la única fuente de verdad y el resultado
/// real se obtiene al ejecutar Checkpoint-Computer.
/// </summary>
public enum RestoreProtectionState
{
    /// <summary>No se pudo determinar el estado de la protección.</summary>
    Unknown = 0,

    /// <summary>La protección parece habilitada (por ejemplo, ya existen puntos de restauración).</summary>
    LikelyEnabled = 1,

    /// <summary>La protección parece deshabilitada (política DisableSR/DisableConfig).</summary>
    LikelyDisabled = 2
}

/// <summary>
/// Disponibilidad detectada del sistema de restauración (servicios VSS/swprv y clase WMI SystemRestore).
/// Reemplaza la sonda obsoleta del servicio "srservice" de Windows XP, que no existe en Windows 10/11.
/// </summary>
public enum RestoreAvailabilityState
{
    /// <summary>No se pudo determinar la disponibilidad.</summary>
    Unknown = 0,

    /// <summary>Se detectaron VSS/swprv o la clase WMI SystemRestore.</summary>
    LikelyAvailable = 1,

    /// <summary>No se detectó ningún componente del sistema de restauración.</summary>
    NotAvailable = 2
}

/// <summary>
/// Estado actual del sistema de puntos de restauración.
/// </summary>
public class RestorePointStatus
{
    /// <summary>
    /// Si la aplicación está ejecutando como administrador.
    /// </summary>
    public bool IsAdministrator { get; set; }

    /// <summary>
    /// Si el servicio de Restaurar sistema está disponible.
    /// </summary>
    public bool IsSystemRestoreAvailable { get; set; }

    /// <summary>
    /// Si la protección del sistema está habilitada.
    /// Valor informativo (advisory); no debe usarse para bloquear la creación.
    /// </summary>
    public bool IsProtectionEnabled { get; set; }

    /// <summary>
    /// Disponibilidad detectada del sistema de restauración (tri-estado).
    /// </summary>
    public RestoreAvailabilityState AvailabilityState { get; set; } = RestoreAvailabilityState.Unknown;

    /// <summary>
    /// Estado probable de la protección del sistema (tri-estado, informativo).
    /// </summary>
    public RestoreProtectionState ProtectionState { get; set; } = RestoreProtectionState.Unknown;

    /// <summary>
    /// Mensaje de estado descriptivo.
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Advertencias encontradas.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Fecha/hora de la última verificación.
    /// </summary>
    public DateTime LastCheckedAt { get; set; }

    /// <summary>
    /// Si se puede proceder a crear un punto.
    /// Condición conservadora histórica: requiere las tres señales en true.
    /// La UI no debe usar esta propiedad para bloquear; usar <see cref="CanAttemptCreate"/>.
    /// </summary>
    public bool CanCreatePoint => IsAdministrator && IsSystemRestoreAvailable && IsProtectionEnabled;

    /// <summary>
    /// Si se puede intentar la creación del punto.
    /// Solo bloquea cuando falta el requisito real de elevación o cuando se comprobó
    /// que el sistema de restauración no existe. La protección del sistema es advisory:
    /// el resultado real lo informa Windows al ejecutar Checkpoint-Computer.
    /// </summary>
    public bool CanAttemptCreate =>
        IsAdministrator && AvailabilityState != RestoreAvailabilityState.NotAvailable;

    /// <summary>
    /// Razón por la que no se puede crear (si aplica). Condición conservadora histórica.
    /// </summary>
    public string CannotCreateReason
    {
        get
        {
            if (!IsAdministrator)
                return "Se requieren permisos de administrador";
            if (!IsSystemRestoreAvailable)
                return "El servicio de Restaurar sistema no está disponible";
            if (!IsProtectionEnabled)
                return "La protección del sistema está deshabilitada";
            return string.Empty;
        }
    }

    /// <summary>
    /// Razón por la que no se puede ni intentar la creación (si aplica).
    /// </summary>
    public string AttemptBlockedReason
    {
        get
        {
            if (!IsAdministrator)
                return "Se requieren permisos de administrador para crear puntos de restauración";
            if (AvailabilityState == RestoreAvailabilityState.NotAvailable)
                return "No se detectó el sistema de restauración (servicios VSS/swprv ni clase WMI SystemRestore)";
            return string.Empty;
        }
    }
}

/// <summary>
/// Método usado para crear el punto de restauración.
/// </summary>
public enum RestorePointMethod
{
    Unknown,
    PowerShellCheckpoint,
    WmiSystemRestore,
    PowerShellDisable,
    ManualRecommendation
}

/// <summary>
/// Códigos de error usados por <see cref="RestorePointResult.ErrorCode"/>.
/// </summary>
public static class RestorePointErrorCodes
{
    /// <summary>Windows denegó el acceso (falta elevación).</summary>
    public const string AccessDenied = "ACCESS_DENIED";

    /// <summary>Windows informó que la protección del sistema está deshabilitada.</summary>
    public const string ProtectionDisabled = "PROTECTION_DISABLED";

    /// <summary>Windows aplicó el límite de frecuencia (por ejemplo, un punto cada 24 horas).</summary>
    public const string FrequencyLimit = "FREQUENCY_LIMIT";

    /// <summary>El servicio de instantáneas (VSS/swprv) no está disponible o está deshabilitado.</summary>
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";

    /// <summary>PowerShell devolvió un error no clasificado.</summary>
    public const string PowerShellError = "POWERSHELL_ERROR";

    /// <summary>No se pudo ejecutar PowerShell.</summary>
    public const string PowerShellException = "POWERSHELL_EXCEPTION";

    /// <summary>La creación superó el tiempo máximo de espera.</summary>
    public const string Timeout = "TIMEOUT";

    /// <summary>El nombre del punto de restauración no es válido.</summary>
    public const string InvalidName = "INVALID_NAME";

    /// <summary>Error inesperado no clasificado.</summary>
    public const string UnexpectedError = "UNEXPECTED_ERROR";
}

/// <summary>
/// Resultado de la creación de un punto de restauración.
/// Se persiste en data/restore-points/restore-point-result-YYYYMMDD-HHMMSS.json
/// </summary>
public class RestorePointResult
{
    /// <summary>
    /// ID único del resultado.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>
    /// Fecha/hora de la solicitud.
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// Fecha/hora de finalización.
    /// </summary>
    public DateTime FinishedAt { get; set; }

    /// <summary>
    /// Nombre del punto de restauración.
    /// </summary>
    public string RestorePointName { get; set; } = string.Empty;

    /// <summary>
    /// Si la creación fue exitosa.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Si se pudo verificar que el punto existe después de crearlo.
    /// </summary>
    public bool Verified { get; set; }

    /// <summary>
    /// Mensaje de error si falló.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Código de error (si aplica).
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Advertencias no bloqueantes asociadas al resultado.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Si requería permisos de administrador.
    /// </summary>
    public bool RequiresAdministrator { get; set; }

    /// <summary>
    /// Si la protección del sistema estaba habilitada.
    /// </summary>
    public bool ProtectionEnabled { get; set; }

    /// <summary>
    /// Método usado para crear el punto.
    /// </summary>
    public RestorePointMethod MethodUsed { get; set; }

    /// <summary>
    /// Notas adicionales.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del técnico que solicitó.
    /// </summary>
    public string TechnicianName { get; set; } = string.Empty;

    /// <summary>
    /// Salida estándar del proceso (si se usó PowerShell).
    /// </summary>
    public string StandardOutput { get; set; } = string.Empty;

    /// <summary>
    /// Salida de error del proceso (si se usó PowerShell).
    /// </summary>
    public string StandardError { get; set; } = string.Empty;
}
