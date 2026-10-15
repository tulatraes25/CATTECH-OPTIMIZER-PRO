namespace Cattech.Optimizer.Pro.Core.Models.Evidence;

/// <summary>
/// An observed condition improved, not proof that a specific repair caused it.
/// </summary>
public enum RepairOutcomeStatus
{
    Indeterminate,
    ObservedImprovement,
    Unresolved
}

public enum RepairOutcomeReason
{
    ImprovementObserved,
    TargetNotReached,
    NoDemonstratedChange,
    MissingMeasurement,
    IncompatibleMeasurement,
    IncompatibleContext,
    IncompatibleSubject,
    InvalidTimeline
}

/// <summary>
/// Immutable, versioned postcondition; no commands or repair executor.
/// </summary>
public sealed class RepairOutcomeRule
{
    public RepairOutcomeRule(
        string ruleId, string version, string factKey, EvidenceValue expectedPostValue)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("Rule ID is required.", nameof(ruleId));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version is required.", nameof(version));
        if (string.IsNullOrWhiteSpace(factKey))
            throw new ArgumentException("Fact key is required.", nameof(factKey));
        ArgumentNullException.ThrowIfNull(expectedPostValue);

        RuleId = ruleId;
        Version = version;
        FactKey = factKey;
        ExpectedPostValue = expectedPostValue;
    }

    public string RuleId { get; }
    public string Version { get; }
    public string FactKey { get; }
    public EvidenceValue ExpectedPostValue { get; }
}

/// <summary>
/// Keeps only evidence identifiers; never copies raw values, file paths or PII.
/// </summary>
/// <summary>
/// Outcome is created by Core's evaluator, not arbitrary callers.
/// This is an API-integrity constraint, NOT an authenticity signature.
/// </summary>
public sealed class RepairOutcomeAssessment
{
    internal RepairOutcomeAssessment(
        string ruleId, string ruleVersion, string factKey,
        Guid beforeEvidenceId, Guid afterEvidenceId,
        RepairOutcomeStatus status, RepairOutcomeReason reason,
        DiagnosticContextId contextId, DiagnosticSubjectId subjectId)
    {
        if (string.IsNullOrWhiteSpace(ruleId)
            || string.IsNullOrWhiteSpace(ruleVersion)
            || string.IsNullOrWhiteSpace(factKey))
            throw new ArgumentException("Outcome rule metadata is required.");
        if (beforeEvidenceId == Guid.Empty || afterEvidenceId == Guid.Empty)
            throw new ArgumentException("Outcome evidence IDs are required.");
        if (!Enum.IsDefined(status) || !Enum.IsDefined(reason))
            throw new ArgumentOutOfRangeException(nameof(status));

        if (status == RepairOutcomeStatus.ObservedImprovement
            && reason != RepairOutcomeReason.ImprovementObserved
            || status == RepairOutcomeStatus.Unresolved
            && reason != RepairOutcomeReason.TargetNotReached
            || status == RepairOutcomeStatus.Indeterminate
            && reason is RepairOutcomeReason.ImprovementObserved
                or RepairOutcomeReason.TargetNotReached)
            throw new ArgumentException("Outcome reason contradicts its status.");

        if (status != RepairOutcomeStatus.Indeterminate
            && (!contextId.IsValid || !subjectId.IsValid
                || beforeEvidenceId == afterEvidenceId))
            throw new ArgumentException(
                "A non-indeterminate outcome requires a bound case and component.");

        RuleId = ruleId;
        RuleVersion = ruleVersion;
        FactKey = factKey;
        BeforeEvidenceId = beforeEvidenceId;
        AfterEvidenceId = afterEvidenceId;
        Status = status;
        Reason = reason;
        ContextId = contextId;
        SubjectId = subjectId;
    }

    public string RuleId { get; }
    public string RuleVersion { get; }
    public string FactKey { get; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid BeforeEvidenceId { get; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid AfterEvidenceId { get; }
    public RepairOutcomeStatus Status { get; }
    public RepairOutcomeReason Reason { get; }
    [System.Text.Json.Serialization.JsonIgnore]
    public DiagnosticContextId ContextId { get; }
    [System.Text.Json.Serialization.JsonIgnore]
    public DiagnosticSubjectId SubjectId { get; }
}

/// <summary>
/// Compare two observations of the same measurement with an explicit postcondition.
/// Never claims root cause or repair success from process exit codes.
/// </summary>
public static class RepairOutcomeEvaluator
{
    public static RepairOutcomeAssessment Compare(
        RepairOutcomeRule rule, EvidenceRecord before, EvidenceRecord after)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        RepairOutcomeAssessment Result(RepairOutcomeStatus status,
            RepairOutcomeReason reason) =>
            new(rule.RuleId, rule.Version, rule.FactKey,
                before.Id, after.Id, status, reason, before.ContextId,
                before.SubjectId);

        // Reject comparisons across equipment service contexts before any
        // apparent improvement is considered. Mismatches have no valid
        // assessment context and cannot be attached to a public passport.
        if (before.ContextId != after.ContextId
            || !before.ContextId.IsValid || !after.ContextId.IsValid)
            return new RepairOutcomeAssessment(rule.RuleId, rule.Version,
                rule.FactKey, before.Id, after.Id,
                RepairOutcomeStatus.Indeterminate,
                RepairOutcomeReason.IncompatibleContext, default, default);

        // Same machine does not imply the same disk or physical component.
        if (before.SubjectId != after.SubjectId
            || !before.SubjectId.IsValid || !after.SubjectId.IsValid)
            return new RepairOutcomeAssessment(rule.RuleId, rule.Version,
                rule.FactKey, before.Id, after.Id,
                RepairOutcomeStatus.Indeterminate,
                RepairOutcomeReason.IncompatibleSubject, before.ContextId, default);

        // Different subjects, sources, identities or an inverted timeline are
        // not comparable. A failed comparison must never be reported as healthy.
        if (before.Id == after.Id
            || before.ObservedAtUtc >= after.ObservedAtUtc)
            return Result(RepairOutcomeStatus.Indeterminate,
                RepairOutcomeReason.InvalidTimeline);

        if (!string.Equals(before.FactKey, rule.FactKey, StringComparison.Ordinal)
            || !string.Equals(after.FactKey, rule.FactKey, StringComparison.Ordinal)
            || !string.Equals(before.SourceId, after.SourceId, StringComparison.Ordinal))
            return Result(RepairOutcomeStatus.Indeterminate,
                RepairOutcomeReason.IncompatibleMeasurement);

        if (before.Availability != EvidenceAvailability.Observed
            || after.Availability != EvidenceAvailability.Observed
            || before.Value is null || after.Value is null)
            return Result(RepairOutcomeStatus.Indeterminate,
                RepairOutcomeReason.MissingMeasurement);

        if (before.Value.Kind != rule.ExpectedPostValue.Kind
            || after.Value.Kind != rule.ExpectedPostValue.Kind)
            return Result(RepairOutcomeStatus.Indeterminate,
                RepairOutcomeReason.IncompatibleMeasurement);

        if (before.Value == rule.ExpectedPostValue
            && after.Value == rule.ExpectedPostValue)
            return Result(RepairOutcomeStatus.Indeterminate,
                RepairOutcomeReason.NoDemonstratedChange);

        if (after.Value == rule.ExpectedPostValue)
            return Result(RepairOutcomeStatus.ObservedImprovement,
                RepairOutcomeReason.ImprovementObserved);

        return Result(RepairOutcomeStatus.Unresolved,
            RepairOutcomeReason.TargetNotReached);
    }
}
