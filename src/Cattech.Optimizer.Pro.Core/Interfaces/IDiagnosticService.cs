using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para ejecutar diagnósticos no invasivos del sistema.
/// </summary>
public interface IDiagnosticService
{
    /// <summary>
    /// Ejecuta un diagnóstico rápido completo del sistema.
    /// No modifica nada en el sistema.
    /// </summary>
    Task<DiagnosticReport> RunQuickDiagnosticAsync(IProgress<int>? progress = null);

    /// <summary>
    /// Guarda un diagnóstico en disco.
    /// </summary>
    Task<string> SaveDiagnosticAsync(DiagnosticReport report);

    /// <summary>
    /// Carga un diagnóstico por su ID.
    /// </summary>
    Task<DiagnosticReport?> LoadDiagnosticAsync(string diagnosticId);

    /// <summary>
    /// Lista los diagnósticos guardados (más recientes primero).
    /// </summary>
    Task<List<DiagnosticSummary>> ListDiagnosticsAsync(int maxResults = 20);

    /// <summary>
    /// Elimina un diagnóstico por su ID.
    /// </summary>
    Task<bool> DeleteDiagnosticAsync(string diagnosticId);
}

/// <summary>
/// Resumen de un diagnóstico para listados.
/// </summary>
public class DiagnosticSummary
{
    public string Id { get; set; } = string.Empty;
    public DateTime DiagnosisDate { get; set; }
    public string ComputerName { get; set; } = string.Empty;
    public string OsName { get; set; } = string.Empty;
    public int AlertCount { get; set; }
    public int CriticalAlertCount { get; set; }
    public string FileName { get; set; } = string.Empty;
}
