using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Startup;

namespace Cattech.Optimizer.Pro.Infrastructure.Startup;

/// <summary>
/// Implementación de IStartupService.
/// Analiza programas de inicio de forma no invasiva.
/// </summary>
public class StartupService : IStartupService
{
    private readonly string _analysisDirectory;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Prefijos y palabras clave para detectar Microsoft
    private static readonly string[] MicrosoftKeywords =
    [
        "microsoft", "ms-", "windows", "msn", "msedge", "msoffice",
        "onedrive", "teams", "office", "defender", "security",
        "cortana", "xbox", "surface", "halo"
    ];

    // Palabras clave para riesgo alto / revisar
    private static readonly string[] HighRiskPatterns =
    [
        @"\temp\", @"\tmp\", @"\appdata\local\temp",
        "powershell -enc", "cmd /c", "wscript", "cscript",
        "mshta", "regsvr32", "rundll32"
    ];

    // Programas conocidos como safe (mantener)
    private static readonly string[] SafePrograms =
    [
        "securityhealth", "windows defender", "cftmon", "ctfmon",
        "igfxtray", "hkcmd", "igfxpers", "nvtray", "nvmctray",
        "rthdvcpl", "rtkwmaudio", "realtek", "synaptics",
        "elanservice", "lenovo", "hp", "dell", "audio"
    ];

    // Programas conocidos como posible desactivar
    private static readonly string[] PossibleDisablePatterns =
    [
        "update", "updater", "helper", "assistant", "launcher",
        "chat", "cloud", "sync", "backup", "telemetry",
        "reporter", "crash", "error", "optimizer"
    ];

    public StartupService(string? baseDirectory = null)
    {
        _analysisDirectory = Path.Combine(
            baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory,
            "data",
            "startup-analysis");
    }

    /// <inheritdoc/>
    public async Task<StartupAnalysis> AnalyzeStartupAsync()
    {
        var analysis = new StartupAnalysis();
        var entries = new List<StartupEntry>();
        var seenCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Registry: HKCU\Software\Microsoft\Windows\CurrentVersion\Run
        entries.AddRange(ReadRegistryEntries(
            "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run", StartupSourceType.RegistryRun));

        // 2. Registry: HKLM\Software\Microsoft\Windows\CurrentVersion\Run
        entries.AddRange(ReadRegistryEntries(
            "HKLM", @"Software\Microsoft\Windows\CurrentVersion\Run", StartupSourceType.RegistryRun));

        // 3. Registry: HKCU\Software\Microsoft\Windows\CurrentVersion\RunOnce
        entries.AddRange(ReadRegistryEntries(
            "HKCU", @"Software\Microsoft\Windows\CurrentVersion\RunOnce", StartupSourceType.RegistryRunOnce));

        // 4. Registry: HKLM\Software\Microsoft\Windows\CurrentVersion\RunOnce
        entries.AddRange(ReadRegistryEntries(
            "HKLM", @"Software\Microsoft\Windows\CurrentVersion\RunOnce", StartupSourceType.RegistryRunOnce));

        // 5. Startup folders
        var userStartupFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\Startup");
        entries.AddRange(ReadFolderEntries(userStartupFolder, StartupSourceType.StartupFolder));

        var commonStartupFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\Startup");
        entries.AddRange(ReadFolderEntries(commonStartupFolder, StartupSourceType.StartupFolderCommon));

        // 6. Tareas programadas comunes (solo lectura, inicio de sesión)
        entries.AddRange(await ReadScheduledTaskEntriesAsync());

        // Clasificar y evaluar cada entrada
        foreach (var entry in entries)
        {
            ClassifyEntry(entry);

            // Detectar duplicados
            var key = $"{entry.Name}|{entry.Command}";
            if (!seenCommands.Add(key))
            {
                entry.Notes += " [Duplicado]";
            }
        }

        analysis.Entries = entries;
        analysis.TechnicianName = await GetTechnicianNameAsync();

        return analysis;
    }

