using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Adversarial synthetic fixtures only. Never reads hardware or real customer data.
/// </summary>
public class EvidenceContextIsolationTests
{
    private static readonly DiagnosticContextId CaseA =
        new(Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
    private static readonly DiagnosticContextId CaseB =
        new(Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
    private static readonly Guid BeforeId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid AfterId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ExportId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly DateTimeOffset AsOf =
        new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static EvidenceRecord Fact(
        Guid id, DiagnosticContextId context, string value,
        DateTimeOffset? observed = null) =>
        EvidenceRecord.Observed(id, context, "same-provider", "health",
            observed ?? AsOf.AddMinutes(-30), EvidenceValue.FromText(value),
            "synthetic-only", EvidencePrivacyClass.Equipment);

    private static DiagnosticRule Rule() => new(
        "rule", "1", "Check one synthetic health fact",
        "Another value would refute the expected fact.",
        new[] { new EvidenceExpectation("health", EvidenceValue.FromText("healthy")) });

    private static RepairOutcomeRule OutcomeRule() =>
        new("after", "1", "health", EvidenceValue.FromText("healthy"));

    [Fact]
    public void ContextId_IsOpaqueNonEmptyAndGeneratedPerServiceCase()
    {
        Assert.Throws<ArgumentException>(() => new DiagnosticContextId(Guid.Empty));
        Assert.False(default(DiagnosticContextId).IsValid);
        var first = DiagnosticContextId.NewSession();
        var second = DiagnosticContextId.NewSession();
        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ObservedAndMissingFacts_RejectDefaultContext()
    {
        Assert.Throws<ArgumentException>(() => EvidenceRecord.Observed(
            BeforeId, default, "source", "health", AsOf,
            EvidenceValue.FromText("healthy"), "fixture",
            EvidencePrivacyClass.Equipment));

        Assert.Throws<ArgumentException>(() => EvidenceRecord.Missing(
            AfterId, default, "source", "health", AsOf,
            EvidenceAvailability.Unavailable, "fixture",
            EvidencePrivacyClass.Equipment, "No observation."));
    }

    [Fact]
    public void DiagnosticEvaluator_RejectsMixedCasesEvenIfValuesMatch()
    {
        var first = Fact(BeforeId, CaseA, "healthy");
        var second = Fact(AfterId, CaseB, "healthy");

        Assert.Throws<ArgumentException>(() =>
            DeterministicDiagnosticEvaluator.Evaluate(
                CaseA, Rule(), new[] { first, second }, AsOf));
        Assert.Throws<ArgumentException>(() =>
            DeterministicDiagnosticEvaluator.Evaluate(
                CaseB, Rule(), new[] { second, first }, AsOf));
    }

    [Fact]
    public void DiagnosticEvaluator_RejectsIrrelevantForeignObservations()
    {
        var first = Fact(BeforeId, CaseA, "healthy");
        var foreign = EvidenceRecord.Observed(
            AfterId, CaseB, "same-provider", "irrelevant.gpu",
            AsOf.AddMinutes(-1), EvidenceValue.FromNumber(50),
            "synthetic-only", EvidencePrivacyClass.Equipment);

        Assert.Throws<ArgumentException>(() =>
            DeterministicDiagnosticEvaluator.Evaluate(
                CaseA, Rule(), new[] { first, foreign }, AsOf));
    }

    [Fact]
    public void EmptyEvidence_CanOnlyProduceScopedInsufficientAssessment()
    {
        var result = DeterministicDiagnosticEvaluator.Evaluate(
            CaseA, Rule(), Array.Empty<EvidenceRecord>(), AsOf);

        Assert.Equal(CaseA, result.ContextId);
        Assert.Equal(DiagnosticAssessmentStatus.InsufficientEvidence, result.Status);
        Assert.Empty(result.SupportingEvidenceIds);
        Assert.Throws<ArgumentException>(() =>
            DeterministicDiagnosticEvaluator.Evaluate(
                default, Rule(), Array.Empty<EvidenceRecord>(), AsOf));
    }

    [Fact]
    public void OutcomeEvaluator_MixedPCs_ReturnsIndeterminateNotImproved()
    {
        var before = Fact(BeforeId, CaseA, "unhealthy", AsOf.AddHours(-1));
        var after = Fact(AfterId, CaseB, "healthy", AsOf.AddMinutes(-1));
        var result = RepairOutcomeEvaluator.Compare(OutcomeRule(), before, after);

        Assert.Equal(RepairOutcomeStatus.Indeterminate, result.Status);
        Assert.Equal(RepairOutcomeReason.IncompatibleContext, result.Reason);
        Assert.False(result.ContextId.IsValid);
    }

    [Fact]
    public void OutcomeEvaluator_WithinSameCase_KeepsConservativeImprovement()
    {
        var before = Fact(BeforeId, CaseA, "unhealthy", AsOf.AddHours(-1));
        var after = Fact(AfterId, CaseA, "healthy", AsOf.AddMinutes(-1));
        var result = RepairOutcomeEvaluator.Compare(OutcomeRule(), before, after);

        Assert.Equal(CaseA, result.ContextId);
        Assert.Equal(RepairOutcomeStatus.ObservedImprovement, result.Status);
    }

    [Fact]
    public void Passport_RejectsCrossEquipmentRecordsAndWrongChosenContext()
    {
        var first = Fact(BeforeId, CaseA, "healthy");
        var second = Fact(AfterId, CaseB, "healthy");
        Assert.Throws<ArgumentException>(() =>
            TechnicalPassportBuilder.Build(CaseA, ExportId, AsOf,
                new[] { first, second }, Array.Empty<DiagnosticAssessment>(),
                Array.Empty<RepairOutcomeAssessment>()));
        Assert.Throws<ArgumentException>(() =>
            TechnicalPassportBuilder.Build(CaseB, ExportId, AsOf,
                new[] { first }, Array.Empty<DiagnosticAssessment>(),
                Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void Passport_RejectsUnlinkedForeignDiagnosticOrOutcome()
    {
        var local = Fact(BeforeId, CaseA, "healthy");
        var foreignDiagnostic = DeterministicDiagnosticEvaluator.Evaluate(
            CaseB, Rule(), Array.Empty<EvidenceRecord>(), AsOf);
        Assert.Throws<ArgumentException>(() =>
            TechnicalPassportBuilder.Build(CaseA, ExportId, AsOf,
                new[] { local }, new[] { foreignDiagnostic },
                Array.Empty<RepairOutcomeAssessment>()));

        var foreignOutcome = new RepairOutcomeAssessment(
            "other", "1", "health", BeforeId, BeforeId,
            RepairOutcomeStatus.Indeterminate,
            RepairOutcomeReason.NoDemonstratedChange, CaseB);
        Assert.Throws<ArgumentException>(() =>
            TechnicalPassportBuilder.Build(CaseA, ExportId, AsOf,
                new[] { local }, Array.Empty<DiagnosticAssessment>(),
                new[] { foreignOutcome }));
    }

    [Fact]
    public void Passport_RejectsUnscopedOrReusedExportIdentity()
    {
        var local = Fact(BeforeId, CaseA, "healthy");
        Assert.Throws<ArgumentException>(() =>
            TechnicalPassportBuilder.Build(default, ExportId, AsOf,
                new[] { local }, Array.Empty<DiagnosticAssessment>(),
                Array.Empty<RepairOutcomeAssessment>()));
        Assert.Throws<ArgumentException>(() =>
            TechnicalPassportBuilder.Build(CaseA, CaseA.Value, AsOf,
                new[] { local }, Array.Empty<DiagnosticAssessment>(),
                Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void Passport_NeverExportsCaseIdentityOrEquipmentIdentifiers()
    {
        var local = Fact(BeforeId, CaseA, "healthy");
        var assessment = DeterministicDiagnosticEvaluator.Evaluate(
            CaseA, Rule(), new[] { local }, AsOf);
        var passport = TechnicalPassportBuilder.Build(CaseA, ExportId, AsOf,
            new[] { local }, new[] { assessment },
            Array.Empty<RepairOutcomeAssessment>());

        Assert.True(passport.HasMatchingDigest());
        Assert.DoesNotContain(CaseA.Value.ToString(), passport.PayloadJson,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(BeforeId.ToString(), passport.PayloadJson,
            StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(passport.PayloadJson);
        Assert.Equal(1, document.RootElement.GetProperty("evidence")
            .GetProperty("observed").GetInt32());
        Assert.Equal("Corroborated",
            document.RootElement.GetProperty("diagnosticAssessments")[0]
                .GetProperty("status").GetString());
    }
}
