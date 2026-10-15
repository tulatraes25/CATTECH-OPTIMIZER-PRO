using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class RepairOutcomeEvaluatorTests
{
    private static readonly DateTimeOffset BaselineTime =
        new(2026, 10, 14, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid BeforeId =
        Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid AfterId =
        Guid.Parse("66666666-6666-4666-8666-666666666666");

    private static RepairOutcomeRule Rule() =>
        new("postcheck", "1", "windows.component.health",
            EvidenceValue.FromText("healthy"));

    private static EvidenceRecord Observed(Guid id, string value,
        DateTimeOffset time, string source = "diagnostic-check") =>
        EvidenceRecord.Observed(id, source, "windows.component.health",
            time, EvidenceValue.FromText(value), "synthetic-only",
            EvidencePrivacyClass.Equipment);

    [Fact]
    public void RecoveredPostcondition_IsObservedImprovementNotCausalProof()
    {
        var result = RepairOutcomeEvaluator.Compare(Rule(),
            Observed(BeforeId, "unhealthy", BaselineTime),
            Observed(AfterId, "healthy", BaselineTime.AddMinutes(30)));

        Assert.Equal(RepairOutcomeStatus.ObservedImprovement, result.Status);
        Assert.Equal(RepairOutcomeReason.ImprovementObserved, result.Reason);
        Assert.Equal(BeforeId, result.BeforeEvidenceId);
        Assert.Equal(AfterId, result.AfterEvidenceId);
        Assert.Equal("1", result.RuleVersion);
    }

    [Fact]
    public void AlreadyHealthyBeforeAndAfter_CannotBeAttributedToRepair()
    {
        var result = RepairOutcomeEvaluator.Compare(Rule(),
            Observed(BeforeId, "healthy", BaselineTime),
            Observed(AfterId, "healthy", BaselineTime.AddMinutes(30)));

        Assert.Equal(RepairOutcomeStatus.Indeterminate, result.Status);
        Assert.Equal(RepairOutcomeReason.NoDemonstratedChange, result.Reason);
    }

    [Fact]
    public void TargetStillNotReached_RemainsUnresolved()
    {
        var result = RepairOutcomeEvaluator.Compare(Rule(),
            Observed(BeforeId, "unhealthy", BaselineTime),
            Observed(AfterId, "unhealthy", BaselineTime.AddMinutes(30)));

        Assert.Equal(RepairOutcomeStatus.Unresolved, result.Status);
        Assert.Equal(RepairOutcomeReason.TargetNotReached, result.Reason);
    }

    [Fact]
    public void AccessDeniedAfterRepair_DoesNotClaimSuccess()
    {
        var missing = EvidenceRecord.Missing(AfterId, "diagnostic-check",
            "windows.component.health", BaselineTime.AddMinutes(30),
            EvidenceAvailability.AccessDenied, "synthetic-only",
            EvidencePrivacyClass.Equipment, "No access.");
        var result = RepairOutcomeEvaluator.Compare(Rule(),
            Observed(BeforeId, "unhealthy", BaselineTime), missing);

        Assert.Equal(RepairOutcomeStatus.Indeterminate, result.Status);
        Assert.Equal(RepairOutcomeReason.MissingMeasurement, result.Reason);
    }

    [Fact]
    public void DifferentSources_CannotBeComparedSilently()
    {
        var result = RepairOutcomeEvaluator.Compare(Rule(),
            Observed(BeforeId, "unhealthy", BaselineTime, "tool-A"),
            Observed(AfterId, "healthy", BaselineTime.AddMinutes(30), "tool-B"));

        Assert.Equal(RepairOutcomeStatus.Indeterminate, result.Status);
        Assert.Equal(RepairOutcomeReason.IncompatibleMeasurement, result.Reason);
    }

    [Fact]
    public void BackwardsTimeline_AndReusedId_AreIndeterminate()
    {
        var backwards = RepairOutcomeEvaluator.Compare(Rule(),
            Observed(BeforeId, "unhealthy", BaselineTime),
            Observed(AfterId, "healthy", BaselineTime.AddMinutes(-1)));
        var reused = RepairOutcomeEvaluator.Compare(Rule(),
            Observed(BeforeId, "unhealthy", BaselineTime),
            Observed(BeforeId, "healthy", BaselineTime.AddMinutes(1)));

        Assert.Equal(RepairOutcomeReason.InvalidTimeline, backwards.Reason);
        Assert.Equal(RepairOutcomeReason.InvalidTimeline, reused.Reason);
        Assert.Equal(RepairOutcomeStatus.Indeterminate, reused.Status);
    }

    [Fact]
    public void DifferentValueKinds_AreNotComparable()
    {
        var post = EvidenceRecord.Observed(AfterId, "diagnostic-check",
            "windows.component.health", BaselineTime.AddMinutes(30),
            EvidenceValue.FromBoolean(true), "synthetic-only",
            EvidencePrivacyClass.Equipment);

        var result = RepairOutcomeEvaluator.Compare(Rule(),
            Observed(BeforeId, "unhealthy", BaselineTime), post);

        Assert.Equal(RepairOutcomeStatus.Indeterminate, result.Status);
        Assert.Equal(RepairOutcomeReason.IncompatibleMeasurement, result.Reason);
    }

    [Fact]
    public void SensitiveValues_AreNotCopiedToOutcome()
    {
        const string privateMarker = "SYNTHETIC-PRIVATE-POSTCONDITION";
        var rule = new RepairOutcomeRule("private", "1", "private.marker",
            EvidenceValue.FromText(privateMarker));
        var before = EvidenceRecord.Observed(BeforeId, "fixture", "private.marker",
            BaselineTime, EvidenceValue.FromText("not-ready"), "synthetic",
            EvidencePrivacyClass.Sensitive);
        var after = EvidenceRecord.Observed(AfterId, "fixture", "private.marker",
            BaselineTime.AddMinutes(30), EvidenceValue.FromText(privateMarker),
            "synthetic", EvidencePrivacyClass.Sensitive);
        var result = RepairOutcomeEvaluator.Compare(rule, before, after);

        Assert.Equal(RepairOutcomeStatus.ObservedImprovement, result.Status);
        Assert.DoesNotContain(privateMarker,
            System.Text.Json.JsonSerializer.Serialize(result));
    }
}
