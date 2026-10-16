using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// SPEC #43: assert selected security promises of parallel PRs #29/#31/#34
/// against the current stacked v0.3 Core API. Synthetic evidence only.
/// These tests do not imply full semantic reconciliation or hardware identity.
/// </summary>
public class ParallelEvidenceBranchParityTests
{
    private static readonly DiagnosticContextId CaseA =
        new(Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
    private static readonly DiagnosticContextId CaseB =
        new(Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
    private static readonly DiagnosticSubjectId DiskA =
        new(Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"));
    private static readonly DiagnosticSubjectId DiskB =
        new(Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"));
    private static readonly DateTimeOffset AsOf =
        new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid FirstEvidenceId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SecondEvidenceId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static EvidenceRecord Observe(
        Guid id, DiagnosticContextId caseId, DiagnosticSubjectId subject,
        string value, DateTimeOffset time) =>
        EvidenceRecord.Observed(id, caseId, subject,
            "synthetic-check", "windows.component.health", time,
            EvidenceValue.FromText(value), "synthetic-only",
            EvidencePrivacyClass.Equipment);

    private static DiagnosticRule ExpectHealthyDisk() =>
        new("parallel-parity", "1", "Synthetic disk health",
            "A non-healthy observation refutes this claim.",
            new[]
            {
                new EvidenceExpectation(DiskA, "windows.component.health",
                    EvidenceValue.FromText("healthy"))
            });

    private static RepairOutcomeRule Postcondition() =>
        new("postcheck", "1", "windows.component.health",
            EvidenceValue.FromText("healthy"));

    [Fact]
    public void EvidenceFromAnotherCase_CannotCorroborateThisCase()
    {
        var local = Observe(FirstEvidenceId, CaseA, DiskA, "healthy",
            AsOf.AddMinutes(-20));
        var external = Observe(SecondEvidenceId, CaseB, DiskA, "healthy",
            AsOf.AddMinutes(-10));

        Assert.Throws<ArgumentException>(() =>
            DeterministicDiagnosticEvaluator.Evaluate(
                CaseA, ExpectHealthyDisk(), new[] { local, external }, AsOf));
        Assert.Throws<ArgumentException>(() =>
            DeterministicDiagnosticEvaluator.Evaluate(
                CaseA, ExpectHealthyDisk(), new[] { external, local }, AsOf));
    }

    [Fact]
    public void EmptyEvidence_NeedsAnExplicitValidSelectedCase()
    {
        Assert.Throws<ArgumentException>(() =>
            DeterministicDiagnosticEvaluator.Evaluate(
                default, ExpectHealthyDisk(), Array.Empty<EvidenceRecord>(), AsOf));

        var assessment = DeterministicDiagnosticEvaluator.Evaluate(
            CaseA, ExpectHealthyDisk(), Array.Empty<EvidenceRecord>(), AsOf);
        Assert.Equal(DiagnosticAssessmentStatus.InsufficientEvidence,
            assessment.Status);
        Assert.Equal(CaseA, assessment.ContextId);
        Assert.Equal(new[] { "windows.component.health" }, assessment.MissingFactKeys);
    }

    [Fact]
    public void UnboundLegacyContext_AndUnboundComponent_CannotCreateEvidence()
    {
        Assert.Throws<ArgumentException>(() => Observe(
            FirstEvidenceId, default, DiskA, "healthy", AsOf));
        Assert.Throws<ArgumentException>(() => Observe(
            FirstEvidenceId, CaseA, default, "healthy", AsOf));
    }

    [Fact]
    public void SameSourceAndMetric_CannotImplyRecoveryAcrossDifferentCases()
    {
        var before = Observe(FirstEvidenceId, CaseA, DiskA,
            "unhealthy", AsOf.AddMinutes(-30));
        var after = Observe(SecondEvidenceId, CaseB, DiskA,
            "healthy", AsOf.AddMinutes(-10));

        var outcome = RepairOutcomeEvaluator.Compare(Postcondition(), before, after);

        Assert.Equal(RepairOutcomeStatus.Indeterminate, outcome.Status);
        Assert.Equal(RepairOutcomeReason.IncompatibleContext, outcome.Reason);
        Assert.False(outcome.ContextId.IsValid);
        Assert.False(outcome.SubjectId.IsValid);
    }

    [Fact]
    public void SameCase_DifferentDisk_CannotImplyRecovery()
    {
        var before = Observe(FirstEvidenceId, CaseA, DiskA,
            "unhealthy", AsOf.AddMinutes(-30));
        var after = Observe(SecondEvidenceId, CaseA, DiskB,
            "healthy", AsOf.AddMinutes(-10));

        var outcome = RepairOutcomeEvaluator.Compare(Postcondition(), before, after);

        Assert.Equal(RepairOutcomeStatus.Indeterminate, outcome.Status);
        Assert.Equal(RepairOutcomeReason.IncompatibleSubject, outcome.Reason);
        Assert.Equal(CaseA, outcome.ContextId);
        Assert.False(outcome.SubjectId.IsValid);
    }

    [Fact]
    public void PublicOutcomeJson_DoesNotLeakCaseOrDiskOpaqueIdentifiers()
    {
        var before = Observe(FirstEvidenceId, CaseA, DiskA,
            "unhealthy", AsOf.AddMinutes(-30));
        var after = Observe(SecondEvidenceId, CaseA, DiskA,
            "healthy", AsOf.AddMinutes(-10));

        var outcome = RepairOutcomeEvaluator.Compare(Postcondition(), before, after);
        Assert.Equal(RepairOutcomeStatus.ObservedImprovement, outcome.Status);

        var json = JsonSerializer.Serialize(outcome);
        Assert.DoesNotContain(CaseA.Value.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(DiskA.Value.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(FirstEvidenceId.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecondEvidenceId.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
    }
}
