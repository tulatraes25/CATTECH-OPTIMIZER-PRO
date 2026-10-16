using System.Management;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Hardware;

namespace Cattech.Optimizer.Pro.Infrastructure.Hardware;

/// <summary>
/// Implementación de IHardwareService usando WMI (Windows Management Instrumentation).
/// </summary>
public class WmiHardwareService : IHardwareService
{
    private readonly IWmiMemoryReader _memoryReader;
    private readonly IDiskTopologyReader _diskTopologyReader;

    public WmiHardwareService()
        : this(new WmiMemoryReader(), new WmiDiskTopologyReader())
    {
    }

    internal WmiHardwareService(IWmiMemoryReader memoryReader)
        : this(memoryReader, new WmiDiskTopologyReader())
    {
    }

    internal WmiHardwareService(
        IWmiMemoryReader memoryReader, IDiskTopologyReader diskTopologyReader)
    {
        _memoryReader = memoryReader ?? throw new ArgumentNullException(nameof(memoryReader));
        _diskTopologyReader = diskTopologyReader ??
            throw new ArgumentNullException(nameof(diskTopologyReader));
    }
    /// <inheritdoc/>
    public async Task<HardwareReport> GetHardwareReportAsync()
    {
        var report = new HardwareReport
        {
            ReportDate = DateTime.Now
        };

        try
        {
            report.System = await GetSystemInfoAsync();
            report.Cpu = await GetCpuInfoAsync();
            report.Memory = await GetMemoryInfoAsync();
            report.Gpus = await GetGpuInfoAsync();
            report.Disks = await GetDiskInfoAsync();
            report.Motherboard = await GetMotherboardInfoAsync();
        }
        catch (Exception)
        {
            // En caso de error, retornar lo que se pudo obtener
        }

        return report;
    }

    /// <inheritdoc/>
    public Task<SystemInfo> GetSystemInfoAsync() => Task.Run(ReadSystemInfo);

    // All blocking WMI/DXGI reads execute off the UI dispatcher thread.
    private SystemInfo ReadSystemInfo()
    {
        var info = new SystemInfo();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                info.OsName = obj["Caption"]?.ToString() ?? "Unknown";
                info.OsVersion = obj["Version"]?.ToString() ?? "Unknown";
                info.BuildNumber = obj["BuildNumber"]?.ToString() ?? "Unknown";
                info.ComputerName = obj["CSName"]?.ToString() ?? Environment.MachineName;

                if (obj["InstallDate"] is string installDateStr)
                {
                    try
                    {
                        info.InstallDate = ManagementDateTimeConverter.ToDateTime(installDateStr);
                    }
                    catch
                    {
                        // Date format not recognized
                    }
                }

                break;
            }

