using System.Collections.ObjectModel;

namespace Cattech.Optimizer.Pro.Core.Models.RepairPreview;

/// <summary>
/// Describes proposed work only. There are deliberately no executors, commands
/// or privileged OS interfaces anywhere in this model.
/// </summary>
public enum PreviewStepKind
{
    ReadOnlyInspection,
    SystemChange
}

public enum PreviewReversibility
{
    NotApplicable,
    Unknown,
    DocumentedProcedure,
    NotReversible
}

public sealed record RepairPreviewStep
{
    public RepairPreviewStep(
        string stepId, string intent, string expectedEffect,
        string preconditions, string verificationCriterion,
        PreviewStepKind kind, PreviewReversibility reversibility,
        bool requiresElevation = false, bool requiresRestart = false)
    {
        if (string.IsNullOrWhiteSpace(stepId))
            throw new ArgumentException("Step ID is required.", nameof(stepId));
        if (string.IsNullOrWhiteSpace(intent))
            throw new ArgumentException("Intent is required.", nameof(intent));
        if (string.IsNullOrWhiteSpace(expectedEffect))
            throw new ArgumentException("Expected effect is required.", nameof(expectedEffect));
        if (string.IsNullOrWhiteSpace(preconditions))
            throw new ArgumentException("Preconditions are required.", nameof(preconditions));
        if (string.IsNullOrWhiteSpace(verificationCriterion))
            throw new ArgumentException("Post-verification is required.",
                nameof(verificationCriterion));
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (!Enum.IsDefined(reversibility))
            throw new ArgumentOutOfRangeException(nameof(reversibility));
        if (kind == PreviewStepKind.ReadOnlyInspection
            && (reversibility != PreviewReversibility.NotApplicable
                || requiresRestart))
            throw new ArgumentException(
                "Read-only previews cannot claim restart or reversibility.");
        if (kind == PreviewStepKind.SystemChange
            && reversibility == PreviewReversibility.NotApplicable)
            throw new ArgumentException(
                "A proposed system change must disclose its reversibility.");

        StepId = stepId;
        Intent = intent;
        ExpectedEffect = expectedEffect;
        Preconditions = preconditions;
        VerificationCriterion = verificationCriterion;
        Kind = kind;
        Reversibility = reversibility;
        RequiresElevation = requiresElevation;
        RequiresRestart = requiresRestart;
    }

    public string StepId { get; }
    public string Intent { get; }
    public string ExpectedEffect { get; }
    public string Preconditions { get; }
    public string VerificationCriterion { get; }
    public PreviewStepKind Kind { get; }
    public PreviewReversibility Reversibility { get; }
    public bool RequiresElevation { get; }
    public bool RequiresRestart { get; }
}

/// <summary>
/// Immutable description of a potential intervention. This plan cannot authorize
/// or execute a repair: a future, separate executor must enforce consent, scopes,
/// preflight and trusted process/OS identity at the time of use.
/// </summary>
public sealed class RepairPreviewPlan
{
    public RepairPreviewPlan(
        Guid planId, string version, string purpose, string rationale,
        IEnumerable<Guid> sourceEvidenceIds, IEnumerable<RepairPreviewStep> steps)
    {
        if (planId == Guid.Empty)
            throw new ArgumentException("A plan needs a stable ID.", nameof(planId));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version is required.", nameof(version));
        if (string.IsNullOrWhiteSpace(purpose))
            throw new ArgumentException("Purpose is required.", nameof(purpose));
        if (string.IsNullOrWhiteSpace(rationale))
            throw new ArgumentException("Evidence rationale is required.", nameof(rationale));
        ArgumentNullException.ThrowIfNull(sourceEvidenceIds);
        ArgumentNullException.ThrowIfNull(steps);

        var evidence = sourceEvidenceIds.Take(257).ToArray();
        if (evidence.Length is 0 or > 256 || evidence.Any(id => id == Guid.Empty)
            || evidence.Distinct().Count() != evidence.Length)
            throw new ArgumentException("Require 1-256 unique evidence IDs.",
                nameof(sourceEvidenceIds));

        var actions = steps.Take(65).ToArray();
        if (actions.Length is 0 or > 64 || actions.Any(step => step is null)
            || actions.Select(step => step.StepId)
                .Distinct(StringComparer.Ordinal).Count() != actions.Length)
            throw new ArgumentException("Require 1-64 steps with unique IDs.",
                nameof(steps));

        PlanId = planId;
        Version = version;
        Purpose = purpose;
        Rationale = rationale;
        SourceEvidenceIds = Array.AsReadOnly(evidence.OrderBy(id => id).ToArray());
        Steps = Array.AsReadOnly(actions);
    }

    public Guid PlanId { get; }
    public string Version { get; }
    public string Purpose { get; }
    public string Rationale { get; }
    public ReadOnlyCollection<Guid> SourceEvidenceIds { get; }
    public ReadOnlyCollection<RepairPreviewStep> Steps { get; }

    public bool MayModifySystem => Steps.Any(step =>
        step.Kind == PreviewStepKind.SystemChange);

    // Conservative defaults for all proposed system changes.
    public bool RequiresExplicitConsent => MayModifySystem;
    public bool RequiresBackupReview => MayModifySystem;
    public bool RequiresElevation => Steps.Any(step => step.RequiresElevation);
    public bool RequiresRestart => Steps.Any(step => step.RequiresRestart);

    public bool HasUncertainReversibility => Steps.Any(step =>
        step.Kind == PreviewStepKind.SystemChange
        && (step.Reversibility is PreviewReversibility.Unknown
            or PreviewReversibility.NotReversible));
}
