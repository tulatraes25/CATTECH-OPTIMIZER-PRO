using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Hardware;

namespace Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

/// <summary>
/// Implementación de IDiagnosticService.
/// Ejecuta diagnósticos no invasivos del sistema usando WMI, Registry y FileSystem.
/// No modifica nada en el sistema.
/// </summary>
public class DiagnosticService : IDiagnosticService
{
    private readonly IHardwareService _hardwareService;
    private readonly string _diagnosticsDirectory;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    public DiagnosticService(IHardwareService hardwareService, string? baseDirectory = null)
    {
        _hardwareService = hardwareService;
        _diagnosticsDirectory = Path.Combine(
            baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory,
            "data",
            "diagnostics");
    }

    /// <inheritdoc/>
    public async Task<DiagnosticReport> RunQuickDiagnosticAsync(IProgress<int>? progress = null)
    {
        var report = new DiagnosticReport();

        // 1. Hardware (10%)
        progress?.Report(5);
        var hw = await _hardwareService.GetHardwareReportAsync();
        FillSystemInfo(report, hw);
        FillHardwareInfo(report, hw);
        FillDiskInfo(report, hw);
        progress?.Report(15);

        // 2. Programas de inicio (20%)
        report.Startup = await GetStartupInfoAsync();
        progress?.Report(35);

        // 3. Temporales (20%)
        report.TempFiles = await GetTempFilesInfoAsync();
        progress?.Report(55);

        // 4. Seguridad (15%)
        report.Security = await GetSecurityInfoAsync();
        progress?.Report(70);

        // 5. Memoria virtual (10%)
        report.VirtualMemory = GetVirtualMemoryInfo();
        progress?.Report(80);

        // 6. Generar alertas (10%)
        report.Alerts = GenerateAlerts(report);
        progress?.Report(95);

        // 7. Técnico (si hay configuración)
        try
        {
            var settingsService = new Data.JsonSettingsService();
            var settings = await settingsService.LoadSettingsAsync();
            report.TechnicianName = settings.Company.TechnicianName;
        }
        catch
        {
            // No es crítico
        }

        progress?.Report(100);
        return report;
    }

