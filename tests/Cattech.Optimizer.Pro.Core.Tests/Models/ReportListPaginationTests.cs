using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Data;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class ReportListPaginationTests
{
    [Fact]
    public async Task ListReports_CorruptNewestFile_DoesNotHideOlderValidReport()
    {
        var root = Path.Combine(Path.GetTempPath(), "cattech-report-pagination-" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, "data", "service-reports");
        Directory.CreateDirectory(folder);
        try
        {
            var service = new JsonServiceReportService(root);
            var report = new ServiceReport { Client = new ClientInfo { Name = "Synthetic" } };
            var savedName = await service.SaveReportAsync(report);
            var saved = Path.Combine(folder, savedName);
            File.SetLastWriteTimeUtc(saved, DateTime.UtcNow.AddMinutes(-3));
            var corrupt = Path.Combine(folder, "service-report-20990101-000000-corrupt.json");
            await File.WriteAllTextAsync(corrupt, "{ invalid json");
            File.SetLastWriteTimeUtc(corrupt, DateTime.UtcNow);

            var results = await service.ListReportsAsync(1);

            Assert.Single(results);
            Assert.Equal(report.Id, results[0].Id);
            Assert.True(File.Exists(corrupt));
            Assert.True(File.Exists(saved));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ListReports_NonPositiveLimit_ReturnsEmptyWithoutMutatingFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "cattech-report-limit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new JsonServiceReportService(root);
            var saved = await service.SaveReportAsync(new ServiceReport());
            Assert.Empty(await service.ListReportsAsync(0));
            Assert.Empty(await service.ListReportsAsync(-1));
            Assert.True(File.Exists(Path.Combine(root, "data", "service-reports", saved)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
