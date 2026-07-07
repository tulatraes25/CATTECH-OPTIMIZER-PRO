using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para generar informes técnicos HTML.
/// </summary>
public interface IReportGenerationService
{
    /// <summary>
    /// Genera un informe HTML con las opciones especificadas.
    /// </summary>
    Task<string> GenerateHtmlReportAsync(ReportGenerationOptions options);

    /// <summary>
    /// Guarda la información del informe generado.
    /// </summary>
    Task SaveReportInfoAsync(GeneratedReportInfo info);

    /// <summary>
    /// Lista los informes generados.
    /// </summary>
    Task<List<GeneratedReportInfo>> ListGeneratedReportsAsync(int maxResults = 20);

    /// <summary>
    /// Abre un archivo HTML en el navegador predeterminado.
    /// </summary>
    Task OpenReportAsync(string htmlPath);

    /// <summary>
    /// Abre la carpeta de informes en el explorador.
    /// </summary>
    Task OpenReportsFolderAsync();
}