    /// <inheritdoc/>
    public async Task<string> SaveDiagnosticAsync(DiagnosticReport report)
    {
        EnsureDirectoryExists();

        var fileName = $"diagnostic-{report.DiagnosisDate:yyyyMMdd-HHmmss}.json";
        var filePath = Path.Combine(_diagnosticsDirectory, fileName);

        if (File.Exists(filePath))
        {
            fileName = $"diagnostic-{report.DiagnosisDate:yyyyMMdd-HHmmss}-{report.Id}.json";
            filePath = Path.Combine(_diagnosticsDirectory, fileName);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(report, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json);

        return fileName;
    }

    /// <inheritdoc/>
    public async Task<DiagnosticReport?> LoadDiagnosticAsync(string diagnosticId)
    {
        if (!Directory.Exists(_diagnosticsDirectory))
            return null;

        var files = Directory.GetFiles(_diagnosticsDirectory, "diagnostic-*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var report = System.Text.Json.JsonSerializer.Deserialize<DiagnosticReport>(json, SerializerOptions);

                if (report?.Id == diagnosticId)
                    return report;
            }
            catch
            {
                // Saltar archivos corruptos
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<List<DiagnosticSummary>> ListDiagnosticsAsync(int maxResults = 20)
    {
        var summaries = new List<DiagnosticSummary>();

        if (!Directory.Exists(_diagnosticsDirectory))
            return summaries;

        var files = Directory.GetFiles(_diagnosticsDirectory, "diagnostic-*.json")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .Take(maxResults);

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var report = System.Text.Json.JsonSerializer.Deserialize<DiagnosticReport>(json, SerializerOptions);

                if (report != null)
                {
                    summaries.Add(new DiagnosticSummary
                    {
                        Id = report.Id,
                        DiagnosisDate = report.DiagnosisDate,
                        ComputerName = report.ComputerName,
                        OsName = report.OsName,
                        AlertCount = report.Alerts.Count,
                        CriticalAlertCount = report.Alerts.Count(a => a.Severity == AlertSeverity.Critical),
                        FileName = Path.GetFileName(file)
                    });
                }
            }
            catch
            {
                // Saltar archivos corruptos
            }
        }

        return summaries;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteDiagnosticAsync(string diagnosticId)
    {
        if (!Directory.Exists(_diagnosticsDirectory))
            return false;

        var files = Directory.GetFiles(_diagnosticsDirectory, "diagnostic-*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var report = System.Text.Json.JsonSerializer.Deserialize<DiagnosticReport>(json, SerializerOptions);

                if (report?.Id == diagnosticId)
                {
                    File.Delete(file);
                    return true;
                }
            }
            catch
            {
                // Saltar archivos corruptos
            }
        }

        return false;
    }

    // --- Métodos privados de recolección ---

    private static void FillSystemInfo(DiagnosticReport report, HardwareReport hw)
    {
        report.OsName = hw.System.OsName;
        report.WindowsEdition = hw.System.OsVersion;
        report.Architecture = hw.System.Architecture;
        report.ComputerName = hw.System.ComputerName;
        report.CurrentUser = Environment.UserName;
    }

    private static void FillHardwareInfo(DiagnosticReport report, HardwareReport hw)
    {
        report.Processor = hw.Cpu.Name;
        report.CpuCores = hw.Cpu.Cores;
        report.RamTotalGB = hw.Memory.TotalGB;
        report.RamUsedGB = hw.Memory.UsedGB;
        report.RamAvailableGB = hw.Memory.AvailableGB;
        report.RamUsagePercent = Math.Round(hw.Memory.UsagePercent, 1);
    }

    private static void FillDiskInfo(DiagnosticReport report, HardwareReport hw)
    {
        var mainDisk = hw.Disks.FirstOrDefault();
        if (mainDisk != null)
        {
            report.PrimaryDiskName = mainDisk.Name;
            report.DiskType = !string.IsNullOrEmpty(mainDisk.MediaType) ? mainDisk.MediaType : "No detectado";
            report.DiskCapacityGB = mainDisk.TotalGB;
            report.DiskFreeGB = mainDisk.FreeGB;
            report.DiskFreePercent = mainDisk.TotalGB > 0
                ? Math.Round((mainDisk.FreeGB / mainDisk.TotalGB) * 100, 1)
                : 0;
        }
    }

    private static async Task<StartupInfo> GetStartupInfoAsync()
    {
        var info = new StartupInfo();

        try
        {
            // HKCU\Software\Microsoft\Windows\CurrentVersion\Run
            var userStartup = ReadRegistryStartup("HKCU");
            // HKLM\Software\Microsoft\Windows\CurrentVersion\Run
            var machineStartup = ReadRegistryStartup("HKLM");

            var allPrograms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            allPrograms.UnionWith(userStartup);
            allPrograms.UnionWith(machineStartup);

            // Startup folders
            var userStartupFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs\Startup");

            var commonStartupFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Microsoft\Windows\Start Menu\Programs\Startup");

            var folderPrograms = GetStartupFolderPrograms(userStartupFolder);
            folderPrograms.AddRange(GetStartupFolderPrograms(commonStartupFolder));

            foreach (var prog in folderPrograms)
            {
                allPrograms.Add(prog);
            }

            info.ProgramNames = allPrograms.ToList();
            info.TotalCount = allPrograms.Count;

            // Estimar third party (no contiene "microsoft" en el nombre)
            info.ThirdPartyCount = allPrograms.Count(p =>
                !p.Contains("microsoft", StringComparison.OrdinalIgnoreCase) &&
                !p.Contains("ms-", StringComparison.OrdinalIgnoreCase) &&
                !p.Contains("windows", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            // No crítico
        }

        return await Task.FromResult(info);
    }

    private static List<string> ReadRegistryStartup(string hive)
    {
        var programs = new List<string>();

        try
        {
            var keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            RegistryKey? key;

            if (hive == "HKCU")
                key = Registry.CurrentUser.OpenSubKey(keyPath);
            else
                key = Registry.LocalMachine.OpenSubKey(keyPath);

            if (key != null)
            {
                foreach (var valueName in key.GetValueNames())
                {
                    if (!string.IsNullOrWhiteSpace(valueName))
                        programs.Add(valueName);
                }
                key.Close();
            }
        }
        catch
        {
            // No crítico
        }

        return programs;
    }

    private static List<string> GetStartupFolderPrograms(string folderPath)
    {
        var programs = new List<string>();

        try
        {
            if (Directory.Exists(folderPath))
            {
                var files = Directory.GetFiles(folderPath);
                foreach (var file in files)
                {
                    programs.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
        }
        catch
        {
            // No crítico
        }

        return programs;
    }

    private static async Task<TempFilesInfo> GetTempFilesInfoAsync()
    {
        var info = new TempFilesInfo();

        var locations = new List<string>
        {
            Path.GetTempPath(),                          // %TEMP% del usuario
            Path.Combine(Path.GetTempPath(), ".."),       // Intento de Windows\Temp
            @"C:\Windows\Temp"
        };

        // Agregar solo carpetas que existen y son distintas
        var uniqueLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var loc in locations)
        {
            try
            {
                var fullPath = Path.GetFullPath(loc);
                if (Directory.Exists(fullPath))
                    uniqueLocations.Add(fullPath);
            }
            catch { }
        }

        foreach (var location in uniqueLocations)
        {
            try
            {
                var detail = await CalculateFolderSizeAsync(location);
                if (detail.SizeBytes > 0)
                {
                    info.Locations.Add(detail);
                    info.TotalSizeBytes += detail.SizeBytes;
                    info.FileCount += detail.FileCount;
                    info.FolderCount++;
                }
            }
            catch
            {
                // No crítico, skip
            }
        }

        return info;
    }

    private static async Task<TempLocationDetail> CalculateFolderSizeAsync(string path)
    {
        return await Task.Run(() =>
        {
            var detail = new TempLocationDetail { Path = path };
            var sw = Stopwatch.StartNew();

            try
            {
                var dirInfo = new DirectoryInfo(path);
                var files = dirInfo.GetFiles("*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    MaxRecursionDepth = 3
                });

                foreach (var file in files)
                {
                    try
                    {
                        detail.SizeBytes += file.Length;
                        detail.FileCount++;
                    }
                    catch { }

                    // Timeout de seguridad: no más de 3 segundos por carpeta
                    if (sw.ElapsedMilliseconds > 3000)
                        break;
                }
            }
            catch { }

            sw.Stop();
            return detail;
        });
    }

    private static async Task<SecurityInfo> GetSecurityInfoAsync()
    {
        var info = new SecurityInfo();

        // Antivirus via WMI
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM AntiVirusProduct");
            foreach (var obj in searcher.Get())
            {
                var name = obj["displayName"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    info.AntivirusName = name;

                    // productState: 266240 = inactive, 262144 = active (aprox)
                    var state = obj["productState"]?.ToString();
                    if (!string.IsNullOrEmpty(state))
                    {
                        var stateInt = Convert.ToInt32(state);
                        info.AntivirusActive = (stateInt & 0xF000) == 0x1000;
                    }
                    else
                    {
                        info.AntivirusActive = true; // Asumir activo si no se puede determinar
                    }

                    break; // Tomar el primero
                }
            }
        }
        catch
        {
            info.AntivirusName = "No detectado";
        }

        // Firewall via WMI
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM FirewallProduct");
            foreach (var obj in searcher.Get())
            {
                info.FirewallActive = true;
                break;
            }
        }
        catch
        {
            // No se pudo determinar
        }

        // Windows Update status
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_Service WHERE Name='wuauserv'");
            foreach (var obj in searcher.Get())
            {
                var state = obj["State"]?.ToString();
                info.WindowsUpdateStatus = state == "Running" ? "Servicio activo" :
                                           state == "Stopped" ? "Servicio detenido" :
                                           $"Estado: {state}";
                break;
            }
        }
        catch
        {
            info.WindowsUpdateStatus = "No determinado";
        }

        return await Task.FromResult(info);
    }