            // Detectar arquitectura
            info.Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";
        }
        catch
        {
            info.OsName = Environment.OSVersion.ToString();
            info.ComputerName = Environment.MachineName;
        }

        return info;
    }

    /// <inheritdoc/>
    public Task<CpuInfo> GetCpuInfoAsync() => Task.Run(ReadCpuInfo);

    // All blocking WMI/DXGI reads execute off the UI dispatcher thread.
    private CpuInfo ReadCpuInfo()
    {
        var info = new CpuInfo();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                info.Name = obj["Name"]?.ToString() ?? "Unknown";
                info.Manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown";
                info.Cores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                info.Threads = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);

                var speed = obj["MaxClockSpeed"];
                if (speed != null)
                {
                    info.BaseSpeedGHz = Math.Round(Convert.ToDouble(speed) / 1000, 2);
                }

                break;
            }
        }
        catch
        {
            info.Name = "No detectado";
        }

        return info;
    }

    /// <inheritdoc/>
    public Task<MemoryInfo> GetMemoryInfoAsync() => Task.Run(ReadMemoryInfo);

    // All blocking WMI/DXGI reads execute off the UI dispatcher thread.
    private MemoryInfo ReadMemoryInfo()
    {
        var info = new MemoryInfo();

        try
        {
            var snapshot = _memoryReader.Read();

            if (snapshot.TotalPhysicalMemoryBytes.HasValue)
            {
                info.TotalGB = Math.Round((double)snapshot.TotalPhysicalMemoryBytes.Value / (1024 * 1024 * 1024), 2);
            }

            if (snapshot.FreePhysicalMemoryKilobytes.HasValue)
            {
                info.AvailableGB = Math.Round((double)snapshot.FreePhysicalMemoryKilobytes.Value / (1024 * 1024), 2);
            }

            BuildModuleInventory(snapshot, info);
            BuildSlotSummary(snapshot, info);
            BuildSummaryType(snapshot, info);
            BuildSummarySpeed(snapshot, info);
        }
        catch
        {
            info.TotalGB = 0;
            info.AvailableGB = 0;
        }

        return info;
    }

    private static void BuildModuleInventory(WmiMemorySnapshot snapshot, MemoryInfo info)
    {
        foreach (var module in snapshot.Modules)
        {
            info.Modules.Add(new MemoryModuleInfo
            {
                DeviceLocator = TrimOrEmpty(module.DeviceLocator),
                BankLabel = TrimOrEmpty(module.BankLabel),
                Manufacturer = TrimOrEmpty(module.Manufacturer),
                PartNumber = TrimOrEmpty(module.PartNumber),
                SerialNumber = TrimOrEmpty(module.SerialNumber),
                CapacityBytes = module.CapacityBytes,
                ConfiguredClockSpeedMHz = module.ConfiguredClockSpeedMHz is > 0 ? module.ConfiguredClockSpeedMHz : null,
                SMBIOSMemoryTypeCode = module.SMBIOSMemoryTypeCode,
                MemoryType = MapSmbiosMemoryType(module.SMBIOSMemoryTypeCode),
                DataWidthBits = module.DataWidthBits,
                TotalWidthBits = module.TotalWidthBits,
                Rank = module.Attributes is > 0 ? module.Attributes : null
            });
        }

        // Slots usados: únicamente módulos con capacidad válida
        info.SlotsUsed = snapshot.Modules.Count(m => m.CapacityBytes is > 0);
    }

    private static void BuildSlotSummary(WmiMemorySnapshot snapshot, MemoryInfo info)
    {
        // Sumar MemoryDevices de todos los arrays de memoria de sistema (Use == 3)
        info.SlotsTotal = snapshot.Arrays
            .Where(a => a.Use == 3)
            .Sum(a => (int)a.MemoryDevices);
    }

    private static void BuildSummaryType(WmiMemorySnapshot snapshot, MemoryInfo info)
    {
        var recognized = info.Modules
            .Select(m => m.MemoryType)
            .Where(t => t != "Desconocida")
            .Distinct()
            .ToList();

        info.Type = recognized.Count switch
        {
            0 => "Desconocida",
            1 => recognized[0],
            _ => "Mixta"
        };
    }

    private static void BuildSummarySpeed(WmiMemorySnapshot snapshot, MemoryInfo info)
    {
        var validSpeeds = info.Modules
            .Select(m => m.ConfiguredClockSpeedMHz)
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .Distinct()
            .ToList();

        info.SpeedMHz = validSpeeds.Count == 1 ? (int)validSpeeds[0] : 0;
    }

    private static string MapSmbiosMemoryType(uint? code)
    {
        if (!code.HasValue)
        {
            return "Desconocida";
        }

        return code.Value switch
        {
            18 => "DDR",
            19 => "DDR2",
            24 => "DDR3",
            26 => "DDR4",
            27 => "LPDDR",
            28 => "LPDDR2",
            29 => "LPDDR3",
            30 => "LPDDR4",
            34 => "DDR5",
            35 => "LPDDR5",
            _ => "Desconocida"
        };
    }

    private static string TrimOrEmpty(string value) => value?.Trim() ?? string.Empty;

    /// <inheritdoc/>
    public Task<List<GpuInfo>> GetGpuInfoAsync() => Task.Run(ReadGpuInfo);

    // All blocking WMI/DXGI reads execute off the UI dispatcher thread.
    private List<GpuInfo> ReadGpuInfo()
    {
        var gpus = new List<GpuInfo>();

        // Obtener memoria dedicada vía DXGI (fuente fiable, sin limitación uint32)
        List<DxgiGpuMemoryReader.DxgiAdapterInfo> dxgiAdapters;
        try
        {
            dxgiAdapters = DxgiGpuMemoryReader.EnumerateAdapters();
        }
        catch
        {
            dxgiAdapters = new List<DxgiGpuMemoryReader.DxgiAdapterInfo>();
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                var gpu = new GpuInfo
                {
                    Name = obj["Name"]?.ToString() ?? "Unknown",
                    Manufacturer = obj["AdapterCompatibility"]?.ToString() ?? "Unknown"
                };

                // Buscar adaptador DXGI correspondiente por VendorId+DeviceId
                var pnpDeviceId = obj["PNPDeviceID"]?.ToString() ?? "";
                var matchedAdapter = MatchDxgiAdapter(dxgiAdapters, pnpDeviceId, gpu.Name);

                if (matchedAdapter != null && !matchedAdapter.IsSoftware)
                {
                    // DXGI DedicatedVideoMemory es la fuente autoritativa
                    gpu.MemoryGB = Math.Round(matchedAdapter.DedicatedVideoMemoryBytes / (1024.0 * 1024.0 * 1024.0), 2);
                }
                else
                {
                    // Sin DXGI o adaptador software: MemoryGB queda 0 (UI lo muestra como N/D)
                    gpu.MemoryGB = 0;
                }

                gpus.Add(gpu);
            }
        }
        catch
        {
            // Retornar lista vacía en caso de error
        }

        return gpus;
    }

    /// <summary>
    /// Correlaciona un adaptador DXGI con un controlador WMI por VendorId+DeviceId.
    /// </summary>
    private static DxgiGpuMemoryReader.DxgiAdapterInfo? MatchDxgiAdapter(
        List<DxgiGpuMemoryReader.DxgiAdapterInfo> adapters,
        string pnpDeviceId,
        string gpuName)
    {
        // Intentar extraer VendorId y DeviceId del PNPDeviceID (formato: ...\VEN_xxxx&DEV_xxxx...)
        uint? wmiVendorId = null;
        uint? wmiDeviceId = null;

        var venMatch = System.Text.RegularExpressions.Regex.Match(pnpDeviceId, @"VEN_([0-9A-Fa-f]{4})");
        if (venMatch.Success) wmiVendorId = Convert.ToUInt32(venMatch.Groups[1].Value, 16);

        var devMatch = System.Text.RegularExpressions.Regex.Match(pnpDeviceId, @"DEV_([0-9A-Fa-f]{4})");
        if (devMatch.Success) wmiDeviceId = Convert.ToUInt32(devMatch.Groups[1].Value, 16);

        // Correlación primaria: VendorId + DeviceId
        if (wmiVendorId.HasValue && wmiDeviceId.HasValue)
        {
            var match = adapters.FirstOrDefault(a =>
                a.VendorId == wmiVendorId.Value &&
                a.DeviceId == wmiDeviceId.Value &&
                !a.IsSoftware);
            if (match != null) return match;
        }

        // Correlación fallback: nombre normalizado (solo si hay exactamente un candidato no-software)
        var normalizedGpuName = gpuName.Trim().ToLowerInvariant();
        var candidates = adapters
            .Where(a => !a.IsSoftware)
            .Where(a => a.Description.Trim().ToLowerInvariant().Contains(normalizedGpuName)
                     || normalizedGpuName.Contains(a.Description.Trim().ToLowerInvariant()))
            .ToList();

        return candidates.Count == 1 ? candidates[0] : null;
    }

    /// <inheritdoc/>
    public Task<List<DiskInfo>> GetDiskInfoAsync() => Task.Run(ReadDiskInfo);

    // All blocking WMI/DXGI reads execute off the UI dispatcher thread.
    private List<DiskInfo> ReadDiskInfo()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return new List<DiskInfo>();

            var snapshots = _diskTopologyReader.Read();
            // The actual Windows installation root decides which volume is
            // the system volume, NEVER the first disk returned by WMI.
            var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
            var result = DiskTopologyMapper.Map(snapshots, systemRoot);
            return result;
        }
        catch (Exception ex) when (ex is ManagementException
            or System.Runtime.InteropServices.COMException
            or UnauthorizedAccessException or System.Security.SecurityException
            or IOException or InvalidOperationException)
        {
            // Unable to establish trustworthy relations: return no inferred
            // physical/logical mapping. Do not fabricate a full disk.
            return new List<DiskInfo>();
        }
    }

    /// <inheritdoc/>
    public Task<MotherboardInfo> GetMotherboardInfoAsync() => Task.Run(ReadMotherboardInfo);

    // All blocking WMI/DXGI reads execute off the UI dispatcher thread.
    private MotherboardInfo ReadMotherboardInfo()
    {
        var info = new MotherboardInfo();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (var obj in searcher.Get())
            {
                info.Manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown";
                info.Model = obj["Product"]?.ToString() ?? "Unknown";
                break;
            }

            using var biosSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            foreach (var obj in biosSearcher.Get())
            {
                info.BiosVersion = obj["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown";

                var releaseDate = obj["ReleaseDate"];
                if (releaseDate is string releaseDateStr)
                {
                    try
                    {
                        info.BiosDate = ManagementDateTimeConverter.ToDateTime(releaseDateStr);
                    }
                    catch
                    {
                        // Date format not recognized
                    }
                }

                break;
            }
        }
        catch
        {
            info.Manufacturer = "No detectado";
        }

        return info;
    }
}
