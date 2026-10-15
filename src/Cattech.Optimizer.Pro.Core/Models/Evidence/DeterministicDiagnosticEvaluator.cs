using System.Collections.ObjectModel;

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
    public EvidenceExpectation(string factKey, EvidenceValue expected)
    {
        if (string.IsNullOrWhiteSpace(factKey))
            throw new ArgumentException("Fact key is required.", nameof(factKey));
        ArgumentNullException.ThrowIfNull(expected);

        FactKey = factKey;
        Expected = expected;
    }

    public string FactKey { get; }
    public EvidenceValue Expected { get; }
}

/// <summary>
/// A versioned, pure-data diagnostic rule. One requirement per fact key.
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

        if (requirements.Select(item => item.FactKey)
            .Distinct(StringComparer.Ordinal).Count() != requirements.Length)
            throw new ArgumentException("Required fact keys must be unique.",
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
/// Evidence IDs only; sensitive raw values and paths are not copied into an assessment.
/// </summary>
public sealed class DiagnosticAssessment
{
    internal DiagnosticAssessment(DiagnosticRule rule,
        DiagnosticContextId contextId,
        DiagnosticAssessmentStatus status,
        IEnumerable<Guid> supporting, IEnumerable<Guid> contradicting,
        IEnumerable<string> missing, IEnumerable<string> futureDated)
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
        MissingFactKeys = Array.AsReadOnly(
            missing.Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray());
        FutureDatedFactKeys = Array.AsReadOnly(
            futureDated.Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    public DiagnosticContextId ContextId { get; }
    public string RuleId { get; }
    public string RuleVersion { get; }
    public string Hypothesis { get; }
    public string FalsificationCriterion { get; }
    public DiagnosticAssessmentStatus Status { get; }
    public ReadOnlyCollection<Guid> SupportingEvidenceIds { get; }
    public ReadOnlyCollection<Guid> ContradictingEvidenceIds { get; }
    public ReadOnlyCollection<string> MissingFactKeys { get; }
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
        DiagnosticRule rule, IEnumerable<EvidenceRecord> evidence,
        DateTimeOffset evaluationAt) =>
        Evaluate(rule, evidence, evaluationAt, default);

    /// <summary>
    /// An explicit scope permits a missing-data-only assessment with zero records;
    /// a scope-free empty input or mixed/unbound records are never accepted.
    /// </summary>
    public static DiagnosticAssessment Evaluate(
        DiagnosticRule rule, IEnumerable<EvidenceRecord> evidence,
        DateTimeOffset evaluationAt, DiagnosticContextId expectedContext)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(evidence);

        var snapshot = evidence.Take(10_001).ToArray();
        if (snapshot.Length > 10_000 || snapshot.Any(item => item is null))
            throw new ArgumentException("Evidence must contain <= 10,000 valid records.",
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

        var context = expectedContext.IsBound
            ? expectedContext
            : unique.Count == 0 ? default : unique.Values.First().ContextId;

        if (!context.IsBound
            || unique.Values.Any(item => item.ContextId != context))
            throw new ArgumentException(
                "An assessment requires one bound diagnostic context; missing or mixed contexts are rejected.",
                nameof(evidence));

        var atUtc = evaluationAt.ToUniversalTime();
        var supporting = new List<Guid>();
        var contradicting = new List<Guid>();
        var missing = new List<string>();
        var future = new List<string>();

        foreach (var requirement in rule.RequiredFacts)
        {
            var candidates = unique.Values
                .Where(item => string.Equals(item.FactKey, requirement.FactKey,
                    StringComparison.Ordinal)).ToArray();

            // Clock inconsistencies invalidate a rule conclusion until reviewed.
            if (candidates.Any(item => item.ObservedAtUtc > atUtc))
            {
                future.Add(requirement.FactKey);
                continue;
            }

            var observed = candidates
                .Where(item => item.Availability == EvidenceAvailability.Observed)
                .ToArray();
            if (observed.Length == 0)
            {
                missing.Add(requirement.FactKey);
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

        return new DiagnosticAssessment(rule, context, status, supporting,
            contradicting, missing, future);
    }
}
