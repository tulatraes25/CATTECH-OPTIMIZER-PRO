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

/// <summary>A separate immutable-by-CATTECH event; the claim stays on disk.</summary>
internal sealed record SourceBindingRevocationEntry(
    int SchemaVersion, Guid EventId, Guid BindingId,
    DateTimeOffset RevokedAtUtc, string ReviewerAlias);

internal enum SourceBindingJournalStatus
{
    Recorded,
    Revoked,
    AlreadyRevoked,
    UnknownBinding,
    ReviewRequired,
    SourceUnavailable,
    UnsafeStore,
    InvalidJournal,
    ConflictingClaim,
    SourceChangedSinceReview,
    WriteFailed
}

internal sealed record SourceBindingJournalOutcome(
    SourceBindingJournalStatus Status, Guid? BindingId = null);

/// <summary>
/// Read-only CURRENT snapshot inspection, not a grant to create an attributed
/// HTML/PDF. A file or revocation may change as soon as this method returns.
/// </summary>
internal enum SourceBindingReadStatus
{
    CurrentSnapshot,
    UnknownBinding,
    Revoked,
    CaseMismatch,
    SourceChanged,
    SourceUnavailable,
    UnsafeStore,
    InvalidJournal
}

internal sealed record SourceBindingReadOutcome(SourceBindingReadStatus Status);

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

    /// <summary>
    /// Inspect one reviewed claim against the COMPLETE currently readable
    /// journal and a fresh on-disk source fingerprint. No directories, files,
    /// receipts or markers are created. Never cache this result as consent.
    /// The C2-D renderer must re-open and verify during its actual output
    /// transaction, not rely on a prior "CurrentSnapshot" status.
    /// </summary>
    internal async Task<SourceBindingReadOutcome> InspectCurrentAsync(
        Guid bindingId, Guid serviceCaseId, string sourceKind,
        string relativeSourcePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (bindingId == Guid.Empty || serviceCaseId == Guid.Empty
            || !ServiceSourceBindingClaimPolicy.IsSafeRelativePath(relativeSourcePath)
            || string.IsNullOrWhiteSpace(sourceKind))
            return new(SourceBindingReadStatus.UnsafeStore);
        if (!Directory.Exists(_journalDirectory))
            return new(SourceBindingReadStatus.UnknownBinding);

        try
        {
            if (!JournalAncestryIsSafe() || MarkerIsReparsePoint()
                || !File.Exists(_marker))
                return new(SourceBindingReadStatus.UnsafeStore);
            // FileMode.Open is intentionally non-creating. FileShare.None
            // coordinates with journal writers and readers without changing
            // persistent state. Busy or missing lock => no positive result.
            await using var lease = new FileStream(_marker, FileMode.Open,
                FileAccess.Read, FileShare.None, 1, FileOptions.Asynchronous);
            var claims = await ReadAllEntriesAsync(cancellationToken)
                .ConfigureAwait(false);
            var revocations = await ReadAllRevocationsAsync(cancellationToken)
                .ConfigureAwait(false);
            if (claims is null || revocations is null
                || revocations.Any(e => !claims.Any(c =>
                    c.Claim.BindingId == e.BindingId)))
                return new(SourceBindingReadStatus.InvalidJournal);

            var claimEntry = claims.SingleOrDefault(e => e.Claim.BindingId == bindingId);
            if (claimEntry is null)
                return new(SourceBindingReadStatus.UnknownBinding);
            if (revocations.Any(e => e.BindingId == bindingId))
                return new(SourceBindingReadStatus.Revoked);
            var claim = claimEntry.Claim;
            if (claim.ServiceCaseId != serviceCaseId)
                return new(SourceBindingReadStatus.CaseMismatch);
            if (!string.Equals(claim.SourceKind, sourceKind, StringComparison.Ordinal)
                || !string.Equals(claim.RelativeSourcePath, relativeSourcePath,
                    StringComparison.Ordinal))
                return new(SourceBindingReadStatus.SourceChanged);

            var observed = await _sourceReader.InspectAsync(sourceKind,
                relativeSourcePath, cancellationToken).ConfigureAwait(false);
            if (!observed.HasReadableSnapshot)
                return new(SourceBindingReadStatus.SourceUnavailable);
            var fingerprint = observed.Fingerprint!;
            var validation = ServiceSourceBindingClaimPolicy.Validate(
                claim, serviceCaseId, sourceKind, fingerprint.SourceRecordId,
                fingerprint.Sha256, fingerprint.RelativePath,
                claims.Select(e => e.Claim).ToArray());
            return validation == ServiceSourceBindingFailure.None
                ? new(SourceBindingReadStatus.CurrentSnapshot)
                : new(SourceBindingReadStatus.SourceChanged);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            return new(SourceBindingReadStatus.InvalidJournal);
        }
        catch (UnauthorizedAccessException)
        {
            return new(SourceBindingReadStatus.UnsafeStore);
        }
        catch (ArgumentException)
        {
            return new(SourceBindingReadStatus.UnsafeStore);
        }
    }

    /// <summary>
    /// Future C2-C confirmation entrypoint: accepts the EXACT SHA-256 that
    /// was displayed during source review and requires a unique real source
    /// ID in an approved store. Confirmation cannot silently switch files.
    /// No UI is connected to this internal API yet.
    /// </summary>
    internal async Task<SourceBindingJournalOutcome> RecordReviewedByIdAsync(
        Guid serviceCaseId, string sourceKind, string sourceRecordId,
        string reviewedSha256, string reviewerAlias, bool explicitConsent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!explicitConsent || serviceCaseId == Guid.Empty
            || !ServiceSourceBindingClaimPolicy.IsSafeHash(reviewedSha256)
            || !ServiceSourceBindingClaimPolicy.IsSafeSourceRecordId(sourceRecordId)
            || reviewerAlias is null || !ReviewerPattern.IsMatch(reviewerAlias))
            return new(SourceBindingJournalStatus.ReviewRequired);

        var resolved = await _sourceReader.ResolveUniqueAsync(sourceKind,
            sourceRecordId, cancellationToken).ConfigureAwait(false);
        if (resolved.Status == SourceIdResolutionStatus.Ambiguous)
            return new(SourceBindingJournalStatus.ConflictingClaim);
        if (resolved.Status != SourceIdResolutionStatus.FoundUnique
            || resolved.Fingerprint is null)
            return new(SourceBindingJournalStatus.SourceUnavailable);
        if (!string.Equals(resolved.Fingerprint.Sha256, reviewedSha256,
                StringComparison.OrdinalIgnoreCase))
            return new(SourceBindingJournalStatus.SourceChangedSinceReview);

        return await RecordReviewedAsync(serviceCaseId, sourceKind,
            resolved.Fingerprint.RelativePath, reviewerAlias, true,
            cancellationToken, reviewedSha256, requireUniqueSourceId: true)
            .ConfigureAwait(false);
    }

    internal async Task<SourceBindingJournalOutcome> RecordReviewedAsync(
        Guid serviceCaseId, string sourceKind, string relativeSourcePath,
        string reviewerAlias, bool explicitConsent,
        CancellationToken cancellationToken = default,
        string? expectedSourceSha256 = null,
        bool requireUniqueSourceId = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!explicitConsent || serviceCaseId == Guid.Empty
            || reviewerAlias is null || !ReviewerPattern.IsMatch(reviewerAlias))
            return new(SourceBindingJournalStatus.ReviewRequired);

        var first = await _sourceReader.InspectAsync(sourceKind,
            relativeSourcePath, cancellationToken).ConfigureAwait(false);
        if (!first.HasReadableSnapshot)
            return new(SourceBindingJournalStatus.SourceUnavailable);
        if (expectedSourceSha256 is not null
            && (!ServiceSourceBindingClaimPolicy.IsSafeHash(expectedSourceSha256)
                || !string.Equals(first.Fingerprint!.Sha256,
                    expectedSourceSha256, StringComparison.OrdinalIgnoreCase)))
            return new(SourceBindingJournalStatus.SourceChangedSinceReview);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!JournalAncestryIsSafe())
                return new(SourceBindingJournalStatus.UnsafeStore);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_journalDirectory);
            if (!JournalAncestryIsSafe() || MarkerIsReparsePoint())
                return new(SourceBindingJournalStatus.UnsafeStore);

            // Permanent marker + OS sharing lock: concurrent cooperating
            // CATTECH processes cannot publish conflicting claims.
            await using var lease = await JsonServiceReportService
                .AcquireStoreLeaseAsync(_marker, TimeSpan.FromSeconds(15), cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var previous = await ReadAllEntriesAsync(cancellationToken)
                .ConfigureAwait(false);
            var revocations = await ReadAllRevocationsAsync(cancellationToken)
                .ConfigureAwait(false);
            if (previous is null || revocations is null
                || revocations.Any(e => !previous.Any(c =>
                    c.Claim.BindingId == e.BindingId)))
                return new(SourceBindingJournalStatus.InvalidJournal);

            // Capture again under the lease. Changes between captures,
            // including same-ID/different-content replacements, fail closed.
            var current = await _sourceReader.InspectAsync(sourceKind,
                relativeSourcePath, cancellationToken).ConfigureAwait(false);
            if (!current.HasReadableSnapshot || current.Fingerprint != first.Fingerprint)
                return new(SourceBindingJournalStatus.SourceUnavailable);

            var snapshot = current.Fingerprint!;
            if (requireUniqueSourceId)
            {
                // Re-scan under the cooperating-process journal lease. An
                // unexpected new duplicate, an inaccessible sibling, a
                // renamed file or different bytes invalidates the review.
                var unique = await _sourceReader.ResolveUniqueAsync(
                    sourceKind, snapshot.SourceRecordId, cancellationToken)
                    .ConfigureAwait(false);
                if (unique.Status == SourceIdResolutionStatus.Ambiguous)
                    return new(SourceBindingJournalStatus.ConflictingClaim);
                if (unique.Status != SourceIdResolutionStatus.FoundUnique
                    || unique.Fingerprint != snapshot)
                    return new(SourceBindingJournalStatus.SourceChangedSinceReview);
            }
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
                // Last cooperative cancellation checkpoint BEFORE the
                // no-overwrite atomic publication of this audit receipt.
                cancellationToken.ThrowIfCancellationRequested();
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

    /// <summary>
    /// Append a revocation event without deleting/overwriting the reviewed
    /// source or earlier claim. No export consumer is yet wired to this API;
    /// C2-D must independently check the active journal at use time.
    /// </summary>
    internal async Task<SourceBindingJournalOutcome> RevokeAsync(
        Guid bindingId, string reviewerAlias, bool explicitConsent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!explicitConsent || bindingId == Guid.Empty
            || reviewerAlias is null || !ReviewerPattern.IsMatch(reviewerAlias))
            return new(SourceBindingJournalStatus.ReviewRequired);
        if (!Directory.Exists(_journalDirectory))
            return new(SourceBindingJournalStatus.UnknownBinding);
        try
        {
            if (!JournalAncestryIsSafe() || MarkerIsReparsePoint())
                return new(SourceBindingJournalStatus.UnsafeStore);
            await using var lease = await JsonServiceReportService
                .AcquireStoreLeaseAsync(_marker, TimeSpan.FromSeconds(15), cancellationToken)
                .ConfigureAwait(false);
            var claims = await ReadAllEntriesAsync(cancellationToken)
                .ConfigureAwait(false);
            var revoked = await ReadAllRevocationsAsync(cancellationToken)
                .ConfigureAwait(false);
            if (claims is null || revoked is null
                || revoked.Any(e => !claims.Any(c => c.Claim.BindingId == e.BindingId)))
                return new(SourceBindingJournalStatus.InvalidJournal);
            if (claims.Count(e => e.Claim.BindingId == bindingId) != 1)
                return new(SourceBindingJournalStatus.UnknownBinding);
            if (revoked.Any(e => e.BindingId == bindingId))
                return new(SourceBindingJournalStatus.AlreadyRevoked);

            var entry = new SourceBindingRevocationEntry(1, Guid.NewGuid(),
                bindingId, DateTimeOffset.UtcNow, reviewerAlias);
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxJournalEntryBytes)
                return new(SourceBindingJournalStatus.WriteFailed);

            var staged = Path.Combine(_journalDirectory,
                ".cattech-revocation-" + Guid.NewGuid().ToString("N") + ".tmp");
            var published = Path.Combine(_journalDirectory,
                "revoked-" + bindingId.ToString("N") + ".json");
            try
            {
                await using (var stream = new FileStream(staged, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous))
                await using (var writer = new StreamWriter(stream,
                    new System.Text.UTF8Encoding(false)))
                {
                    await writer.WriteAsync(json.AsMemory(), cancellationToken)
                        .ConfigureAwait(false);
                    await writer.FlushAsync(cancellationToken)
                        .ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(staged, published);
                var readback = await ReadAllRevocationsAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (readback is null || readback.Count != revoked.Count + 1
                    || readback.Count(e => e.EventId == entry.EventId
                        && e.BindingId == bindingId) != 1)
                    return new(SourceBindingJournalStatus.WriteFailed);

                return new(SourceBindingJournalStatus.Revoked, bindingId);
            }
            finally
            {
                try { if (File.Exists(staged)) File.Delete(staged); }
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

    private async Task<List<SourceBindingRevocationEntry>?> ReadAllRevocationsAsync(
        CancellationToken cancellationToken)
    {
        var records = new List<SourceBindingRevocationEntry>();
        foreach (var path in Directory.EnumerateFiles(
            _journalDirectory, "revoked-*.json"))
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
                using var document = JsonDocument.Parse(buffer.AsMemory());
                if (!HasUniqueJsonProperties(document.RootElement))
                    return null;
                var entry = document.RootElement.Deserialize<SourceBindingRevocationEntry>(
                    JsonOptions);
                if (entry is null || entry.SchemaVersion != 1
                    || entry.EventId == Guid.Empty || entry.BindingId == Guid.Empty
                    || entry.RevokedAtUtc == default
                    || !ReviewerPattern.IsMatch(entry.ReviewerAlias ?? "")
                    || Path.GetFileName(path) !=
                        "revoked-" + entry.BindingId.ToString("N") + ".json")
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
        if (records.Select(e => e.BindingId).Distinct().Count() != records.Count
            || records.Select(e => e.EventId).Distinct().Count() != records.Count)
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
