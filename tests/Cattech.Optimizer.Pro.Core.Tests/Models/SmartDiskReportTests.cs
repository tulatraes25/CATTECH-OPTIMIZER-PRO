using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Infrastructure.Smart;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class SmartDiskReportTests
{
    // =====================
    // JSON simulados
    // =====================

    private const string HealthySataJson = @"{
        ""smart_status"": { ""passed"": true },
        ""user_capacity"": { ""bytes"": 500107862016 },
        ""model_name"": ""Samsung SSD 860 EVO"",
        ""serial_number"": ""S3Z9NB0K123456"",
        ""firmware_version"": ""RVT02B6Q"",
        ""power_on_time"": { ""hours"": 1234 },
        ""power_cycle_count"": { ""value"": 567 },
        ""temperature"": { ""current"": 35 },
        ""ata_smart_attributes"": {
            ""table"": [
                { ""id"": 5, ""name"": ""Reallocated_Sector_Ct"", ""value"": 100, ""worst"": 100, ""thresh"": 10, ""raw"": { ""value"": 0 }, ""flags"": { ""string"": ""PO--CK "" } },
                { ""id"": 9, ""name"": ""Power_On_Hours"", ""value"": 95, ""worst"": 95, ""thresh"": 0, ""raw"": { ""value"": 1234 }, ""flags"": { ""string"": ""O--CE- "" } },
                { ""id"": 197, ""name"": ""Current_Pending_Sector"", ""value"": 100, ""worst"": 100, ""thresh"": 0, ""raw"": { ""value"": 0 }, ""flags"": { ""string"": ""----O- "" } },
                { ""id"": 198, ""name"": ""Offline_Uncorrectable"", ""value"": 100, ""worst"": 100, ""thresh"": 0, ""raw"": { ""value"": 0 }, ""flags"": { ""string"": ""----O- "" } }
            ]
        }
    }";

    private const string HddWithPendingSectorsJson = @"{
        ""smart_status"": { ""passed"": true },
        ""user_capacity"": { ""bytes"": 1000204886016 },
        ""model_name"": ""Seagate Barracuda ST1000DM003"",
        ""serial_number"": ""ZA123456"",
        ""firmware_version"": ""CC43"",
        ""power_on_time"": { ""hours"": 45678 },
        ""power_cycle_count"": { ""value"": 1234 },
        ""temperature"": { ""current"": 42 },
        ""ata_smart_attributes"": {
            ""table"": [
                { ""id"": 5, ""name"": ""Reallocated_Sector_Ct"", ""value"": 100, ""worst"": 100, ""thresh"": 10, ""raw"": { ""value"": 5 }, ""flags"": { ""string"": ""PO--CK "" } },
                { ""id"": 197, ""name"": ""Current_Pending_Sector"", ""value"": 100, ""worst"": 100, ""thresh"": 0, ""raw"": { ""value"": 3 }, ""flags"": { ""string"": ""----O- "" } },
                { ""id"": 198, ""name"": ""Offline_Uncorrectable"", ""value"": 100, ""worst"": 100, ""thresh"": 0, ""raw"": { ""value"": 0 }, ""flags"": { ""string"": ""----O- "" } }
            ]
        }
    }";

    private const string HealthyNvmeJson = @"{
        ""smart_status"": { ""passed"": true },
        ""user_capacity"": { ""bytes"": 1000204886016 },
        ""model_name"": ""Samsung SSD 990 PRO"",
        ""serial_number"": ""S6Z3NY0T789012"",
        ""firmware_version"": ""4B2QJXD7"",
        ""power_on_time"": { ""hours"": 567 },
        ""power_cycle_count"": { ""value"": 234 },
        ""temperature"": { ""current"": 32 },
        ""nvme_smart_health_information"": {
            ""critical_warning"": ""0"",
            ""temperature"": 32,
            ""available_spare"": 100,
            ""available_spare_threshold"": 10,
            ""percentage_used"": 5,
            ""data_units_read"": 12345678,
            ""data_units_written"": 23456789,
            ""power_on_hours"": 567,
            ""power_cycles"": 234,
            ""media_errors"": 0,
            ""num_err_log_entries"": 0
        }
    }";

    private const string NvmeHighWearJson = @"{
        ""smart_status"": { ""passed"": true },
        ""user_capacity"": { ""bytes"": 500107862016 },
        ""model_name"": ""WD Black SN770"",
        ""serial_number"": ""WD-123456"",
        ""firmware_version"": ""731100WW"",
        ""power_on_time"": { ""hours"": 8760 },
        ""power_cycle_count"": { ""value"": 500 },
        ""temperature"": { ""current"": 38 },
        ""nvme_smart_health_information"": {
            ""critical_warning"": ""0"",
            ""temperature"": 38,
            ""available_spare"": 95,
            ""available_spare_threshold"": 10,
            ""percentage_used"": 85,
            ""data_units_read"": 50000000,
            ""data_units_written"": 100000000,
            ""power_on_hours"": 8760,
            ""power_cycles"": 500,
            ""media_errors"": 0,
            ""num_err_log_entries"": 0
        }
    }";

    private const string NvmeCriticalWarningJson = @"{
        ""smart_status"": { ""passed"": true },
        ""user_capacity"": { ""bytes"": 250059096576 },
        ""model_name"": ""Intel 660p"",
        ""serial_number"": ""INTEL-123"",
        ""firmware_version"": ""9CV101H0"",
        ""power_on_time"": { ""hours"": 2345 },
        ""power_cycle_count"": { ""value"": 100 },
        ""temperature"": { ""current"": 45 },
        ""nvme_smart_health_information"": {
            ""critical_warning"": ""3"",
            ""temperature"": 45,
            ""available_spare"": 5,
            ""available_spare_threshold"": 10,
            ""percentage_used"": 95,
            ""data_units_read"": 80000000,
            ""data_units_written"": 120000000,
            ""power_on_hours"": 2345,
            ""power_cycles"": 100,
            ""media_errors"": 5,
            ""num_err_log_entries"": 10
        }
    }";

    private const string HealthFailedJson = @"{
        ""smart_status"": { ""passed"": false },
        ""user_capacity"": { ""bytes"": 500107862016 },
        ""model_name"": ""Unknown Disk"",
        ""serial_number"": ""UNKNOWN"",
        ""firmware_version"": ""1.0""
    }";

    // =====================
    // Tests de parseo SMART
    // =====================

    [Fact]
    public void ParseSmartJson_HealthySata_GoodStatus()
    {
        var device = new SmartDiskDevice
        {
            Name = "/dev/sda",
            InfoName = "/dev/sda",
            ApproximateDiskType = "SSD",
            Protocol = "SATA",
            ModelName = "Samsung SSD 860 EVO",
            SerialNumber = "S3Z9NB0K123456"
        };

        var report = SmartctlParser.ParseSmartJson(HealthySataJson, device, "smartctl 7.4");

        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
        Assert.True(report.OverallHealthPassed);
        Assert.True(report.IsAnalysisSuccessful);
        Assert.Equal("Samsung SSD 860 EVO", report.ModelName);
        Assert.Equal(35, report.TemperatureCelsius);
        Assert.Equal(1234, report.PowerOnHours);
        Assert.Equal(567, report.PowerCycleCount);
        Assert.False(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void ParseSmartJson_HddPendingSectors_WarningStatus()
    {
        var device = new SmartDiskDevice
        {
            Name = "/dev/sda",
            InfoName = "/dev/sda",
            ApproximateDiskType = "HDD",
            Protocol = "SATA",
            ModelName = "Seagate Barracuda",
            SerialNumber = "ZA123456"
        };

        var report = SmartctlParser.ParseSmartJson(HddWithPendingSectorsJson, device, "smartctl 7.4");

        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
        Assert.True(report.OverallHealthPassed);
        // Verificar que hay atributos con severidad Warning
        Assert.Contains(report.ImportantAttributes, a => a.Severity == SmartSeverity.Warning);
    }

    [Fact]
    public void ParseSmartJson_HealthyNvme_GoodStatus()
    {
        var device = new SmartDiskDevice
        {
            Name = "/dev/nvme0n1",
            InfoName = "/dev/nvme0n1",
            ApproximateDiskType = "NVMe",
            Protocol = "NVMe",
            ModelName = "Samsung SSD 990 PRO",
            SerialNumber = "S6Z3NY0T789012"
        };

        var report = SmartctlParser.ParseSmartJson(HealthyNvmeJson, device, "smartctl 7.4");

        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
        Assert.True(report.OverallHealthPassed);
        Assert.True(report.IsAnalysisSuccessful);
        Assert.Equal(32, report.TemperatureCelsius);
        Assert.Equal(567, report.PowerOnHours);
    }

    [Fact]
    public void ParseSmartJson_NvmeHighWear_WarningStatus()
    {
        var device = new SmartDiskDevice
        {
            Name = "/dev/nvme0n1",
            InfoName = "/dev/nvme0n1",
            ApproximateDiskType = "NVMe",
            Protocol = "NVMe",
            ModelName = "WD Black SN770",
            SerialNumber = "WD-123456"
        };

        var report = SmartctlParser.ParseSmartJson(NvmeHighWearJson, device, "smartctl 7.4");

        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
        Assert.Contains(report.Warnings, w => w.Contains("85%"));
    }

    [Fact]
    public void ParseSmartJson_NvmeCriticalWarning_CriticalStatus()
    {
        var device = new SmartDiskDevice
        {
            Name = "/dev/nvme0n1",
            InfoName = "/dev/nvme0n1",
            ApproximateDiskType = "NVMe",
            Protocol = "NVMe",
            ModelName = "Intel 660p",
            SerialNumber = "INTEL-123"
        };

        var report = SmartctlParser.ParseSmartJson(NvmeCriticalWarningJson, device, "smartctl 7.4");

        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
        Assert.True(report.RequiresBackupRecommendation);
        Assert.Contains(report.Errors, e => e.Contains("critical_warning"));
    }

    [Fact]
    public void ParseSmartJson_HealthFailed_CriticalStatus()
    {
        var device = new SmartDiskDevice
        {
            Name = "/dev/sda",
            InfoName = "/dev/sda",
            ApproximateDiskType = "HDD",
            Protocol = "SATA"
        };

        var report = SmartctlParser.ParseSmartJson(HealthFailedJson, device, "smartctl 7.4");

        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
        Assert.False(report.OverallHealthPassed);
        Assert.True(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void ParseSmartJson_EmptyJson_NotFailed()
    {
        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "HDD" };

        var report = SmartctlParser.ParseSmartJson("{}", device, "smartctl 7.4");

        // No debe fallar, pero el estado será Unknown o Good sin datos
        Assert.True(report.IsAnalysisSuccessful);
        Assert.NotNull(report.HealthStatus);
    }

    [Fact]
    public void ParseSmartJson_InvalidJson_ErrorsNotEmpty()
    {
        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "HDD" };

        var report = SmartctlParser.ParseSmartJson("not json at all", device, "smartctl 7.4");

        Assert.False(report.IsAnalysisSuccessful);
        Assert.NotEmpty(report.Errors);
    }

    [Fact]
    public void ParseSmartJson_NullInput_ErrorsNotEmpty()
    {
        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "HDD" };

        var report = SmartctlParser.ParseSmartJson(null!, device, "smartctl 7.4");

        Assert.False(report.IsAnalysisSuccessful);
        Assert.NotEmpty(report.Errors);
    }

    [Fact]
    public void ParseSmartJson_CapturesImportantAttributes()
    {
        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "SSD" };

        var report = SmartctlParser.ParseSmartJson(HealthySataJson, device, "smartctl 7.4");

        Assert.NotEmpty(report.ImportantAttributes);
        Assert.Contains(report.ImportantAttributes, a => a.Id == 5); // Reallocated
        Assert.Contains(report.ImportantAttributes, a => a.Id == 197); // Pending
    }

    [Fact]
    public void ParseSmartJson_AttributeDescriptions_AreSet()
    {
        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "SSD" };

        var report = SmartctlParser.ParseSmartJson(HealthySataJson, device, "smartctl 7.4");

        var attr5 = report.ImportantAttributes.FirstOrDefault(a => a.Id == 5);
        Assert.NotNull(attr5);
        Assert.Equal("Sectores reasignados", attr5!.Description);
    }

    [Fact]
    public void ParseSmartJson_Capacity_ExtractedCorrectly()
    {
        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "SSD" };

        var report = SmartctlParser.ParseSmartJson(HealthySataJson, device, "smartctl 7.4");

        Assert.Equal(500107862016, report.CapacityBytes);
        Assert.True(report.CapacityGB > 460); // ~465 GB
    }

    [Fact]
    public void SmartAnalysisResult_DefaultValues_AreValid()
    {
        var result = new SmartAnalysisResult();

        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
        Assert.True(result.StartedAt <= DateTime.Now);
        Assert.NotNull(result.Reports);
        Assert.NotNull(result.Errors);
        Assert.NotNull(result.Warnings);
    }

    [Fact]
    public void SmartDiskReport_DefaultValues_AreValid()
    {
        var report = new SmartDiskReport();

        Assert.NotNull(report.Id);
        Assert.NotEmpty(report.Id);
        Assert.True(report.CreatedAt <= DateTime.Now);
        Assert.False(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void SmartHealthStatus_HasAllValues()
    {
        Assert.Equal(0, (int)SmartHealthStatus.Good);
        Assert.Equal(1, (int)SmartHealthStatus.Warning);
        Assert.Equal(2, (int)SmartHealthStatus.Critical);
        Assert.Equal(3, (int)SmartHealthStatus.NotAvailable);
        Assert.Equal(4, (int)SmartHealthStatus.Unknown);
    }

    [Fact]
    public void SmartSeverity_HasAllValues()
    {
        Assert.Equal(0, (int)SmartSeverity.Info);
        Assert.Equal(1, (int)SmartSeverity.Warning);
        Assert.Equal(2, (int)SmartSeverity.Critical);
        Assert.Equal(3, (int)SmartSeverity.Unknown);
    }

    [Fact]
    public async Task SmartDiskService_NoSmartctl_ReturnsNotAvailable()
    {
        var runner = new SmartctlRunner("/nonexistent/smartctl.exe");
        var service = new SmartDiskService(runner);

        var result = await service.AnalyzeAllDisksAsync();

        Assert.False(result.SmartctlAvailable);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task SmartDiskService_AnalyzeDisk_NoSmartctl_ReturnsNotAvailable()
    {
        var runner = new SmartctlRunner("/nonexistent/smartctl.exe");
        var service = new SmartDiskService(runner);

        var device = new SmartDiskDevice
        {
            Name = "/dev/sda",
            InfoName = "/dev/sda",
            ApproximateDiskType = "HDD"
        };

        var report = await service.AnalyzeDiskAsync(device);

        Assert.Equal(SmartHealthStatus.NotAvailable, report.HealthStatus);
    }
}
