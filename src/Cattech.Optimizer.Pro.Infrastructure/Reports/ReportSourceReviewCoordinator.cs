using System.Security.Cryptography;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.Infrastructure.Reports;

/// <summary>
/// A short-lived LOCAL review proposal; neither signed evidence nor permission
/// for HTML/PDF export. Contains hashes and local record IDs, never customer
/// names, serial numbers or a raw source payload.
/// </summary>
internal sealed record SourceReviewPreview(
    string ServiceReportId,
    Guid ServiceCaseId,
    string ServiceReportSha256,
    string SourceKind,
    string SourceRecordId,
    string SourceRelativePath,
    string SourceSha256,
    DateTimeOffset ReviewedAtUtc);

internal enum SourceReviewCoordinationStatus
{
    Prepared,
    Recorded,
    ReviewRequired,
    CaseUnavailable,
    SourceUnavailable,
    AmbiguousSource,
    SourceChanged,
    ExpiredPreview,
    StorageFailure
}

internal sealed record SourceReviewCoordinationResult(
    SourceReviewCoordinationStatus Status,
    SourceReviewPreview? Preview = null,
    Guid? BindingId = null);

/// <summary>
/// SPEC #92 C2-C0: combine verified saved service case and exact historical
/// source fingerprint BEFORE offering future explicit WPF review. C2-C UI
/// and C2-D export are still disabled. No source or client files are edited.
/// </summary>
internal sealed class ReportSourceReviewCoordinator
{
    private readonly IServiceReportService _serviceReports;
    private readonly ReportSourceFingerprintReader _reader;
    private readonly SourceBindingReviewJournal _journal;
    private readonly Func<DateTimeOffset> _clock;
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(5);

