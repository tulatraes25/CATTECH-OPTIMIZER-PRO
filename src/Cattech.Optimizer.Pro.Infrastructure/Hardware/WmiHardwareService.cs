using System.Management;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Hardware;

namespace Cattech.Optimizer.Pro.Infrastructure.Hardware;

/// <summary>
/// Implementación de IHardwareService usando WMI (Windows Management Instrumentation).
/// </summary>
public class WmiHardwareService : IHardwareService
{
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
    public Task<SystemInfo> GetSystemInfoAsync()
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

                if (obj["InstallDate"] is ManagementDateTime mdt)
                {
                    info.InstallDate = mdt.ToLocalTime();
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

        return Task.FromResult(info);
    }

    /// <inheritdoc/>
    public Task<CpuInfo> GetCpuInfoAsync()
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

        return Task.FromResult(info);
    }

    /// <inheritdoc/>
    public Task<MemoryInfo> GetMemoryInfoAsync()
    {
        var info = new MemoryInfo();

        try
        {
            // Obtener memoria total
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
            {
                foreach (var obj in searcher.Get())
                {
                    var totalBytes = Convert.ToDouble(obj["TotalPhysicalMemory"] ?? 0);
                    info.TotalGB = Math.Round(totalBytes / (1024 * 1024 * 1024), 2);
                    break;
                }
            }

            // Obtener memoria disponible
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
            {
                foreach (var obj in searcher.Get())
                {
                    var freeBytes = Convert.ToDouble(obj["FreePhysicalMemory"] ?? 0);
                    info.AvailableGB = Math.Round(freeBytes / (1024 * 1024), 2);
                    break;
                }
            }

            // Obtener información de slots
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory"))
            {
                var slots = searcher.Get();
                info.SlotsUsed = slots.Count;
            }

            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemoryArray"))
            {
                foreach (var obj in searcher.Get())
                {
                    info.SlotsTotal = Convert.ToInt32(obj["MemoryDevices"] ?? 0);
                    break;
                }
            }
        }
        catch
        {
            info.TotalGB = 0;
            info.AvailableGB = 0;
        }

        return Task.FromResult(info);
    }

    /// <inheritdoc/>
    public Task<List<GpuInfo>> GetGpuInfoAsync()
    {
        var gpus = new List<GpuInfo>();

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

                var adapterRam = obj["AdapterRAM"];
                if (adapterRam != null)
                {
                    gpu.MemoryGB = Math.Round(Convert.ToDouble(adapterRam) / (1024 * 1024 * 1024), 2);
                }

                gpus.Add(gpu);
            }
        }
        catch
        {
            // Retornar lista vacía en caso de error
        }

        return Task.FromResult(gpus);
    }

    /// <inheritdoc/>
    public Task<List<DiskInfo>> GetDiskInfoAsync()
    {
        var disks = new List<DiskInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            foreach (var obj in searcher.Get())
            {
                var disk = new DiskInfo
                {
                    Name = obj["Model"]?.ToString() ?? "Unknown",
                    Interface = obj["InterfaceType"]?.ToString() ?? "Unknown"
                };

                var size = obj["Size"];
                if (size != null)
                {
                    disk.TotalGB = Math.Round(Convert.ToDouble(size) / (1024 * 1024 * 1024), 2);
                }

                // Detectar tipo de medio
                var mediaType = obj["MediaType"]?.ToString() ?? "";
                disk.MediaType = mediaType.Contains("SSD") ? "SSD" : "HDD";

                disks.Add(disk);
            }

            // Asignar letras de unidad y espacio libre desde Win32_LogicalDisk
            using var logicalSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType=3");
            foreach (var obj in logicalSearcher.Get())
            {
                var letter = obj["DeviceID"]?.ToString() ?? "";
                var existingDisk = disks.FirstOrDefault(d => string.IsNullOrEmpty(d.DriveLetter));

                if (existingDisk != null)
                {
                    existingDisk.DriveLetter = letter;

                    var freeSpace = obj["FreeSpace"];
                    if (freeSpace != null)
                    {
                        existingDisk.FreeGB = Math.Round(Convert.ToDouble(freeSpace) / (1024 * 1024 * 1024), 2);
                    }
                }
            }
        }
        catch
        {
            // Retornar lista vacía en caso de error
        }

        return Task.FromResult(disks);
    }

    /// <inheritdoc/>
    public Task<MotherboardInfo> GetMotherboardInfoAsync()
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
                if (releaseDate is ManagementDateTime mdt)
                {
                    info.BiosDate = mdt.ToLocalTime();
                }

                break;
            }
        }
        catch
        {
            info.Manufacturer = "No detectado";
        }

        return Task.FromResult(info);
    }
}
