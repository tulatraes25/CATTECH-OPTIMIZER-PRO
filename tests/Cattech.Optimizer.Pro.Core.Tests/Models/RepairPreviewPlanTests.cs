using Cattech.Optimizer.Pro.Core.Models.RepairPreview;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class RepairPreviewPlanTests
{
    private static readonly Guid PlanId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid EvidenceId =
        Guid.Parse("44444444-4444-4444-8444-444444444444");

    private static RepairPreviewStep Inspection(bool elevation = false) => new(
        "inspect", "Inspect service state", "Collect a status snapshot",
        "Provider available", "State captured or marked unavailable",
        PreviewStepKind.ReadOnlyInspection, PreviewReversibility.NotApplicable,
        requiresElevation: elevation);

    private static RepairPreviewStep ProposedChange(
        PreviewReversibility reversibility = PreviewReversibility.Unknown) => new(
        "review-change", "Propose a controlled Windows repair",
        "Some OS configuration could change",
        "Backups, safety and privileges must be verified",
        "Post-repair state must be independently checked",
        PreviewStepKind.SystemChange, reversibility,
        requiresElevation: true, requiresRestart: true);

    [Fact]
    public void ProposedSystemChange_RequiresConsentBackupAndPostVerification()
    {
        var plan = new RepairPreviewPlan(PlanId, "1", "Synthetic repair plan",
            "Two synthetic evidence records suggest a safe inspection first.",
            new[] { EvidenceId }, new[] { Inspection(), ProposedChange() });

        Assert.True(plan.MayModifySystem);
        Assert.True(plan.RequiresExplicitConsent);
        Assert.True(plan.RequiresBackupReview);
        Assert.True(plan.RequiresElevation);
        Assert.True(plan.RequiresRestart);
        Assert.True(plan.HasUncertainReversibility);
        Assert.Equal(2, plan.Steps.Count);
        Assert.Contains("Post-repair", plan.Steps[1].VerificationCriterion);
    }

    [Fact]
    public void ReadOnlyPlan_RequiresNoRepairConsentOrBackupReview()
    {
        var plan = new RepairPreviewPlan(PlanId, "1", "Inspection",
            "Synthetic evidence suggests checking the current status.",
            new[] { EvidenceId }, new[] { Inspection() });

        Assert.False(plan.MayModifySystem);
        Assert.False(plan.RequiresExplicitConsent);
        Assert.False(plan.RequiresBackupReview);
        Assert.False(plan.RequiresElevation);
        Assert.False(plan.RequiresRestart);
        Assert.False(plan.HasUncertainReversibility);
    }

    [Fact]
    public void ReadOnlyInspection_MayRequireElevationButNeverClaimRestart()
    {
        var elevated = Inspection(elevation: true);
        Assert.True(elevated.RequiresElevation);
        Assert.Throws<ArgumentException>(() => new RepairPreviewStep(
            "invalid", "Inspect", "Get facts", "Preflight", "Verify",
            PreviewStepKind.ReadOnlyInspection, PreviewReversibility.NotApplicable,
            requiresRestart: true));
    }

    [Fact]
    public void SystemModification_MustDiscloseReversibility()
    {
        Assert.Throws<ArgumentException>(() => new RepairPreviewStep(
            "invalid", "Change", "A possible change", "Preflight", "Verify",
            PreviewStepKind.SystemChange, PreviewReversibility.NotApplicable));

        var documented = ProposedChange(PreviewReversibility.DocumentedProcedure);
        Assert.Equal(PreviewReversibility.DocumentedProcedure, documented.Reversibility);
    }

    [Fact]
    public void Plan_RejectsNoEvidenceOrDuplicateStepIds()
    {
        Assert.Throws<ArgumentException>(() => new RepairPreviewPlan(
            PlanId, "1", "Repair", "Reason", Array.Empty<Guid>(),
            new[] { Inspection() }));
        Assert.Throws<ArgumentException>(() => new RepairPreviewPlan(
            PlanId, "1", "Repair", "Reason", new[] { EvidenceId },
            new[] { Inspection(), Inspection() }));
        Assert.Throws<ArgumentException>(() => new RepairPreviewPlan(
            PlanId, "1", "Repair", "Reason", new[] { EvidenceId, EvidenceId },
            new[] { Inspection() }));
    }

    [Fact]
    public void Plan_IsSnapshotNotAReferenceToCallerCollections()
    {
        var evidence = new List<Guid> { EvidenceId };
        var steps = new List<RepairPreviewStep> { Inspection() };
        var plan = new RepairPreviewPlan(PlanId, "1", "Inspect",
            "Synthetic evidence", evidence, steps);
        evidence.Clear();
        steps.Clear();

        Assert.Single(plan.SourceEvidenceIds);
        Assert.Single(plan.Steps);
        Assert.Equal(EvidenceId, plan.SourceEvidenceIds[0]);
        Assert.Equal("inspect", plan.Steps[0].StepId);
    }

    [Fact]
    public void DataOnlyPreview_ExposesNoExecutionSurface()
    {
        var names = typeof(RepairPreviewPlan).GetMethods()
            .Concat(typeof(RepairPreviewStep).GetMethods())
            .Select(method => method.Name).ToArray();

        Assert.DoesNotContain(names, name =>
            name.StartsWith("Execute", StringComparison.Ordinal)
            || name.StartsWith("Run", StringComparison.Ordinal)
            || name.StartsWith("Apply", StringComparison.Ordinal));
    }
}
