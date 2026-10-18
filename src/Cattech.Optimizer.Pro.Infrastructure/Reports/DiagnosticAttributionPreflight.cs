using System.Security.Cryptography;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.Infrastructure.Reports;

/// <summary>
/// Read-only case/diagnostic/journal PRE-FLIGHT. No report export permission.
/// A revocation or file replacement may occur after this check returns.
/// </summary>
internal enum DiagnosticAttributionPreflightStatus
{
    SnapshotForFurtherValidation,
    InvalidSelection,
    CaseUnavailable,
    AmbiguousSource,
    SourceUnavailable,
    SourceChanged,
    BindingMissing,
    BindingRevoked,
    CaseMismatch,
    AuditUnavailable
}

/// <summary>
/// Stable private model serialization, never a retained reference to WPF
/// selectedCase or to the mutable DiagnosticReport returned by a file reader.
/// Callers obtain a NEW detached clone each time. Not an export permit.
/// </summary>
internal sealed class DiagnosticAttributionCandidate
{
    private readonly string _caseJson;
    private readonly string _diagnosticJson;

    internal Guid BindingId { get; }
    internal Guid ServiceCaseId { get; }
    internal string SavedCaseSha256 { get; }
    internal string SourceRecordId { get; }
    internal string SourceSha256 { get; }

    internal DiagnosticAttributionCandidate(
        Guid bindingId, Guid serviceCaseId, string savedCaseSha256,
        ServiceReport savedCase, DiagnosticSourceSnapshot diagnostic)
    {
        BindingId = bindingId;
        ServiceCaseId = serviceCaseId;
        SavedCaseSha256 = savedCaseSha256;
        SourceRecordId = diagnostic.Fingerprint.SourceRecordId;
        SourceSha256 = diagnostic.Fingerprint.Sha256;
        _caseJson = JsonSerializer.Serialize(savedCase);
        _diagnosticJson = JsonSerializer.Serialize(diagnostic.Report);
    }

    internal (ServiceReport SavedCase, Cattech.Optimizer.Pro.Core.Models.Diagnostics.DiagnosticReport Diagnostic)
        ReadDetachedModels()
    {
        var saved = JsonSerializer.Deserialize<ServiceReport>(_caseJson)
            ?? throw new InvalidOperationException("Invalid captured service report.");
        var source = JsonSerializer.Deserialize<Cattech.Optimizer.Pro.Core.Models.Diagnostics.DiagnosticReport>(
            _diagnosticJson)
            ?? throw new InvalidOperationException("Invalid captured diagnostic.");
        return (saved, source);
    }
}

internal sealed record DiagnosticAttributionPreflightResult(
    DiagnosticAttributionPreflightStatus Status,
    DiagnosticAttributionCandidate? Candidate = null);

internal sealed class DiagnosticAttributionPreflight
{
    private readonly IServiceReportService _cases;
    private readonly ReportSourceFingerprintReader _reader;
    private readonly SourceBindingReviewJournal _journal;

    internal DiagnosticAttributionPreflight(
        IServiceReportService cases, string baseDirectory)
    {
        _cases = cases ?? throw new ArgumentNullException(nameof(cases));
        _reader = new ReportSourceFingerprintReader(baseDirectory);
        _journal = new SourceBindingReviewJournal(baseDirectory);
    }

