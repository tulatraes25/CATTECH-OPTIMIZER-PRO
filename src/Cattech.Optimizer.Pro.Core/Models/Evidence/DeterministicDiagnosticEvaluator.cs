using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Cattech.Optimizer.Pro.Core.Models.Evidence;

/// <summary>
/// Corroborated means that the configured expectations match observed facts.
/// It does NOT prove the root cause of an incident.
/// </summary>
public enum DiagnosticAssessmentStatus
{
    InsufficientEvidence,
    Corroborated,
    Refuted,
    ConflictingEvidence
}

public sealed record EvidenceExpectation
{
    public EvidenceExpectation(DiagnosticSubjectId subjectId, string factKey, EvidenceValue expected)
    {
        if (!subjectId.IsValid)
            throw new ArgumentException("Expected fact needs an explicit subject.", nameof(subjectId));
        if (string.IsNullOrWhiteSpace(factKey))
            throw new ArgumentException("Fact key is required.", nameof(factKey));
        ArgumentNullException.ThrowIfNull(expected);

        SubjectId = subjectId;
        FactKey = factKey;
        Expected = expected;
    }

    public DiagnosticSubjectId SubjectId { get; }
    public string FactKey { get; }
    public EvidenceValue Expected { get; }
}

/// <summary>
/// A versioned, pure-data diagnostic rule. One requirement per subject/fact pair.
/// The rule does not execute any repair or query the operating system.
/// </summary>
public sealed class DiagnosticRule
{
    public DiagnosticRule(
        string ruleId, string version, string hypothesis,
        string falsificationCriterion, IEnumerable<EvidenceExpectation> requiredFacts)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("Rule ID is required.", nameof(ruleId));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Rule version is required.", nameof(version));
        if (string.IsNullOrWhiteSpace(hypothesis))
            throw new ArgumentException("Hypothesis is required.", nameof(hypothesis));
        if (string.IsNullOrWhiteSpace(falsificationCriterion))
            throw new ArgumentException("Falsification criterion is required.",
                nameof(falsificationCriterion));
        ArgumentNullException.ThrowIfNull(requiredFacts);

        var requirements = requiredFacts.Take(65).ToArray();
        if (requirements.Length == 0 || requirements.Length > 64
            || requirements.Any(item => item is null))
            throw new ArgumentException("A rule requires 1-64 non-null facts.",
                nameof(requiredFacts));

        if (requirements.Select(item => (item.SubjectId, item.FactKey))
            .Distinct().Count() != requirements.Length)
            throw new ArgumentException("Subject/fact key combinations must be unique.",
                nameof(requiredFacts));

        RuleId = ruleId;
        Version = version;
        Hypothesis = hypothesis;
        FalsificationCriterion = falsificationCriterion;
        RequiredFacts = Array.AsReadOnly(requirements);
    }

    public string RuleId { get; }
    public string Version { get; }
    public string Hypothesis { get; }
    public string FalsificationCriterion { get; }
    public ReadOnlyCollection<EvidenceExpectation> RequiredFacts { get; }
}

/// <summary>
/// Identifies one expected observation without leaking a raw hardware identifier.
/// It is internal assessment metadata; the public passport never exports it.
/// </summary>
public sealed record DiagnosticFactReference
{
    public DiagnosticFactReference(DiagnosticSubjectId subjectId, string factKey)
    {
        if (!subjectId.IsValid)
            throw new ArgumentException("A missing fact must have an explicit subject.",
                nameof(subjectId));
        if (string.IsNullOrWhiteSpace(factKey))
            throw new ArgumentException("Fact key is required.", nameof(factKey));
        SubjectId = subjectId;
        FactKey = factKey;
    }

    public DiagnosticSubjectId SubjectId { get; }
    public string FactKey { get; }
}

