using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// SPEC #43: selected compatibility guarantees of parallel passport PR #34.
/// All identities and observations are synthetic. No devices or clipboard.
/// </summary>
public sealed class PassportIntegrationParityStage2Tests
{
    private static readonly DiagnosticContextId CaseA =
        new(Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
    private static readonly DiagnosticContextId CaseB =
        new(Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
    private static readonly DiagnosticSubjectId Disk =
        new(Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"));
    private static readonly Guid ExportId =
        Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private static readonly Guid BeforeId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid AfterId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static EvidenceRecord Observe(
        Guid id, DiagnosticContextId context, string value) =>
        EvidenceRecord.Observed(id, context, Disk,
            "synthetic-only", "windows.component.health",
            IssuedAt.AddMinutes(-20), EvidenceValue.FromText(value),
            "fixture", EvidencePrivacyClass.Equipment);

    [Fact]
    public void EmptyPassport_RequiresActualEvidenceRatherThanJustCaseGuid()
    {
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            CaseA, ExportId, IssuedAt, Array.Empty<EvidenceRecord>(),
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void MissingOnlyAssessment_CannotGeneratePassportWithoutEvidence()
    {
        var rule = new DiagnosticRule(
            "synthetic", "1", "A checked hypothetical rule", "A contradiction",
            new[] { new EvidenceExpectation(Disk, "windows.component.health",
                EvidenceValue.FromText("healthy")) });
        var insufficient = DeterministicDiagnosticEvaluator.Evaluate(
            CaseA, rule, Array.Empty<EvidenceRecord>(), IssuedAt);

        Assert.Equal(DiagnosticAssessmentStatus.InsufficientEvidence,
            insufficient.Status);
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            CaseA, ExportId, IssuedAt, Array.Empty<EvidenceRecord>(),
            new[] { insufficient },
            Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void MixedCaseEvidence_IsRejectedWithoutPublishingCounts()
    {
        var local = Observe(BeforeId, CaseA, "unhealthy");
        var foreign = Observe(AfterId, CaseB, "healthy");

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            CaseA, ExportId, IssuedAt, new[] { local, foreign },
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void ForeignDiagnosticWithSameEvidenceId_CannotBecomeLocal()
    {
        var local = Observe(BeforeId, CaseA, "healthy");
        var foreign = Observe(BeforeId, CaseB, "healthy");
        var rule = new DiagnosticRule("fixture", "1", "Synthetic hypothesis",
            "Synthetic contradiction", new[]
            {
                new EvidenceExpectation(Disk, "windows.component.health",
                    EvidenceValue.FromText("healthy"))
            });
        var foreignAssessment = DeterministicDiagnosticEvaluator.Evaluate(
            CaseB, rule, new[] { foreign }, IssuedAt);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            CaseA, ExportId, IssuedAt, new[] { local },
            new[] { foreignAssessment }, Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void CrossCasePostcheckWithKnownEvidenceIds_CannotBeExported()
    {
        var before = Observe(BeforeId, CaseA, "unhealthy");
        var after = Observe(AfterId, CaseA, "healthy");
        var foreignAfter = Observe(AfterId, CaseB, "healthy");
        var rule = new RepairOutcomeRule("synthetic", "1",
            "windows.component.health", EvidenceValue.FromText("healthy"));
        var incompatible = RepairOutcomeEvaluator.Compare(
            rule, before, foreignAfter);

        Assert.Equal(RepairOutcomeStatus.Indeterminate, incompatible.Status);
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            CaseA, ExportId, IssuedAt, new[] { before, after },
            Array.Empty<DiagnosticAssessment>(), new[] { incompatible }));
    }

    [Fact]
    public void UnavailableEvidence_IsAllowedButNeverDeclaredHealthy()
    {
        var unavailable = EvidenceRecord.Missing(
            BeforeId, CaseA, Disk, "synthetic-only",
            "windows.component.health", IssuedAt.AddMinutes(-20),
            EvidenceAvailability.AccessDenied, "fixture",
            EvidencePrivacyClass.Equipment, "fixture");

        var passport = TechnicalPassportBuilder.Build(
            CaseA, ExportId, IssuedAt, new[] { unavailable },
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>());

        using var doc = JsonDocument.Parse(passport.PayloadJson);
        Assert.Equal(0, doc.RootElement.GetProperty("evidence")
            .GetProperty("observed").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("evidence")
            .GetProperty("accessDenied").GetInt32());
        Assert.DoesNotContain(CaseA.Value.ToString(), passport.PayloadJson,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CaseA.Value.ToString(),
            JsonSerializer.Serialize(passport), StringComparison.OrdinalIgnoreCase);
    }
}
