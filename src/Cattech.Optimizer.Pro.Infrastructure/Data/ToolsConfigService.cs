using System.Text;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Configuration;

namespace Cattech.Optimizer.Pro.Infrastructure.Data;

/// <summary>
/// Implementación de <see cref="IToolsConfigService"/> que persiste
/// <c>config/herramientas.json</c> junto al ejecutable de la aplicación.
/// </summary>
public sealed class ToolsConfigService : IToolsConfigService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    public ToolsConfigService(string? configFilePath = null)
    {
        ConfigFilePath = configFilePath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config",
            "herramientas.json");
    }

    /// <inheritdoc/>
    public string ConfigFilePath { get; }

    /// <inheritdoc/>
    public async Task<ToolsConfig> LoadAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);

        try
        {
            return await LoadCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(ToolsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        await _lock.WaitAsync().ConfigureAwait(false);

        try
        {
            await SaveCoreAsync(config).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<ToolsConfig> LoadCoreAsync()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return ToolsConfig.CreateDefault();
        }

        try
        {
            var json = await File.ReadAllTextAsync(ConfigFilePath).ConfigureAwait(false);
            return Parse(json);
        }
        catch (JsonException)
        {
            // JSON inválido: misma política que la autodetección de smartctl.
            return ToolsConfig.CreateDefault();
        }
        catch (IOException)
        {
            return ToolsConfig.CreateDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return ToolsConfig.CreateDefault();
        }
    }

    /// <summary>
    /// Parsea el JSON con tolerancia al formato de herramientas.json.
    /// Propiedades ausentes o de tipo inesperado usan el valor por defecto.
    /// </summary>
    internal static ToolsConfig Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ToolsConfig.CreateDefault();
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var config = ToolsConfig.CreateDefault();
        if (root.ValueKind != JsonValueKind.Object)
        {
            return config;
        }

        if (root.TryGetProperty("smartctlPath", out var pathElement) &&
            pathElement.ValueKind == JsonValueKind.String)
        {
            config.SmartctlPath = pathElement.GetString() ?? string.Empty;
        }

        // Mismo criterio que SmartctlRunner: solo un false explícito desactiva
        // la autodetección; ausente o tipo inesperado => true.
        config.SmartctlAutoDetect =
            !root.TryGetProperty("smartctlAutoDetect", out var autoElement) ||
            autoElement.ValueKind != JsonValueKind.False;

        return config;
    }

    private async Task SaveCoreAsync(ToolsConfig config)
    {
        var directory = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var normalized = new ToolsConfig
        {
            SmartctlPath = (config.SmartctlPath ?? string.Empty).Trim(),
            SmartctlAutoDetect = config.SmartctlAutoDetect
        };

        var json = JsonSerializer.Serialize(normalized, SerializerOptions) + Environment.NewLine;
        var buffer = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);

        // Escritura atómica: archivo temporal en el mismo directorio (misma unidad)
        // y reemplazo con File.Move(overwrite). Un lector concurrente nunca ve un
        // JSON a medio escribir.
        var tempPath = Path.Combine(
            directory ?? Path.GetTempPath(),
            $".{Path.GetFileName(ConfigFilePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllBytesAsync(tempPath, buffer).ConfigureAwait(false);
            File.Move(tempPath, ConfigFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                    // El temporal queda inerte; nunca se interpreta como configuración.
                }
                catch (UnauthorizedAccessException)
                {
                    // Ídem: no se interrumpe el error original de guardado.
                }
            }
        }
    }
}
