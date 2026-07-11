using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para analizar discos SMART de forma no invasiva.
/// Solo lectura: no ejecuta tests SMART ni modifica discos.
/// </summary>
public interface ISmartDiskService
{
    /// <summary>
    /// Analiza todos los discos detectados por smartctl.
    /// </summary>
    Task<SmartAnalysisResult> AnalyzeAllDisksAsync();

    /// <summary>
    /// Analiza un disco individual.
    /// </summary>
    Task<SmartDiskReport> AnalyzeDiskAsync(SmartDiskDevice device);

    /// <summary>
    /// Guarda un resultado de análisis en disco.
    /// </summary>
    Task<string> SaveResultAsync(SmartAnalysisResult result);

    /// <summary>
    /// Lista los resultados de análisis guardados.
    /// </summary>
    Task<IReadOnlyList<SmartAnalysisResult>> ListResultsAsync(int maxResults = 20);
}
