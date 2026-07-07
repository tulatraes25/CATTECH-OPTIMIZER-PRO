namespace Cattech.Optimizer.Pro.Core.Models.RestorePoint;

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
    /// </summary>
    public bool IsProtectionEnabled { get; set; }

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
    /// </summary>
    public bool CanCreatePoint => IsAdministrator && IsSystemRestoreAvailable && IsProtectionEnabled;

    /// <summary>
    /// Razón por la que no se puede crear (si aplica).
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
    /// Mensaje de error si falló.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Código de error (si aplica).
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

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
