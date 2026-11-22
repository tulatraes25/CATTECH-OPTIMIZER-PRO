using System.Globalization;
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
                    setting.RegistryValueName);

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

        List<VisualOptimizationBackup> backups;
        try
        {
            backups = await LoadBackupsInternalAsync();
        }
        catch (InvalidDataException ex)
        {
            // No se pisa un historial de backups dañado: se reporta el problema.
            result.FinishedAt = DateTime.Now;
            result.FailedCount = 1;
            result.Errors.Add(new VisualOptimizationError
            {
                SettingId = "VISUAL_BACKUPS",
                SettingName = "Backups de optimización visual",
                Message = ex.Message,
                ErrorType = "BackupCorrupt"
            });
            return result;
        }

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
    /// <remarks>
    /// Si el archivo de backups está dañado se propaga <see cref="InvalidDataException"/>
    /// en lugar de devolver una lista vacía silenciosa: la UI puede informar el problema.
    /// </remarks>
    public async Task<List<VisualOptimizationBackup>> ListBackupsAsync()
    {
        var backups = await LoadBackupsInternalAsync();
        return backups.OrderByDescending(b => b.CreatedAt).ToList();
    }

    /// <inheritdoc/>
    public async Task<bool> RestoreAsync(VisualOptimizationBackup backup)
    {
        var result = await RestoreWithResultAsync(backup);
        return result.Success;
    }

    /// <inheritdoc/>
    public async Task<VisualOptimizationRestoreResult> RestoreWithResultAsync(VisualOptimizationBackup backup)
    {
        ArgumentNullException.ThrowIfNull(backup);

        List<VisualOptimizationBackup> backups;
        try
        {
            backups = await LoadBackupsInternalAsync();
        }
        catch (InvalidDataException ex)
        {
            return VisualOptimizationRestoreResult.Fail(backup, "BackupCorrupt", ex.Message);
        }

        var existing = backups.FirstOrDefault(b => b.Id == backup.Id);

        if (existing is null)
        {
            return VisualOptimizationRestoreResult.Fail(
                backup,
                "BackupNotFound",
                $"No se encontró el backup de '{backup.SettingName}'. Es posible que ya haya sido restaurado o eliminado.");
        }

        if (!existing.CanRestore)
        {
            return VisualOptimizationRestoreResult.Fail(
                existing,
                "BackupAlreadyRestored",
                $"El ajuste '{existing.SettingName}' ya fue restaurado anteriormente.");
        }

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
            return VisualOptimizationRestoreResult.Ok(existing);
        }
        catch (Exception ex)
        {
            // No se traga la excepción: se devuelve el motivo real para la UI.
            return VisualOptimizationRestoreResult.Fail(
                existing,
                "RegistryRestoreError",
                $"No se pudo restaurar '{existing.SettingName}': {ex.Message}");
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

    /// <inheritdoc/>
    /// <remarks>
    /// Un archivo de resultados dañado o inaccesible se omite (no se borra ni
    /// se repara) para que no oculte los resultados válidos restantes. Los
    /// resultados se devuelven del más reciente al más antiguo.
    /// </remarks>
    public async Task<IReadOnlyList<VisualOptimizationResult>> ListResultsAsync(
        int maxResults = 20)
    {
        if (maxResults <= 0 || !Directory.Exists(_resultsDirectory))
            return Array.Empty<VisualOptimizationResult>();

        string[] files;
        try
        {
            files = Directory.GetFiles(_resultsDirectory,
                "visual-optimization-result-*.json");
        }
        catch (IOException) { return Array.Empty<VisualOptimizationResult>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<VisualOptimizationResult>(); }

        var results = new List<VisualOptimizationResult>();
        foreach (var file in files.OrderByDescending(f => File.GetLastWriteTimeUtc(f)))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                var parsed = JsonSerializer.Deserialize<VisualOptimizationResult>(
                    json, SerializerOptions);
                if (parsed is not null)
                    results.Add(parsed);
            }
            catch (IOException) { /* Archivo dañado o bloqueado: se omite. */ }
            catch (UnauthorizedAccessException) { /* Sin permisos: se omite. */ }
            catch (JsonException) { /* JSON inválido: se omite sin inventar datos. */ }
            catch (NotSupportedException) { /* Tipo de JSON no soportado: se omite. */ }
        }

        return results
            .OrderByDescending(r => r.StartedAt)
            .Take(maxResults)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<VisualOptimizationResult?> LoadLatestResultAsync()
    {
        var results = await ListResultsAsync(1).ConfigureAwait(false);
        return results.Count == 0 ? null : results[0];
    }

    // --- Métodos de registro ---

    /// <summary>
    /// Lee un valor de HKCU y lo representa como texto.
    /// Los valores binarios (por ejemplo UserPreferencesMask) se guardan como
    /// hexadecimal, nunca como "System.Byte[]".
    /// </summary>
    private static string? ReadRegistryValue(string path, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path, false);
        if (key == null) return null;

        var value = key.GetValue(valueName);
        return value switch
        {
            null => null,
            byte[] bytes => Convert.ToHexString(bytes),
            _ => value.ToString()
        };
    }

    private static void WriteRegistryValue(string path, string valueName, string value, string valueType)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path, true);
        if (key == null)
            throw new InvalidOperationException($"No se pudo crear/abrir la clave: {path}");

        switch (valueType.ToUpperInvariant())
        {
            case "DWORD":
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    throw new InvalidOperationException($"El valor '{value}' no es un DWORD válido.");
                key.SetValue(valueName, intValue, RegistryValueKind.DWord);
                break;

            case "BINARY":
                var bytes = ParseHexBytes(value);
                key.SetValue(valueName, bytes, RegistryValueKind.Binary);
                break;

            default:
                key.SetValue(valueName, value, RegistryValueKind.String);
                break;
        }
    }

    /// <summary>
    /// Convierte el hexadecimal guardado en el backup a bytes reales.
    /// Si el backup proviene de una versión anterior que guardó
    /// "System.Byte[]" (o cualquier texto que no sea hex), se informa un error
    /// claro en lugar de fallar con FormatException sin contexto.
    /// </summary>
    private static byte[] ParseHexBytes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("El valor binario guardado en el backup está vacío.");

        try
        {
            return Convert.FromHexString(value);
        }
        catch (FormatException ex)
        {
            var detail = value.Length <= 40 ? value : value[..40] + "...";
            throw new InvalidOperationException(
                $"El valor binario guardado en el backup no es hexadecimal válido ({detail}). " +
                "Puede provenir de un backup antiguo corrupto.",
                ex);
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
                   ?? throw new InvalidDataException(
                       "El archivo de backups de optimización visual está vacío o no es válido.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "El archivo de backups de optimización visual está dañado y no se puede leer. " +
                "No se modificó ningún ajuste del sistema.",
                ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"No se pudo leer el archivo de backups de optimización visual: {ex.Message}",
                ex);
        }
    }

    private async Task SaveBackupsInternalAsync(List<VisualOptimizationBackup> backups)
    {
        EnsureDirectoryExists(_backupsDirectory);
        var json = JsonSerializer.Serialize(backups, SerializerOptions);

        // Escritura atómica: un corte a mitad de guardado no debe dejar el JSON corrupto.
        var tempPath = _backupsJsonPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, _backupsJsonPath, overwrite: true);
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
