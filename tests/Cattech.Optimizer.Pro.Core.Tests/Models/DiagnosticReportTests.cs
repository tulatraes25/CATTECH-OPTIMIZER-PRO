using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class DiagnosticReportTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void DiagnosticReport_DefaultValues_AreValid()
    {
        var report = new DiagnosticReport();

        Assert.NotNull(report.Id);
        Assert.NotEmpty(report.Id);
        Assert.True(report.DiagnosisDate <= DateTime.Now);
        Assert.NotNull(report.Alerts);
        Assert.NotNull(report.Startup);
        Assert.NotNull(report.TempFiles);
        Assert.NotNull(report.Security);
        Assert.NotNull(report.VirtualMemory);
    }

    [Fact]
    public void DiagnosticReport_Id_Is8Chars()
    {
        var report = new DiagnosticReport();

        Assert.Equal(8, report.Id.Length);
    }

    [Fact]
    public void DiagnosticReport_SerializeToJson_ProducesValidJson()
    {
        var report = new DiagnosticReport
        {
            OsName = "Windows 11 Pro",
            WindowsEdition = "23H2",
            Architecture = "x64",
            ComputerName = "DESKTOP-001",
            CurrentUser = "usuario",
            Processor = "Intel Core i7-12700K",
            CpuCores = 12,
            RamTotalGB = 16,
            RamUsedGB = 8,
            RamAvailableGB = 8,
            RamUsagePercent = 50,
            PrimaryDiskName = "Samsung SSD 980 PRO",
            DiskType = "NVMe",
            DiskCapacityGB = 931,
            DiskFreeGB = 450,
            DiskFreePercent = 48.3,
            TechnicianName = "Tecnico Test"
        };

        report.Alerts.Add(new DiagnosticAlert
        {
            Severity = AlertSeverity.Warning,
            Category = "RAM",
            Message = "RAM justa: 8 GB",
            Recommendation = "Adecuado para uso basico."
        });

        var json = JsonSerializer.Serialize(report, SerializerOptions);

        Assert.Contains("Windows 11 Pro", json);
        Assert.Contains("Intel Core i7-12700K", json);
        Assert.Contains("Samsung SSD 980 PRO", json);
        Assert.Contains("NVMe", json);
        Assert.Contains("RAM justa", json);
        Assert.Contains("Tecnico Test", json);
    }

    [Fact]
    public void DiagnosticReport_DeserializeFromJson_PreservesAllFields()
    {
        var original = new DiagnosticReport
        {
            OsName = "Windows 10 Pro",
            RamTotalGB = 8,
            DiskType = "SSD"
        };

        original.Alerts.Add(new DiagnosticAlert
        {
            Severity = AlertSeverity.Critical,
            Category = "Disco",
            Message = "Disco lleno",
            Recommendation = "Liberar espacio"
        });

        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<DiagnosticReport>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("Windows 10 Pro", deserialized!.OsName);
        Assert.Equal(8, deserialized.RamTotalGB);
        Assert.Equal("SSD", deserialized.DiskType);
        Assert.Single(deserialized.Alerts);
        Assert.Equal(AlertSeverity.Critical, deserialized.Alerts[0].Severity);
        Assert.Equal("Disco lleno", deserialized.Alerts[0].Message);
    }

    [Fact]
    public void DiagnosticAlert_DefaultValues_AreValid()
    {
        var alert = new DiagnosticAlert();

        Assert.Equal(AlertSeverity.Info, alert.Severity);
        Assert.Equal(string.Empty, alert.Category);
        Assert.Equal(string.Empty, alert.Message);
        Assert.Equal(string.Empty, alert.Recommendation);
    }

    [Fact]
    public void StartupInfo_DefaultValues_AreEmpty()
    {
        var startup = new StartupInfo();

        Assert.Equal(0, startup.TotalCount);
        Assert.Equal(0, startup.ThirdPartyCount);
        Assert.NotNull(startup.ProgramNames);
        Assert.Empty(startup.ProgramNames);
    }

    [Fact]
    public void TempFilesInfo_DefaultValues_AreZero()
    {
        var temp = new TempFilesInfo();

        Assert.Equal(0, temp.TotalSizeBytes);
        Assert.Equal(0, temp.TotalSizeGB);
        Assert.Equal(0, temp.FileCount);
        Assert.NotNull(temp.Locations);
        Assert.Empty(temp.Locations);
    }

    [Fact]
    public void TempLocationDetail_SizeGB_CalculatesCorrectly()
    {
        var detail = new TempLocationDetail
        {
            SizeBytes = 3L * 1024 * 1024 * 1024 // 3 GB
        };

        Assert.Equal(3, detail.SizeGB);
    }

    [Fact]
    public void SecurityInfo_DefaultValues_AreConsistent()
    {
        var security = new SecurityInfo();

        Assert.Equal("No detectado", security.AntivirusName);
        Assert.False(security.FirewallActive);
        Assert.Equal("No determinado", security.WindowsUpdateStatus);
    }

    [Fact]
    public void VirtualMemoryInfo_DefaultValues_AreZero()
    {
        var vm = new VirtualMemoryInfo();

        Assert.Equal(0, vm.PagingFileSizeGB);
        Assert.False(vm.IsAutoManaged);
        Assert.Equal(string.Empty, vm.Location);
    }

    [Fact]
    public void AlertSeverity_HasAllValues()
    {
        Assert.Equal(0, (int)AlertSeverity.Info);
        Assert.Equal(1, (int)AlertSeverity.Warning);
        Assert.Equal(2, (int)AlertSeverity.Critical);
    }
}
