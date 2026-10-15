using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class DeterministicDiagnosticEvaluatorTests
{
    private static readonly DiagnosticContextId Context =
        new(Guid.Parse("99999999-9999-4999-8999-999999999999"));

    private static readonly DateTimeOffset AsOf =
        new(2026, 10, 14, 22, 0, 0, TimeSpan.Zero);

    private static readonly Guid IdA =
        Guid.Parse("11111111-1111-4111-8111-111111111111");

    private static readonly Guid IdB =
        Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static DiagnosticRule OneFactRule() => new(
        "bsod-evidence", "1", "A specified bugcheck was observed",
        "A different observed bugcheck would refute this condition.",
        new[] { new EvidenceExpectation("crash.bugcheck",
            EvidenceValue.FromText("0x0000009F")) });

    private static EvidenceRecord Crash(Guid id, string code,
        DateTimeOffset? timestamp = null, string source = "wer") =>
        EvidenceRecord.Observed(id, Context, source, "crash.bugcheck",
            timestamp ?? AsOf.AddHours(-1), EvidenceValue.FromText(code),
            "synthetic-only", EvidencePrivacyClass.Equipment);

    [Fact]
    public void TwoRequiredFacts_CorroboratedOnlyWhenBothPresent()
    {
        var rule = new DiagnosticRule("correlation", "v1",
            "Two observations coexist", "Any missing or contrary observation.",
            new[] {
                new EvidenceExpectation("crash.bugcheck", EvidenceValue.FromText("0x0000009F")),
                new EvidenceExpectation("disk.health", EvidenceValue.FromText("warning"))
            });
        var facts = new[]
        {
            Crash(IdB, "0x0000009F"),
            EvidenceRecord.Observed(IdA, Context, "smart", "disk.health",
                AsOf.AddHours(-1), EvidenceValue.FromText("warning"),
                "synthetic", EvidencePrivacyClass.Equipment)
        };

        var first = DeterministicDiagnosticEvaluator.Evaluate(Context, rule, facts, AsOf);
        var reversed = DeterministicDiagnosticEvaluator.Evaluate(Context, 
            rule, facts.Reverse(), AsOf);

        Assert.Equal(DiagnosticAssessmentStatus.Corroborated, first.Status);
        Assert.Equal(first.Status, reversed.Status);
        Assert.Equal(new[] { IdA, IdB }, first.SupportingEvidenceIds);
        Assert.Equal(first.SupportingEvidenceIds, reversed.SupportingEvidenceIds);
        Assert.Empty(first.MissingFactKeys);
        Assert.Empty(first.ContradictingEvidenceIds);
        Assert.Equal("v1", first.RuleVersion);
    }

    [Fact]
    public void MissingSmartObservation_StaysInsufficientNotHealthy()
    {
        var rule = new DiagnosticRule("multisource", "1",
            "Cross check crash and disk", "An observation would contradict a requirement.",
            new[] {
                new EvidenceExpectation("crash.bugcheck", EvidenceValue.FromText("0x0000009F")),
                new EvidenceExpectation("disk.health", EvidenceValue.FromText("healthy"))
            });
        var absent = EvidenceRecord.Missing(IdB, Context, "smart", "disk.health",
            AsOf.AddHours(-1), EvidenceAvailability.AccessDenied,
            "synthetic", EvidencePrivacyClass.Equipment, "Missing SMART permission.");

        var result = DeterministicDiagnosticEvaluator.Evaluate(Context, 
            rule, new[] { Crash(IdA, "0x0000009F"), absent }, AsOf);

        Assert.Equal(DiagnosticAssessmentStatus.InsufficientEvidence, result.Status);
        Assert.Equal(new[] { "disk.health" }, result.MissingFactKeys);
        Assert.Single(result.SupportingEvidenceIds);
        Assert.Empty(result.ContradictingEvidenceIds);
    }

    [Fact]
    public void ConflictingSources_DoNotProduceAnArtificialConfidence()
    {
        var result = DeterministicDiagnosticEvaluator.Evaluate(Context, OneFactRule(),
            new[] { Crash(IdA, "0x0000009F", source: "wer"),
                    Crash(IdB, "0x0000007E", source: "dumpchk") }, AsOf);

        Assert.Equal(DiagnosticAssessmentStatus.ConflictingEvidence, result.Status);
        Assert.Equal(new[] { IdA }, result.SupportingEvidenceIds);
        Assert.Equal(new[] { IdB }, result.ContradictingEvidenceIds);
    }

    [Fact]
    public void Contradiction_RefutesOnlyTheConfiguredExpectation()
    {
        var result = DeterministicDiagnosticEvaluator.Evaluate(Context, OneFactRule(),
            new[] { Crash(IdA, "0x0000007E") }, AsOf);

        Assert.Equal(DiagnosticAssessmentStatus.Refuted, result.Status);
        Assert.Empty(result.SupportingEvidenceIds);
        Assert.Single(result.ContradictingEvidenceIds);
    }

    [Fact]
    public void FutureDatedRequiredObservation_InvalidatesConclusion()
    {
        var result = DeterministicDiagnosticEvaluator.Evaluate(Context, OneFactRule(),
            new[] { Crash(IdA, "0x0000009F", timestamp: AsOf.AddMinutes(3)) }, AsOf);

        Assert.Equal(DiagnosticAssessmentStatus.InsufficientEvidence, result.Status);
        Assert.Equal(new[] { "crash.bugcheck" }, result.FutureDatedFactKeys);
        Assert.Empty(result.SupportingEvidenceIds);
    }

    [Fact]
    public void IrrelevantFacts_AndDuplicateIdenticalRecords_DoNotChangeStatus()
    {
        var valid = Crash(IdA, "0x0000009F");
        var irrelevant = EvidenceRecord.Observed(IdB, Context, "mock", "gpu.temperature",
            AsOf, EvidenceValue.FromNumber(40), "synthetic",
            EvidencePrivacyClass.Equipment);

        var result = DeterministicDiagnosticEvaluator.Evaluate(Context, OneFactRule(),
            new[] { irrelevant, valid, valid }, AsOf);

        Assert.Equal(DiagnosticAssessmentStatus.Corroborated, result.Status);
        Assert.Equal(new[] { IdA }, result.SupportingEvidenceIds);
    }

    [Fact]
    public void ReusedEvidenceIdWithDifferentPayload_FailsClosed()
    {
        Assert.Throws<ArgumentException>(() =>
            DeterministicDiagnosticEvaluator.Evaluate(Context, OneFactRule(),
                new[] { Crash(IdA, "0x0000009F"), Crash(IdA, "0x0000007E") },
                AsOf));
    }

    [Fact]
    public void RuleSnapshotsItsRequirements_AndRejectsDuplicateKeys()
    {
        var requirements = new List<EvidenceExpectation>
        {
            new("crash.bugcheck", EvidenceValue.FromText("0x0000009F"))
        };
        var rule = new DiagnosticRule("stable", "1", "hypothesis",
            "a contradicting result", requirements);
        requirements.Clear();
        Assert.Single(rule.RequiredFacts);
        Assert.Throws<ArgumentException>(() =>
            new DiagnosticRule("bad", "1", "hypothesis", "a contradiction",
                new[] {
                    new EvidenceExpectation("disk", EvidenceValue.FromBoolean(true)),
                    new EvidenceExpectation("disk", EvidenceValue.FromBoolean(false))
                }));
        Assert.Throws<ArgumentException>(() =>
            new DiagnosticRule("bad", "1", "hypothesis", "a contradiction",
                Array.Empty<EvidenceExpectation>()));
    }

    [Fact]
    public void PrivateObservationRawValue_IsNotCopiedIntoAssessment()
    {
        const string sensitive = "SYNTHETIC-PRIVATE-IDENTIFIER";
        var rule = new DiagnosticRule("private", "1", "A private fact matches",
            "A different value refutes it.",
            new[] { new EvidenceExpectation("diagnostic.private",
                EvidenceValue.FromText(sensitive)) });
        var fact = EvidenceRecord.Observed(IdA, Context, "test", "diagnostic.private",
            AsOf, EvidenceValue.FromText(sensitive), "synthetic",
            EvidencePrivacyClass.Sensitive);

        var result = DeterministicDiagnosticEvaluator.Evaluate(Context, 
            rule, new[] { fact }, AsOf);

        Assert.Equal(DiagnosticAssessmentStatus.Corroborated, result.Status);
        Assert.Single(result.SupportingEvidenceIds);
        Assert.DoesNotContain(sensitive, System.Text.Json.JsonSerializer.Serialize(result));
    }
}
