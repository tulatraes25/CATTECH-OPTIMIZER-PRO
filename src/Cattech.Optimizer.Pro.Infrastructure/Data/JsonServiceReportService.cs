using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.Infrastructure.Data;

/// <summary>
/// Implementación de IServiceReportService que persiste reportes en JSON.
/// Los archivos se guardan en data/service-reports/service-report-YYYYMMDD-HHMMSS.json
/// </summary>
public class JsonServiceReportService : IServiceReportService, IServiceReportInventoryReader
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

        // Validate a serialized snapshot BEFORE creating a directory or file.
        // A malformed new record must never poison all existing reads.
        // Persist exactly the bytes that passed the common read contract.
        var json = JsonSerializer.Serialize(report, SerializerOptions);
        if (DeserializeValidatedReport(json) is null)
            throw new ArgumentException(
                "El informe contiene datos estructurales inválidos.", nameof(report));

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
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_reportsDirectory))
                return null;

            // The first matching logical ID is NEVER sufficient: older
            // report IDs may be only eight hex characters, and a snapshot
            // may have been saved more than once with the same ID.
            var match = await FindUniqueReportAsync(reportId)
                .ConfigureAwait(false);
            return match?.Report;
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

            if (maxResults <= 0)
                return summaries;

            // Corrupt files must not consume the limit and hide valid entries.
            // The legacy list is not a certificate of safe load/delete.
            var files = Directory.GetFiles(_reportsDirectory, "service-report-*.json")
                .OrderByDescending(f => File.GetLastWriteTime(f));

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var report = DeserializeValidatedReport(json);

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
                        if (summaries.Count >= maxResults)
                            break;
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

    /// <summary>
    /// Read-only diagnostic inventory. The returned snapshot never grants
    /// permission to load or delete reports; those actions revalidate.
    /// No client details, paths or raw exception messages are returned.
    /// </summary>
    public async Task<ServiceReportInventory> InspectReportInventoryAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_reportsDirectory))
                return new ServiceReportInventory(0, 0, 0, 0, false);

            string[] files;
            try
            {
                files = Directory.GetFiles(_reportsDirectory, "service-report-*.json");
            }
            catch (IOException)
            {
                return new ServiceReportInventory(0, 0, 0, 0, true);
            }
            catch (UnauthorizedAccessException)
            {
                return new ServiceReportInventory(0, 0, 0, 0, true);
            }

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var invalid = 0;
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                    var report = DeserializeValidatedReport(json);
                    if (report is null)
                    {
                        invalid++;
                        continue;
                    }

                    counts.TryGetValue(report.Id, out var count);
                    counts[report.Id] = count + 1;
                }
                catch (IOException) { invalid++; }
                catch (UnauthorizedAccessException) { invalid++; }
                catch (JsonException) { invalid++; }
            }

            var duplicates = counts.Values.Where(count => count > 1).Sum();
            return new ServiceReportInventory(files.Length, counts.Values.Sum() - duplicates,
                duplicates, invalid, false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteReportAsync(string reportId)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_reportsDirectory))
                return false;

            // Never delete the first apparent match: multiple stored files
            // can carry the same logical Id. This is a best-effort local
            // uniqueness gate, NOT an inter-process atomic identity lock.
            var match = await FindUniqueReportAsync(reportId)
                .ConfigureAwait(false);
            if (match is null)
                return false;

            try
            {
                File.Delete(match.Value.FilePath);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Reject ambiguous, unreadable, or malformed report directories.
    /// Reading a corrupt file cannot prove that its logical ID is distinct,
    /// so returning any other report (or deleting it) would be unsafe.
    /// Caller already holds this instance's gate.
    /// </summary>
    private async Task<(ServiceReport Report, string FilePath)?>
        FindUniqueReportAsync(string? reportId)
    {
        if (string.IsNullOrWhiteSpace(reportId))
            return null;

        (ServiceReport Report, string FilePath)? match = null;
        var files = Directory.GetFiles(_reportsDirectory,
            "service-report-*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file)
                    .ConfigureAwait(false);
                var report = DeserializeValidatedReport(json);
                if (report is null)
                    return null;

                if (string.Equals(report.Id, reportId,
                    StringComparison.Ordinal))
                {
                    if (match is not null)
                        return null; // Ambiguous ID: no first-match fallback.
                    match = (report, file);
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (JsonException) { return null; }
        }

        return match;
    }

    /// <summary>
    /// Shared persisted-record contract for inventory, list and load/delete.
    /// Require exactly one id/client/equipment/service JSON key to avoid
    /// silently defaulting missing sections. Legacy short IDs are accepted.
    /// Unknown extra fields and absent optional service arrays are accepted.
    /// </summary>
    private static ServiceReport? DeserializeValidatedReport(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!TrySingleProperty(root, "id", JsonValueKind.String, out var id)
            || string.IsNullOrWhiteSpace(id.GetString())
            || !TrySingleProperty(root, "client", JsonValueKind.Object, out _)
            || !TrySingleProperty(root, "equipment", JsonValueKind.Object, out _)
            || !TrySingleProperty(root, "service", JsonValueKind.Object, out var service)
            || !ValidOptionalStringArray(service, "actionsPerformed")
            || !ValidOptionalStringArray(service, "recommendations"))
            return null;

        var report = JsonSerializer.Deserialize<ServiceReport>(json, SerializerOptions);
        if (report is null || !string.Equals(report.Id, id.GetString(), StringComparison.Ordinal)
            || report.Client is null || report.Equipment is null
            || report.Service is null || report.Service.ActionsPerformed is null
            || report.Service.Recommendations is null)
            return null;
        return report;
    }

    private static bool TrySingleProperty(
        JsonElement obj, string name, JsonValueKind expected, out JsonElement value)
    {
        value = default;
        if (obj.ValueKind != JsonValueKind.Object)
            return false;
        var found = false;
        foreach (var property in obj.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (found || !property.NameEquals(name) || property.Value.ValueKind != expected)
                return false;
            value = property.Value;
            found = true;
        }
        return found;
    }

    private static bool ValidOptionalStringArray(JsonElement obj, string name)
    {
        var found = false;
        foreach (var property in obj.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (found || !property.NameEquals(name)
                || property.Value.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var element in property.Value.EnumerateArray())
                if (element.ValueKind != JsonValueKind.String)
                    return false;
            found = true;
        }
        return true;
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_reportsDirectory))
        {
            Directory.CreateDirectory(_reportsDirectory);
        }
    }
}

