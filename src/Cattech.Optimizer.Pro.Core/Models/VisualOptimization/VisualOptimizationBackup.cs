namespace Cattech.Optimizer.Pro.Core.Models.VisualOptimization;

/// <summary>
/// Backup de un ajuste visual antes de modificarlo.
/// Se persiste en backups/visual/visual-backups.json
/// </summary>
public class VisualOptimizationBackup
{
    /// <summary>
    /// ID único del backup.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>
    /// Fecha/hora de creación del backup.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// ID del ajuste original.
    /// </summary>
    public string SettingId { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del ajuste.
    /// </summary>
    public string SettingName { get; set; } = string.Empty;

    /// <summary>
    /// Ruta del registro.
    /// </summary>
    public string RegistryPath { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del valor en el registro.
    /// </summary>
    public string RegistryValueName { get; set; } = string.Empty;

    /// <summary>
    /// Valor original antes del cambio.
    /// </summary>
    public string? OriginalValue { get; set; }

    /// <summary>
    /// Nuevo valor aplicado.
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Tipo de valor.
    /// </summary>
    public string ValueType { get; set; } = "DWORD";

    /// <summary>
    /// Si puede ser restaurado.
    /// </summary>
    public bool CanRestore { get; set; } = true;

    /// <summary>
    /// Fecha/hora de restauración.
    /// </summary>
    public DateTime? RestoredAt { get; set; }

    /// <summary>
    /// Técnico que realizó el cambio.
    /// </summary>
    public string AppliedBy { get; set; } = string.Empty;
}

/// <summary>
/// Resultado de una operación de optimización visual.
/// Se persiste en data/visual-optimization-results/
/// </summary>
public class VisualOptimizationResult
{
    /// <summary>
    /// ID único del resultado.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>
    /// Fecha/hora de inicio.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Fecha/hora de fin.
    /// </summary>
    public DateTime FinishedAt { get; set; }

    /// <summary>
    /// Duración total.
    /// </summary>
    public TimeSpan Duration => FinishedAt - StartedAt;

    /// <summary>
    /// Cantidad de ajustes aplicados.
    /// </summary>
    public int AppliedCount { get; set; }

    /// <summary>
    /// Cantidad de ajustes omitidos.
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// Cantidad de ajustes con error.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Si algún ajuste requiere reinicio.
    /// </summary>
    public bool RequiresRestart { get; set; }

    /// <summary>
    /// Si algún ajuste requiere cierre de sesión.
    /// </summary>
    public bool RequiresSignOut { get; set; }

    /// <summary>
    /// Errores encontrados.
    /// </summary>
    public List<VisualOptimizationError> Errors { get; set; } = new();

    /// <summary>
    /// Backups creados durante la operación.
    /// </summary>
    public List<VisualOptimizationBackup> Backups { get; set; } = new();

    /// <summary>
    /// Técnico que ejecutó la operación.
    /// </summary>
    public string TechnicianName { get; set; } = string.Empty;

    /// <summary>
    /// Si la operación fue exitosa.
    /// </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>
    /// Si hubo advertencias.
    /// </summary>
    public bool HasWarnings => SkippedCount > 0 || FailedCount > 0;
}

/// <summary>
/// Error ocurrido durante la optimización visual.
/// </summary>
public class VisualOptimizationError
{
    public string SettingId { get; set; } = string.Empty;
    public string SettingName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
}
