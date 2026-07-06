using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Startup;

namespace Cattech.Optimizer.Pro.Infrastructure.Startup;

/// <summary>
/// Implementación de IStartupService.
/// Analiza, desactiva y restaura programas de inicio de forma segura.
/// </summary>
public class StartupService : IStartupService
{
    private readonly string _baseDirectory;
    private readonly string _analysisDirectory;
    private readonly string _backupsDirectory;
    private readonly string _backupsJsonPath;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Fuentes soportadas para desactivación
    private static readonly StartupSourceType[] DisableableSources =
    [
        StartupSourceType.RegistryRun,
        StartupSourceType.StartupFolder,
        StartupSourceType.StartupFolderCommon
    ];

    // Prefijos y palabras clave para detectar Microsoft
    private static readonly string[] MicrosoftKeywords =
    [
        "microsoft", "ms-", "windows", "msn", "msedge", "msoffice",
        "onedrive", "teams", "office", "defender", "security",
        "cortana", "xbox", "surface", "halo"
    ];

    private static readonly string[] HighRiskPatterns =
    [
        @"\temp\", @"\tmp\", @"\appdata\local\temp",
        "powershell -enc", "cmd /c", "wscript", "cscript",
        "mshta", "regsvr32", "rundll32"
    ];

    private static readonly string[] SafePrograms =
    [
        "securityhealth", "windows defender", "cftmon", "ctfmon",
        "igfxtray", "hkcmd", "igfxpers", "nvtray", "nvmctray",
        "rthdvcpl", "rtkwmaudio", "realtek", "synaptics",
        "elanservice", "lenovo", "hp", "dell", "audio"
    ];

    private static readonly string[] PossibleDisablePatterns =
    [
        "update", "updater", "helper", "assistant", "launcher",
        "chat", "cloud", "sync", "backup", "telemetry",
        "reporter", "crash", "error", "optimizer"
    ];

    public StartupService(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        _analysisDirectory = Path.Combine(_baseDirectory, "data", "startup-analysis");
        _backupsDirectory = Path.Combine(_baseDirectory, "backups", "startup");
        _backupsJsonPath = Path.Combine(_backupsDirectory, "startup-backups.json");
    }

    // =====================
    // ANÁLISIS (SOLO LECTURA)
    // =====================

