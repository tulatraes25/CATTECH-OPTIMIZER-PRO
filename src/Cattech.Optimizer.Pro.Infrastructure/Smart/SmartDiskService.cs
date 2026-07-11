using System.Diagnostics;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.Infrastructure.Smart;

/// <summary>
/// Implementación de ISmartDiskService.
/// Analiza discos SMART de forma no invasiva usando smartctl.
/// </summary>
public class SmartDiskService : ISmartDiskService
{
    private readonly ISmartctlRunner _smartctlRunner;
    private readonly string _reportsDirectory;
    private static readonly TimeSpan DiskAnalysisTimeout = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SmartDiskService(ISmartctlRunner smartctlRunner, string? baseDirectory = null)
    {
        _smartctlRunner = smartctlRunner;
        _reportsDirectory = Path.Combine(
            baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory,
            "data",
            "smart-reports");
    }

    /// <inheritdoc/>
    public async Task<SmartAnalysisResult> AnalyzeAllDisksAsync()
    {
        var result = new SmartAnalysisResult
        {
            StartedAt = DateTime.Now
        };

        // Verificar disponibilidad de smartctl
        var availability = await _smartctlRunner.CheckAvailabilityAsync();
        result.SmartctlAvailable = availability.IsAvailable;
        result.SmartctlVersion = availability.Version;

        if (!availability.IsAvailable)
        {
            result.Errors.Add($"Smartctl no disponible: {availability.ErrorMessage}");
            result.FinishedAt = DateTime.Now;
            return result;
        }

        // Obtener lista de discos
        var devices = await _smartctlRunner.ListDevicesAsync();
        result.DevicesAnalyzed = devices.Count;

        if (devices.Count == 0)
        {
            result.Warnings.Add("No se encontraron dispositivos de almacenamiento");
            result.FinishedAt = DateTime.Now;
            return result;
        }

        // Analizar cada disco
        foreach (var device in devices)
        {
            try
            {
                var report = await AnalyzeDiskInternalAsync(device, availability.Version);
                result.Reports.Add(report);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error al analizar {device.Name}: {ex.Message}");
                result.Reports.Add(new SmartDiskReport
                {
                    Device = device.Name,
                    DeviceName = device.InfoName,
                    DeviceType = device.ApproximateDiskType,
                    HealthStatus = SmartHealthStatus.Unknown,
                    HealthSummary = $"Error: {ex.Message}",
                    IsAnalysisSuccessful = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        result.FinishedAt = DateTime.Now;
        return result;
    }

    /// <inheritdoc/>
    public async Task<SmartDiskReport> AnalyzeDiskAsync(SmartDiskDevice device)
    {
        var availability = await _smartctlRunner.CheckAvailabilityAsync();

        if (!availability.IsAvailable)
        {
            return new SmartDiskReport
            {
                Device = device.Name,
                DeviceName = device.InfoName,
                DeviceType = device.ApproximateDiskType,
                HealthStatus = SmartHealthStatus.NotAvailable,
                HealthSummary = "Smartctl no disponible",
                IsAnalysisSuccessful = false,
                ErrorMessage = availability.ErrorMessage
            };
        }

        return await AnalyzeDiskInternalAsync(device, availability.Version);
    }

    /// <inheritdoc/>
    public async Task<string> SaveResultAsync(SmartAnalysisResult result)
    {
        EnsureDirectoryExists();

        var fileName = $"smart-analysis-{result.StartedAt:yyyyMMdd-HHmmss}.json";
        var filePath = Path.Combine(_reportsDirectory, fileName);

        if (File.Exists(filePath))
        {
            fileName = $"smart-analysis-{result.StartedAt:yyyyMMdd-HHmmss}-{result.Id}.json";
            filePath = Path.Combine(_reportsDirectory, fileName);
        }

        var json = JsonSerializer.Serialize(result, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json);

        return fileName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SmartAnalysisResult>> ListResultsAsync(int maxResults = 20)
    {
        var results = new List<SmartAnalysisResult>();

        if (!Directory.Exists(_reportsDirectory))
            return results;

        var files = Directory.GetFiles(_reportsDirectory, "smart-analysis-*.json")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .Take(maxResults);

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var analysis = JsonSerializer.Deserialize<SmartAnalysisResult>(json, SerializerOptions);
                if (analysis != null)
                    results.Add(analysis);
            }
            catch { }
        }

        return results;
    }

    // --- Métodos internos ---

    private async Task<SmartDiskReport> AnalyzeDiskInternalAsync(SmartDiskDevice device, string smartctlVersion)
    {
        // Ejecutar smartctl -a -j para leer SMART completo
        var result = await _smartctlRunner.RunAsync($"-a -j {device.Name}", DiskAnalysisTimeout);

        if (result.TimedOut)
        {
            return new SmartDiskReport
            {
                Device = device.Name,
                DeviceName = device.InfoName,
                DeviceType = device.ApproximateDiskType,
                Protocol = device.Protocol,
                ModelName = device.ModelName,
                SerialNumber = device.SerialNumber,
                SmartctlVersion = smartctlVersion,
                HealthStatus = SmartHealthStatus.Unknown,
                HealthSummary = "Timeout: smartctl tardó demasiado en responder",
                IsAnalysisSuccessful = false,
                ErrorMessage = "Timeout"
            };
        }

        if (!result.IsSuccess && string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return new SmartDiskReport
            {
                Device = device.Name,
                DeviceName = device.InfoName,
                DeviceType = device.ApproximateDiskType,
                Protocol = device.Protocol,
                ModelName = device.ModelName,
                SerialNumber = device.SerialNumber,
                SmartctlVersion = smartctlVersion,
                HealthStatus = SmartHealthStatus.NotAvailable,
                HealthSummary = "No se pudo obtener datos SMART",
                IsAnalysisSuccessful = false,
                ErrorMessage = result.StandardError
            };
        }

        // Parsear JSON
        return SmartctlParser.ParseSmartJson(result.StandardOutput, device, smartctlVersion);
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_reportsDirectory))
            Directory.CreateDirectory(_reportsDirectory);
    }
}
