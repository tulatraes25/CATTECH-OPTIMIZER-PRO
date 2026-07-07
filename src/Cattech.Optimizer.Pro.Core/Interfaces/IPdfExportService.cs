namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Información sobre el exportador PDF disponible.
/// </summary>
public class PdfExporterInfo
{
    /// <summary>
    /// Nombre del exportador.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Si el exportador está disponible en esta máquina.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Mensaje de estado.
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Versión del exportador (si aplica).
    /// </summary>
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Interfaz para exportar informes HTML a PDF.
/// </summary>
public interface IPdfExportService
{
    /// <summary>
    /// Verifica si la exportación a PDF está disponible.
    /// </summary>
    Task<PdfExporterInfo> CanExportAsync();

    /// <summary>
    /// Exporta un archivo HTML a PDF.
    /// </summary>
    /// <param name="htmlPath">Ruta del archivo HTML.</param>
    /// <param name="outputPdfPath">Ruta de salida del PDF.</param>
    Task<bool> ExportHtmlToPdfAsync(string htmlPath, string outputPdfPath);

    /// <summary>
    /// Genera la ruta de salida para un PDF basado en el HTML.
    /// </summary>
    string GetPdfOutputPath(string htmlPath);

    /// <summary>
    /// Abre un archivo PDF en el visor predeterminado.
    /// </summary>
    Task OpenPdfAsync(string pdfPath);
}
