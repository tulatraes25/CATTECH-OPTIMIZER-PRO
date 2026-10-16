using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Public passport aggregation must count unique logical evaluations, not
/// repeated enumerable entries. All fixtures represent fictional hardware.
/// </summary>
public sealed class PassportDerivedAssessmentIntegrityTests
{
    private static readonly DiagnosticContextId Case =
        new(Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
    private static readonly DiagnosticSubjectId DiskA =
        new(Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
    private static readonly DiagnosticSubjectId DiskB =
        new(Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"));
    private static readonly Guid ExportId =
        Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private static readonly DateTimeOffset AsOf =
        new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static EvidenceRecord Read(
        Guid id, DiagnosticSubjectId disk, string value,
        int minutesBefore = 10) =>
        EvidenceRecord.Observed(id, Case, disk, "synthetic",
            "windows.disk.health", AsOf.AddMinutes(-minutesBefore),
            EvidenceValue.FromText(value), "fixture",
            EvidencePrivacyClass.Equipment);

    private static DiagnosticAssessment Assess(EvidenceRecord item)
    {
        var rule = new DiagnosticRule("shared-rule", "1", "Test hypothesis",
            "A contradictory disk fact", new[]
            {
                new EvidenceExpectation(item.SubjectId, "windows.disk.health",
                    EvidenceValue.FromText("healthy"))
            });
        return DeterministicDiagnosticEvaluator.Evaluate(
            Case, rule, new[] { item }, AsOf);
    }

    private static RepairOutcomeAssessment Postcheck(
        EvidenceRecord before, EvidenceRecord after) =>
        RepairOutcomeEvaluator.Compare(
            new RepairOutcomeRule("shared-postcheck", "1",
                "windows.disk.health", EvidenceValue.FromText("healthy")),
            before, after);

    [Fact]
    public void SameDiagnosticObjectRepeated_MustNotInflatePublicCount()
    {
        var fact = Read(Guid.NewGuid(), DiskA, "healthy");
        var first = Assess(fact);
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Case, ExportId, AsOf, new[] { fact }, new[] { first, first },
            Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void SeparatelyEvaluatedSameDiagnosticBasis_IsStillOneLogicalEvaluation()
    {
        var fact = Read(Guid.NewGuid(), DiskA, "healthy");
        var first = Assess(fact);
        var repeated = Assess(fact);
        Assert.NotSame(first, repeated);
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Case, ExportId, AsOf, new[] { fact },
            new[] { first, repeated }, Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void SamePostcheckObjectRepeated_MustNotInflateRecoveryCount()
    {
        var before = Read(Guid.NewGuid(), DiskA, "unhealthy", 25);
        var after = Read(Guid.NewGuid(), DiskA, "healthy", 5);
        var first = Postcheck(before, after);
        Assert.Equal(RepairOutcomeStatus.ObservedImprovement, first.Status);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Case, ExportId, AsOf, new[] { before, after },
            Array.Empty<DiagnosticAssessment>(), new[] { first, first }));
    }

    [Fact]
    public void SeparatelyEvaluatedSamePostcheckPair_IsStillOneLogicalPostcheck()
    {
        var before = Read(Guid.NewGuid(), DiskA, "unhealthy", 25);
        var after = Read(Guid.NewGuid(), DiskA, "healthy", 5);
        var first = Postcheck(before, after);
        var repeated = Postcheck(before, after);
        Assert.NotSame(first, repeated);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Case, ExportId, AsOf, new[] { before, after },
            Array.Empty<DiagnosticAssessment>(), new[] { first, repeated }));
    }

    [Fact]
    public void SameRuleOnDifferentDisks_StillCountsTwoGenuineDiagnostics()
    {
        var a = Read(Guid.NewGuid(), DiskA, "healthy");
        var b = Read(Guid.NewGuid(), DiskB, "healthy");
        var passport = TechnicalPassportBuilder.Build(
            Case, ExportId, AsOf, new[] { a, b },
            new[] { Assess(a), Assess(b) },
            Array.Empty<RepairOutcomeAssessment>());

        using var json = JsonDocument.Parse(passport.PayloadJson);
        var statuses = json.RootElement.GetProperty("diagnosticAssessments");
        var bucket = Assert.Single(statuses.EnumerateArray());
        Assert.Equal("Corroborated", bucket.GetProperty("status").GetString());
        Assert.Equal(2, bucket.GetProperty("count").GetInt32());
    }

    [Fact]
    public void SamePostcheckRuleOnDifferentDisks_AllowsTwoDistinctEvidencePairs()
    {
        var a0 = Read(Guid.NewGuid(), DiskA, "unhealthy", 25);
        var a1 = Read(Guid.NewGuid(), DiskA, "healthy", 5);
        var b0 = Read(Guid.NewGuid(), DiskB, "unhealthy", 25);
        var b1 = Read(Guid.NewGuid(), DiskB, "healthy", 5);
        var first = Postcheck(a0, a1);
        var second = Postcheck(b0, b1);
        Assert.Equal(RepairOutcomeStatus.ObservedImprovement, first.Status);
        Assert.Equal(RepairOutcomeStatus.ObservedImprovement, second.Status);

        var passport = TechnicalPassportBuilder.Build(
            Case, ExportId, AsOf, new[] { a0, a1, b0, b1 },
            Array.Empty<DiagnosticAssessment>(), new[] { first, second });

        using var json = JsonDocument.Parse(passport.PayloadJson);
        var statuses = json.RootElement.GetProperty("outcomeAssessments");
        var bucket = Assert.Single(statuses.EnumerateArray());
        Assert.Equal("ObservedImprovement", bucket.GetProperty("status").GetString());
        Assert.Equal(2, bucket.GetProperty("count").GetInt32());
    }
}
