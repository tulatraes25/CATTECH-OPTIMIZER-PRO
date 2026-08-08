using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para ejecutar self-tests SMART de forma segura.
/// El test ocurre internamente en el firmware del disco.
/// CATTECH solo inicia, consulta estado y lee resultados.
/// </summary>
public interface ISmartTestService
{
    /// <summary>
    /// Inicia un test SMART corto sobre un disco.
    /// No espera a que termine: retorna inmediatamente con el estado "InProgress" o fallo.
    /// </summary>
    Task<SmartTestSession> StartShortTestAsync(SmartDiskDevice device);

    /// <summary>
    /// Consulta el estado actual de una sesión de test.
    /// </summary>
    Task<SmartTestSession> CheckStatusAsync(SmartTestSession session);

    /// <summary>
    /// Obtiene el último resultado de test disponible para un disco.
    /// </summary>
    Task<SmartTestResult?> GetLatestResultAsync(SmartDiskDevice device);

    /// <summary>
    /// Guarda una sesión de test en disco.
    /// </summary>
    Task<string> SaveSessionAsync(SmartTestSession session);

    /// <summary>
    /// Lista las sesiones de test guardadas.
    /// </summary>
    Task<IReadOnlyList<SmartTestSession>> ListSessionsAsync(int maxResults = 20);
}