    /// <inheritdoc/>
    public async Task<StartupAnalysis> AnalyzeStartupAsync()
    {
        var analysis = new StartupAnalysis();
        var entries = new List<StartupEntry>();
        var seenCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        entries.AddRange(ReadRegistryEntries(
            "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run", StartupSourceType.RegistryRun));
        entries.AddRange(ReadRegistryEntries(
            "HKLM", @"Software\Microsoft\Windows\CurrentVersion\Run", StartupSourceType.RegistryRun));
        entries.AddRange(ReadRegistryEntries(
            "HKCU", @"Software\Microsoft\Windows\CurrentVersion\RunOnce", StartupSourceType.RegistryRunOnce));
        entries.AddRange(ReadRegistryEntries(
            "HKLM", @"Software\Microsoft\Windows\CurrentVersion\RunOnce", StartupSourceType.RegistryRunOnce));

        var userStartupFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\Startup");
        entries.AddRange(ReadFolderEntries(userStartupFolder, StartupSourceType.StartupFolder));

        var commonStartupFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\Startup");
        entries.AddRange(ReadFolderEntries(commonStartupFolder, StartupSourceType.StartupFolderCommon));

        entries.AddRange(await ReadScheduledTaskEntriesAsync());

        foreach (var entry in entries)
        {
            ClassifyEntry(entry);
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
        EnsureDirectoryExists(_analysisDirectory);

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
        if (!Directory.Exists(_analysisDirectory)) return null;

        var files = Directory.GetFiles(_analysisDirectory, "startup-analysis-*.json");
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var analysis = JsonSerializer.Deserialize<StartupAnalysis>(json, SerializerOptions);
                if (analysis?.Id == analysisId) return analysis;
            }
            catch { }
        }
        return null;
    }

    /// <inheritdoc/>
    public async Task<List<StartupAnalysisSummary>> ListAnalysesAsync(int maxResults = 20)
    {
        var summaries = new List<StartupAnalysisSummary>();
        if (!Directory.Exists(_analysisDirectory)) return summaries;

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
        if (!Directory.Exists(_analysisDirectory)) return false;

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

    // =====================
    // DESACTIVACIÓN (CON BACKUP)
    // =====================

    /// <inheritdoc/>
    public bool CanDisableStartupEntry(StartupEntry entry)
    {
        if (entry.IsMicrosoft) return false;
        if (!DisableableSources.Contains(entry.SourceType)) return false;
        return true;
    }

    /// <inheritdoc/>
    public async Task<StartupDisableSummary> DisableSelectedAsync(IEnumerable<StartupEntry> entries, string reason = "")
    {
        var summary = new StartupDisableSummary();
        var backups = await LoadBackupsInternalAsync();
        var technician = await GetTechnicianNameAsync();

        foreach (var entry in entries)
        {
            summary.TotalAttempted++;

            // Verificar si es Microsoft
            if (entry.IsMicrosoft)
            {
                summary.SkippedMicrosoftCount++;
                summary.Results.Add(new StartupDisableResult
                {
                    EntryName = entry.Name,
                    EntryId = entry.Id,
                    Result = StartupActionResult.SkippedMicrosoft,
                    Message = "No se puede desactivar: entrada de Microsoft"
                });
                continue;
            }

            // Verificar si la fuente es soportada
            if (!DisableableSources.Contains(entry.SourceType))
            {
                summary.SkippedUnsupportedCount++;
                summary.Results.Add(new StartupDisableResult
                {
                    EntryName = entry.Name,
                    EntryId = entry.Id,
                    Result = StartupActionResult.SkippedUnsupportedSource,
                    Message = $"Fuente no soportada para desactivación: {entry.SourceType}"
                });
                continue;
            }

            // Verificar si ya tiene backup
            if (backups.Any(b => b.EntryId == entry.Id && b.CanRestore))
            {
                summary.FailedCount++;
                summary.Results.Add(new StartupDisableResult
                {
                    EntryName = entry.Name,
                    EntryId = entry.Id,
                    Result = StartupActionResult.AlreadyDisabled,
                    Message = "La entrada ya fue desactivada anteriormente"
                });
                continue;
            }

            // Ejecutar desactivación
            try
            {
                var backup = await DisableEntryInternalAsync(entry, reason, technician);
                backups.Add(backup);
                summary.SuccessCount++;
                summary.Results.Add(new StartupDisableResult
                {
                    EntryName = entry.Name,
                    EntryId = entry.Id,
                    Result = StartupActionResult.Success,
                    Message = "Desactivada correctamente",
                    BackupId = backup.Id
                });
            }
            catch (Exception ex)
            {
                summary.FailedCount++;
                summary.Results.Add(new StartupDisableResult
                {
                    EntryName = entry.Name,
                    EntryId = entry.Id,
                    Result = StartupActionResult.Failed,
                    Message = $"Error: {ex.Message}"
                });
            }
        }

        // Guardar backups
        await SaveBackupsInternalAsync(backups);

        return summary;
    }

    /// <inheritdoc/>
    public async Task<StartupActionResult> RestoreAsync(StartupBackupRecord backup)
    {
        var backups = await LoadBackupsInternalAsync();
        var existing = backups.FirstOrDefault(b => b.Id == backup.Id);

        if (existing == null) return StartupActionResult.NotFound;
        if (!existing.CanRestore) return StartupActionResult.AlreadyDisabled;

        try
        {
            switch (existing.SourceType)
            {
                case StartupSourceType.RegistryRun:
                case StartupSourceType.RegistryRunOnce:
                    RestoreRegistryEntry(existing);
                    break;

                case StartupSourceType.StartupFolder:
                case StartupSourceType.StartupFolderCommon:
                    await RestoreFolderEntryAsync(existing);
                    break;

                default:
                    return StartupActionResult.RestoreFailed;
            }

            // Marcar como restaurado
            existing.CanRestore = false;
            existing.RestoredAt = DateTime.Now;
            existing.RestoredBy = await GetTechnicianNameAsync();
            existing.Notes += $"Restaurado el {existing.RestoredAt:yyyy-MM-dd HH:mm}. ";

            await SaveBackupsInternalAsync(backups);

            return StartupActionResult.Success;
        }
        catch
        {
            return StartupActionResult.RestoreFailed;
        }
    }

    /// <inheritdoc/>
    public async Task<List<StartupBackupRecord>> ListBackupsAsync()
    {
        var backups = await LoadBackupsInternalAsync();
        return backups.OrderByDescending(b => b.CreatedAt).ToList();
    }

    /// <inheritdoc/>
    public async Task<StartupBackupRecord?> LoadBackupAsync(string backupId)
    {
        var backups = await LoadBackupsInternalAsync();
        return backups.FirstOrDefault(b => b.Id == backupId);
    }

    // --- Métodos internos de desactivación ---

    private async Task<StartupBackupRecord> DisableEntryInternalAsync(
        StartupEntry entry, string reason, string technician)
    {
        var record = new StartupBackupRecord
        {
            EntryId = entry.Id,
            EntryName = entry.Name,
            SourceType = entry.SourceType,
            OriginalLocation = entry.Location,
            Command = entry.Command,
            Publisher = entry.Publisher,
            WasMicrosoft = entry.IsMicrosoft,
            Reason = reason,
            DisabledBy = technician
        };

        switch (entry.SourceType)
        {
            case StartupSourceType.RegistryRun:
            case StartupSourceType.RegistryRunOnce:
                DisableRegistryEntry(entry, record);
                break;

            case StartupSourceType.StartupFolder:
            case StartupSourceType.StartupFolderCommon:
                await DisableFolderEntryAsync(entry, record);
                break;
        }

        return record;
    }

    private static void DisableRegistryEntry(StartupEntry entry, StartupBackupRecord record)
    {
        var hive = entry.Location.StartsWith("HKCU") ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
        var keyPath = entry.Location.Contains("RunOnce")
            ? @"Software\Microsoft\Windows\CurrentVersion\RunOnce"
            : @"Software\Microsoft\Windows\CurrentVersion\Run";

        var backupKeyPath = entry.Location.Contains("RunOnce")
            ? @"Software\CATTECH\OptimizerPro\DisabledStartup\RunOnce"
            : @"Software\CATTECH\OptimizerPro\DisabledStartup\Run";

        // Leer valor original
        using var sourceKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)
            ?.OpenSubKey(keyPath, false);

        if (sourceKey == null)
            throw new InvalidOperationException($"No se pudo acceder a la clave: {keyPath}");

        var value = sourceKey.GetValue(entry.Name);
        if (value == null)
            throw new InvalidOperationException($"No se encontró el valor: {entry.Name}");

        record.OriginalValue = value.ToString();
        record.BackupLocation = $"{(hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM")}\\{backupKeyPath}";

        // Copiar a clave de backup
        using var backupKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)
            ?.CreateSubKey(backupKeyPath, true);

        if (backupKey == null)
            throw new InvalidOperationException("No se pudo crear la clave de backup");

        // Guardar metadata en el nombre del valor
        var backupValueName = $"CATTECH_{entry.Id}_{entry.Name}";
        backupKey.SetValue(backupValueName, value, RegistryValueKind.String);

        record.BackupValue = value.ToString();
        record.BackupLocation = $"{record.BackupLocation}\\{backupValueName}";

        // Eliminar valor original
        sourceKey.DeleteValue(entry.Name, false);

        sourceKey.Close();
    }

    private async Task DisableFolderEntryAsync(StartupEntry entry, StartupBackupRecord record)
    {
        var sourcePath = entry.Location;

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"No se encontró el archivo: {sourcePath}");

        // Crear carpeta de backup con timestamp
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupFolder = Path.Combine(_backupsDirectory, timestamp);
        EnsureDirectoryExists(backupFolder);

        var fileName = Path.GetFileName(sourcePath);
        var backupPath = Path.Combine(backupFolder, fileName);

        // Mover archivo
        File.Move(sourcePath, backupPath);

        record.OriginalLocation = sourcePath;
        record.BackupLocation = backupPath;
        record.OriginalValue = sourcePath;
        record.BackupValue = backupPath;

        await Task.CompletedTask;
    }

    private static void RestoreRegistryEntry(StartupBackupRecord record)
    {
        var hive = record.OriginalLocation.StartsWith("HKCU") ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
        var keyPath = record.SourceType == StartupSourceType.RegistryRunOnce
            ? @"Software\Microsoft\Windows\CurrentVersion\RunOnce"
            : @"Software\Microsoft\Windows\CurrentVersion\Run";

        // Leer valor del backup
        var backupKeyPath = record.SourceType == StartupSourceType.RegistryRunOnce
            ? @"Software\CATTECH\OptimizerPro\DisabledStartup\RunOnce"
            : @"Software\CATTECH\OptimizerPro\DisabledStartup\Run";

        using var backupKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)
            ?.OpenSubKey(backupKeyPath, false);

        if (backupKey == null)
            throw new InvalidOperationException("No se encontró la clave de backup");

        var backupValueName = $"CATTECH_{record.EntryId}_{record.EntryName}";
        var value = backupKey.GetValue(backupValueName);

        if (value == null)
            throw new InvalidOperationException($"No se encontró el backup: {backupValueName}");

        // Restaurar en ubicación original
        using var targetKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)
            ?.CreateSubKey(keyPath, true);

        if (targetKey == null)
            throw new InvalidOperationException("No se pudo crear la clave de restauración");

        targetKey.SetValue(record.EntryName, value, RegistryValueKind.String);

        // Eliminar del backup
        backupKey.DeleteValue(backupValueName, false);
    }

    private async Task RestoreFolderEntryAsync(StartupBackupRecord record)
    {
        if (!File.Exists(record.BackupLocation))
            throw new FileNotFoundException($"No se encontró el backup: {record.BackupLocation}");

        var directory = Path.GetDirectoryName(record.OriginalLocation);
        if (!string.IsNullOrEmpty(directory))
            EnsureDirectoryExists(directory);

        File.Move(record.BackupLocation, record.OriginalLocation);

        await Task.CompletedTask;
    }

    // --- Persistencia de backups ---

    private async Task<List<StartupBackupRecord>> LoadBackupsInternalAsync()
    {
        if (!File.Exists(_backupsJsonPath))
            return new List<StartupBackupRecord>();

        try
        {
            var json = await File.ReadAllTextAsync(_backupsJsonPath);
            return JsonSerializer.Deserialize<List<StartupBackupRecord>>(json, SerializerOptions)
                   ?? new List<StartupBackupRecord>();
        }
        catch
        {
            return new List<StartupBackupRecord>();
        }
    }

    private async Task SaveBackupsInternalAsync(List<StartupBackupRecord> backups)
    {
        EnsureDirectoryExists(_backupsDirectory);
        var json = JsonSerializer.Serialize(backups, SerializerOptions);
        await File.WriteAllTextAsync(_backupsJsonPath, json);
    }

    // =====================
    // MÉTODOS COMPARTIDOS (LECTURA)
    // =====================

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
        catch { }
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
                string command = ext == ".lnk" ? ResolveShortcutTarget(file) ?? file : file;

                entries.Add(new StartupEntry
                {
                    Name = name,
                    Command = command,
                    Location = file,
                    SourceType = sourceType,
                    OriginalFilePath = file,
                    Status = File.Exists(file) ? StartupEntryStatus.Active : StartupEntryStatus.PathNotFound
                });
            }
        }
        catch { }
        return entries;
    }

    private static async Task<List<StartupEntry>> ReadScheduledTaskEntriesAsync()
    {
        var entries = new List<StartupEntry>();
        try
        {
            await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                using var searcher = new System.Management.ManagementObjectSearcher(
                    @"SELECT * FROM Win32_ScheduledTask WHERE Triggers LIKE '%Logon%'");
                foreach (var obj in searcher.Get())
                {
                    if (sw.ElapsedMilliseconds > 5000) break;
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
        catch { }
        return entries;
    }

    private static void ClassifyEntry(StartupEntry entry)
    {
        entry.IsMicrosoft = IsMicrosoftEntry(entry);
        entry.Publisher = DetectPublisher(entry);

        var extractedPath = ExtractPathFromCommand(entry.Command);
        if (!string.IsNullOrEmpty(extractedPath) && !File.Exists(extractedPath))
        {
            entry.Status = StartupEntryStatus.PathNotFound;
            entry.Notes += "Ruta no encontrada. ";
        }

        if (IsSuspiciousPath(entry.Command))
        {
            entry.Notes += "Ruta en Temp/AppData sospechosa. ";
            entry.Risk = RiskLevel.High;
        }

        ClassifyRiskAndRecommendation(entry);
    }

    private static bool IsMicrosoftEntry(StartupEntry entry)
    {
        var nameAndCommand = $"{entry.Name} {entry.Command}".ToLowerInvariant();
        return MicrosoftKeywords.Any(k => nameAndCommand.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static string DetectPublisher(StartupEntry entry)
    {
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
        if (entry.Risk == RiskLevel.High) { entry.Recommendation = StartupRecommendation.Review; return; }
        if (entry.IsMicrosoft) { entry.Risk = RiskLevel.Low; entry.Recommendation = StartupRecommendation.Keep; return; }

        var nameLower = entry.Name.ToLowerInvariant();
        if (SafePrograms.Any(s => nameLower.Contains(s, StringComparison.OrdinalIgnoreCase)))
        { entry.Risk = RiskLevel.Low; entry.Recommendation = StartupRecommendation.Keep; return; }

        if (entry.SourceType == StartupSourceType.RegistryRunOnce)
        { entry.Risk = RiskLevel.Low; entry.Recommendation = StartupRecommendation.PossibleDisable; entry.Notes += "RunOnce: se ejecuta una sola vez. "; return; }

        if (entry.SourceType == StartupSourceType.ScheduledTask && !entry.IsMicrosoft)
        { entry.Risk = RiskLevel.Medium; entry.Recommendation = StartupRecommendation.Review; return; }

        var nameAndCommand = $"{entry.Name} {entry.Command}".ToLowerInvariant();
        if (PossibleDisablePatterns.Any(p => nameAndCommand.Contains(p, StringComparison.OrdinalIgnoreCase)))
        { entry.Risk = RiskLevel.Medium; entry.Recommendation = StartupRecommendation.PossibleDisable; return; }

        if (string.IsNullOrEmpty(entry.Publisher))
        { entry.Risk = RiskLevel.Medium; entry.Recommendation = StartupRecommendation.Review; entry.Notes += "Editor desconocido. "; return; }

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
        if (command.StartsWith('"'))
        {
            var endQuote = command.IndexOf('"', 1);
            if (endQuote > 1) return command[1..endQuote];
        }
        var spaceIndex = command.IndexOf(' ');
        return spaceIndex > 0 ? command[..spaceIndex] : command;
    }

    private static string? ResolveShortcutTarget(string shortcutPath)
    {
        try
        {
            var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!) as dynamic;
            if (shell != null)
            {
                var shortcut = shell.CreateShortcut(shortcutPath);
                var target = shortcut.TargetPath as string;
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
                return target;
            }
        }
        catch { }
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
        catch { return string.Empty; }
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
