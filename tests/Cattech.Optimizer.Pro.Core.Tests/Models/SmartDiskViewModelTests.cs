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

    // =====================
    // Tests de resultados parciales
    // =====================

    [Fact]
    public void SmartAnalysisResult_PartialFailure_KeepsOtherReports()
    {
        var result = new SmartAnalysisResult
        {
            Reports =
            [
                new() { HealthStatus = SmartHealthStatus.Good, Device = "/dev/sda" },
                new() { HealthStatus = SmartHealthStatus.Unknown, Device = "/dev/sdb", IsAnalysisSuccessful = false, ErrorMessage = "Timeout" }
            ],
            Errors = ["Error al analizar /dev/sdb: Timeout"]
        };

        // El disco fallido no invalida el análisis completo
        Assert.Equal(2, result.Reports.Count);
        Assert.Equal(SmartHealthStatus.Good, result.Reports[0].HealthStatus);
        Assert.False(result.Reports[1].IsAnalysisSuccessful);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void SmartAnalysisResult_NoSmartctl_StillValidResult()
    {
        var result = new SmartAnalysisResult
        {
            SmartctlAvailable = false,
            Errors = ["Smartctl no disponible"]
        };

        // El resultado existe aunque smartctl no esté disponible
        Assert.NotNull(result);
        Assert.False(result.SmartctlAvailable);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(result.Reports);
    }

    [Fact]
    public void SmartAnalysisResult_NotAvailable_DoesNotMeanHealthy()
    {
        var report = new SmartDiskReport
        {
            HealthStatus = SmartHealthStatus.NotAvailable,
            HealthSummary = "SMART no soportado o deshabilitado",
            IsAnalysisSuccessful = false
        };

        // No disponible no es igual a sano
        Assert.Equal(SmartHealthStatus.NotAvailable, report.HealthStatus);
        Assert.False(report.IsAnalysisSuccessful);
        Assert.False(report.OverallHealthPassed);
    }

    [Fact]
    public void SmartDiskReport_HealthSummary_ContainsClearStatus()
    {
        var good = new SmartDiskReport { HealthStatus = SmartHealthStatus.Good, HealthSummary = "Salud general: Buena" };
        var critical = new SmartDiskReport { HealthStatus = SmartHealthStatus.Critical, HealthSummary = "CRÍTICO: Backup inmediato recomendado" };

        Assert.Contains("Buena", good.HealthSummary);
        Assert.Contains("CRÍTICO", critical.HealthSummary);
        Assert.Contains("Backup", critical.HealthSummary);
    }

    // =====================
    // Tests de métricas calculadas
    // =====================

    [Fact]
    public void ReallocatedSectorCount_ReturnsRawValueOfId5()
    {
        var report = new SmartDiskReport
        {
            ImportantAttributes =
            [
                new SmartAttribute { Id = 5, Name = "Reallocated_Sector_Ct", RawValue = 7 },
                new SmartAttribute { Id = 9, Name = "Power_On_Hours", RawValue = 1000 }
            ]
        };

        Assert.Equal(7, report.ReallocatedSectorCount);
    }

    [Fact]
    public void PendingSectorCount_ReturnsRawValueOfId197()
    {
        var report = new SmartDiskReport
        {
            ImportantAttributes =
            [
                new SmartAttribute { Id = 197, Name = "Current_Pending_Sector", RawValue = 3 }
            ]
        };

        Assert.Equal(3, report.PendingSectorCount);
    }

    [Fact]
    public void OfflineUncorrectableCount_ReturnsRawValueOfId198()
    {
        var report = new SmartDiskReport
        {
            ImportantAttributes =
            [
                new SmartAttribute { Id = 198, Name = "Offline_Uncorrectable", RawValue = 5 }
            ]
        };

        Assert.Equal(5, report.OfflineUncorrectableCount);
    }

    [Fact]
    public void UDMACrcErrorCount_ReturnsRawValueOfId199()
    {
        var report = new SmartDiskReport
        {
            ImportantAttributes =
            [
                new SmartAttribute { Id = 199, Name = "UDMA_CRC_Error_Count", RawValue = 2 }
            ]
        };

        Assert.Equal(2, report.UDMACrcErrorCount);
    }

    [Fact]
    public void CalculatedMetrics_MissingAttribute_ReturnsZero()
    {
        var report = new SmartDiskReport(); // Sin atributos

        Assert.Equal(0, report.ReallocatedSectorCount);
        Assert.Equal(0, report.PendingSectorCount);
        Assert.Equal(0, report.OfflineUncorrectableCount);
        Assert.Equal(0, report.UDMACrcErrorCount);
    }

    [Fact]
    public void CalculatedMetrics_DoNotUseImportantAttributesCount()
    {
        // 5 atributos en la lista, pero ninguno es ID 5
        var report = new SmartDiskReport
        {
            ImportantAttributes =
            [
                new SmartAttribute { Id = 9, RawValue = 100 },
                new SmartAttribute { Id = 12, RawValue = 50 },
                new SmartAttribute { Id = 194, RawValue = 35 },
                new SmartAttribute { Id = 231, RawValue = 90 },
                new SmartAttribute { Id = 233, RawValue = 80 }
            ]
        };

        // Count = 5, pero ReallocatedSectorCount debe ser 0 (no existe ID 5)
        Assert.Equal(5, report.ImportantAttributes.Count);
        Assert.Equal(0, report.ReallocatedSectorCount);
    }

    // =====================
    // Tests de métricas NVMe
    // =====================

    [Fact]
    public void NvmeMetrics_AreStoredStructured()
    {
        var report = new SmartDiskReport
        {
            NvmePercentageUsed = 85,
            NvmeAvailableSpare = 10,
            NvmeMediaErrors = 5,
            NvmeUnsafeShutdowns = 3
        };

        Assert.Equal(85, report.NvmePercentageUsed);
        Assert.Equal(10, report.NvmeAvailableSpare);
        Assert.Equal(5, report.NvmeMediaErrors);
        Assert.Equal(3, report.NvmeUnsafeShutdowns);
    }

    [Fact]
    public void NvmeMetrics_DefaultToNull()
    {
        var report = new SmartDiskReport();

        Assert.Null(report.NvmePercentageUsed);
        Assert.Null(report.NvmeAvailableSpare);
        Assert.Null(report.NvmeMediaErrors);
        Assert.Null(report.NvmeUnsafeShutdowns);
    }

    // =====================
    // Tests de estados del resumen
    // =====================

    [Fact]
    public void SummaryStatus_AllGood_AllDisksHealthy()
    {
        var reports = new List<SmartDiskReport>
        {
            new() { HealthStatus = SmartHealthStatus.Good },
            new() { HealthStatus = SmartHealthStatus.Good }
        };

        var good = reports.Count(r => r.HealthStatus == SmartHealthStatus.Good);
        var notAvailable = reports.Count(r => r.HealthStatus == SmartHealthStatus.NotAvailable);
        var unknown = reports.Count(r => r.HealthStatus == SmartHealthStatus.Unknown);

        // Solo "todos sanos" si todos son Good
        Assert.Equal(2, good);
        Assert.Equal(0, notAvailable);
        Assert.Equal(0, unknown);
        Assert.True(reports.Count > 0 && good == reports.Count);
    }

    [Fact]
    public void SummaryStatus_GoodPlusNotAvailable_NotAllHealthy()
    {
        var reports = new List<SmartDiskReport>
        {
            new() { HealthStatus = SmartHealthStatus.Good },
            new() { HealthStatus = SmartHealthStatus.NotAvailable }
        };

        var good = reports.Count(r => r.HealthStatus == SmartHealthStatus.Good);
        var notAvailable = reports.Count(r => r.HealthStatus == SmartHealthStatus.NotAvailable);

        // No todos son Good → NO debe mostrar "todos sanos"
        Assert.Equal(1, good);
        Assert.Equal(1, notAvailable);
        Assert.False(good == reports.Count);
    }

    [Fact]
    public void SummaryStatus_GoodPlusUnknown_NotAllHealthy()
    {
        var reports = new List<SmartDiskReport>
        {
            new() { HealthStatus = SmartHealthStatus.Good },
            new() { HealthStatus = SmartHealthStatus.Unknown }
        };

        var good = reports.Count(r => r.HealthStatus == SmartHealthStatus.Good);
        var unknown = reports.Count(r => r.HealthStatus == SmartHealthStatus.Unknown);

        Assert.Equal(1, good);
        Assert.Equal(1, unknown);
        Assert.False(good == reports.Count);
    }

    [Fact]
    public void SummaryUnknown_CalculatesCorrectly()
    {
        var reports = new List<SmartDiskReport>
        {
            new() { HealthStatus = SmartHealthStatus.Good },
            new() { HealthStatus = SmartHealthStatus.Warning },
            new() { HealthStatus = SmartHealthStatus.Unknown },
            new() { HealthStatus = SmartHealthStatus.Unknown }
        };

        var unknown = reports.Count(r => r.HealthStatus == SmartHealthStatus.Unknown);

        Assert.Equal(2, unknown);
    }

    // =====================
    // Tests de preservación del resultado
    // =====================

    [Fact]
    public void SmartAnalysisResult_Timestamps_ArePreserved()
    {
        var startedAt = new DateTime(2026, 1, 15, 10, 30, 0);
        var finishedAt = new DateTime(2026, 1, 15, 10, 31, 30);

        var result = new SmartAnalysisResult
        {
            StartedAt = startedAt,
            FinishedAt = finishedAt
        };

        Assert.Equal(startedAt, result.StartedAt);
        Assert.Equal(finishedAt, result.FinishedAt);
    }

    [Fact]
    public void SmartAnalysisResult_ErrorsAndWarnings_ArePreserved()
    {
        var result = new SmartAnalysisResult
        {
            Errors = ["Error 1", "Error 2"],
            Warnings = ["Warning 1"]
        };

        Assert.Equal(2, result.Errors.Count);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void SmartAnalysisResult_SerializePreservesTimestamps()
    {
        var startedAt = new DateTime(2026, 1, 15, 10, 30, 0);
        var result = new SmartAnalysisResult
        {
            StartedAt = startedAt,
            Errors = ["Test error"],
            Warnings = ["Test warning"],
            Reports = [new() { HealthStatus = SmartHealthStatus.Good }]
        };

        var json = JsonSerializer.Serialize(result, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<SmartAnalysisResult>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(startedAt, deserialized!.StartedAt);
        Assert.Single(deserialized.Errors);
        Assert.Single(deserialized.Warnings);
    }
}
