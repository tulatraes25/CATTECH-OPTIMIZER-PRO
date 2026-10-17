using System.Text.Json;
using System.Text.RegularExpressions;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Data;

namespace Cattech.Optimizer.Pro.Infrastructure.Reports;

/// <summary>
/// Local journal is append-only BY THIS APPLICATION, not cryptographically
/// tamper-proof. A written review is NOT a certificate of device ownership
/// and it never authorizes attributed reports before C2-C/D are implemented.
/// </summary>
internal sealed record SourceBindingJournalEntry(
    int SchemaVersion, Guid EventId, string ReviewerAlias,
    ServiceSourceBindingClaim Claim);

internal enum SourceBindingJournalStatus
{
    Recorded,
    ReviewRequired,
    SourceUnavailable,
    UnsafeStore,
    InvalidJournal,
    ConflictingClaim,
    WriteFailed
}

internal sealed record SourceBindingJournalOutcome(
    SourceBindingJournalStatus Status, Guid? BindingId = null);

/// <summary>
/// SPEC #92 C2-B2-A: local, append-only claim receipts for future explicit
/// technician-reviewed binding workflow. No public HTML/PDF integration.
/// Files are staged then published without overwrite under a process lease.
/// Every source is independently fingerprinted before and after acquiring
/// the journal lease; a stored claim is invalid without export-time recheck.
/// </summary>
internal sealed class SourceBindingReviewJournal
{
    private const int MaxJournalEntryBytes = 8192;
    private readonly string _journalDirectory;
    private readonly string _marker;
    private readonly ReportSourceFingerprintReader _sourceReader;

    private static readonly Regex ReviewerPattern =
        new(@"\A[A-Za-z0-9_-]{3,48}\z", RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        MaxDepth = 24
    };

    internal SourceBindingReviewJournal(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        var dataRoot = Path.GetFullPath(Path.Combine(baseDirectory, "data"));
        _journalDirectory = Path.Combine(dataRoot, "source-binding-journal");
        _marker = Path.Combine(_journalDirectory, ".cattech-source-binding.lock");
        _sourceReader = new ReportSourceFingerprintReader(baseDirectory);
    }

    internal async Task<SourceBindingJournalOutcome> RecordReviewedAsync(
        Guid serviceCaseId, string sourceKind, string relativeSourcePath,
        string reviewerAlias, bool explicitConsent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!explicitConsent || serviceCaseId == Guid.Empty
            || reviewerAlias is null || !ReviewerPattern.IsMatch(reviewerAlias))
            return new(SourceBindingJournalStatus.ReviewRequired);

        var first = await _sourceReader.InspectAsync(sourceKind,
            relativeSourcePath, cancellationToken).ConfigureAwait(false);
        if (!first.HasReadableSnapshot)
            return new(SourceBindingJournalStatus.SourceUnavailable);

        try
        {
            if (!JournalAncestryIsSafe())
                return new(SourceBindingJournalStatus.UnsafeStore);
            Directory.CreateDirectory(_journalDirectory);
            if (!JournalAncestryIsSafe() || MarkerIsReparsePoint())
                return new(SourceBindingJournalStatus.UnsafeStore);

            // Permanent marker + OS sharing lock: concurrent cooperating
            // CATTECH processes cannot publish conflicting claims.
            await using var lease = await JsonServiceReportService
                .AcquireStoreLeaseAsync(_marker, TimeSpan.FromSeconds(15))
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var previous = await ReadAllEntriesAsync(cancellationToken)
                .ConfigureAwait(false);
            if (previous is null)
                return new(SourceBindingJournalStatus.InvalidJournal);

            // Capture again under the lease. Changes between captures,
            // including same-ID/different-content replacements, fail closed.
            var current = await _sourceReader.InspectAsync(sourceKind,
                relativeSourcePath, cancellationToken).ConfigureAwait(false);
            if (!current.HasReadableSnapshot || current.Fingerprint != first.Fingerprint)
                return new(SourceBindingJournalStatus.SourceUnavailable);

            var snapshot = current.Fingerprint!;
            var claim = new ServiceSourceBindingClaim(
                ServiceSourceBindingClaimPolicy.SchemaVersion,
                Guid.NewGuid(), serviceCaseId, snapshot.SourceKind,
                snapshot.SourceRecordId, snapshot.Sha256, snapshot.RelativePath,
                DateTimeOffset.UtcNow,
                ServiceSourceBindingClaimPolicy.ExplicitReview);
            var allClaims = previous.Select(e => e.Claim)
                .Append(claim).ToArray();
            var preflight = ServiceSourceBindingClaimPolicy.Validate(
                claim, serviceCaseId, sourceKind, snapshot.SourceRecordId,
                snapshot.Sha256, relativeSourcePath, allClaims);
            if (preflight != ServiceSourceBindingFailure.None)
                return new(SourceBindingJournalStatus.ConflictingClaim);

            var entry = new SourceBindingJournalEntry(
                1, Guid.NewGuid(), reviewerAlias, claim);
            var text = JsonSerializer.Serialize(entry, JsonOptions);
            if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxJournalEntryBytes)
                return new(SourceBindingJournalStatus.WriteFailed);

