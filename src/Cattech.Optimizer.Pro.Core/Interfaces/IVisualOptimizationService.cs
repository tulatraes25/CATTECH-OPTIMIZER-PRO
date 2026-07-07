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
    /// Guarda un resultado de optimización.
    /// </summary>
    Task<string> SaveResultAsync(VisualOptimizationResult result);
}
