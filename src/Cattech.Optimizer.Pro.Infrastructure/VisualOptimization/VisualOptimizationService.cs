using System.Text.Json;
using Microsoft.Win32;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.VisualOptimization;

namespace Cattech.Optimizer.Pro.Infrastructure.VisualOptimization;

/// <summary>
/// Implementación de IVisualOptimizationService.
/// Aplica optimizaciones visuales de forma segura con backup y reversión.
/// </summary>
public class VisualOptimizationService : IVisualOptimizationService
{
    private readonly string _baseDirectory;
    private readonly string _backupsDirectory;
    private readonly string _backupsJsonPath;
    private readonly string _resultsDirectory;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public VisualOptimizationService(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        _backupsDirectory = Path.Combine(_baseDirectory, "backups", "visual");
        _backupsJsonPath = Path.Combine(_backupsDirectory, "visual-backups.json");
        _resultsDirectory = Path.Combine(_baseDirectory, "data", "visual-optimization-results");
    }

    /// <inheritdoc/>
    public Task<List<VisualOptimizationSetting>> AnalyzeAsync()
    {
        var settings = VisualOptimizationPresets.GetDefaultSettings();

        foreach (var setting in settings)
        {
            try
            {
                setting.CurrentValue = ReadRegistryValue(
                    setting.RegistryPath,
                    setting.RegistryValueName,
                    setting.ValueType);

                setting.IsSupported = true;
            }
            catch
            {
                setting.IsSupported = false;
                setting.Notes = "No se pudo leer el registro";
            }
        }

        return Task.FromResult(settings);
    }

    /// <inheritdoc/>
    public async Task<VisualOptimizationResult> ApplyAsync(
        IEnumerable<VisualOptimizationSetting> settings, string reason = "")
    {
        var result = new VisualOptimizationResult
        {
            StartedAt = DateTime.Now
        };

        var backups = await LoadBackupsInternalAsync();
        var technician = await GetTechnicianNameAsync();

        foreach (var setting in settings)
        {
            if (!setting.IsSelected)
            {
                result.SkippedCount++;
                continue;
            }

            if (!setting.IsSupported)
            {
                result.SkippedCount++;
                continue;
            }

            if (setting.IsAlreadyOptimized)
            {
                result.SkippedCount++;
                continue;
            }

            try
            {
                // Crear backup
                var backup = new VisualOptimizationBackup
                {
                    SettingId = setting.Id,
                    SettingName = setting.DisplayName,
                    RegistryPath = setting.RegistryPath,
                    RegistryValueName = setting.RegistryValueName,
                    OriginalValue = setting.CurrentValue,
                    NewValue = setting.RecommendedValue,
                    ValueType = setting.ValueType,
                    AppliedBy = technician
                };

                // Aplicar cambio
                WriteRegistryValue(
                    setting.RegistryPath,
                    setting.RegistryValueName,
                    setting.RecommendedValue,
                    setting.ValueType);

                backups.Add(backup);
                result.Backups.Add(backup);
                result.AppliedCount++;

                if (setting.RequiresRestart) result.RequiresRestart = true;
                if (setting.RequiresSignOut) result.RequiresSignOut = true;
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                result.Errors.Add(new VisualOptimizationError
                {
                    SettingId = setting.Id,
                    SettingName = setting.DisplayName,
                    Message = ex.Message,
                    ErrorType = "RegistryWriteError"
                });
            }
        }

        result.FinishedAt = DateTime.Now;
        result.TechnicianName = technician;

        // Guardar backups
        await SaveBackupsInternalAsync(backups);

        return result;
    }

    /// <inheritdoc/>
    public async Task<List<VisualOptimizationBackup>> ListBackupsAsync()
    {
        var backups = await LoadBackupsInternalAsync();
        return backups.OrderByDescending(b => b.CreatedAt).ToList();
    }

    /// <inheritdoc/>
    public async Task<bool> RestoreAsync(VisualOptimizationBackup backup)
    {
        var backups = await LoadBackupsInternalAsync();
        var existing = backups.FirstOrDefault(b => b.Id == backup.Id);

        if (existing == null || !existing.CanRestore)
            return false;

        try
        {
            // Restaurar valor original
            if (existing.OriginalValue == null)
            {
                // Si no había valor, eliminar la clave creada
                DeleteRegistryValue(existing.RegistryPath, existing.RegistryValueName);
            }
            else
            {
                WriteRegistryValue(
                    existing.RegistryPath,
                    existing.RegistryValueName,
                    existing.OriginalValue,
                    existing.ValueType);
            }

            // Marcar como restaurado
            existing.CanRestore = false;
            existing.RestoredAt = DateTime.Now;

            await SaveBackupsInternalAsync(backups);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<string> SaveResultAsync(VisualOptimizationResult result)
    {
        EnsureDirectoryExists(_resultsDirectory);

        var fileName = $"visual-optimization-result-{result.StartedAt:yyyyMMdd-HHmmss}.json";
        var filePath = Path.Combine(_resultsDirectory, fileName);

        if (File.Exists(filePath))
        {
            fileName = $"visual-optimization-result-{result.StartedAt:yyyyMMdd-HHmmss}-{result.Id}.json";
            filePath = Path.Combine(_resultsDirectory, fileName);
        }

        var json = JsonSerializer.Serialize(result, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json);

        return fileName;
    }

    // --- Métodos de registro ---

    private static string? ReadRegistryValue(string path, string valueName, string valueType)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path, false);
        if (key == null) return null;

        var value = key.GetValue(valueName);
        return value?.ToString();
    }

    private static void WriteRegistryValue(string path, string valueName, string value, string valueType)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path, true);
        if (key == null)
            throw new InvalidOperationException($"No se pudo crear/abrir la clave: {path}");

        switch (valueType.ToUpperInvariant())
        {
            case "DWORD":
                if (int.TryParse(value, out var intValue))
                    key.SetValue(valueName, intValue, RegistryValueKind.DWord);
                else
                    key.SetValue(valueName, value, RegistryValueKind.String);
                break;

            case "BINARY":
                var bytes = Convert.FromHexString(value);
                key.SetValue(valueName, bytes, RegistryValueKind.Binary);
                break;

            default:
                key.SetValue(valueName, value, RegistryValueKind.String);
                break;
        }
    }

    private static void DeleteRegistryValue(string path, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path, true);
        key?.DeleteValue(valueName, false);
    }

    // --- Persistencia ---

    private async Task<List<VisualOptimizationBackup>> LoadBackupsInternalAsync()
    {
        if (!File.Exists(_backupsJsonPath))
            return new List<VisualOptimizationBackup>();

        try
        {
            var json = await File.ReadAllTextAsync(_backupsJsonPath);
            return JsonSerializer.Deserialize<List<VisualOptimizationBackup>>(json, SerializerOptions)
                   ?? new List<VisualOptimizationBackup>();
        }
        catch
        {
            return new List<VisualOptimizationBackup>();
        }
    }

    private async Task SaveBackupsInternalAsync(List<VisualOptimizationBackup> backups)
    {
        EnsureDirectoryExists(_backupsDirectory);
        var json = JsonSerializer.Serialize(backups, SerializerOptions);
        await File.WriteAllTextAsync(_backupsJsonPath, json);
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
