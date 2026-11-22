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

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOnceKeyPath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string DisabledRunKeyPath = @"Software\CATTECH\OptimizerPro\DisabledStartup\Run";
    private const string DisabledRunOnceKeyPath = @"Software\CATTECH\OptimizerPro\DisabledStartup\RunOnce";

    // Señales explícitas de Microsoft. No se usan palabras genéricas como
    // "windows", "office" o "security": bloqueaban aplicaciones legítimas de
    // terceros (LibreOffice, antivirus, etc.) y volvían inutilizable la
    // desactivación. Solo nombre de la entrada, ubicación y ruta del ejecutable.
    private static readonly string[] MicrosoftKeywords =
    [
        "microsoft", "windows defender", "windows security", "windows update",
        "windows explorer", "securityhealth", "msedge", "windowsapps",
        "windows backup", "windows mobile", "cortana", "xbox game"
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
            // Deterministic identity: the previous random id changed on every
            // analysis, so "already disabled" and restore correlation could
            // never match the same entry twice.
            entry.Id = ComputeEntryId(entry);
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
                summary.SkippedAlreadyDisabledCount++;
                summary.Results.Add(new StartupDisableResult
                {
                    EntryName = entry.Name,
                    EntryId = entry.Id,
                    Result = StartupActionResult.AlreadyDisabled,
                    Message = "La entrada ya fue desactivada anteriormente (hay backup para restaurar)."
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
        var hive = entry.Location.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase)
            ? RegistryHive.CurrentUser
            : RegistryHive.LocalMachine;
        var isRunOnce = entry.SourceType == StartupSourceType.RegistryRunOnce
            || entry.Location.Contains("RunOnce", StringComparison.OrdinalIgnoreCase);
        var keyPath = isRunOnce ? RunOnceKeyPath : RunKeyPath;
        var backupKeyPath = isRunOnce ? DisabledRunOnceKeyPath : DisabledRunKeyPath;

        // La clave se abre ESCRIBIBLE: la versión anterior la abría solo para
        // lectura y DeleteValue lanzaba UnauthorizedAccessException siempre,
        // por lo que ningún programa de inicio se desactivaba jamás.
        using var sourceKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)
            .OpenSubKey(keyPath, writable: true);

        if (sourceKey == null)
            throw new UnauthorizedAccessException(
                $"No se pudo abrir {keyPath} para escritura. " +
                (hive == RegistryHive.LocalMachine
                    ? "Ejecutá CATTECH como administrador para modificar HKLM."
                    : "Verificá los permisos del usuario actual."));

        var value = sourceKey.GetValue(
            entry.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value == null)
            throw new InvalidOperationException($"No se encontró el valor: {entry.Name}");

        var valueKind = sourceKey.GetValueKind(entry.Name);
        record.OriginalValue = value.ToString();
        record.OriginalValueKind = valueKind.ToString();
        record.BackupLocation =
            $"{(hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM")}\\{backupKeyPath}";

        using var backupKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)
            .CreateSubKey(backupKeyPath, writable: true);
        if (backupKey == null)
            throw new InvalidOperationException("No se pudo crear la clave de backup");

        var backupValueName = $"CATTECH_{entry.Id}_{entry.Name}";
        backupKey.SetValue(backupValueName, value, valueKind);
        record.BackupValue = value.ToString();
        record.BackupLocation = $"{record.BackupLocation}\\{backupValueName}";

        try
        {
            sourceKey.DeleteValue(entry.Name, throwOnMissingValue: false);
        }
        catch
        {
            // No dejar un backup huérfano si el borrado falló: sin este
            // rollback la entrada aparecía como "ya desactivada".
            try { backupKey.DeleteValue(backupValueName, throwOnMissingValue: false); }
            catch { /* Preservar el error original. */ }
            throw;
        }
    }

    private async Task DisableFolderEntryAsync(StartupEntry entry, StartupBackupRecord record)
    {
        var sourcePath = entry.Location;

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"No se encontró el archivo: {sourcePath}");

        // Crear carpeta de backup única (timestamps con resolución de segundo
        // colisionaban y sobrescribían respaldos de distintas entradas).
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupFolder = Path.Combine(
            _backupsDirectory, $"{timestamp}-{Guid.NewGuid().ToString("N")[..8]}");
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
        var hive = record.OriginalLocation.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase)
            ? RegistryHive.CurrentUser
            : RegistryHive.LocalMachine;
        var isRunOnce = record.SourceType == StartupSourceType.RegistryRunOnce;
        var keyPath = isRunOnce ? RunOnceKeyPath : RunKeyPath;
        var backupKeyPath = isRunOnce ? DisabledRunOnceKeyPath : DisabledRunKeyPath;

        // La clave de backup también se abre escribible: la versión anterior
        // la abría solo-lectura y DeleteValue fallaba tras restaurar el valor.
        using var backupKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)
            .OpenSubKey(backupKeyPath, writable: true);

        if (backupKey == null)
            throw new InvalidOperationException("No se encontró la clave de backup");

        var backupValueName = $"CATTECH_{record.EntryId}_{record.EntryName}";
        var value = backupKey.GetValue(
            backupValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);

        if (value == null)
            throw new InvalidOperationException($"No se encontró el backup: {backupValueName}");

        // Restaurar en ubicación original preservando el tipo del valor.
        using var targetKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)
            .CreateSubKey(keyPath, writable: true);

        if (targetKey == null)
            throw new InvalidOperationException("No se pudo crear la clave de restauración");

        targetKey.SetValue(record.EntryName, value, ParseValueKind(record.OriginalValueKind, value));

        // Eliminar del backup
        backupKey.DeleteValue(backupValueName, throwOnMissingValue: false);
    }

    private static RegistryValueKind ParseValueKind(string? storedKind, object value)
    {
        if (Enum.TryParse<RegistryValueKind>(storedKind, ignoreCase: true, out var kind)
            && kind is not RegistryValueKind.Unknown and not RegistryValueKind.None)
            return kind;

        return value switch
        {
            string[] => RegistryValueKind.MultiString,
            byte[] => RegistryValueKind.Binary,
            int => RegistryValueKind.DWord,
            long => RegistryValueKind.QWord,
            _ => RegistryValueKind.String
        };
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

    /// <summary>
    /// Stable 8-char identity: the same source, location and name always
    /// produce the same id across analyses, so "already disabled" and restore
    /// correlation keep working.
    /// </summary>
    internal static string ComputeEntryId(StartupEntry entry)
    {
        var payload = $"{entry.SourceType}|{entry.Location}|{entry.Name}";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash)[..8];
    }

    private static async Task<List<StartupEntry>> ReadScheduledTaskEntriesAsync()
    {
        var entries = new List<StartupEntry>();
        try
        {
            await Task.Run(() =>
            {
                // Win32_ScheduledTask no existe en Windows 10/11. Se enumeran
                // las tareas con trigger de inicio de sesión/arranque mediante
                // el cmdlet nativo y se leen como JSON (solo lectura).
                var script = string.Join(" ",
                    "try {",
                    "Get-ScheduledTask -ErrorAction Stop |",
                    "Where-Object { $_.Triggers | Where-Object { $_.CimClass.CimClassName -in @('MSFT_TaskLogonTrigger','MSFT_TaskBootTrigger') } } |",
                    "Where-Object { $_.State -ne 'Disabled' } |",
                    "Select-Object TaskName, TaskPath, State |",
                    "ConvertTo-Json -Compress",
                    "} catch { }");

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" +
                        script.Replace("\"", "\\\"") + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process is null)
                    return;
                var output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(8000))
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return;
                }
                if (string.IsNullOrWhiteSpace(output))
                    return;

                using var document = JsonDocument.Parse(output);
                var items = document.RootElement.ValueKind == JsonValueKind.Array
                    ? document.RootElement.EnumerateArray().ToList()
                    : [document.RootElement];
                foreach (var item in items)
                {
                    var name = GetJsonString(item, "TaskName");
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    var path = GetJsonString(item, "TaskPath") ?? string.Empty;
                    entries.Add(new StartupEntry
                    {
                        Name = name,
                        Command = $"Tarea programada: {path}{name}",
                        Location = $"TareaProgramada\\{path}{name}",
                        SourceType = StartupSourceType.ScheduledTask,
                        Status = StartupEntryStatus.Active
                    });
                }
            });
        }
        catch { }
        return entries;
    }

    private static string? GetJsonString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
    }

    // Internal para poder probar la clasificación con entradas sintéticas sin
    // depender del registro real del equipo.
    internal static void ClassifyEntry(StartupEntry entry)
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
        // Solo nombre de la entrada y ruta del ejecutable. La ubicación se
        // excluye a propósito: toda entrada de Run vive bajo
        // Software\Microsoft\Windows\... y marcaba TODO como Microsoft.
        var executable = ExtractPathFromCommand(entry.Command);
        var signals = $"{entry.Name} {executable}".ToLowerInvariant();
        return MicrosoftKeywords.Any(k => signals.Contains(k, StringComparison.OrdinalIgnoreCase));
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
        return await Helpers.SettingsHelper.GetTechnicianNameAsync(new Data.JsonSettingsService());
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