    /// <inheritdoc/>
    public async Task<string> SaveAnalysisAsync(StartupAnalysis analysis)
    {
        EnsureDirectoryExists();

        var fileName = $"startup-analysis-{analysis.AnalysisDate:yyyyMMdd-HHmmss}.json";
        var filePath = Path.Combine(_analysisDirectory, fileName);

        if (File.Exists(filePath))
        {
            fileName = $"startup-analysis-{analysis.AnalysisDate:yyyyMMdd-HHmmss}-{analysis.Id}.json";
            filePath = Path.Combine(_analysisDirectory, fileName);
        }

        var json = JsonSerializer.Serialize(analysis, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json);

        return fileName;
    }

    /// <inheritdoc/>
    public async Task<StartupAnalysis?> LoadAnalysisAsync(string analysisId)
    {
        if (!Directory.Exists(_analysisDirectory))
            return null;

        var files = Directory.GetFiles(_analysisDirectory, "startup-analysis-*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var analysis = JsonSerializer.Deserialize<StartupAnalysis>(json, SerializerOptions);

                if (analysis?.Id == analysisId)
                    return analysis;
            }
            catch { }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<List<StartupAnalysisSummary>> ListAnalysesAsync(int maxResults = 20)
    {
        var summaries = new List<StartupAnalysisSummary>();

        if (!Directory.Exists(_analysisDirectory))
            return summaries;

        var files = Directory.GetFiles(_analysisDirectory, "startup-analysis-*.json")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .Take(maxResults);

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var analysis = JsonSerializer.Deserialize<StartupAnalysis>(json, SerializerOptions);

                if (analysis != null)
                {
                    summaries.Add(new StartupAnalysisSummary
                    {
                        Id = analysis.Id,
                        AnalysisDate = analysis.AnalysisDate,
                        TotalEntries = analysis.TotalCount,
                        ThirdPartyCount = analysis.ThirdPartyCount,
                        AlertCount = analysis.AlertCount,
                        FileName = Path.GetFileName(file)
                    });
                }
            }
            catch { }
        }

        return summaries;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAnalysisAsync(string analysisId)
    {
        if (!Directory.Exists(_analysisDirectory))
            return false;

        var files = Directory.GetFiles(_analysisDirectory, "startup-analysis-*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var analysis = JsonSerializer.Deserialize<StartupAnalysis>(json, SerializerOptions);

                if (analysis?.Id == analysisId)
                {
                    File.Delete(file);
                    return true;
                }
            }
            catch { }
        }

