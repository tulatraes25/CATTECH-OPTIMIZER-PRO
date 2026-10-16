using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Data;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class ReportInventoryHealthTests
{
    [Fact]
    public async Task Inventory_DistinguishesDuplicatesAndMalformedWithoutDeleting()
    {
        var root = Path.Combine(Path.GetTempPath(), "cattech-inventory-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new JsonServiceReportService(root);
            var report = new ServiceReport();
            var first = await service.SaveReportAsync(report);
            var second = await service.SaveReportAsync(report);
            var directory = Path.Combine(root, "data", "service-reports");
            var corrupt = Path.Combine(directory, "service-report-corrupt.json");
            await File.WriteAllTextAsync(corrupt, "{oops");

            var health = await service.InspectReportInventoryAsync();
            Assert.Equal(3, health.TotalFiles);
            Assert.Equal(0, health.UniqueEntries);
            Assert.Equal(2, health.AmbiguousEntries);
            Assert.Equal(1, health.InvalidEntries);
            Assert.True(health.RequiresReview);
            Assert.False(health.ScanFailed);
            Assert.True(File.Exists(Path.Combine(directory, first)));
            Assert.True(File.Exists(Path.Combine(directory, second)));
            Assert.True(File.Exists(corrupt));
            Assert.DoesNotContain("cattech-inventory-", health.ToString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Inventory_HealthyAndMissingDirectory_AreReadOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "cattech-health-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new JsonServiceReportService(root);
            var empty = await service.InspectReportInventoryAsync();
            Assert.Equal(0, empty.TotalFiles);
            Assert.False(empty.RequiresReview);
            Assert.False(Directory.Exists(root));

            await service.SaveReportAsync(new ServiceReport());
            var healthy = await service.InspectReportInventoryAsync();
            Assert.Equal(1, healthy.TotalFiles);
            Assert.Equal(1, healthy.UniqueEntries);
            Assert.Equal(0, healthy.InvalidEntries);
            Assert.False(healthy.RequiresReview);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
