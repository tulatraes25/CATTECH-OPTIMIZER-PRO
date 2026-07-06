namespace Cattech.Optimizer.Pro.Core.Models.Startup;

/// <summary>
/// Resultado de una acción de desactivación/reversión.
/// </summary>
public enum StartupActionResult
{
    Success,
    Failed,
    SkippedMicrosoft,
    SkippedUnsupportedSource,
    AlreadyDisabled,
    BackupFailed,
    RestoreFailed,
    NotFound
}

/// <summary>
/// Registro de backup de una entrada de inicio desactivada.
/// Se persiste en backups/startup/startup-backups.json
/// </summary>
public class StartupBackupRecord
{
    /// <summary>
    /// ID único del registro de backup.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>
    /// ID de la entrada de origen.
    /// </summary>
    public string EntryId { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de la entrada.
    /// </summary>
    public string EntryName { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de origen de la entrada.
    /// </summary>
    public StartupSourceType SourceType { get; set; }

    /// <summary>
    /// Ubicación original de la entrada.
    /// </summary>
    public string OriginalLocation { get; set; } = string.Empty;

    /// <summary>
    /// Ubicación del backup (clave del registro o ruta de archivo).
    /// </summary>
    public string BackupLocation { get; set; } = string.Empty;

    /// <summary>
    /// Valor original del registro (si aplica).
    /// </summary>
    public string? OriginalValue { get; set; }

    /// <summary>
    /// Valor guardado en backup (si aplica).
    /// </summary>
    public string? BackupValue { get; set; }

    /// <summary>
    /// Comando/ruta original.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Editor detectado.
    /// </summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>
    /// Si era de Microsoft.
    /// </summary>
    public bool WasMicrosoft { get; set; }

    /// <summary>
    /// Fecha de creación del backup (desactivación).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Técnico que realizó la desactivación.
    /// </summary>
    public string DisabledBy { get; set; } = string.Empty;

    /// <summary>
    /// Motivo de la desactivación.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Si la entrada puede ser restaurada.
    /// </summary>
    public bool CanRestore { get; set; } = true;

    /// <summary>
    /// Fecha de restauración (si fue restaurada).
    /// </summary>
    public DateTime? RestoredAt { get; set; }

    /// <summary>
    /// Técnico que restauró la entrada.
    /// </summary>
    public string? RestoredBy { get; set; }

    /// <summary>
    /// Notas adicionales.
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Resultado parcial de una operación de desactivación.
/// </summary>
public class StartupDisableResult
{
    /// <summary>
    /// Entrada que se intentó desactivar.
    /// </summary>
    public string EntryName { get; set; } = string.Empty;

    /// <summary>
    /// ID de la entrada.
    /// </summary>
    public string EntryId { get; set; } = string.Empty;

    /// <summary>
    /// Resultado de la operación.
    /// </summary>
    public StartupActionResult Result { get; set; }

    /// <summary>
    /// Mensaje descriptivo del resultado.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// ID del backup creado (si fue exitoso).
    /// </summary>
    public string? BackupId { get; set; }
}

/// <summary>
/// Resumen de una operación de desactivación múltiple.
/// </summary>
public class StartupDisableSummary
{
    public int TotalAttempted { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedMicrosoftCount { get; set; }
    public int SkippedUnsupportedCount { get; set; }
    public List<StartupDisableResult> Results { get; set; } = new();
}
