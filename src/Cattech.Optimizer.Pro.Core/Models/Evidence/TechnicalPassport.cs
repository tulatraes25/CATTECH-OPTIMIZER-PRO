using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cattech.Optimizer.Pro.Core.Models.Evidence;

/// <summary>
/// Counts and controlled status names only. Neither source evidence nor arbitrary
/// client, user, machine, file or diagnostic text is copied into this contract.
/// </summary>
public sealed record PassportStatusCount(string Status, int Count);

public sealed record PassportEvidenceCoverage(
    int Observed,
    int Unavailable,
    int AccessDenied,
    int Unsupported,
    int InvalidData);

/// <summary>
/// Privacy-minimized, deterministic v1 payload for an explicitly requested export.
/// Diagnostic and repair entries are aggregated; no private rule text or IDs leave
/// the Core layer. This is not a complete technical service history.
/// </summary>
public sealed record TechnicalPassportPublicPayload(
    string SchemaVersion,
    Guid ExportId,
    DateTimeOffset IssuedAtUtc,
    PassportEvidenceCoverage Evidence,
    IReadOnlyList<PassportStatusCount> DiagnosticAssessments,
    IReadOnlyList<PassportStatusCount> OutcomeAssessments,
    string IntegrityNotice);

/// <summary>
/// SHA-256 detects byte-level corruption when the expected digest is protected
/// independently. It is NOT a digital signature, provenance proof or evidence
/// that a repair took place.
/// </summary>
public sealed class TechnicalPassportArtifact
{
    internal TechnicalPassportArtifact(
        string payloadJson, DiagnosticContextId contextId)
    {
        if (!contextId.IsValid)
            throw new ArgumentException(
                "A passport must be bound to its original diagnostic case.",
                nameof(contextId));
        PayloadJson = payloadJson ?? throw new ArgumentNullException(
            nameof(payloadJson));
        ContextId = contextId;
        Sha256Hex = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
    }

    // Private presentation gate metadata: NEVER serialize the service-session
    // identity into public JSON. A matching digest is not a signature.
    [System.Text.Json.Serialization.JsonIgnore]
    public DiagnosticContextId ContextId { get; }

    public string PayloadJson { get; }
    public string Sha256Hex { get; }

    public static bool MatchesDigest(string? payloadJson, string? expectedSha256Hex)
    {
        if (payloadJson is null || expectedSha256Hex is null
            || expectedSha256Hex.Length != 64
            || !expectedSha256Hex.All(Uri.IsHexDigit))
            return false;

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson));
        var expected = Convert.FromHexString(expectedSha256Hex);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public bool HasMatchingDigest() => MatchesDigest(PayloadJson, Sha256Hex);
}

/// <summary>
/// EE-05A: in-memory export, no disk writes, no networking, no automatic export.
/// Caller must obtain consent before sharing the returned JSON externally.
/// </summary>
public static class TechnicalPassportBuilder
{
    private const int MaximumEvidence = 10_000;
    private const int MaximumAssessments = 256;
    private const string SchemaVersion = "cattech.technical-passport.public.v1";
    private const string Notice =
        "SHA-256 detects changes only. No signature, authenticity or repair guarantee.";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

