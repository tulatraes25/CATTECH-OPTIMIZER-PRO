using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Data;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>Only disposable synthetic records; no real customer paths.</summary>
public class ServiceReportWriteGateTests
{
    [Theory]
    [InlineData("client")]
    [InlineData("equipment")]
    [InlineData("service")]
    [InlineData("actions")]
    [InlineData("recommendations")]
    [InlineData("null-action")]
    [InlineData("id")]
    public async Task InvalidNewReport_ThrowsWithoutCreatingDirectoryOrFile(string scenario)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-save-gate-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonServiceReportService(root);
            var invalid = new ServiceReport();
            switch (scenario)
            {
                case "client": invalid.Client = null!; break;
                case "equipment": invalid.Equipment = null!; break;
                case "service": invalid.Service = null!; break;
                case "actions": invalid.Service.ActionsPerformed = null!; break;
                case "recommendations": invalid.Service.Recommendations = null!; break;
                case "null-action": invalid.Service.ActionsPerformed.Add(null!); break;
                case "id": invalid.Id = "  "; break;
            }

            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveReportAsync(invalid));

            Assert.False(Directory.Exists(root));
            var health = await store.InspectReportInventoryAsync();
            Assert.Equal(0, health.TotalFiles);
            Assert.False(health.RequiresReview);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task BadNewRecord_NeverPoisonsExistingReadableLegacyReport()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-save-safe-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonServiceReportService(root);
            var legacy = new ServiceReport
            {
                Id = "ABCD1234",
                Client = new ClientInfo { Name = "Synthetic Legacy" }
            };
            var filename = await store.SaveReportAsync(legacy);
            var path = Path.Combine(root, "data", "service-reports", filename);
            var before = await File.ReadAllTextAsync(path);
            var invalid = new ServiceReport { Service = null! };

            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveReportAsync(invalid));

            var health = await store.InspectReportInventoryAsync();
            Assert.Equal(1, health.UniqueEntries);
            Assert.Equal(0, health.InvalidEntries);
            Assert.Equal(before, await File.ReadAllTextAsync(path));
            Assert.Equal("ABCD1234", (await store.LoadReportAsync("ABCD1234"))?.Id);
            Assert.Single(await store.ListReportsAsync());
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "service-report-*.json"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
