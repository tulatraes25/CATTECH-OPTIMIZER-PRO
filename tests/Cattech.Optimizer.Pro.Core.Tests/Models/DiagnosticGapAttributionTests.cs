using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class DiagnosticGapAttributionTests
{
    private static readonly DiagnosticContextId Context =
        new(Guid.Parse("c1c1c1c1-c1c1-41c1-81c1-c1c1c1c1c1c1"));
    private static readonly DiagnosticSubjectId DiskA =
        new(Guid.Parse("a1a1a1a1-a1a1-41a1-81a1-a1a1a1a1a1a1"));
    private static readonly DiagnosticSubjectId DiskB =
        new(Guid.Parse("b1b1b1b1-b1b1-41b1-81b1-b1b1b1b1b1b1"));
    private static readonly DateTimeOffset AsOf =
        new(2026, 10, 15, 13, 0, 0, TimeSpan.Zero);

    private static DiagnosticRule BothDisksRule() => new(
        "two-disks", "v1", "Both drives satisfy the health check",
        "A missing, future, or contrary reading prevents corroboration.",
        new[]
        {
            new EvidenceExpectation(DiskA, "disk.health",
                EvidenceValue.FromText("healthy")),
            new EvidenceExpectation(DiskB, "disk.health",
                EvidenceValue.FromText("healthy"))
        });

    private static EvidenceRecord Read(
        Guid id, DiagnosticSubjectId subject, DateTimeOffset? time = null) =>
        EvidenceRecord.Observed(id, Context, subject, "synthetic",
            "disk.health", time ?? AsOf.AddMinutes(-10),
            EvidenceValue.FromText("healthy"), "synthetic-only",
            EvidencePrivacyClass.Equipment);

    [Fact]
    public void MissingDiskB_IsAttributedToDiskBNotJustGenericHealthKey()
    {
        var a = Read(Guid.Parse("11111111-1111-4111-8111-111111111111"), DiskA);
        var result = DeterministicDiagnosticEvaluator.Evaluate(
            Context, BothDisksRule(), new[] { a }, AsOf);

        Assert.Equal(DiagnosticAssessmentStatus.InsufficientEvidence, result.Status);
        var missing = Assert.Single(result.MissingFactReferences);
        Assert.Equal(DiskB, missing.SubjectId);
        Assert.Equal("disk.health", missing.FactKey);
        Assert.Equal(new[] { "disk.health" }, result.MissingFactKeys);
        Assert.Single(result.SupportingEvidenceIds);
    }

    [Fact]
    public void BothDisksMissing_KeepTwoSubjectReferences_AndOneLegacyFactKey()
    {
        var result = DeterministicDiagnosticEvaluator.Evaluate(
            Context, BothDisksRule(), Array.Empty<EvidenceRecord>(), AsOf);

        Assert.Equal(2, result.MissingFactReferences.Count);
        Assert.Equal(new[] { DiskA, DiskB },
            result.MissingFactReferences.Select(x => x.SubjectId));
        Assert.Equal(new[] { "disk.health" }, result.MissingFactKeys);
        Assert.Equal(DiagnosticAssessmentStatus.InsufficientEvidence, result.Status);
    }

    [Fact]
    public void FutureDatedDiskB_IsAttributedToDiskB_AndBlocksFullCorroboration()
    {
        var a = Read(Guid.Parse("11111111-1111-4111-8111-111111111111"), DiskA);
        var b = Read(Guid.Parse("22222222-2222-4222-8222-222222222222"),
            DiskB, AsOf.AddMinutes(3));
        var result = DeterministicDiagnosticEvaluator.Evaluate(
            Context, BothDisksRule(), new[] { b, a }, AsOf);

        Assert.Equal(DiagnosticAssessmentStatus.InsufficientEvidence, result.Status);
        var future = Assert.Single(result.FutureDatedFactReferences);
        Assert.Equal(DiskB, future.SubjectId);
        Assert.Equal("disk.health", future.FactKey);
        Assert.Equal(new[] { "disk.health" }, result.FutureDatedFactKeys);
    }

    [Fact]
    public void GapReference_RejectsUnscopedOrBlankKeys()
    {
        Assert.Throws<ArgumentException>(() =>
            new DiagnosticFactReference(default, "disk.health"));
        Assert.Throws<ArgumentException>(() =>
            new DiagnosticFactReference(DiskA, ""));
    }

    [Fact]
    public void RawAssessmentJson_DoesNotAccidentallySerializeSubjectGapRefs()
    {
        var assessment = DeterministicDiagnosticEvaluator.Evaluate(
            Context, BothDisksRule(), Array.Empty<EvidenceRecord>(), AsOf);
        var json = JsonSerializer.Serialize(assessment);

        Assert.DoesNotContain(DiskA.Value.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(DiskB.Value.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("missingFactReferences", json,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, assessment.MissingFactReferences.Count);
    }

    [Fact]
    public void DifferentInputOrder_ProducesStableAttribution()
    {
        var a = Read(Guid.Parse("11111111-1111-4111-8111-111111111111"), DiskA);
        var b = Read(Guid.Parse("22222222-2222-4222-8222-222222222222"),
            DiskB, AsOf.AddMinutes(1));
        var first = DeterministicDiagnosticEvaluator.Evaluate(
            Context, BothDisksRule(), new[] { a, b }, AsOf);
        var second = DeterministicDiagnosticEvaluator.Evaluate(
            Context, BothDisksRule(), new[] { b, a }, AsOf);

        Assert.Equal(first.FutureDatedFactReferences, second.FutureDatedFactReferences);
        Assert.Equal(first.MissingFactReferences, second.MissingFactReferences);
        Assert.Equal(first.Status, second.Status);
    }
}
