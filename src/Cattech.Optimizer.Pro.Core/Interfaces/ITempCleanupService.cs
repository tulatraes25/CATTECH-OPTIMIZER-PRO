using Cattech.Optimizer.Pro.Core.Models.Cleanup;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para escanear y limpiar archivos temporales de forma segura.
/// </summary>
public interface ITempCleanupService
{
    /// <summary>
    /// Escanea las ubicaciones de temporales y retorna targets con tamaño estimado.
    /// No borra nada.
    /// </summary>
    Task<List<TempCleanupTarget>> ScanAsync(IProgress<int>? progress = null);

    /// <summary>
    /// Limpia las ubicaciones seleccionadas.
    /// Crea backup/registro de cada acción.
    /// </summary>
    Task<TempCleanupResult> CleanupAsync(IEnumerable<TempCleanupTarget> targets, IProgress<int>? progress = null);

    /// <summary>
    /// Guarda un resultado de limpieza en disco.
    /// </summary>
    Task<string> SaveResultAsync(TempCleanupResult result);

    /// <summary>
    /// Lista los resultados de limpieza guardados.
    /// </summary>
    Task<List<TempCleanupResultSummary>> ListResultsAsync(int maxResults = 20);
}

/// <summary>
/// Resumen de un resultado de limpieza para listados.
/// </summary>
public class TempCleanupResultSummary
{
    public string Id { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public double DeletedMB { get; set; }
    public int DeletedFiles { get; set; }
    public bool HasWarnings { get; set; }
    public string FileName { get; set; } = string.Empty;
}
