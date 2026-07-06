using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Data;

namespace Cattech.Optimizer.Pro.Core.Tests;

public class JsonServiceReportServiceTests : IDisposable
{
    private readonly string _testDir;

    public JsonServiceReportServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"cattech_reports_test_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task SaveReportAsync_CreatesFileAndDirectory()
    {
        var service = new JsonServiceReportService(_testDir);
        var report = CreateTestReport("Test Client");

        var fileName = await service.SaveReportAsync(report);

        Assert.NotNull(fileName);
        Assert.StartsWith("service-report-", fileName);
        Assert.EndsWith(".json", fileName);

        var reportsDir = Path.Combine(_testDir, "data", "service-reports");
        Assert.True(Directory.Exists(reportsDir));
        Assert.Single(Directory.GetFiles(reportsDir, "*.json"));
    }

    [Fact]
    public async Task SaveAndLoad_PreservesAllData()
    {
        var service = new JsonServiceReportService(_testDir);
        var original = CreateTestReport("Preserve Test");
        original.Client.Phone = "555-0100";
        original.Client.Company = "Test Corp";
        original.Equipment.Brand = "Dell";
        original.Equipment.Model = "XPS 15";
        original.Equipment.EquipmentType = "Notebook";
        original.Equipment.RamTotalGB = 32;
        original.Equipment.DiskType = "NVMe";

        var fileName = await service.SaveReportAsync(original);

        // Load by finding the file (we don't know the exact ID without reading)
        var reports = await service.ListReportsAsync();
        Assert.Single(reports);

        var loaded = await service.LoadReportAsync(original.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Preserve Test", loaded!.Client.Name);
        Assert.Equal("555-0100", loaded.Client.Phone);
        Assert.Equal("Test Corp", loaded.Client.Company);
        Assert.Equal("Dell", loaded.Equipment.Brand);
        Assert.Equal("XPS 15", loaded.Equipment.Model);
        Assert.Equal("Notebook", loaded.Equipment.EquipmentType);
        Assert.Equal(32, loaded.Equipment.RamTotalGB);
        Assert.Equal("NVMe", loaded.Equipment.DiskType);
    }

    [Fact]
    public async Task ListReportsAsync_ReturnsMultipleReports()
    {
        var service = new JsonServiceReportService(_testDir);

        await service.SaveReportAsync(CreateTestReport("Client 1"));
        await Task.Delay(1100); // Ensure different timestamp
        await service.SaveReportAsync(CreateTestReport("Client 2"));
        await Task.Delay(1100);
        await service.SaveReportAsync(CreateTestReport("Client 3"));

        var reports = await service.ListReportsAsync();

        Assert.Equal(3, reports.Count);
        // Most recent first
        Assert.Equal("Client 3", reports[0].ClientName);
        Assert.Equal("Client 2", reports[1].ClientName);
        Assert.Equal("Client 1", reports[2].ClientName);
    }

    [Fact]
    public async Task ListReportsAsync_RespectsMaxResults()
    {
        var service = new JsonServiceReportService(_testDir);

        for (int i = 0; i < 5; i++)
        {
            await service.SaveReportAsync(CreateTestReport($"Client {i}"));
            await Task.Delay(100);
        }

        var reports = await service.ListReportsAsync(maxResults: 3);

        Assert.Equal(3, reports.Count);
    }

    [Fact]
    public async Task LoadReportAsync_NotFound_ReturnsNull()
    {
        var service = new JsonServiceReportService(_testDir);

        var result = await service.LoadReportAsync("NONEXISTENT");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteReportAsync_RemovesFile()
    {
        var service = new JsonServiceReportService(_testDir);
        var report = CreateTestReport("Delete Me");

        await service.SaveReportAsync(report);
        var deleted = await service.DeleteReportAsync(report.Id);

        Assert.True(deleted);

        var loaded = await service.LoadReportAsync(report.Id);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeleteReportAsync_NotFound_ReturnsFalse()
    {
        var service = new JsonServiceReportService(_testDir);

        var deleted = await service.DeleteReportAsync("NONEXISTENT");

        Assert.False(deleted);
    }

    [Fact]
    public async Task SaveReportAsync_DuplicateTimestamp_UsesSuffix()
    {
        var service = new JsonServiceReportService(_testDir);

        var report1 = CreateTestReport("First");
        var report2 = CreateTestReport("Second");

        // Same timestamp
        report1.CreatedAt = new DateTime(2024, 6, 15, 10, 30, 0);
        report2.CreatedAt = new DateTime(2024, 6, 15, 10, 30, 0);

        var file1 = await service.SaveReportAsync(report1);
        var file2 = await service.SaveReportAsync(report2);

        Assert.NotEqual(file1, file2);

        var reportsDir = Path.Combine(_testDir, "data", "service-reports");
        Assert.Equal(2, Directory.GetFiles(reportsDir, "*.json").Length);
    }

    [Fact]
    public async Task ListReportsAsync_EmptyDirectory_ReturnsEmptyList()
    {
        var service = new JsonServiceReportService(_testDir);

        var reports = await service.ListReportsAsync();

        Assert.NotNull(reports);
        Assert.Empty(reports);
    }

    private static ServiceReport CreateTestReport(string clientName)
    {
        return new ServiceReport
        {
            Client = new ClientInfo
            {
                Name = clientName,
                Email = $"{clientName.ToLower().Replace(" ", ".")}@test.com"
            },
            Equipment = new EquipmentInfo
            {
                Brand = "TestBrand",
                Model = "TestModel",
                EquipmentType = "PC de escritorio",
                ServiceReason = "Test reason"
            },
            Service = new ServiceInfo
            {
                ServiceDate = DateTime.Now,
                Reason = "Test reason"
            },
            TechnicianName = "Test Technician"
        };
    }
}
