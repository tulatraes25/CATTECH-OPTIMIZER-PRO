using System.Management;

namespace Cattech.Optimizer.Pro.Infrastructure.Hardware;

/// <summary>
/// Módulo de memoria física tal como lo expone WMI (Win32_PhysicalMemory).
/// </summary>
internal sealed class WmiPhysicalMemoryModule
{
    public string DeviceLocator { get; set; } = string.Empty;
    public string BankLabel { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public ulong? CapacityBytes { get; set; }
    public uint? ConfiguredClockSpeedMHz { get; set; }
    public uint? SMBIOSMemoryTypeCode { get; set; }
    public ushort? DataWidthBits { get; set; }
    public ushort? TotalWidthBits { get; set; }
    public uint? Attributes { get; set; }
}

/// <summary>
/// Array de memoria física (Win32_PhysicalMemoryArray).
/// </summary>
internal sealed class WmiPhysicalMemoryArray
{
    public uint MemoryDevices { get; set; }
    public ushort? Use { get; set; }
}

/// <summary>
/// Snapshot de memoria física leído desde WMI.
/// </summary>
internal sealed class WmiMemorySnapshot
{
    public ulong? TotalPhysicalMemoryBytes { get; set; }
    public ulong? FreePhysicalMemoryKilobytes { get; set; }
    public List<WmiPhysicalMemoryModule> Modules { get; set; } = new();
    public List<WmiPhysicalMemoryArray> Arrays { get; set; } = new();
}

/// <summary>
/// Abstracción del lector WMI de memoria. Permite simular datos en tests.
/// </summary>
internal interface IWmiMemoryReader
{
    WmiMemorySnapshot Read();
}

/// <summary>
/// Lector real de memoria mediante ManagementObjectSearcher (namespace root\CIMV2).
/// Solo consulta los campos requeridos.
/// </summary>
internal sealed class WmiMemoryReader : IWmiMemoryReader
{
    private const string MemoryNamespace = @"root\CIMV2";

    public WmiMemorySnapshot Read()
    {
        var snapshot = new WmiMemorySnapshot();

        using (var searcher = new ManagementObjectSearcher(MemoryNamespace,
                   "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
        {
            foreach (var obj in searcher.Get())
            {
                snapshot.TotalPhysicalMemoryBytes = ToUInt64(obj["TotalPhysicalMemory"]);
                break;
            }
        }

        using (var searcher = new ManagementObjectSearcher(MemoryNamespace,
                   "SELECT FreePhysicalMemory FROM Win32_OperatingSystem"))
        {
            foreach (var obj in searcher.Get())
            {
                snapshot.FreePhysicalMemoryKilobytes = ToUInt64(obj["FreePhysicalMemory"]);
                break;
            }
        }

        using (var searcher = new ManagementObjectSearcher(MemoryNamespace,
                   "SELECT DeviceLocator, BankLabel, Capacity, Manufacturer, PartNumber, " +
                   "SerialNumber, ConfiguredClockSpeed, SMBIOSMemoryType, DataWidth, " +
                   "TotalWidth, Attributes FROM Win32_PhysicalMemory"))
        {
            foreach (var obj in searcher.Get())
            {
                snapshot.Modules.Add(new WmiPhysicalMemoryModule
                {
                    DeviceLocator = ToString(obj["DeviceLocator"]),
                    BankLabel = ToString(obj["BankLabel"]),
                    Manufacturer = ToString(obj["Manufacturer"]),
                    PartNumber = ToString(obj["PartNumber"]),
                    SerialNumber = ToString(obj["SerialNumber"]),
                    CapacityBytes = ToUInt64(obj["Capacity"]),
                    ConfiguredClockSpeedMHz = ToUInt32(obj["ConfiguredClockSpeed"]),
                    SMBIOSMemoryTypeCode = ToUInt32(obj["SMBIOSMemoryType"]),
                    DataWidthBits = ToUInt16(obj["DataWidth"]),
                    TotalWidthBits = ToUInt16(obj["TotalWidth"]),
                    Attributes = ToUInt32(obj["Attributes"])
                });
            }
        }

        using (var searcher = new ManagementObjectSearcher(MemoryNamespace,
                   "SELECT MemoryDevices, Use FROM Win32_PhysicalMemoryArray"))
        {
            foreach (var obj in searcher.Get())
            {
                snapshot.Arrays.Add(new WmiPhysicalMemoryArray
                {
                    MemoryDevices = ToUInt32(obj["MemoryDevices"]) ?? 0,
                    Use = ToUInt16(obj["Use"])
                });
            }
        }

        return snapshot;
    }

    private static string ToString(object? value) => value?.ToString() ?? string.Empty;

    private static ulong? ToUInt64(object? value)
    {
        if (value == null)
        {
            return null;
        }

        try
        {
            return Convert.ToUInt64(value);
        }
        catch
        {
            return null;
        }
    }

    private static uint? ToUInt32(object? value)
    {
        if (value == null)
        {
            return null;
        }

        try
        {
            return Convert.ToUInt32(value);
        }
        catch
        {
            return null;
        }
    }

    private static ushort? ToUInt16(object? value)
    {
        if (value == null)
        {
            return null;
        }

        try
        {
            return Convert.ToUInt16(value);
        }
        catch
        {
            return null;
        }
    }
}