        return false;
    }

    // --- Métodos privados de recolección ---

    private static List<StartupEntry> ReadRegistryEntries(string hive, string keyPath, StartupSourceType sourceType)
    {
        var entries = new List<StartupEntry>();

        try
        {
            RegistryKey? key = hive == "HKCU"
                ? Registry.CurrentUser.OpenSubKey(keyPath)
                : Registry.LocalMachine.OpenSubKey(keyPath);

            if (key == null) return entries;

            foreach (var valueName in key.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(valueName)) continue;

                var value = key.GetValue(valueName)?.ToString() ?? string.Empty;

                entries.Add(new StartupEntry
                {
                    Name = valueName,
                    Command = value,
                    Location = $"{hive}\\{keyPath}",
                    SourceType = sourceType,
                    OriginalRegistryValue = value,
                    Status = File.Exists(ExtractPathFromCommand(value))
                        ? StartupEntryStatus.Active
                        : StartupEntryStatus.PathNotFound
                });
            }

            key.Close();
        }
        catch
        {
            // No crítico
        }

        return entries;
    }

    private static List<StartupEntry> ReadFolderEntries(string folderPath, StartupSourceType sourceType)
    {
        var entries = new List<StartupEntry>();

        try
        {
            if (!Directory.Exists(folderPath)) return entries;

            var files = Directory.GetFiles(folderPath);
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var ext = Path.GetExtension(file).ToLowerInvariant();

                string command = ext switch
                {
                    ".lnk" => ResolveShortcutTarget(file) ?? file,
                    _ => file
                };

                entries.Add(new StartupEntry
                {
                    Name = name,
                    Command = command,
                    Location = file,
                    SourceType = sourceType,
                    OriginalFilePath = file,
                    Status = File.Exists(file)
                        ? StartupEntryStatus.Active
                        : StartupEntryStatus.PathNotFound
                });
            }
        }
        catch
        {
            // No crítico
        }

        return entries;
    }

    private static async Task<List<StartupEntry>> ReadScheduledTaskEntriesAsync()
    {
        var entries = new List<StartupEntry>();

        try
        {
            // Solo tareas de inicio de sesión, en modo lectura
            await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();

                using var searcher = new System.Management.ManagementObjectSearcher(
                    @"SELECT * FROM Win32_ScheduledTask WHERE Triggers LIKE '%Logon%'");

                foreach (var obj in searcher.Get())
                {
                    if (sw.ElapsedMilliseconds > 5000) break; // Timeout de seguridad

                    var name = obj["Name"]?.ToString();
                    var path = obj["Path"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        entries.Add(new StartupEntry
                        {
                            Name = name,
                            Command = path ?? "No disponible",
                            Location = path ?? "Desconocido",
                            SourceType = StartupSourceType.ScheduledTask,
                            Status = StartupEntryStatus.Active
                        });
                    }
                }
            });
        }
        catch
        {
            // No crítico - las tareas programadas pueden no estar disponibles
        }

        return entries;
    }

    // --- Clasificación ---

    private static void ClassifyEntry(StartupEntry entry)
    {
        // Detectar Microsoft
        entry.IsMicrosoft = IsMicrosoftEntry(entry);

        // Detectar publisher
        entry.Publisher = DetectPublisher(entry);

        // Verificar si la ruta existe
        var extractedPath = ExtractPathFromCommand(entry.Command);
        if (!string.IsNullOrEmpty(extractedPath) && !File.Exists(extractedPath))
        {
            entry.Status = StartupEntryStatus.PathNotFound;
            entry.Notes += "Ruta no encontrada. ";
        }

        // Verificar rutas sospechosas
        if (IsSuspiciousPath(entry.Command))
        {
            entry.Notes += "Ruta en Temp/AppData sospechosa. ";
            entry.Risk = RiskLevel.High;
        }

        // Clasificar riesgo y recomendación
        ClassifyRiskAndRecommendation(entry);
    }

    private static bool IsMicrosoftEntry(StartupEntry entry)
    {
        var nameAndCommand = $"{entry.Name} {entry.Command}".ToLowerInvariant();
        return MicrosoftKeywords.Any(k => nameAndCommand.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static string DetectPublisher(StartupEntry entry)
    {
        // Intentar detectar publisher del nombre del archivo
        var command = entry.Command.ToLowerInvariant();

        if (command.Contains("microsoft") || command.Contains("windows")) return "Microsoft";
        if (command.Contains("google")) return "Google";
        if (command.Contains("mozilla")) return "Mozilla";
        if (command.Contains("adobe")) return "Adobe";
        if (command.Contains("nvidia")) return "NVIDIA";
        if (command.Contains("amd") || command.Contains("ati")) return "AMD";
        if (command.Contains("intel")) return "Intel";
        if (command.Contains("realtek")) return "Realtek";
        if (command.Contains("synaptics")) return "Synaptics";
        if (command.Contains("lenovo")) return "Lenovo";
        if (command.Contains("hp") || command.Contains("hewlett")) return "HP";
        if (command.Contains("dell")) return "Dell";
        if (command.Contains("asus")) return "ASUS";
        if (command.Contains("logitech")) return "Logitech";
        if (command.Contains("steam")) return "Valve";
        if (command.Contains("discord")) return "Discord";
        if (command.Contains("slack")) return "Slack";
        if (command.Contains("zoom")) return "Zoom";
        if (command.Contains("spotify")) return "Spotify";
        if (command.Contains("dropbox")) return "Dropbox";
        if (command.Contains("onedrive")) return "Microsoft";

        return string.Empty;
    }

    private static void ClassifyRiskAndRecommendation(StartupEntry entry)
    {
        // Si ya tiene riesgo alto por ruta sospechosa, mantener
        if (entry.Risk == RiskLevel.High)
        {
            entry.Recommendation = StartupRecommendation.Review;
            return;
        }

        // Microsoft / del sistema → mantener
        if (entry.IsMicrosoft)
        {
            entry.Risk = RiskLevel.Low;
            entry.Recommendation = StartupRecommendation.Keep;
            return;
        }

        // Programas conocidos seguros
        var nameLower = entry.Name.ToLowerInvariant();
        if (SafePrograms.Any(s => nameLower.Contains(s, StringComparison.OrdinalIgnoreCase)))
        {
            entry.Risk = RiskLevel.Low;
            entry.Recommendation = StartupRecommendation.Keep;
            return;
        }

        // RunOnce → posible desactivar
        if (entry.SourceType == StartupSourceType.RegistryRunOnce)
        {
            entry.Risk = RiskLevel.Low;
            entry.Recommendation = StartupRecommendation.PossibleDisable;
            entry.Notes += "RunOnce: se ejecuta una sola vez. ";
            return;
        }

        // Tareas programadas de terceros → revisar
        if (entry.SourceType == StartupSourceType.ScheduledTask && !entry.IsMicrosoft)
        {
            entry.Risk = RiskLevel.Medium;
            entry.Recommendation = StartupRecommendation.Review;
            return;
        }

        // Patrones de posible desactivar
        var nameAndCommand = $"{entry.Name} {entry.Command}".ToLowerInvariant();
        if (PossibleDisablePatterns.Any(p => nameAndCommand.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            entry.Risk = RiskLevel.Medium;
            entry.Recommendation = StartupRecommendation.PossibleDisable;
            return;
        }

        // Sin publisher conocido
        if (string.IsNullOrEmpty(entry.Publisher))
        {
            entry.Risk = RiskLevel.Medium;
            entry.Recommendation = StartupRecommendation.Review;
            entry.Notes += "Editor desconocido. ";
            return;
        }

        // Default: riesgo bajo, revisar
        entry.Risk = RiskLevel.Low;
        entry.Recommendation = StartupRecommendation.Keep;
    }

    private static bool IsSuspiciousPath(string command)
    {
        var lower = command.ToLowerInvariant();
        return HighRiskPatterns.Any(p => lower.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractPathFromCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return string.Empty;

        // Si empieza con comillas, extraer hasta la siguiente comilla
        if (command.StartsWith('"'))
        {
            var endQuote = command.IndexOf('"', 1);
            if (endQuote > 1)
                return command[1..endQuote];
        }

        // Tomar hasta el primer espacio
        var spaceIndex = command.IndexOf(' ');
        return spaceIndex > 0 ? command[..spaceIndex] : command;
    }

    private static string? ResolveShortcutTarget(string shortcutPath)
    {
        try
        {
            // Usar COM para resolver .lnk sin librerías externas
            var shell = Activator.CreateInstance(
                Type.GetTypeFromProgID("WScript.Shell")!) as dynamic;

            if (shell != null)
            {
                var shortcut = shell.CreateShortcut(shortcutPath);
                var target = shortcut.TargetPath as string;
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
                return target;
            }
        }
        catch
        {
            // No crítico
        }

        return null;
    }

    private static async Task<string> GetTechnicianNameAsync()
    {
        try
        {
            var settingsService = new Data.JsonSettingsService();
            var settings = await settingsService.LoadSettingsAsync();
            return settings.Company.TechnicianName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_analysisDirectory))
        {
            Directory.CreateDirectory(_analysisDirectory);
        }
    }
}
