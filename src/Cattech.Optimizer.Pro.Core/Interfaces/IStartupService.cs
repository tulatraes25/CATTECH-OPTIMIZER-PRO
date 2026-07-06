using Cattech.Optimizer.Pro.Core.Models.Startup;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para analizar programas de inicio de Windows.
/// Solo lectura: no modifica registro ni archivos.
/// </summary>
public interface IStartupService
{
    /// <summary>
    /// Analiza todas las fuentes de programas de inicio.
    /// </summary>
    Task<StartupAnalysis> AnalyzeStartupAsync();

    /// <summary>
    /// Guarda un análisis en disco.
    /// </summary>
    Task<string> SaveAnalysisAsync(StartupAnalysis analysis);

    /// <summary>
    /// Carga un análisis por su ID.
    /// </summary>
    Task<StartupAnalysis?> LoadAnalysisAsync(string analysisId);

    /// <summary>
    /// Lista los análisis guardados (más recientes primero).
    /// </summary>
    Task<List<StartupAnalysisSummary>> ListAnalysesAsync(int maxResults = 20);

    /// <summary>
    /// Elimina un análisis por su ID.
    /// </summary>
    Task<bool> DeleteAnalysisAsync(string analysisId);
}

/// <summary>
/// Resumen de un análisis para listados.
/// </summary>
public class StartupAnalysisSummary
{
    public string Id { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; }
    public int TotalEntries { get; set; }
    public int ThirdPartyCount { get; set; }
    public int AlertCount { get; set; }
    public string FileName { get; set; } = string.Empty;
}
