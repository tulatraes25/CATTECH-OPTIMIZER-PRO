using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Optional read-only report-store health capability. A snapshot is advisory;
/// clients must independently revalidate any subsequent action.
/// </summary>
public interface IServiceReportInventoryReader
{
    Task<ServiceReportInventory> InspectReportInventoryAsync();
}