/// <summary>
/// Evidence IDs only; sensitive raw values and paths are not copied into an assessment.
/// </summary>
public sealed class DiagnosticAssessment
{
    internal DiagnosticAssessment(DiagnosticContextId contextId, DiagnosticRule rule,
        DiagnosticAssessmentStatus status,
        IEnumerable<Guid> supporting, IEnumerable<Guid> contradicting,
        IEnumerable<DiagnosticFactReference> missing,
        IEnumerable<DiagnosticFactReference> futureDated,
        IEnumerable<EvidenceRecord> evaluatedObservations)
    {
        ContextId = contextId;
        RuleId = rule.RuleId;
        RuleVersion = rule.Version;
        Hypothesis = rule.Hypothesis;
        FalsificationCriterion = rule.FalsificationCriterion;
        Status = status;
        SupportingEvidenceIds = Array.AsReadOnly(
            supporting.Distinct().OrderBy(x => x).ToArray());
        ContradictingEvidenceIds = Array.AsReadOnly(
            contradicting.Distinct().OrderBy(x => x).ToArray());
        var missingFacts = missing.Distinct()
            .OrderBy(x => x.FactKey, StringComparer.Ordinal)
            .ThenBy(x => x.SubjectId.Value).ToArray();
        var futureFacts = futureDated.Distinct()
            .OrderBy(x => x.FactKey, StringComparer.Ordinal)
            .ThenBy(x => x.SubjectId.Value).ToArray();
        MissingFactReferences = Array.AsReadOnly(missingFacts);
        FutureDatedFactReferences = Array.AsReadOnly(futureFacts);
        // Compatibility views: fact keys alone do not identify a component.
        MissingFactKeys = Array.AsReadOnly(missingFacts.Select(x => x.FactKey)
            .Distinct(StringComparer.Ordinal).ToArray());
        FutureDatedFactKeys = Array.AsReadOnly(futureFacts.Select(x => x.FactKey)
            .Distinct(StringComparer.Ordinal).ToArray());

        // Keep immutable observation references private to the assessment.
        // The technical passport must reject reuse of a known evidence GUID
        // after its actual facts, timestamp, source or privacy have changed.
        // These are never sent to public JSON and are not an attestation.
        EvaluatedObservations = new ReadOnlyDictionary<Guid, EvidenceRecord>(
            evaluatedObservations.ToDictionary(item => item.Id));
        EvaluatedFactReferences = Array.AsReadOnly(rule.RequiredFacts
            .Select(item => new DiagnosticFactReference(item.SubjectId, item.FactKey))
            .ToArray());
    }

    [JsonIgnore]
    internal IReadOnlyDictionary<Guid, EvidenceRecord> EvaluatedObservations { get; }

    // All subject/fact pairs that the rule evaluated, including pairs with
    // zero observations. Adding a later contradictory or previously missing
    // reading must invalidate an older public diagnostic conclusion.
    [JsonIgnore]
    internal ReadOnlyCollection<DiagnosticFactReference> EvaluatedFactReferences { get; }

    // This is an in-memory evaluator object, NOT a public JSON contract.
    // All source identifiers, arbitrary rule strings, fact keys and evidence
    // GUIDs must be projected explicitly by an authorized privacy-safe DTO.
    [JsonIgnore]
    public DiagnosticContextId ContextId { get; }
    [JsonIgnore]
    public string RuleId { get; }
    [JsonIgnore]
    public string RuleVersion { get; }
    [JsonIgnore]
    public string Hypothesis { get; }
    [JsonIgnore]
    public string FalsificationCriterion { get; }
    public DiagnosticAssessmentStatus Status { get; }
    [JsonIgnore]
    public ReadOnlyCollection<Guid> SupportingEvidenceIds { get; }
    [JsonIgnore]
    public ReadOnlyCollection<Guid> ContradictingEvidenceIds { get; }
    [JsonIgnore]
    public ReadOnlyCollection<DiagnosticFactReference> MissingFactReferences { get; }
    [JsonIgnore]
    public ReadOnlyCollection<DiagnosticFactReference> FutureDatedFactReferences { get; }
    [JsonIgnore]
    public ReadOnlyCollection<string> MissingFactKeys { get; }
    [JsonIgnore]
    public ReadOnlyCollection<string> FutureDatedFactKeys { get; }
}

