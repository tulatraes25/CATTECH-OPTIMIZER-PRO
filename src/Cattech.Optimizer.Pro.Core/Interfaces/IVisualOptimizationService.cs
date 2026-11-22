using Cattech.Optimizer.Pro.Core.Models.VisualOptimization;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para analizar y aplicar optimizaciones visuales de forma segura.
/// </summary>
public interface IVisualOptimizationService
{
    /// <summary>
    /// Analiza el estado actual de los ajustes visuales.
    /// </summary>
    Task<List<VisualOptimizationSetting>> AnalyzeAsync();

    /// <summary>
    /// Aplica los ajustes seleccionados con backup previo.
    /// </summary>
    Task<VisualOptimizationResult> ApplyAsync(
        IEnumerable<VisualOptimizationSetting> settings,
        string reason = "");

    /// <summary>
    /// Lista los backups disponibles.
    /// </summary>
    Task<List<VisualOptimizationBackup>> ListBackupsAsync();

    /// <summary>
    /// Restaura un ajuste desde su backup.
    /// </summary>
    Task<bool> RestoreAsync(VisualOptimizationBackup backup);

    /// <summary>
    /// Restaura un ajuste desde su backup informando el resultado detallado
    /// (incluye el motivo real del fallo, no solo un bool).
    /// </summary>
    /// <remarks>
    /// Tiene implementación por defecto para no romper implementaciones
    /// existentes (por ejemplo dobles de prueba). La implementación real
    /// (<c>VisualOptimizationService</c>) la sobrescribe con diagnóstico completo.
    /// </remarks>
    async Task<VisualOptimizationRestoreResult> RestoreWithResultAsync(VisualOptimizationBackup backup)
    {
        ArgumentNullException.ThrowIfNull(backup);

        var success = await RestoreAsync(backup).ConfigureAwait(false);
        return success
            ? VisualOptimizationRestoreResult.Ok(backup)
            : VisualOptimizationRestoreResult.Fail(
                backup,
                "RestoreFailed",
                "No se pudo restaurar el ajuste desde el backup.");
    }

    /// <summary>
    /// Guarda un resultado de optimización.
    /// </summary>
    Task<string> SaveResultAsync(VisualOptimizationResult result);

    /// <summary>
    /// Lista los resultados de optimización persistidos, más recientes primero.
    /// </summary>
    /// <remarks>
    /// Tiene implementación por defecto (lista vacía) para no romper
    /// implementaciones o dobles de prueba existentes. La implementación real
    /// (<c>VisualOptimizationService</c>) la sobrescribe leyendo su carpeta de
    /// resultados y omitiendo archivos dañados o ilegibles.
    /// </remarks>
    Task<IReadOnlyList<VisualOptimizationResult>> ListResultsAsync(int maxResults = 20)
        => Task.FromResult<IReadOnlyList<VisualOptimizationResult>>(
            Array.Empty<VisualOptimizationResult>());

    /// <summary>
    /// Carga el último resultado persistido, o <c>null</c> si no hay registros.
    /// </summary>
    /// <remarks>
    /// Implementación por defecto sobre <see cref="ListResultsAsync"/>.
    /// </remarks>
    async Task<VisualOptimizationResult?> LoadLatestResultAsync()
    {
        var results = await ListResultsAsync(1).ConfigureAwait(false);
        return results.Count == 0 ? null : results[0];
    }
}
