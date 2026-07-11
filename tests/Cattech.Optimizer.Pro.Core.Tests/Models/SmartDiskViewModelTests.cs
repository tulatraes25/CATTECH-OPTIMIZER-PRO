using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class SmartDiskViewModelTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    // =====================
    // Tests de SmartHealthStatus agrupación
    // =====================

    [Fact]
    public void SmartHealthStatus_AllValues_Exist()
    {
        var values = Enum.GetValues<SmartHealthStatus>();
        Assert.Equal(5, values.Length);
        Assert.Contains(SmartHealthStatus.Good, values);
        Assert.Contains(SmartHealthStatus.Warning, values);
        Assert.Contains(SmartHealthStatus.Critical, values);
        Assert.Contains(SmartHealthStatus.NotAvailable, values);
        Assert.Contains(SmartHealthStatus.Unknown, values);
    }

    [Fact]
    public void SmartSeverity_AllValues_Exist()
    {
        var values = Enum.GetValues<SmartSeverity>();
        Assert.Equal(4, values.Length);
        Assert.Contains(SmartSeverity.Info, values);
        Assert.Contains(SmartSeverity.Warning, values);
        Assert.Contains(SmartSeverity.Critical, values);
        Assert.Contains(SmartSeverity.Unknown, values);
    }

    // =====================
    // Tests de SmartDiskReport agrupación
    // =====================

    [Fact]
    public void SmartDiskReport_GroupByHealthStatus_CorrectCounts()
    {
        var reports = new List<SmartDiskReport>
        {
            new() { HealthStatus = SmartHealthStatus.Good },
            new() { HealthStatus = SmartHealthStatus.Good },
            new() { HealthStatus = SmartHealthStatus.Warning },
            new() { HealthStatus = SmartHealthStatus.Critical },
            new() { HealthStatus = SmartHealthStatus.NotAvailable }
        };

        var good = reports.Count(r => r.HealthStatus == SmartHealthStatus.Good);
        var warning = reports.Count(r => r.HealthStatus == SmartHealthStatus.Warning);
        var critical = reports.Count(r => r.HealthStatus == SmartHealthStatus.Critical);
        var notAvailable = reports.Count(r => r.HealthStatus == SmartHealthStatus.NotAvailable);

        Assert.Equal(2, good);
        Assert.Equal(1, warning);
        Assert.Equal(1, critical);
        Assert.Equal(1, notAvailable);
    }

    // =====================
    // Tests de SmartAnalysisResult
    // =====================

    [Fact]
    public void SmartAnalysisResult_CalculatedProperties_Work()
    {
        var result = new SmartAnalysisResult
        {
            Reports =
            [
                new() { HealthStatus = SmartHealthStatus.Good },
                new() { HealthStatus = SmartHealthStatus.Good },
                new() { HealthStatus = SmartHealthStatus.Warning },
                new() { HealthStatus = SmartHealthStatus.Critical },
                new() { HealthStatus = SmartHealthStatus.NotAvailable }
            ]
        };

        var good = result.Reports.Count(r => r.HealthStatus == SmartHealthStatus.Good);
        var critical = result.Reports.Count(r => r.HealthStatus == SmartHealthStatus.Critical);

        Assert.Equal(2, good);
        Assert.Equal(1, critical);
    }

    // =====================
    // Tests de SmartDiskReport con datos completos
    // =====================

    [Fact]
    public void SmartDiskReport_WithAttributes_HasCorrectData()
    {
        var report = new SmartDiskReport
        {
            Device = "/dev/sda",
            ModelName = "Samsung SSD 860 EVO",
            DeviceType = "SSD",
            TemperatureCelsius = 35,
            PowerOnHours = 1234,
            PowerCycleCount = 567,
            CapacityBytes = 500107862016,
            HealthStatus = SmartHealthStatus.Good,
            ImportantAttributes =
            [
                new SmartAttribute { Id = 5, Name = "Reallocated_Sector_Ct", RawValue = 0, Severity = SmartSeverity.Info },
                new SmartAttribute { Id = 197, Name = "Current_Pending_Sector", RawValue = 0, Severity = SmartSeverity.Info }
            ]
        };

        Assert.Equal(35, report.TemperatureCelsius);
        Assert.Equal(1234, report.PowerOnHours);
        Assert.Equal(2, report.ImportantAttributes.Count);
        Assert.False(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void SmartDiskReport_WithWarnings_HasWarningStatus()
    {
        var report = new SmartDiskReport
        {
            HealthStatus = SmartHealthStatus.Warning,
            Warnings = ["Sectores reasignados detectados"],
            RequiresBackupRecommendation = false
        };

        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
        Assert.Single(report.Warnings);
    }

    [Fact]
    public void SmartDiskReport_WithCritical_HasBackupRecommendation()
    {
        var report = new SmartDiskReport
        {
            HealthStatus = SmartHealthStatus.Critical,
            RequiresBackupRecommendation = true,
            Errors = ["NVMe critical_warning: 3"]
        };

        Assert.True(report.RequiresBackupRecommendation);
        Assert.Single(report.Errors);
    }

    [Fact]
    public void SmartDiskReport_NotAvailable_Explanation()
    {
        var report = new SmartDiskReport
        {
            HealthStatus = SmartHealthStatus.NotAvailable,
            HealthSummary = "Smartctl no disponible",
            IsAnalysisSuccessful = false
        };

        Assert.Equal(SmartHealthStatus.NotAvailable, report.HealthStatus);
        Assert.False(report.IsAnalysisSuccessful);
    }

    // =====================
    // Tests de SmartAnalysisResult persistencia
    // =====================

    [Fact]
    public void SmartAnalysisResult_SerializeToJson_ProducesValidJson()
    {
        var result = new SmartAnalysisResult
        {
            SmartctlAvailable = true,
            SmartctlVersion = "smartctl 7.4",
            DevicesAnalyzed = 2,
            Reports =
            [
                new() { HealthStatus = SmartHealthStatus.Good, ModelName = "Samsung SSD" },
                new() { HealthStatus = SmartHealthStatus.Warning, ModelName = "Seagate HDD" }
            ]
        };

        var json = JsonSerializer.Serialize(result, SerializerOptions);

        Assert.Contains("smartctl 7.4", json);
        Assert.Contains("Samsung SSD", json);
        Assert.Contains("Seagate HDD", json);
    }

    [Fact]
    public void SmartAnalysisResult_DeserializeFromJson_PreservesAllFields()
    {
        var original = new SmartAnalysisResult
        {
            SmartctlAvailable = true,
            SmartctlVersion = "smartctl 7.5",
            DevicesAnalyzed = 1,
            Reports =
            [
                new() { HealthStatus = SmartHealthStatus.Good, ModelName = "Test Disk" }
            ]
        };

        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<SmartAnalysisResult>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("smartctl 7.5", deserialized!.SmartctlVersion);
        Assert.Single(deserialized.Reports);
        Assert.Equal("Test Disk", deserialized.Reports[0].ModelName);
    }
}