            var suffix = Guid.NewGuid().ToString("N");
            var pending = Path.Combine(_journalDirectory,
                ".cattech-binding-" + suffix + ".tmp");
            var destination = Path.Combine(_journalDirectory,
                "binding-" + claim.BindingId.ToString("N") + ".json");
            try
            {
                await using (var stream = new FileStream(pending, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous))
                await using (var writer = new StreamWriter(stream,
                    new System.Text.UTF8Encoding(false)))
                {
                    await writer.WriteAsync(text.AsMemory(), cancellationToken)
                        .ConfigureAwait(false);
                    await writer.FlushAsync(cancellationToken)
                        .ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                File.Move(pending, destination); // Must not overwrite existing data.

                // No success claim until the exact published record can be
                // read back in the journal's fail-closed inventory.
                var readback = await ReadAllEntriesAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (readback is null || readback.Count != previous.Count + 1
                    || readback.Count(e => e.Claim == claim
                        && e.EventId == entry.EventId) != 1)
                    return new(SourceBindingJournalStatus.WriteFailed);

                return new(SourceBindingJournalStatus.Recorded, claim.BindingId);
            }
            finally
            {
                try { if (File.Exists(pending)) File.Delete(pending); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return new(SourceBindingJournalStatus.WriteFailed);
        }
        catch (IOException)
        {
            return new(SourceBindingJournalStatus.WriteFailed);
        }
        catch (UnauthorizedAccessException)
        {
            return new(SourceBindingJournalStatus.WriteFailed);
        }
    }

    private bool JournalAncestryIsSafe()
    {
        // This prevents known redirected stores. Atomic directory-handle
        // identity on hostile machines remains a C2-E physical QA gate.
        for (var path = _journalDirectory; path is not null;
            path = Path.GetDirectoryName(path))
        {
            if (Directory.Exists(path)
                && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                return false;
            if (string.Equals(path, Path.GetPathRoot(path),
                    StringComparison.OrdinalIgnoreCase))
                break;
        }
        return true;
    }

    private bool MarkerIsReparsePoint() =>
        File.Exists(_marker)
        && (File.GetAttributes(_marker) & FileAttributes.ReparsePoint) != 0;

    private async Task<List<SourceBindingJournalEntry>?> ReadAllEntriesAsync(
        CancellationToken cancellationToken)
    {
        var records = new List<SourceBindingJournalEntry>();
        foreach (var path in Directory.EnumerateFiles(
            _journalDirectory, "binding-*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                return null;

            await using var stream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
            if (stream.Length <= 0 || stream.Length > MaxJournalEntryBytes)
                return null;
            var buffer = new byte[(int)stream.Length];
            try
            {
                await stream.ReadExactlyAsync(buffer.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                using var doc = JsonDocument.Parse(buffer.AsMemory());
                if (!HasUniqueJsonProperties(doc.RootElement))
                    return null;
                var entry = doc.RootElement.Deserialize<SourceBindingJournalEntry>(
                    JsonOptions);
                if (entry is null || entry.SchemaVersion != 1
                    || entry.EventId == Guid.Empty
                    || !ReviewerPattern.IsMatch(entry.ReviewerAlias ?? "")
                    || entry.Claim is null || entry.Claim.BindingId == Guid.Empty
                    || entry.Claim.ServiceCaseId == Guid.Empty
                    || entry.Claim.SchemaVersion != 1
                    || entry.Claim.ConfirmationMethod !=
                        ServiceSourceBindingClaimPolicy.ExplicitReview
                    || !ServiceSourceBindingClaimPolicy.IsSafeHash(
                        entry.Claim.SourceSha256)
                    || !ServiceSourceBindingClaimPolicy.IsSafeRelativePath(
                        entry.Claim.RelativeSourcePath)
                    || !ServiceSourceBindingClaimPolicy.IsSafeSourceRecordId(
                        entry.Claim.SourceRecordId)
                    || Path.GetFileName(path) !=
                        "binding-" + entry.Claim.BindingId.ToString("N") + ".json")
                    return null;
                records.Add(entry);
            }
            catch (JsonException)
            {
                return null;
            }
            finally
            {
                Array.Clear(buffer, 0, buffer.Length);
            }
        }

        // Even if someone manually injected two syntactically valid files,
        // do not silently select the most recent record.
        if (records.Select(e => e.Claim.BindingId).Distinct().Count()
            != records.Count)
            return null;
        if (records.GroupBy(e => (e.Claim.SourceKind, e.Claim.SourceRecordId))
            .Any(g => g.Count() > 1))
            return null;
        if (records.GroupBy(e => e.Claim.RelativeSourcePath,
                StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            return null;
        return records;
    }

    private static bool HasUniqueJsonProperties(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in node.EnumerateObject())
                if (!names.Add(property.Name) ||
                    !HasUniqueJsonProperties(property.Value))
                    return false;
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in node.EnumerateArray())
                if (!HasUniqueJsonProperties(element))
                    return false;
        }
        return true;
    }
}
