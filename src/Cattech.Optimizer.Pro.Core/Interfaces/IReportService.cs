using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para generar reportes e informes.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Genera un informe en formato HTML.
    /// </summary>
    /// <param name="report">Datos del reporte.</param>
    /// <param name="settings">Configuración de empresa/técnico.</param>
    /// <returns>Ruta del archivo HTML generado.</returns>
    Task<string> GenerateHtmlReportAsync(ServiceReport report, AppSettings settings);

    /// <summary>
    /// Genera un informe en formato PDF.
    /// </summary>
    /// <param name="report">Datos del reporte.</param>
    /// <param name="settings">Configuración de empresa/técnico.</param>
    /// <returns>Ruta del archivo PDF generado.</returns>
    Task<string> GeneratePdfReportAsync(ServiceReport report, AppSettings settings);

    /// <summary>
    /// Abre un archivo en el navegador predeterminado.
    /// </summary>
    Task OpenInBrowserAsync(string filePath);
}
