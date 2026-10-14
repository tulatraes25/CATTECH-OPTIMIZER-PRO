using Cattech.Optimizer.Pro.Infrastructure.Hardware;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class DiskTopologyMapperTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;

    private static PhysicalDiskRecord Disk(string name, string media,
        bool associations, params LogicalVolumeRecord[] volumes) => new()
    {
        Name = name,
        Interface = media.Equals("NVMe", StringComparison.OrdinalIgnoreCase)
            ? "NVMe" : "SATA",
        MediaType = "Fixed hard disk media",
        CapacityBytes = 1000UL * GiB,
        AssociationsAvailable = associations,
        Volumes = volumes
    };

    private static LogicalVolumeRecord Vol(string letter, ulong? totalGiB,
        ulong? freeGiB) => new()
    {
        DriveLetter = letter,
        CapacityBytes = totalGiB * GiB,
        FreeBytes = freeGiB * GiB
    };

    [Fact]
    public void SystemDiskIsLocatedByVerifiedVolume_NotDiskEnumerationOrder()
    {
        var result = DiskTopologyMapper.Map(new[]
        {
            Disk("Data HDD", "SATA", true, Vol("D:", 200, 100)),
            Disk("NVMe WD PC SN560", "NVMe", true, Vol("C:", 300, 120))
        }, "C:\\");

        Assert.Equal(2, result.Count);
        Assert.False(result[0].IsSystemDisk);
        Assert.True(result[1].IsSystemDisk);
        Assert.Equal("C:", result[1].SystemVolume?.DriveLetter);
        Assert.Equal("SSD", result[1].MediaType);
        Assert.Equal(1000, result[1].TotalGB, precision: 2);
        Assert.Equal(300, result[1].SystemVolume!.CapacityGB);
        Assert.Equal(120, result[1].SystemVolume!.FreeGB);
        Assert.Equal(40, result[1].SystemVolume!.FreePercent);
        Assert.Equal(180, result[1].UsedGB);
        Assert.Equal(60, result[1].UsagePercent);
    }

    [Fact]
    public void TwoVolumesOnOnePhysicalDiskBothRemainAssociated()
    {
        var result = DiskTopologyMapper.Map(new[]
        {
            Disk("One physical", "SATA", true,
                Vol("D:", 100, 50), Vol("C:", 300, 120))
        }, "C:");

        var disk = Assert.Single(result);
        Assert.Equal(new[] { "C:", "D:" }, disk.Volumes.Select(v => v.DriveLetter));
        Assert.Equal("C:, D:", disk.DriveLetter);
        Assert.True(disk.IsSystemDisk);
        Assert.Equal(300, disk.SystemVolume!.CapacityGB);
        Assert.Equal(120, disk.SystemVolume!.FreeGB);
    }

    [Fact]
    public void MultiplePhysicalDisksClaimSameSystemVolume_AmbiguousNoOwner()
    {
        var result = DiskTopologyMapper.Map(new[]
        {
            Disk("Physical 0", "SATA", true, Vol("C:", 100, 50)),
            Disk("Physical 1", "SATA", true,
                Vol("C:", 100, 50), Vol("D:", 500, 80))
        }, "C:");

        Assert.All(result, d => Assert.False(d.IsSystemDisk));
        Assert.Empty(result[0].Volumes);
        Assert.Equal("D:", Assert.Single(result[1].Volumes).DriveLetter);
    }

    [Fact]
    public void RepeatedSameLetterOnOnePhysicalDisk_IsNotSilentlyAccepted()
    {
        var disk = Assert.Single(DiskTopologyMapper.Map(new[]
        {
            Disk("Virtual disk", "SATA", true,
                Vol("C:", 150, 70), Vol("C:", 200, 30))
        }, "C:"));

        Assert.Empty(disk.Volumes);
        Assert.False(disk.IsSystemDisk);
    }

    [Fact]
    public void UnavailableAssociationsNeverProduceInventedVolumes()
    {
        var disk = Assert.Single(DiskTopologyMapper.Map(new[]
        {
            Disk("Inaccessible", "SATA", false, Vol("C:", 300, 200))
        }, "C:"));

        Assert.False(disk.VolumeAssociationsVerified);
        Assert.False(disk.IsSystemDisk);
        Assert.Empty(disk.Volumes);
        Assert.Equal("", disk.DriveLetter);
        Assert.False(disk.HasVerifiedSystemVolumeMetrics);
    }

    [Fact]
    public void UsbAndNoMountedVolumeDoNotInheritSystemLetter()
    {
        var result = DiskTopologyMapper.Map(new[]
        {
            new PhysicalDiskRecord
            {
                Name = "USB enclosure",
                Interface = "USB",
                AssociationsAvailable = true,
                Volumes = Array.Empty<LogicalVolumeRecord>()
            },
            Disk("NVMe WD PC SN560", "NVMe", true, Vol("C:", 300, 200))
        }, "C:");

        Assert.False(result[0].IsSystemDisk);
        Assert.True(result[1].IsSystemDisk);
    }

    [Fact]
    public void MissingSystemRootNeverPicksFirstDisk()
    {
        var records = new[]
        {
            Disk("Disk 0", "SATA", true, Vol("C:", 300, 120)),
            Disk("Disk 1", "SATA", true, Vol("D:", 500, 80))
        };
        foreach (var root in new[] { null, "", "Invalid", @"C:\Windows", @"\\.\C:" })
        {
            var disks = DiskTopologyMapper.Map(records, root);
            Assert.All(disks, d => Assert.False(d.IsSystemDisk));
        }
    }

    [Fact]
    public void WindowsInstalledOnNonCLetter_SelectsVerifiedSystemVolume()
    {
        var disks = DiskTopologyMapper.Map(new[]
        {
            Disk("Disk C", "SATA", true, Vol("C:", 300, 120)),
            Disk("Disk E", "NVMe", true, Vol("E:", 500, 80))
        }, "e:\\");

        Assert.False(disks[0].IsSystemDisk);
        Assert.True(disks[1].IsSystemDisk);
        Assert.Equal("E:", disks[1].SystemVolume?.DriveLetter);
    }

    [Theory]
    [InlineData("C:", "C:")]
    [InlineData("c:\\", "C:")]
    [InlineData("E:/", "E:")]
    [InlineData("C:\\Windows", null)]
    [InlineData(@"\\?\Volume{ID}\", null)]
    [InlineData(@"\\.\PHYSICALDRIVE0", null)]
    [InlineData("CD:", null)]
    [InlineData("", null)]
    public void DriveLetterNormalization_StrictNotFuzzy(string input, string? expected)
    {
        Assert.Equal(expected, DiskTopologyMapper.NormalizeDriveLetter(input));
    }

    [Fact]
    public void ZeroFreeBytesKnownMeansRealFullVolume()
    {
        var disk = Assert.Single(DiskTopologyMapper.Map(new[]
        {
            Disk("Disk", "SATA", true, Vol("C:", 300, 0))
        }, "C:"));
        Assert.True(disk.HasVerifiedSystemVolumeMetrics);
        Assert.Equal(0, disk.SystemVolume!.FreeGB);
        Assert.Equal(0, disk.SystemVolume.FreePercent);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(300, null)]
    [InlineData(0, 0)]
    [InlineData(300, 301)]
    public void UnknownOrImpossibleCapacityCannotBeMistakenForFull(
        ulong? size, ulong? free)
    {
        var disk = Assert.Single(DiskTopologyMapper.Map(new[]
        {
            Disk("Disk", "SATA", true, Vol("C:", size, free))
        }, "C:"));
        Assert.False(disk.HasVerifiedSystemVolumeMetrics);
        Assert.Null(disk.SystemVolume!.FreeGB);
        Assert.Null(disk.SystemVolume.FreePercent);
    }

    [Fact]
    public void PartitionSizeNeverDividedByUnrelatedPhysicalDiskTotal()
    {
        var disk = Assert.Single(DiskTopologyMapper.Map(new[]
        {
            Disk("Physical 1000GiB", "SATA", true, Vol("C:", 100, 25))
        }, "C:"));
        Assert.Equal(1000, disk.TotalGB);
        Assert.Equal(25, disk.SystemVolume?.FreeGB);
        Assert.Equal(25, disk.SystemVolume?.FreePercent);
        Assert.Equal(75, disk.UsagePercent);
    }

    [Fact]
    public void NoPhysicalDisks_ReturnsEmptyWithoutInventingAHostDisk()
    {
        Assert.Empty(DiskTopologyMapper.Map(Array.Empty<PhysicalDiskRecord>(), "C:"));
    }
}
