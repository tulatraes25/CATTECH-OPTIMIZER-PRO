using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Data;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class ReportInventoryContractTests
{
    [Fact]
    public async Task CoreReadOnlyCapability_CanBeAccessedWithoutRewritingStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "cattech-inventory-contract-" + Guid.NewGuid().ToString("N"));
        try
        {
            IServiceReportInventoryReader reader = new JsonServiceReportService(root);
            ServiceReportInventory empty = await reader.InspectReportInventoryAsync();
            Assert.Equal(0, empty.TotalFiles);
            Assert.False(empty.RequiresReview);
            Assert.False(Directory.Exists(root));

            var writer = new JsonServiceReportService(root);
            var name = await writer.SaveReportAsync(new ServiceReport());
            var health = await reader.InspectReportInventoryAsync();
            Assert.Equal(1, health.UniqueEntries);
            Assert.Equal(1, health.TotalFiles);
            Assert.False(health.ScanFailed);
            Assert.False(health.RequiresReview);
            Assert.True(File.Exists(Path.Combine(root, "data", "service-reports", name)));
            Assert.DoesNotContain(root, health.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