    public static TechnicalPassportArtifact Build(
        DiagnosticContextId contextId,
        Guid freshExportId,
        DateTimeOffset issuedAt,
        IEnumerable<EvidenceRecord> evidence,
        IEnumerable<DiagnosticAssessment> diagnostics,
        IEnumerable<RepairOutcomeAssessment> outcomes)
    {
        if (!contextId.IsValid)
            throw new ArgumentException("Cannot export an unscoped diagnostic case.",
                nameof(contextId));
        if (freshExportId == Guid.Empty || freshExportId == contextId.Value)
            throw new ArgumentException("A distinct random export ID is required.",
                nameof(freshExportId));

        var records = CaptureBounded(evidence, MaximumEvidence, nameof(evidence));
        // No evidence is NOT evidence that a machine is healthy, repaired
        // or even observed. To report an inaccessible/unsupported sensor,
        // capture an explicit Unavailable/AccessDenied evidence record.
        // Never mint an empty 'technical passport' from a case GUID alone.
        if (records.Length == 0)
            throw new ArgumentException(
                "At least one scoped evidence record is required for a passport.",
                nameof(evidence));

        var assessments = CaptureBounded(
            diagnostics, MaximumAssessments, nameof(diagnostics));
        var postchecks = CaptureBounded(
            outcomes, MaximumAssessments, nameof(outcomes));

        // Every record and derived assessment must belong to exactly one
        // explicitly selected equipment service session. Unknown or mixed
        // contexts fail closed rather than polluting a client report.
        if (records.Any(item => item.ContextId != contextId))
            throw new ArgumentException("Mixed equipment evidence is not exportable.",
                nameof(evidence));
        if (assessments.Any(item => item.ContextId != contextId))
            throw new ArgumentException("A diagnostic assessment belongs to another case.",
                nameof(diagnostics));
        if (postchecks.Any(item => item.ContextId != contextId))
            throw new ArgumentException("A repair outcome belongs to another case.",
                nameof(outcomes));

        // Reusing an evidence ID (even with identical data) can inflate counts.
        if (records.Select(item => item.Id).Distinct().Count() != records.Length)
            throw new ArgumentException("Duplicate evidence IDs are not exportable.",
                nameof(evidence));
        if (records.Any(item => item.Id == freshExportId))
            throw new ArgumentException(
                "The export ID must not reuse a source evidence ID.",
                nameof(freshExportId));

        // Assessments and postchecks are countable events in the public
        // passport. Without a distinct identity for repeated evaluations,
        // listing the same logical evaluation twice inflates outcome totals.
        // Different disks/rules remain legitimate when linked evidence differs.
        if (assessments.Select(DiagnosticBasisKey)
            .Distinct(StringComparer.Ordinal).Count() != assessments.Length)
            throw new ArgumentException(
                "A passport cannot count a diagnostic basis twice.",
                nameof(diagnostics));

        if (postchecks.Select(item => (item.RuleId, item.RuleVersion,
                item.FactKey, item.BeforeEvidenceId, item.AfterEvidenceId))
            .Distinct().Count() != postchecks.Length)
            throw new ArgumentException(
                "A passport cannot count one postcheck pair twice.",
                nameof(outcomes));

        var atUtc = issuedAt.ToUniversalTime();
        if (records.Any(item => item.ObservedAtUtc > atUtc))
            throw new ArgumentException(
                "Future-dated observations require clock review before export.",
                nameof(issuedAt));

        var recordsById = records.ToDictionary(item => item.Id);
        var knownIds = recordsById.Keys.ToHashSet();
        var sensitiveIds = records.Where(item =>
            item.PrivacyClass == EvidencePrivacyClass.Sensitive)
            .Select(item => item.Id).ToHashSet();

        foreach (var assessment in assessments)
        {
            if (!Enum.IsDefined(assessment.Status))
                throw new ArgumentException("Unknown diagnostic assessment state.",
                    nameof(diagnostics));

            if (assessment.SupportingEvidenceIds
                .Concat(assessment.ContradictingEvidenceIds)
                .Any(id => !knownIds.Contains(id)))
                throw new ArgumentException("Assessment refers to missing evidence.",
                    nameof(diagnostics));

            // A record with the same opaque ID can represent different facts
            // after a stale reload or accidental cross-report rebinding.
            // A countable conclusion must still point to the exact immutable
            // observations that the evaluator actually inspected.
            if (assessment.EvaluatedObservations.Any(pair =>
                    !recordsById.TryGetValue(pair.Key, out var current)
                    || current != pair.Value))
                throw new ArgumentException(
                    "Diagnostic evaluated a different evidence snapshot.",
                    nameof(diagnostics));
        }

        foreach (var outcome in postchecks)
        {
            if (!Enum.IsDefined(outcome.Status)
                || !Enum.IsDefined(outcome.Reason))
                throw new ArgumentException("Unknown outcome state.", nameof(outcomes));

            if (!knownIds.Contains(outcome.BeforeEvidenceId)
                || !knownIds.Contains(outcome.AfterEvidenceId))
                throw new ArgumentException(
                    "Outcome refers to missing evidence.", nameof(outcomes));

            var before = recordsById[outcome.BeforeEvidenceId];
            var after = recordsById[outcome.AfterEvidenceId];
            if (before != outcome.EvaluatedBefore
                || after != outcome.EvaluatedAfter)
                throw new ArgumentException(
                    "Postcheck evaluated a different evidence snapshot.",
                    nameof(outcomes));
            if (!outcome.SubjectId.IsValid || before.SubjectId != after.SubjectId
                || before.SubjectId != outcome.SubjectId)
                throw new ArgumentException(
                    "Outcome evidence refers to incompatible components.",
                    nameof(outcomes));
        }

        // Deliberate omission: sensitive records, labels, arbitrary rule text,
        // identifiers, serial numbers, dumps, user paths, measurements and
        // source metadata never enter the public payload.
        var visibleEvidence = records.Where(item =>
            item.PrivacyClass != EvidencePrivacyClass.Sensitive).ToArray();

        // A missing/future-dated fact has no trustworthy per-record link to
        // its potential sensitivity class. Fail closed rather than leak
        // an incomplete diagnostic status through public aggregate counts.
        var safeAssessments = assessments.Where(item =>
            item.MissingFactKeys.Count == 0
            && item.FutureDatedFactKeys.Count == 0
            && (item.SupportingEvidenceIds.Count > 0
                || item.ContradictingEvidenceIds.Count > 0)
            && !item.SupportingEvidenceIds.Any(sensitiveIds.Contains)
            && !item.ContradictingEvidenceIds.Any(sensitiveIds.Contains));
        var safeOutcomes = postchecks.Where(item =>
            !sensitiveIds.Contains(item.BeforeEvidenceId)
            && !sensitiveIds.Contains(item.AfterEvidenceId));

        var payload = new TechnicalPassportPublicPayload(
            SchemaVersion,
            freshExportId,
            atUtc,
            new PassportEvidenceCoverage(
                visibleEvidence.Count(item =>
                    item.Availability == EvidenceAvailability.Observed),
                visibleEvidence.Count(item =>
                    item.Availability == EvidenceAvailability.Unavailable),
                visibleEvidence.Count(item =>
                    item.Availability == EvidenceAvailability.AccessDenied),
                visibleEvidence.Count(item =>
                    item.Availability == EvidenceAvailability.Unsupported),
                visibleEvidence.Count(item =>
                    item.Availability == EvidenceAvailability.InvalidData)),
            SummarizeStatuses(safeAssessments.Select(item => item.Status)),
            SummarizeStatuses(safeOutcomes.Select(item => item.Status)),
            Notice);

        // Fixed-property contract and sorted status buckets make the same
        // input snapshot byte-stable regardless of input enumeration order.
        return new TechnicalPassportArtifact(
            JsonSerializer.Serialize(payload, JsonOptions), contextId);
    }

