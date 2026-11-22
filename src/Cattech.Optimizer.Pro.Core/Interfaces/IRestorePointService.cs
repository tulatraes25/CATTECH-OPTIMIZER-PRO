using Cattech.Optimizer.Pro.Core.Models.RestorePoint;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para crear y gestionar puntos de restauración de Windows.
/// </summary>
public interface IRestorePointService
{
    /// <summary>
    /// Verifica el estado actual del sistema de puntos de restauración.
    /// Detecta permisos y disponibilidad real (VSS/swprv/WMI) e informa el estado probable
    /// de la protección del sistema como dato advisory: no se usa para bloquear la creación.
    /// </summary>
    Task<RestorePointStatus> CheckStatusAsync();

    /// <summary>
    /// Crea un punto de restauración con el nombre especificado ejecutando Checkpoint-Computer.
    /// Requiere confirmación previa del técnico. El resultado refleja la respuesta real de Windows:
    /// si falla, <see cref="RestorePointResult.ErrorCode"/> y
    /// <see cref="RestorePointResult.ErrorMessage"/> describen el error devuelto por el sistema.
    /// </summary>
    Task<RestorePointResult> CreateRestorePointAsync(string name);

    /// <summary>
    /// Genera el nombre estándar para un punto de restauración.
    /// </summary>
    string GenerateRestorePointName();

    /// <summary>
    /// Guarda un resultado de creación en disco.
    /// </summary>
    Task<string> SaveResultAsync(RestorePointResult result);

    /// <summary>
    /// Lista los resultados de creación guardados.
    /// </summary>
    Task<List<RestorePointResultSummary>> ListResultsAsync(int maxResults = 20);
}

/// <summary>
/// Resumen de un resultado para listados.
/// </summary>
public class RestorePointResultSummary
{
    public string Id { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public string RestorePointName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
