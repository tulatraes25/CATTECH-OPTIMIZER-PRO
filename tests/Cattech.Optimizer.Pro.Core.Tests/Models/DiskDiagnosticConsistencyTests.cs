using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Hardware;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Diagnostics;
using Cattech.Optimizer.Pro.Infrastructure.Hardware;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class DiskDiagnosticConsistencyTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;

    private static HardwareReport Device(
        string name, string media, bool mapping,
        ulong? volumeSizeGiB, ulong? volumeFreeGiB) => new()
    {
        Disks = DiskTopologyMapper.Map(new[]
        {
            new PhysicalDiskRecord
            {
                Name = name,
                Interface = media,
                MediaType = "Fixed hard disk media",
                CapacityBytes = 1000 * GiB,
                AssociationsAvailable = mapping,
                Volumes = new[]
                {
                    new LogicalVolumeRecord
                    {
                        DriveLetter = "C:",
                        CapacityBytes = volumeSizeGiB * GiB,
                        FreeBytes = volumeFreeGiB * GiB
                    }
                }
            }
        }, "C:")
    };

    private static DiagnosticReport Diagnose(HardwareReport hardware)
    {
        var diag = new DiagnosticReport();
        DiagnosticService.FillDiskInfo(diag, hardware);
        return diag;
    }

    private static List<DiagnosticAlert> DiskAlerts(DiagnosticReport report) =>
        DiagnosticService.GenerateAlerts(report)
            .Where(alert => alert.Category == "Disco").ToList();

    private static List<ReportRecommendation> DiskRecommendations(DiagnosticReport report) =>
        ReportRecommendationEngine.GenerateRecommendations(new ReportGenerationOptions
        {
            DiagnosticReport = report
        }).Where(r => r.Category == "Disco").ToList();

    [Fact]
    public void UnknownMappingNeverClaimsPrimaryOrFullDisk()
    {
        var diag = Diagnose(Device("SATA with no association", "SATA", false,
            volumeSizeGiB: 300, volumeFreeGiB: 0));

        Assert.Equal("No detectado", diag.PrimaryDiskName);
        Assert.Equal("No determinado", diag.DiskType);
        Assert.False(diag.DiskFreeSpaceKnown);
        Assert.Equal(0, diag.DiskFreePercent);
        Assert.Equal("", diag.SystemVolumeLetter);
        Assert.Empty(DiskAlerts(diag));
        Assert.Empty(DiskRecommendations(diag));
    }

    [Fact]
    public void NoMountedSystemVolumeCannotUseOtherVolumeCapacity()
    {
        var hardware = new HardwareReport
        {
            Disks = DiskTopologyMapper.Map(new[]
            {
                new PhysicalDiskRecord
                {
                    Name = "Data Disk",
                    Interface = "SATA",
                    MediaType = "HDD",
                    CapacityBytes = 1000 * GiB,
                    AssociationsAvailable = true,
                    Volumes = new[]
                    {
                        new LogicalVolumeRecord
                        {
                            DriveLetter = "D:",
                            CapacityBytes = 800 * GiB,
                            FreeBytes = 400 * GiB
                        }
                    }
                }
            }, "C:")
        };
        var diag = Diagnose(hardware);
        Assert.False(diag.DiskFreeSpaceKnown);
        Assert.Equal("No detectado", diag.PrimaryDiskName);
        Assert.Empty(DiskAlerts(diag));
    }

    [Fact]
    public void KnownSystemVolumeUsesVolumeCapacityNotPhysicalCapacity()
    {
        var diag = Diagnose(Device("NVMe WD PC SN560", "NVMe", true, 200, 50));

        Assert.Equal("NVMe WD PC SN560", diag.PrimaryDiskName);
        Assert.Equal("SSD", diag.DiskType);
        Assert.Equal(200, diag.DiskCapacityGB);
        Assert.Equal(50, diag.DiskFreeGB);
        Assert.Equal(25, diag.DiskFreePercent);
        Assert.True(diag.DiskFreeSpaceKnown);
        Assert.Equal("C:", diag.SystemVolumeLetter);
        Assert.Empty(DiskAlerts(diag));
        Assert.Empty(DiskRecommendations(diag));
    }

    [Fact]
    public void ActualZeroFreeWithVerifiedCapacityCreatesCriticalAlert()
    {
        var diag = Diagnose(Device("SATA HDD", "SATA", true, 300, 0));

        Assert.True(diag.DiskFreeSpaceKnown);
        Assert.Equal(0, diag.DiskFreeGB);
        var alerts = DiskAlerts(diag);
        Assert.Contains(alerts, a =>
            a.Severity == AlertSeverity.Critical &&
            a.Message.Contains("Disco lleno"));
    }

    [Fact]
    public void UnknownFreeBytesAreNotMisinterpretedAsFull()
    {
        var diag = Diagnose(Device("NVMe WD PC SN560", "NVMe", true, 300, null));

        Assert.Equal(300, diag.DiskCapacityGB);
        Assert.False(diag.DiskFreeSpaceKnown);
        Assert.Empty(DiskAlerts(diag));
        Assert.Empty(DiskRecommendations(diag));
    }

    [Fact]
    public void ImpossibleFreeGreaterThanCapacityCannotCauseFalseFull()
    {
        var diag = Diagnose(Device("SATA HDD", "SATA", true, 300, 301));

        Assert.False(diag.DiskFreeSpaceKnown);
        Assert.Empty(DiskAlerts(diag).Where(a => a.Severity == AlertSeverity.Critical));
        Assert.DoesNotContain(DiskRecommendations(diag),
            r => r.Message.Contains("espacio libre", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RealLowSpaceUsesMeasuredVolumePercent()
    {
        var diag = Diagnose(Device("NVMe WD PC SN560", "NVMe", true, 100, 9));
        Assert.Equal(9, diag.DiskFreePercent);
        Assert.Contains(DiskAlerts(diag), a => a.Severity == AlertSeverity.Warning);
        Assert.Contains(DiskRecommendations(diag), r => r.Message.Contains("9%"));
    }

    [Fact]
    public void HddMigrationAdviceRequiresPositiveHddEvidence()
    {
        var hdd = Diagnose(Device("WD Blue HDD", "SATA", true, 200, 100));
        var ssd = Diagnose(Device("NVMe WD PC SN560", "NVMe", true, 200, 100));

        Assert.Contains(DiskRecommendations(hdd), r => r.Message.Contains("migrar a SSD"));
        Assert.DoesNotContain(DiskRecommendations(ssd), r => r.Message.Contains("migrar a SSD"));
        Assert.Contains(DiskAlerts(hdd), a => a.Message.Contains("HDD"));
        Assert.DoesNotContain(DiskAlerts(ssd), a => a.Recommendation.Contains("migrar a SSD"));
    }

    [Theory]
    [InlineData("No detectado")]
    [InlineData("No determinado")]
    [InlineData("NVMe")]
    [InlineData("SSD")]
    [InlineData("SSD/HDD")]
    [InlineData(null)]
    public void ReportRecommendationUnknownOrCompoundMediaDoesNotTriggerHddAdvice(
        string? diskType)
    {
        var diag = new DiagnosticReport
        {
            DiskType = diskType!,
            DiskCapacityGB = 300,
            DiskFreeGB = 0,
            DiskFreePercent = 0,
            DiskFreeSpaceKnown = false
        };
        Assert.Empty(DiskRecommendations(diag));
        Assert.Empty(DiskAlerts(diag));
    }

    [Fact]
    public void LegacyReportWithDefaultFalseIsNotTreatedAsFull()
    {
        const string oldJson = """
        {
            "diskType": "No detectado",
            "diskCapacityGB": 300,
            "diskFreeGB": 0,
            "diskFreePercent": 0
        }
        """;
        var legacy = JsonSerializer.Deserialize<DiagnosticReport>(
            oldJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.False(legacy.DiskFreeSpaceKnown);
        Assert.Empty(DiskAlerts(legacy));
        Assert.Empty(DiskRecommendations(legacy));
    }
}