    /// <summary>
    /// In-memory canonical identity for one derived diagnostic based on
    /// source evidence and fact gaps. No customer ID, serialized keys or
    /// private rule contents leave this function or enter the public payload.
    /// The assessment has no evaluation event ID/time, so repeating the same
    /// basis as two counts would falsely inflate public conclusions.
    /// </summary>
    private static string DiagnosticBasisKey(DiagnosticAssessment assessment) =>
        JsonSerializer.Serialize(new
        {
            assessment.RuleId,
            assessment.RuleVersion,
            assessment.SupportingEvidenceIds,
            assessment.ContradictingEvidenceIds,
            Missing = assessment.MissingFactReferences
                .Select(item => new { item.SubjectId, item.FactKey }).ToArray(),
            Future = assessment.FutureDatedFactReferences
                .Select(item => new { item.SubjectId, item.FactKey }).ToArray()
        });

    private static T[] CaptureBounded<T>(
        IEnumerable<T> source, int maxCount, string paramName) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        var snapshot = source.Take(maxCount + 1).ToArray();
        if (snapshot.Length > maxCount || snapshot.Any(item => item is null))
            throw new ArgumentException(
                "Export collection is too large or contains null entries.",
                paramName);
        return snapshot;
    }

    private static IReadOnlyList<PassportStatusCount> SummarizeStatuses<T>(
        IEnumerable<T> statuses) where T : struct, Enum
    {
        var buckets = statuses
            .GroupBy(item => item)
            .OrderBy(group => Convert.ToInt32(group.Key))
            .Select(group => new PassportStatusCount(group.Key.ToString(), group.Count()))
            .ToArray();
        return Array.AsReadOnly(buckets);
    }
}
