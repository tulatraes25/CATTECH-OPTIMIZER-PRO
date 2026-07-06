using Cattech.Optimizer.Pro.Core.Models.Startup;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para analizar y gestionar programas de inicio de Windows.
/// </summary>
public interface IStartupService
{
    // --- Análisis (solo lectura) ---

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

    // --- Desactivación (con backup y reversión) ---

    /// <summary>
    /// Verifica si una entrada puede ser desactivada.
    /// No permite desactivar entradas Microsoft ni fuentes no soportadas.
    /// </summary>
    bool CanDisableStartupEntry(StartupEntry entry);

    /// <summary>
    /// Desactiva las entradas seleccionadas, creando backup de cada una.
    /// No elimina entradas, solo las mueve a ubicación de backup.
    /// </summary>
    Task<StartupDisableSummary> DisableSelectedAsync(IEnumerable<StartupEntry> entries, string reason = "");

    /// <summary>
    /// Restaura una entrada desde su backup.
    /// </summary>
    Task<StartupActionResult> RestoreAsync(StartupBackupRecord backup);

    /// <summary>
    /// Lista los backups disponibles (más recientes primero).
    /// </summary>
    Task<List<StartupBackupRecord>> ListBackupsAsync();

    /// <summary>
    /// Carga un backup por su ID.
    /// </summary>
    Task<StartupBackupRecord?> LoadBackupAsync(string backupId);
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
