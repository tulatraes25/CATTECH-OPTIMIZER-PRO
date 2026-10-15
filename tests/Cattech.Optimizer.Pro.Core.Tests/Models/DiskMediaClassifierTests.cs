using Cattech.Optimizer.Pro.Infrastructure.Diagnostics;
using Cattech.Optimizer.Pro.Infrastructure.Hardware;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class DiskMediaClassifierTests
{
    [Theory]
    [InlineData("NVMe WD PC SN560", "SCSI", "Fixed hard disk media")]
    [InlineData("Samsung SSD 980 PRO", "SCSI", "Fixed hard disk media")]
    [InlineData("Generic disk", "NVMe", "Fixed hard disk media")]
    [InlineData("Unspecified model", "SATA", "SSD")]
    [InlineData("Generic model", "IDE", "Solid State Disk")]
    [InlineData("NVME WD PC SN560", "SCSI", "")]
    public void ExplicitSolidStateEvidence_ClassifiesAsSsd(
        string model, string interfaceType, string wmiMediaType)
    {
        Assert.Equal("SSD",
            DiskMediaClassifier.Classify(model, interfaceType, wmiMediaType));
    }

    [Theory]
    [InlineData("WD Blue HDD", "SATA", "Fixed hard disk media")]
    [InlineData("Storage device", "SATA", "HDD")]
    [InlineData("Device", "SATA", "Rotating Media")]
    public void ExplicitRotatingEvidence_ClassifiesAsHdd(
        string model, string interfaceType, string wmiMediaType)
    {
        Assert.Equal("HDD",
            DiskMediaClassifier.Classify(model, interfaceType, wmiMediaType));
    }

    [Theory]
    [InlineData("WD Blue", "SATA", "Fixed hard disk media")]
    [InlineData("Samsung 860 EVO", "SATA", "Fixed hard disk media")]
    [InlineData("SanDisk", "USB", "Removable Media")]
    [InlineData("Virtual HARDDISK", "SCSI", "Fixed hard disk media")]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    public void GenericOrMissingWmiMediaType_IsUnknownNotHdd(
        string? model, string? interfaceType, string? wmiMediaType)
    {
        Assert.Equal(DiskMediaClassifier.Unknown,
            DiskMediaClassifier.Classify(model, interfaceType, wmiMediaType));
    }

    [Theory]
    [InlineData("NVMe WD PC SN560", "SCSI", "HDD")]
    [InlineData("SSD HDD HYBRID", "SATA", "Fixed hard disk media")]
    public void ContradictoryEvidence_IsUnknown(
        string model, string interfaceType, string wmiMediaType)
    {
        Assert.Equal(DiskMediaClassifier.Unknown,
            DiskMediaClassifier.Classify(model, interfaceType, wmiMediaType));
    }

    [Fact]
    public void FlashStorageIsNotReportedAsRotatingHdd()
    {
        Assert.Equal("Flash",
            DiskMediaClassifier.Classify("eMMC storage", "SCSI", "Fixed hard disk media"));
    }

    [Theory]
    [InlineData("HDD", true)]
    [InlineData("hdd", true)]
    [InlineData(" HDD ", true)]
    [InlineData("SSD", false)]
    [InlineData("NVMe", false)]
    [InlineData("Flash", false)]
    [InlineData("No detectado", false)]
    [InlineData("No determinado", false)]
    [InlineData("SSD/HDD", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void MigrationRecommendation_RequiresExplicitHdd(
        string? classifiedType, bool expected)
    {
        Assert.Equal(expected, DiagnosticService.ShouldSuggestHddUpgrade(classifiedType));
    }
}