/// <summary>
/// Deterministic cross-source comparison, not causal inference.
/// Unknown, inaccessible, future-dated or contradictory data cannot silently
/// produce a healthy or proven-cause claim.
/// </summary>
public static class DeterministicDiagnosticEvaluator
{
    public static DiagnosticAssessment Evaluate(
        DiagnosticContextId contextId, DiagnosticRule rule,
        IEnumerable<EvidenceRecord> evidence,
        DateTimeOffset evaluationAt)
    {
        if (!contextId.IsValid)
            throw new ArgumentException("A valid diagnostic context is required.",
                nameof(contextId));
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(evidence);

        var snapshot = evidence.Take(10_001).ToArray();
        if (snapshot.Length > 10_000 || snapshot.Any(item => item is null))
            throw new ArgumentException("Evidence must contain <= 10,000 valid records.",
                nameof(evidence));
        if (snapshot.Any(item => item.ContextId != contextId))
            throw new ArgumentException("Cross-context diagnostic evidence is forbidden.",
                nameof(evidence));

        // Reject a reused identity containing different facts rather than allowing
        // input order to decide which of the two contradictory records wins.
        var unique = new Dictionary<Guid, EvidenceRecord>();
        foreach (var item in snapshot)
        {
            if (unique.TryGetValue(item.Id, out var prior) && prior != item)
                throw new ArgumentException("An evidence ID was reused for different data.",
                    nameof(evidence));
            unique[item.Id] = item;
        }

        var atUtc = evaluationAt.ToUniversalTime();
        var supporting = new List<Guid>();
        var contradicting = new List<Guid>();
        var missing = new List<DiagnosticFactReference>();
        var future = new List<DiagnosticFactReference>();
        var evaluatedObservations = new List<EvidenceRecord>();

        foreach (var requirement in rule.RequiredFacts)
        {
            var candidates = unique.Values
                .Where(item => item.SubjectId == requirement.SubjectId
                    && string.Equals(item.FactKey, requirement.FactKey,
                        StringComparison.Ordinal)).ToArray();
            // Bind even unavailable/future candidates that influenced a
            // missing/invalid-time conclusion, not only positive evidence.
            evaluatedObservations.AddRange(candidates);

            // Clock inconsistencies invalidate a rule conclusion until reviewed.
            if (candidates.Any(item => item.ObservedAtUtc > atUtc))
            {
                future.Add(new DiagnosticFactReference(requirement.SubjectId,
                    requirement.FactKey));
                continue;
            }

            var observed = candidates
                .Where(item => item.Availability == EvidenceAvailability.Observed)
                .ToArray();
            if (observed.Length == 0)
            {
                missing.Add(new DiagnosticFactReference(requirement.SubjectId,
                    requirement.FactKey));
                continue;
            }

            foreach (var item in observed)
            {
                if (item.Value == requirement.Expected)
                    supporting.Add(item.Id);
                else
                    contradicting.Add(item.Id);
            }
        }

        var status = future.Count > 0
            ? DiagnosticAssessmentStatus.InsufficientEvidence
            : supporting.Count > 0 && contradicting.Count > 0
                ? DiagnosticAssessmentStatus.ConflictingEvidence
                : contradicting.Count > 0
                    ? DiagnosticAssessmentStatus.Refuted
                    : missing.Count > 0 || supporting.Count == 0
                        ? DiagnosticAssessmentStatus.InsufficientEvidence
                        : DiagnosticAssessmentStatus.Corroborated;

        return new DiagnosticAssessment(contextId, rule, status, supporting, contradicting,
            missing, future, evaluatedObservations);
    }
}
