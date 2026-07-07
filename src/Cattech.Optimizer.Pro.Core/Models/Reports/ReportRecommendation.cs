namespace Cattech.Optimizer.Pro.Core.Models.Reports;

/// <summary>
/// Recomendación automática generada para el informe.
/// </summary>
public class ReportRecommendation
{
    /// <summary>
    /// Categoría de la recomendación.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Mensaje de la recomendación.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Severidad: Info, Warning, Critical.
    /// </summary>
    public string Severity { get; set; } = "Info";

    /// <summary>
    /// Icono para mostrar.
    /// </summary>
    public string Icon { get; set; } = "ℹ️";
}
