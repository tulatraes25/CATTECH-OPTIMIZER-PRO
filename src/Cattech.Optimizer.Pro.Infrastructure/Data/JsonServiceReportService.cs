using System.Diagnostics;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.Infrastructure.Data;

/// <summary>
/// Detailed, privacy-minimized report-store inventory. <see cref="DamagedFiles"/>
/// contains ONLY file names (timestamp + random token), never paths, client
/// names or payloads. The snapshot is advisory: load/delete revalidate.
/// </summary>
public sealed record ServiceReportInventoryDetail(
    ServiceReportInventory Inventory,
    IReadOnlyList<string> DamagedFiles)
{
    public bool HasDamagedFiles => DamagedFiles.Count > 0;
}

/// <summary>
/// Optional read-only capability that also reports the NAMES of damaged files
/// so the UI can show a concrete warning without hiding the remaining valid
/// records. Implementations never delete or repair those files.
/// </summary>
public interface IServiceReportInventoryDetailReader
{
    Task<ServiceReportInventoryDetail> InspectReportInventoryDetailedAsync();
}

/// <summary>
/// Implementación de IServiceReportService que persiste reportes en JSON.
/// Los archivos se guardan en data/service-reports/service-report-YYYYMMDD-HHMMSS.json
/// </summary>
public class JsonServiceReportService : IServiceReportService,
    IServiceReportInventoryReader, IServiceReportInventoryDetailReader
{
    private readonly string _reportsDirectory;
    private readonly string _storeLeasePath;
    private static readonly TimeSpan StoreLeaseTimeout = TimeSpan.FromSeconds(15);
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
        _storeLeasePath = Path.Combine(_reportsDirectory, ".cattech-service-reports.lock");
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
            // The entire write is coordinated with cooperating CATTECH
            // instances, not just callers sharing this SemaphoreSlim.
            await using var lease = await AcquireStoreLeaseAsync(
                _storeLeasePath, StoreLeaseTimeout).ConfigureAwait(false);

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

            await using var lease = await AcquireStoreLeaseAsync(
                _storeLeasePath, StoreLeaseTimeout).ConfigureAwait(false);
            // The first matching logical ID is NEVER sufficient: older
            // report IDs may be only eight hex characters, and a snapshot
            // may have been saved more than once with the same ID.
            // Read-only loads tolerate damaged unrelated siblings: they are
            // skipped with a count, so one corrupt file cannot hide every
            // other client. Deletion below remains strict.
            var match = await FindUniqueReportAsync(reportId,
                tolerateUnreadableSiblings: true).ConfigureAwait(false);
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

            await using var lease = await AcquireStoreLeaseAsync(
                _storeLeasePath, StoreLeaseTimeout).ConfigureAwait(false);
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
        => (await InspectReportInventoryDetailedAsync().ConfigureAwait(false))
            .Inventory;

    /// <summary>
    /// Read-only inventory that ALSO returns the file names of damaged
    /// entries. A corrupt file is skipped (never deleted, repaired or
    /// interpreted) and counted; the remaining valid records are still
    /// enumerated so one damaged sibling cannot hide every other client.
    /// File names are timestamp + random tokens, not client data or paths.
    /// </summary>
    public async Task<ServiceReportInventoryDetail> InspectReportInventoryDetailedAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_reportsDirectory))
                return new(new ServiceReportInventory(0, 0, 0, 0, false),
                    Array.Empty<string>());

            // Never publish "healthy" while a cooperating writer is mid-save.
            await using var lease = await AcquireStoreLeaseAsync(
                _storeLeasePath, StoreLeaseTimeout).ConfigureAwait(false);
            string[] files;
            try
            {
                files = Directory.GetFiles(_reportsDirectory, "service-report-*.json");
            }
            catch (IOException)
            {
                return new(new ServiceReportInventory(0, 0, 0, 0, true),
                    Array.Empty<string>());
            }
            catch (UnauthorizedAccessException)
            {
                return new(new ServiceReportInventory(0, 0, 0, 0, true),
                    Array.Empty<string>());
            }

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var damaged = new List<string>();
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                    var report = DeserializeValidatedReport(json);
                    if (report is null)
                    {
                        damaged.Add(Path.GetFileName(file));
                        continue;
                    }

                    counts.TryGetValue(report.Id, out var count);
                    counts[report.Id] = count + 1;
                }
                catch (IOException) { damaged.Add(Path.GetFileName(file)); }
                catch (UnauthorizedAccessException) { damaged.Add(Path.GetFileName(file)); }
                catch (JsonException) { damaged.Add(Path.GetFileName(file)); }
            }

            damaged.Sort(StringComparer.OrdinalIgnoreCase);
            var duplicates = counts.Values.Where(count => count > 1).Sum();
            var inventory = new ServiceReportInventory(files.Length,
                counts.Values.Sum() - duplicates, duplicates, damaged.Count, false);
            return new(inventory, damaged);
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

            await using var lease = await AcquireStoreLeaseAsync(
                _storeLeasePath, StoreLeaseTimeout).ConfigureAwait(false);
            // Never delete the first apparent match: multiple stored files
            // can carry the same logical Id. This is a best-effort local
            // uniqueness gate, NOT an inter-process atomic identity lock.
            // Unlike reads, deletion stays strict: an unreadable sibling may
            // carry the same logical ID, so it blocks the deletion.
            var match = await FindUniqueReportAsync(reportId,
                tolerateUnreadableSiblings: false).ConfigureAwait(false);
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
    /// Locate one readable stored record by logical ID.
    /// When <paramref name="tolerateUnreadableSiblings"/> is true (read-only
    /// loads) damaged/unreadable files are skipped so they cannot hide a
    /// uniquely readable match; two READABLE records with the same ID still
    /// fail as ambiguous. Deletion passes false so an unreadable sibling
    /// blocks the operation. Caller already holds this instance's gate.
    /// </summary>
    private async Task<(ServiceReport Report, string FilePath)?>
        FindUniqueReportAsync(string? reportId, bool tolerateUnreadableSiblings)
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
                {
                    if (tolerateUnreadableSiblings)
                        continue;
                    return null;
                }

                if (string.Equals(report.Id, reportId,
                    StringComparison.Ordinal))
                {
                    if (match is not null)
                        return null; // Ambiguous ID: no first-match fallback.
                    match = (report, file);
                }
            }
            catch (IOException)
            {
                if (tolerateUnreadableSiblings)
                    continue;
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                if (tolerateUnreadableSiblings)
                    continue;
                return null;
            }
            catch (JsonException)
            {
                if (tolerateUnreadableSiblings)
                    continue;
                return null;
            }
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

    /// <summary>
    /// Cooperative cross-process gate for the service-report store. A marker
    /// file is deliberately retained: an abandoned process releases its OS
    /// handle, so neither PID files nor risky stale-lock deletion are needed.
    /// This does not pin each report file handle against uncooperative writes.
    /// </summary>
    internal static async Task<FileStream> AcquireStoreLeaseAsync(
        string leasePath, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        cancellationToken.ThrowIfCancellationRequested();

        var watch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(leasePath, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None, 1, FileOptions.None);
            }
            catch (IOException ex) when ((ex.HResult & 0xFFFF) is 32 or 33)
            {
                if (watch.Elapsed >= timeout)
                    throw new TimeoutException(
                        "Otro proceso está usando el almacenamiento de informes. No se modificó.", ex);

                // A changed case/withdrawn consent must stop waiting for
                // another process rather than retry for the full 15 seconds.
                await Task.Delay(TimeSpan.FromMilliseconds(35), cancellationToken)
                    .ConfigureAwait(false);
            }
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

