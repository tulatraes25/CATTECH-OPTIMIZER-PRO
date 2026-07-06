using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para persistir y cargar reportes de servicio.
/// </summary>
public interface IServiceReportService
{
    /// <summary>
    /// Guarda un reporte de servicio en disco.
    /// </summary>
    Task<string> SaveReportAsync(ServiceReport report);

    /// <summary>
    /// Carga un reporte de servicio por su ID.
    /// </summary>
    Task<ServiceReport?> LoadReportAsync(string reportId);

    /// <summary>
    /// Lista todos los reportes de servicio (más recientes primero).
    /// </summary>
    Task<List<ServiceReportSummary>> ListReportsAsync(int maxResults = 50);

    /// <summary>
    /// Elimina un reporte de servicio por su ID.
    /// </summary>
    Task<bool> DeleteReportAsync(string reportId);
}

/// <summary>
/// Resumen de un reporte para listados.
/// </summary>
public class ServiceReportSummary
{
    public string Id { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string EquipmentBrand { get; set; } = string.Empty;
    public string EquipmentModel { get; set; } = string.Empty;
    public string ServiceReason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string FileName { get; set; } = string.Empty;
}
