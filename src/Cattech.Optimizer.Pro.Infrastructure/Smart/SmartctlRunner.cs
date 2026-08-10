using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.Infrastructure.Smart;

/// <summary>
/// Implementación de ISmartctlRunner.
/// Ejecuta smartctl como binario externo (no integra código GPL).
/// </summary>
public class SmartctlRunner : ISmartctlRunner
{
    private readonly string? _configuredPath;
    private readonly string _baseDirectory;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public SmartctlRunner(string? configuredPath = null, string? baseDirectory = null)
    {
        _configuredPath = configuredPath;
        _baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
    }

    /// <inheritdoc/>
    public async Task<SmartctlAvailability> CheckAvailabilityAsync()
    {
        var availability = new SmartctlAvailability
        {
            CheckedAt = DateTime.Now
        };

        try
        {
            var smartctlPath = await FindSmartctlPathAsync();

            if (string.IsNullOrEmpty(smartctlPath))
            {
                availability.IsAvailable = false;
                availability.ErrorMessage = "smartctl.exe no encontrado. Verifique que smartmontools esté instalado o configure la ruta en config/herramientas.json";
                return availability;
            }

            availability.SmartctlPath = smartctlPath;
            availability.IsAvailable = true;

            // Obtener versión: disponible si el proceso ejecutó sin timeout y hay salida de versión válida
            var versionResult = await RunSmartctlAsync(smartctlPath, "--version");
            if (!versionResult.TimedOut && !versionResult.HasInvocationFailure)
            {
                availability.Version = SmartctlParser.ParseVersion(versionResult.StandardOutput);
            }

            // Verificar soporte JSON: validar que la salida del scan contiene JSON parseable.
            // El exit status del scan no determina el soporte por sí solo.
            var scanResult = await RunSmartctlAsync(smartctlPath, "--scan -j");
            availability.SupportsJson =
                !scanResult.TimedOut &&
                !scanResult.HasInvocationFailure &&
                !string.IsNullOrWhiteSpace(scanResult.StandardOutput) &&
                SmartctlParser.ParseScanJson(scanResult.StandardOutput).Count > 0;

            return availability;
        }
        catch (Exception ex)
        {
            availability.IsAvailable = false;
            availability.ErrorMessage = $"Error al verificar smartctl: {ex.Message}";
            return availability;
        }
    }

    /// <inheritdoc/>
    public async Task<SmartctlCommandResult> RunAsync(string arguments, TimeSpan timeout)
    {
        var smartctlPath = await FindSmartctlPathAsync();

        if (string.IsNullOrEmpty(smartctlPath))
        {
            return new SmartctlCommandResult
            {
                ExitCode = -1,
                StandardError = "smartctl.exe no encontrado",
                TimedOut = false,
                DurationMs = 0
            };
        }

        return await RunSmartctlAsync(smartctlPath, arguments, timeout);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SmartDiskDevice>> ListDevicesAsync()
    {
        var availability = await CheckAvailabilityAsync();
        if (!availability.IsAvailable)
            return Array.Empty<SmartDiskDevice>();

        var devices = new List<SmartDiskDevice>();

        // Intentar con JSON primero
        if (availability.SupportsJson)
        {
            var jsonResult = await RunSmartctlAsync(availability.SmartctlPath, "--scan -j");

            // Salida parcial parseable: no se descarta todo solo por exit status no cero
            if (!jsonResult.TimedOut && !jsonResult.HasInvocationFailure &&
                !string.IsNullOrWhiteSpace(jsonResult.StandardOutput))
            {
                devices.AddRange(SmartctlParser.ParseScanJson(jsonResult.StandardOutput));
            }
        }

        // Si no hay dispositivos o JSON falló, intentar con texto
        if (devices.Count == 0)
        {
            var textResult = await RunSmartctlAsync(availability.SmartctlPath, "--scan");

            if (!textResult.TimedOut && !textResult.HasInvocationFailure &&
                !string.IsNullOrWhiteSpace(textResult.StandardOutput))
            {
                devices.AddRange(SmartctlParser.ParseScanText(textResult.StandardOutput));
            }
        }

        return devices;
    }

    // --- Métodos internos ---

    private async Task<string?> FindSmartctlPathAsync()
    {
        // 1. Ruta configurada por el llamador (tests/programática)
        if (!string.IsNullOrEmpty(_configuredPath) && File.Exists(_configuredPath))
            return _configuredPath;

        // 2. Ruta configurada en config/herramientas.json (smartctlPath / smartctlAutoDetect)
        var configResult = ReadToolsConfig();
        if (configResult.HasValue)
        {
            var (configuredPath, autoDetect) = configResult.Value;
            if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
                return configuredPath;

            // smartctlAutoDetect=false y sin ruta válida: no auto-detección
            if (!autoDetect)
                return null;
        }

        // 3. Rutas comunes junto a la app
        var appDir = _baseDirectory;
        var localPaths = new[]
        {
            Path.Combine(appDir, "tools", "smartmontools", "smartctl.exe"),
            Path.Combine(appDir, "tools", "smartmontools", "bin", "smartctl.exe")
        };

        foreach (var path in localPaths)
        {
            if (File.Exists(path))
                return path;
        }

        // 3. Rutas de instalación comunes
        var installPaths = new[]
        {
            @"C:\Program Files\smartmontools\bin\smartctl.exe",
            @"C:\Program Files (x86)\smartmontools\bin\smartctl.exe"
        };

        foreach (var path in installPaths)
        {
            if (File.Exists(path))
                return path;
        }

        // 4. Buscar en PATH
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "smartctl.exe",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var firstLine = output.Split('\n')[0].Trim();
                if (File.Exists(firstLine))
                    return firstLine;
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Lee config/herramientas.json (smartctlPath, smartctlAutoDetect).
    /// Archivo ausente o inválido → null (se usa autodetección).
    /// </summary>
    private (string SmartctlPath, bool AutoDetect)? ReadToolsConfig()
    {
        try
        {
            var configPath = Path.Combine(_baseDirectory, "config", "herramientas.json");
            if (!File.Exists(configPath))
                return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            var root = doc.RootElement;

            var path = root.TryGetProperty("smartctlPath", out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() ?? string.Empty
                : string.Empty;

            var autoDetect = !root.TryGetProperty("smartctlAutoDetect", out var a) ||
                             a.ValueKind != JsonValueKind.False;

            return (path, autoDetect);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<SmartctlCommandResult> RunSmartctlAsync(
        string smartctlPath, string arguments, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;
        var sw = Stopwatch.StartNew();

        var result = new SmartctlCommandResult();

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = smartctlPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.Start();

            // Leer stdout y stderr en paralelo
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            // Esperar con timeout
            try
            {
                await process.WaitForExitAsync().WaitAsync(effectiveTimeout);
            }
            catch (TimeoutException)
            {
                try { process.Kill(); } catch { }
                result.TimedOut = true;
                result.ExitCode = -1;
                result.StandardError = $"Timeout después de {effectiveTimeout.TotalSeconds} segundos";
                return result;
            }

            result.StandardOutput = await stdoutTask;
            result.StandardError = await stderrTask;
            result.ExitCode = process.ExitCode;
        }
        catch (Exception ex)
        {
            result.ExitCode = -1;
            result.StandardError = $"Error al ejecutar smartctl: {ex.Message}";
        }

        sw.Stop();
        result.DurationMs = sw.ElapsedMilliseconds;

        return result;
    }
}
