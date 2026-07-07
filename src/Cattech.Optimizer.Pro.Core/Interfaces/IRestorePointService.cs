using Cattech.Optimizer.Pro.Core.Models.RestorePoint;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para crear y gestionar puntos de restauración de Windows.
/// </summary>
public interface IRestorePointService
{
    /// <summary>
    /// Verifica el estado actual del sistema de puntos de restauración.
    /// Detecta permisos, disponibilidad del servicio y estado de protección.
    /// </summary>
    Task<RestorePointStatus> CheckStatusAsync();

    /// <summary>
    /// Crea un punto de restauración con el nombre especificado.
    /// Requiere confirmación previa del técnico.
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
