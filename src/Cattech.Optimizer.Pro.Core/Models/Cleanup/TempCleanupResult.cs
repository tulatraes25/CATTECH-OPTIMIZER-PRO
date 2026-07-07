namespace Cattech.Optimizer.Pro.Core.Models.Cleanup;

/// <summary>
/// Resultado de una operación de limpieza de temporales.
/// Se persiste en data/cleanup-results/cleanup-result-YYYYMMDD-HHMMSS.json
/// </summary>
public class TempCleanupResult
{
    /// <summary>
    /// ID único del resultado.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>
    /// Fecha/hora de inicio de la limpieza.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Fecha/hora de fin de la limpieza.
    /// </summary>
    public DateTime FinishedAt { get; set; }

    /// <summary>
    /// Duración total.
    /// </summary>
    public TimeSpan Duration => FinishedAt - StartedAt;

    /// <summary>
    /// Tamaño total seleccionado para limpiar (bytes).
    /// </summary>
    public long TotalSelectedBytes { get; set; }

    /// <summary>
    /// Tamaño total seleccionado en MB.
    /// </summary>
    public double TotalSelectedMB => Math.Round((double)TotalSelectedBytes / (1024 * 1024), 2);

    /// <summary>
    /// Tamaño total efectivamente liberado (bytes).
    /// </summary>
    public long DeletedBytes { get; set; }

    /// <summary>
    /// Tamaño liberado en MB.
    /// </summary>
    public double DeletedMB => Math.Round((double)DeletedBytes / (1024 * 1024), 2);

    /// <summary>
    /// Cantidad de archivos eliminados.
    /// </summary>
    public int DeletedFiles { get; set; }

    /// <summary>
    /// Cantidad de archivos omitidos (bloqueados, en uso).
    /// </summary>
    public int SkippedFiles { get; set; }

    /// <summary>
    /// Cantidad de archivos con error.
    /// </summary>
    public int FailedFiles { get; set; }

    /// <summary>
    /// Errores encontrados durante la limpieza.
    /// </summary>
    public List<CleanupError> Errors { get; set; } = new();

    /// <summary>
    /// Detalle por ubicación limpiada.
    /// </summary>
    public List<TargetResult> TargetResults { get; set; } = new();

    /// <summary>
    /// Nombre del técnico que ejecutó la limpieza.
    /// </summary>
    public string TechnicianName { get; set; } = string.Empty;

    /// <summary>
    /// Si la limpieza fue exitosa (sin errores críticos).
    /// </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>
    /// Si hubo advertencias (archivos omitidos pero limpieza completada).
    /// </summary>
    public bool HasWarnings => SkippedFiles > 0 || FailedFiles > 0;
}

/// <summary>
/// Resultado de limpieza para una ubicación específica.
/// </summary>
public class TargetResult
{
    /// <summary>
    /// ID del target.
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del target.
    /// </summary>
    public string TargetName { get; set; } = string.Empty;

    /// <summary>
    /// Ruta limpiada.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Archivos eliminados en esta ubicación.
    /// </summary>
    public int DeletedFiles { get; set; }

    /// <summary>
    /// Tamaño liberado en bytes.
    /// </summary>
    public long DeletedBytes { get; set; }

    /// <summary>
    /// Archivos omitidos.
    /// </summary>
    public int SkippedFiles { get; set; }

    /// <summary>
    /// Errores en esta ubicación.
    /// </summary>
    public int FailedFiles { get; set; }

    /// <summary>
    /// Si la limpieza de esta ubicación fue exitosa.
    /// </summary>
    public bool IsSuccess { get; set; }
}

/// <summary>
/// Error ocurrido durante la limpieza.
/// </summary>
public class CleanupError
{
    /// <summary>
    /// Ruta del archivo que causó el error.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Mensaje de error.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de error (Locked, AccessDenied, NotFound, etc.).
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;
}