    private static VirtualMemoryInfo GetVirtualMemoryInfo()
    {
        var info = new VirtualMemoryInfo();

        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_PageFileUsage");
            foreach (var obj in searcher.Get())
            {
                var allocated = obj["AllocatedBaseSize"];
                if (allocated != null)
                {
                    info.PagingFileSizeGB = Math.Round(Convert.ToDouble(allocated) / 1024, 2);
                }

                var caption = obj["Caption"]?.ToString();
                info.Location = caption ?? "No determinado";
                break;
            }

            // Verificar si está auto-administrado
            using var sysSearcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_ComputerSystem");
            foreach (var obj in sysSearcher.Get())
            {
                info.IsAutoManaged = Convert.ToBoolean(obj["AutomaticManagedPagefile"] ?? true);
                break;
            }
        }
        catch
        {
            // No crítico
        }

        return info;
    }

    // --- Generación de alertas ---

    private static List<DiagnosticAlert> GenerateAlerts(DiagnosticReport report)
    {
        var alerts = new List<DiagnosticAlert>();

        // RAM
        if (report.RamTotalGB <= 4 && report.RamTotalGB > 0)
        {
            alerts.Add(new DiagnosticAlert
            {
                Severity = AlertSeverity.Warning,
                Category = "RAM",
                Message = $"RAM baja: {report.RamTotalGB} GB",
                Recommendation = "Se recomienda al menos 8 GB para uso general. Considerar agregar memoria."
            });
        }
        else if (report.RamTotalGB > 4 && report.RamTotalGB <= 8)
        {
            alerts.Add(new DiagnosticAlert
            {
                Severity = AlertSeverity.Info,
                Category = "RAM",
                Message = $"RAM justa: {report.RamTotalGB} GB",
                Recommendation = "Adecuado para uso básico. Para multitarea se recomienda 16 GB."
            });
        }

        // Disco
        if (report.DiskFreePercent > 0 && report.DiskFreePercent < 15)
        {
            alerts.Add(new DiagnosticAlert
            {
                Severity = AlertSeverity.Warning,
                Category = "Disco",
                Message = $"Poco espacio en disco: {report.DiskFreePercent}% libre ({report.DiskFreeGB:F1} GB)",
                Recommendation = "Liberar espacio o ampliar almacenamiento. Menos del 15% puede afectar rendimiento."
            });
        }

        if (report.DiskCapacityGB > 0 && report.DiskFreeGB <= 0)
        {
            alerts.Add(new DiagnosticAlert
            {
                Severity = AlertSeverity.Critical,
                Category = "Disco",
                Message = "Disco lleno: 0 GB libres",
                Recommendation = "El equipo podría dejar de funcionar correctamente. Liberar espacio urgentemente."
            });
        }

        if (report.DiskType.Contains("HDD", StringComparison.OrdinalIgnoreCase) ||
            report.DiskType == "No detectado")
        {
            alerts.Add(new DiagnosticAlert
            {
                Severity = AlertSeverity.Info,
                Category = "Disco",
                Message = $"Tipo de disco: {report.DiskType}",
                Recommendation = "Si usa HDD, considerar migrar a SSD para mejorar significativamente el rendimiento."
            });
        }

        // Programas de inicio
        if (report.Startup.TotalCount > 10)
        {
            alerts.Add(new DiagnosticAlert
            {
                Severity = AlertSeverity.Warning,
                Category = "Inicio",
                Message = $"Muchos programas al inicio: {report.Startup.TotalCount}",
                Recommendation = "Los programas de inicio ralentizan el arranque. Revisar y desactivar los innecesarios."
            });
        }

        if (report.Startup.ThirdPartyCount > 5)
        {
            alerts.Add(new DiagnosticAlert
            {
                Severity = AlertSeverity.Warning,
                Category = "Inicio",
                Message = $"{report.Startup.ThirdPartyCount} programas de terceros al inicio",
                Recommendation = "Programas de terceros suelen consumir recursos sin necesidad. Evaluar cuáles son esenciales."
            });
        }

        // Temporales
        if (report.TempFiles.TotalSizeGB > 2)
        {
            alerts.Add(new DiagnosticAlert
            {
                Severity = AlertSeverity.Warning,
                Category = "Temporales",
                Message = $"Archivos temporales acumulados: {report.TempFiles.TotalSizeGB} GB",
                Recommendation = "Limpiar archivos temporales puede liberar espacio y mejorar rendimiento."
            });
        }

        // Windows fuera de objetivo
        if (!string.IsNullOrEmpty(report.OsName) &&
            !report.OsName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase) &&
            !report.OsName.Contains("Windows 11", StringComparison.OrdinalIgnoreCase))
        {
            alerts.Add(new DiagnosticAlert
            {
                Severity = AlertSeverity.Info,
                Category = "Sistema",
                Message = $"Sistema operativo fuera del objetivo del MVP: {report.OsName}",
                Recommendation = "CATTECH Optimizer Pro está optimizado para Windows 10/11. Algunas funciones podrían no estar disponibles."
            });
        }

        // Seguridad - no detectado
        if (report.Security.AntivirusName == "No detectado")
        {
            alerts.Add(new DiagnosticAlert
            {
                Severity = AlertSeverity.Warning,
                Category = "Seguridad",
                Message = "No se detectó antivirus activo",
                Recommendation = "Verificar manualmente si hay protección antivirus instalada."
            });
        }

        if (!report.Security.FirewallActive)
        {
            alerts.Add(new DiagnosticAlert
            {
                Severity = AlertSeverity.Warning,
                Category = "Seguridad",
                Message = "Firewall de Windows no detectado o inactivo",
                Recommendation = "Verificar manualmente el estado del firewall."
            });
        }

        return alerts;
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_diagnosticsDirectory))
        {
            Directory.CreateDirectory(_diagnosticsDirectory);
        }
    }
}