    internal async Task<DiagnosticAttributionPreflightResult> InspectAsync(
        ServiceReport? selectedCase, string diagnosticId, Guid bindingId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (selectedCase?.ServiceCaseId is not { } caseId || caseId == Guid.Empty
            || string.IsNullOrWhiteSpace(selectedCase.Id)
            || bindingId == Guid.Empty
            || !ServiceSourceBindingClaimPolicy.IsSafeSourceRecordId(diagnosticId))
            return new(DiagnosticAttributionPreflightStatus.InvalidSelection);

        try
        {
            // The selected in-memory customer is NOT authoritative.
            var selectedHash = HashCase(selectedCase);
            var stored = await _cases.LoadReportAsync(selectedCase.Id)
                .ConfigureAwait(false);
            if (stored is null || stored.ServiceCaseId != caseId
                || HashCase(stored) != selectedHash)
                return new(DiagnosticAttributionPreflightStatus.CaseUnavailable);

            var unique = await _reader.ResolveUniqueAsync(
                "Diagnostic", diagnosticId, cancellationToken).ConfigureAwait(false);
            if (unique.Status == SourceIdResolutionStatus.Ambiguous)
                return new(DiagnosticAttributionPreflightStatus.AmbiguousSource);
            if (unique.Status != SourceIdResolutionStatus.FoundUnique
                || unique.Fingerprint is null)
                return new(DiagnosticAttributionPreflightStatus.SourceUnavailable);

            var first = await _journal.InspectCurrentAsync(
                bindingId, caseId, "Diagnostic",
                unique.Fingerprint.RelativePath, cancellationToken).ConfigureAwait(false);
            if (first.Status != SourceBindingReadStatus.CurrentSnapshot)
                return new(Map(first.Status));

            // Deserialization and SHA are from the SAME path-verified handle.
            var capture = await _reader.CaptureDiagnosticAsync(
                unique.Fingerprint.RelativePath, cancellationToken).ConfigureAwait(false);
            if (!capture.HasReadableSnapshot)
                return new(DiagnosticAttributionPreflightStatus.SourceUnavailable);
            var diagnostic = capture.Snapshot!;
            if (diagnostic.Fingerprint != unique.Fingerprint
                || diagnostic.Report.Id != diagnosticId)
                return new(DiagnosticAttributionPreflightStatus.SourceChanged);
            if (diagnostic.Report.ServiceCaseId is { } embedded && embedded != caseId)
                return new(DiagnosticAttributionPreflightStatus.CaseMismatch);

            // Re-check both audit/revocation and the persisted case AFTER capture.
            // This is intentionally still NOT an output-scoped atomic lease.
            var second = await _journal.InspectCurrentAsync(
                bindingId, caseId, "Diagnostic",
                diagnostic.Fingerprint.RelativePath, cancellationToken)
                .ConfigureAwait(false);
            if (second.Status != SourceBindingReadStatus.CurrentSnapshot)
                return new(Map(second.Status));

            cancellationToken.ThrowIfCancellationRequested();
            var refreshed = await _cases.LoadReportAsync(selectedCase.Id)
                .ConfigureAwait(false);
            if (refreshed is null || refreshed.ServiceCaseId != caseId
                || HashCase(refreshed) != selectedHash
                || HashCase(selectedCase) != selectedHash)
                return new(DiagnosticAttributionPreflightStatus.CaseUnavailable);

            return new(DiagnosticAttributionPreflightStatus.SnapshotForFurtherValidation,
                new DiagnosticAttributionCandidate(
                    bindingId, caseId, selectedHash, refreshed, diagnostic));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            return new(DiagnosticAttributionPreflightStatus.AuditUnavailable);
        }
        catch (UnauthorizedAccessException)
        {
            return new(DiagnosticAttributionPreflightStatus.AuditUnavailable);
        }
        catch (JsonException)
        {
            return new(DiagnosticAttributionPreflightStatus.AuditUnavailable);
        }
    }

    private static DiagnosticAttributionPreflightStatus Map(
        SourceBindingReadStatus status) => status switch
    {
        SourceBindingReadStatus.UnknownBinding =>
            DiagnosticAttributionPreflightStatus.BindingMissing,
        SourceBindingReadStatus.Revoked =>
            DiagnosticAttributionPreflightStatus.BindingRevoked,
        SourceBindingReadStatus.CaseMismatch =>
            DiagnosticAttributionPreflightStatus.CaseMismatch,
        SourceBindingReadStatus.SourceChanged =>
            DiagnosticAttributionPreflightStatus.SourceChanged,
        SourceBindingReadStatus.SourceUnavailable =>
            DiagnosticAttributionPreflightStatus.SourceUnavailable,
        _ => DiagnosticAttributionPreflightStatus.AuditUnavailable
    };

    private static string HashCase(ServiceReport report) =>
        Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(report)));
}
