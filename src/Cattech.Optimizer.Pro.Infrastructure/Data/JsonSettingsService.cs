using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Configuration;

namespace Cattech.Optimizer.Pro.Infrastructure.Data;

/// <summary>
/// Implementación de ISettingsService que persiste configuración en JSON.
/// </summary>
public class JsonSettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private AppSettings _currentSettings;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AppSettings CurrentSettings => _currentSettings;

    public event EventHandler<AppSettings>? SettingsChanged;

    public JsonSettingsService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config",
            "empresa.json");

        _currentSettings = new AppSettings();
    }

    /// <inheritdoc/>
    public async Task<AppSettings> LoadSettingsAsync()
    {
        await _lock.WaitAsync();

        try
        {
            if (!File.Exists(_settingsPath))
            {
                _currentSettings = new AppSettings();
                return _currentSettings;
            }

            var json = await File.ReadAllTextAsync(_settingsPath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            _currentSettings = JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();

            return _currentSettings;
        }
        catch (Exception)
        {
            // Si hay error, retornar configuración por defecto
            _currentSettings = new AppSettings();
            return _currentSettings;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await _lock.WaitAsync();

        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(_settingsPath, json);

            _currentSettings = settings;
            SettingsChanged?.Invoke(this, settings);
        }
        finally
        {
            _lock.Release();
        }
    }
}