    internal ReportSourceReviewCoordinator(
        IServiceReportService serviceReports, string? baseDirectory = null,
        Func<DateTimeOffset>? clock = null)
    {
        _serviceReports = serviceReports ??
            throw new ArgumentNullException(nameof(serviceReports));
        var root = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        _reader = new ReportSourceFingerprintReader(root);
        _journal = new SourceBindingReviewJournal(root);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    internal async Task<SourceReviewCoordinationResult> PrepareAsync(
        ServiceReport? selectedCase, string sourceKind, string sourceRecordId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (selectedCase?.ServiceCaseId is not { } caseId || caseId == Guid.Empty
            || string.IsNullOrWhiteSpace(selectedCase.Id))
            return new(SourceReviewCoordinationStatus.CaseUnavailable);
        if (!ServiceSourceBindingClaimPolicy.IsSafeSourceRecordId(sourceRecordId))
            return new(SourceReviewCoordinationStatus.SourceUnavailable);

        try
        {
            var stored = await _serviceReports.LoadReportAsync(selectedCase.Id)
                .ConfigureAwait(false);
            if (stored is null || stored.ServiceCaseId != caseId)
                return new(SourceReviewCoordinationStatus.CaseUnavailable);

            var original = HashServiceReport(selectedCase);
            if (!string.Equals(HashServiceReport(stored), original,
                    StringComparison.Ordinal))
                return new(SourceReviewCoordinationStatus.CaseUnavailable);

            var resolved = await _reader.ResolveUniqueAsync(sourceKind,
                sourceRecordId, cancellationToken).ConfigureAwait(false);
            if (resolved.Status == SourceIdResolutionStatus.Ambiguous)
                return new(SourceReviewCoordinationStatus.AmbiguousSource);
            if (resolved.Status != SourceIdResolutionStatus.FoundUnique
                || resolved.Fingerprint is null)
                return new(SourceReviewCoordinationStatus.SourceUnavailable);

            var source = resolved.Fingerprint;
            return new(SourceReviewCoordinationStatus.Prepared,
                new SourceReviewPreview(selectedCase.Id, caseId, original,
                    sourceKind, sourceRecordId, source.RelativePath,
                    source.Sha256, _clock()));
        }
        catch (IOException)
        {
            return new(SourceReviewCoordinationStatus.StorageFailure);
        }
        catch (UnauthorizedAccessException)
        {
            return new(SourceReviewCoordinationStatus.StorageFailure);
        }
        catch (JsonException)
        {
            return new(SourceReviewCoordinationStatus.StorageFailure);
        }
    }

    internal async Task<SourceReviewCoordinationResult> ConfirmAsync(
        SourceReviewPreview? preview, string reviewerAlias, bool explicitConsent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!explicitConsent || preview is null)
            return new(SourceReviewCoordinationStatus.ReviewRequired);
        var age = _clock() - preview.ReviewedAtUtc;
        if (age < TimeSpan.Zero || age > PreviewLifetime)
            return new(SourceReviewCoordinationStatus.ExpiredPreview);
        if (preview.ServiceCaseId == Guid.Empty
            || !ServiceSourceBindingClaimPolicy.IsSafeHash(
                preview.ServiceReportSha256)
            || !ServiceSourceBindingClaimPolicy.IsSafeHash(
                preview.SourceSha256)
            || !ServiceSourceBindingClaimPolicy.IsSafeSourceRecordId(
                preview.SourceRecordId))
            return new(SourceReviewCoordinationStatus.ReviewRequired);

        try
        {
            var currentCase = await _serviceReports.LoadReportAsync(
                preview.ServiceReportId).ConfigureAwait(false);
            if (currentCase is null
                || currentCase.ServiceCaseId != preview.ServiceCaseId
                || !string.Equals(HashServiceReport(currentCase),
                    preview.ServiceReportSha256, StringComparison.Ordinal))
                return new(SourceReviewCoordinationStatus.CaseUnavailable);

            var resolved = await _reader.ResolveUniqueAsync(preview.SourceKind,
                preview.SourceRecordId, cancellationToken).ConfigureAwait(false);
            if (resolved.Status == SourceIdResolutionStatus.Ambiguous)
                return new(SourceReviewCoordinationStatus.AmbiguousSource);
            if (resolved.Status != SourceIdResolutionStatus.FoundUnique
                || resolved.Fingerprint is null)
                return new(SourceReviewCoordinationStatus.SourceUnavailable);
            if (!string.Equals(resolved.Fingerprint.Sha256,
                    preview.SourceSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(resolved.Fingerprint.RelativePath,
                    preview.SourceRelativePath, StringComparison.Ordinal))
                return new(SourceReviewCoordinationStatus.SourceChanged);

            var receipt = await _journal.RecordReviewedByIdAsync(
                preview.ServiceCaseId, preview.SourceKind,
                preview.SourceRecordId, preview.SourceSha256,
                reviewerAlias, true, cancellationToken).ConfigureAwait(false);
            return receipt.Status switch
            {
                SourceBindingJournalStatus.Recorded =>
                    new(SourceReviewCoordinationStatus.Recorded,
                        BindingId: receipt.BindingId),
                SourceBindingJournalStatus.ReviewRequired =>
                    new(SourceReviewCoordinationStatus.ReviewRequired),
                SourceBindingJournalStatus.SourceChangedSinceReview =>
                    new(SourceReviewCoordinationStatus.SourceChanged),
                SourceBindingJournalStatus.ConflictingClaim =>
                    new(SourceReviewCoordinationStatus.AmbiguousSource),
                SourceBindingJournalStatus.SourceUnavailable =>
                    new(SourceReviewCoordinationStatus.SourceUnavailable),
                _ => new(SourceReviewCoordinationStatus.StorageFailure)
            };
        }
        catch (IOException)
        {
            return new(SourceReviewCoordinationStatus.StorageFailure);
        }
        catch (UnauthorizedAccessException)
        {
            return new(SourceReviewCoordinationStatus.StorageFailure);
        }
        catch (JsonException)
        {
            return new(SourceReviewCoordinationStatus.StorageFailure);
        }
    }

    private static string HashServiceReport(ServiceReport report) =>
        Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(report)));
}
