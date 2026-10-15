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
    InvalidTimeline,
    ContextMismatch
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
public sealed class RepairOutcomeAssessment
{
    // Only the evaluated comparison may manufacture an outcome in normal code.
    // This is an API correctness boundary, not a cryptographic attestation.
    internal RepairOutcomeAssessment(
        string ruleId, string ruleVersion, string factKey,
        Guid beforeEvidenceId, Guid afterEvidenceId,
        RepairOutcomeStatus status, RepairOutcomeReason reason,
        DiagnosticContextId contextId)
    {
        if (string.IsNullOrWhiteSpace(ruleId)
            || string.IsNullOrWhiteSpace(ruleVersion)
            || string.IsNullOrWhiteSpace(factKey))
            throw new ArgumentException("Outcome rule metadata is required.");
        if (beforeEvidenceId == Guid.Empty || afterEvidenceId == Guid.Empty)
            throw new ArgumentException("Outcome evidence references are required.");
        if (!Enum.IsDefined(status) || !Enum.IsDefined(reason))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (!contextId.IsBound && status != RepairOutcomeStatus.Indeterminate)
            throw new ArgumentException(
                "A positive or unresolved outcome requires a bound context.",
                nameof(contextId));

        RuleId = ruleId;
        RuleVersion = ruleVersion;
        FactKey = factKey;
        BeforeEvidenceId = beforeEvidenceId;
        AfterEvidenceId = afterEvidenceId;
        Status = status;
        Reason = reason;
        ContextId = contextId;
    }

    public string RuleId { get; }
    public string RuleVersion { get; }
    public string FactKey { get; }
    public Guid BeforeEvidenceId { get; }
    public Guid AfterEvidenceId { get; }
    public RepairOutcomeStatus Status { get; }
    public RepairOutcomeReason Reason { get; }

    [System.Text.Json.Serialization.JsonIgnore]
    public DiagnosticContextId ContextId { get; }
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

        var sameContext = before.ContextId.IsBound
            && before.ContextId == after.ContextId;

        RepairOutcomeAssessment Result(RepairOutcomeStatus status,
            RepairOutcomeReason reason) =>
            new(rule.RuleId, rule.Version, rule.FactKey,
                before.Id, after.Id, status, reason,
                sameContext ? before.ContextId : default);

        // Scope mismatch is checked first: a matching fact/source/time from two
        // machines must never become an apparent improvement.
        if (!sameContext)
            return Result(RepairOutcomeStatus.Indeterminate,
                RepairOutcomeReason.ContextMismatch);

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
