using System.Globalization;
using System.Management;

namespace Cattech.Optimizer.Pro.Infrastructure.Hardware;

/// <summary>
/// WMI records stay internal. Device identifiers, serials and WMI object paths
/// must not be persisted into diagnostics or written to application logs.
/// </summary>
internal sealed class PhysicalDiskRecord
{
    public string Name { get; init; } = string.Empty;
    public string Interface { get; init; } = string.Empty;
    public string MediaType { get; init; } = string.Empty;
    public ulong? CapacityBytes { get; init; }
    public bool AssociationsAvailable { get; init; }
    public IReadOnlyList<LogicalVolumeRecord> Volumes { get; init; } = Array.Empty<LogicalVolumeRecord>();
}

internal sealed class LogicalVolumeRecord
{
    public string DriveLetter { get; init; } = string.Empty;
    public ulong? CapacityBytes { get; init; }
    public ulong? FreeBytes { get; init; }
}

internal interface IDiskTopologyReader
{
    IReadOnlyList<PhysicalDiskRecord> Read();
}

/// <summary>
/// Walks the actual WMI association objects:
/// Win32_DiskDrive -> Win32_DiskDriveToDiskPartition -> Win32_DiskPartition
/// -> Win32_LogicalDiskToPartition -> Win32_LogicalDisk.
/// No positional, model-name, drive-number or substring matching.
/// </summary>
internal sealed class WmiDiskTopologyReader : IDiskTopologyReader
{
    public IReadOnlyList<PhysicalDiskRecord> Read()
    {
        var records = new List<PhysicalDiskRecord>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT DeviceID,Model,InterfaceType,MediaType,Size FROM Win32_DiskDrive");
        using var physicalDisks = searcher.Get();
        foreach (ManagementObject disk in physicalDisks)
        {
            using (disk)
            {
                var volumes = new List<LogicalVolumeRecord>();
                var associationOk = true;

                try
                {
                    // GetRelated with explicit AssocClass, not all related objects.
                    using var partitions = disk.GetRelated(
                        "Win32_DiskPartition", "Win32_DiskDriveToDiskPartition",
                        null!, null!, null!, null!, false, null!);
                    foreach (ManagementObject partition in partitions)
                    {
                        using (partition)
                        {
                            using var logicals = partition.GetRelated(
                                "Win32_LogicalDisk", "Win32_LogicalDiskToPartition",
                                null!, null!, null!, null!, false, null!);
                            foreach (ManagementObject logical in logicals)
                            {
                                using (logical)
                                {
                                    // Fixed local volumes only. All other kinds
                                    // remain unknown, never attached arbitrarily.
                                    if (ParseInt(logical["DriveType"]) != 3)
                                        continue;

                                    volumes.Add(new LogicalVolumeRecord
                                    {
                                        DriveLetter = logical["DeviceID"]?.ToString() ?? "",
                                        CapacityBytes = ParseUInt64(logical["Size"]),
                                        FreeBytes = ParseUInt64(logical["FreeSpace"])
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is ManagementException
                    or UnauthorizedAccessException or System.Runtime.InteropServices.COMException
                    or InvalidOperationException or System.Security.SecurityException)
                {
                    // Fail closed for THIS physical drive. Partial mapping
                    // is not trustworthy. Do not attach incomplete data.
                    volumes.Clear();
                    associationOk = false;
                }

                records.Add(new PhysicalDiskRecord
                {
                    Name = disk["Model"]?.ToString() ?? "Unknown",
                    Interface = disk["InterfaceType"]?.ToString() ?? "",
                    MediaType = disk["MediaType"]?.ToString() ?? "",
                    CapacityBytes = ParseUInt64(disk["Size"]),
                    AssociationsAvailable = associationOk,
                    Volumes = volumes.ToArray()
                });
            }
        }

        return records;
    }

    private static int? ParseInt(object? value)
        => int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result : null;

    private static ulong? ParseUInt64(object? value)
        => ulong.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result : null;
}
