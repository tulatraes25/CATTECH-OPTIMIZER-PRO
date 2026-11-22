namespace Cattech.Optimizer.Pro.Core.Models.VisualOptimization;

/// <summary>
/// Resultado detallado de restaurar un ajuste visual desde su backup.
/// A diferencia de un booleano mudo, conserva el motivo real del fallo para
/// que la UI pueda informarlo en español.
/// </summary>
public class VisualOptimizationRestoreResult
{
    /// <summary>
    /// Si la restauración se completó correctamente.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// ID del ajuste restaurado.
    /// </summary>
    public string SettingId { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del ajuste restaurado.
    /// </summary>
    public string SettingName { get; set; } = string.Empty;

    /// <summary>
    /// Mensaje listo para mostrar en la UI (éxito o motivo del fallo).
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Tipo/categoría del error cuando <see cref="Success"/> es false.
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// Crea un resultado exitoso para el backup indicado.
    /// </summary>
    public static VisualOptimizationRestoreResult Ok(VisualOptimizationBackup backup)
    {
        ArgumentNullException.ThrowIfNull(backup);

        return new VisualOptimizationRestoreResult
        {
            Success = true,
            SettingId = backup.SettingId,
            SettingName = backup.SettingName,
            Message = $"Ajuste '{backup.SettingName}' restaurado correctamente."
        };
    }

    /// <summary>
    /// Crea un resultado fallido conservando el motivo real.
    /// </summary>
    public static VisualOptimizationRestoreResult Fail(
        VisualOptimizationBackup? backup, string errorType, string message)
    {
        return new VisualOptimizationRestoreResult
        {
            Success = false,
            SettingId = backup?.SettingId ?? string.Empty,
            SettingName = backup?.SettingName ?? string.Empty,
            Message = message,
            ErrorType = errorType
        };
    }
}
