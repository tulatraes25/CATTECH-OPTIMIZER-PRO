using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.Infrastructure.Data;

/// <summary>
/// Implementación de IServiceReportService que persiste reportes en JSON.
/// Los archivos se guardan en data/service-reports/service-report-YYYYMMDD-HHMMSS.json
/// </summary>
public class JsonServiceReportService : IServiceReportService
{
    private readonly string _reportsDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonServiceReportService(string? baseDirectory = null)
    {
        _reportsDirectory = Path.Combine(
            baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory,
            "data",
            "service-reports");
    }

    /// <inheritdoc/>
    public async Task<string> SaveReportAsync(ServiceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        await _lock.WaitAsync().ConfigureAwait(false);

        try
        {
            EnsureDirectoryExists();

            // The timestamp and report ID are not sufficient under concurrent
            // writers, repeated saves or across independent service instances.
            // Keep arbitrary report IDs out of the filesystem path.
            var token = Guid.NewGuid().ToString("N");
            var fileName = $"service-report-{report.CreatedAt:yyyyMMdd-HHmmss}-{token}.json";
            var filePath = Path.Combine(_reportsDirectory, fileName);
            var stagedPath = Path.Combine(_reportsDirectory, $".cattech-service-{token}.tmp");
            var json = JsonSerializer.Serialize(report, SerializerOptions);

            try
            {
                // A failure before publication can only leave a temporary file.
                // The final name is published atomically, without overwriting.
                await using (var stream = new FileStream(stagedPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 8192, useAsync: true))
                await using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8))
                {
                    await writer.WriteAsync(json).ConfigureAwait(false);
                }

                File.Move(stagedPath, filePath);
                return fileName;
            }
            finally
            {
                try
                {
                    if (File.Exists(stagedPath))
                        File.Delete(stagedPath);
                }
                catch (IOException) { /* Keep the original failure. */ }
                catch (UnauthorizedAccessException) { /* Keep the original failure. */ }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<ServiceReport?> LoadReportAsync(string reportId)
    {
        await _lock.WaitAsync();

        try
        {
            if (!Directory.Exists(_reportsDirectory))
                return null;

            var files = Directory.GetFiles(_reportsDirectory, "service-report-*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var report = JsonSerializer.Deserialize<ServiceReport>(json, SerializerOptions);

                    if (report?.Id == reportId)
                        return report;
                }
                catch
                {
                    // Saltar archivos corruptos
                }
            }

            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<List<ServiceReportSummary>> ListReportsAsync(int maxResults = 50)
    {
        await _lock.WaitAsync();

        try
        {
            var summaries = new List<ServiceReportSummary>();

            if (!Directory.Exists(_reportsDirectory))
                return summaries;

            var files = Directory.GetFiles(_reportsDirectory, "service-report-*.json")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .Take(maxResults);

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var report = JsonSerializer.Deserialize<ServiceReport>(json, SerializerOptions);

                    if (report != null)
                    {
                        summaries.Add(new ServiceReportSummary
                        {
                            Id = report.Id,
                            ClientName = report.Client.Name,
                            EquipmentBrand = report.Equipment.Brand,
                            EquipmentModel = report.Equipment.Model,
                            ServiceReason = report.Equipment.ServiceReason,
                            CreatedAt = report.CreatedAt,
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
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteReportAsync(string reportId)
    {
        await _lock.WaitAsync();

        try
        {
            if (!Directory.Exists(_reportsDirectory))
                return false;

            var files = Directory.GetFiles(_reportsDirectory, "service-report-*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var report = JsonSerializer.Deserialize<ServiceReport>(json, SerializerOptions);

                    if (report?.Id == reportId)
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
        finally
        {
            _lock.Release();
        }
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_reportsDirectory))
        {
            Directory.CreateDirectory(_reportsDirectory);
        }
    }
}
