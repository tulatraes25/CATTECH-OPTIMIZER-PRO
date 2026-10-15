using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class EvidenceSubjectIsolationTests
{
    private static readonly DiagnosticContextId Context =
        new(Guid.Parse("77777777-1111-4111-8111-111111111111"));
    private static readonly DiagnosticSubjectId DiskA =
        new(Guid.Parse("77777777-2222-4222-8222-222222222222"));
    private static readonly DiagnosticSubjectId DiskB =
        new(Guid.Parse("77777777-3333-4333-8333-333333333333"));
    private static readonly Guid BeforeId =
        Guid.Parse("77777777-4444-4444-8444-444444444444");
    private static readonly Guid AfterId =
        Guid.Parse("77777777-5555-4555-8555-555555555555");
    private static readonly Guid ExportId =
        Guid.Parse("77777777-6666-4666-8666-666666666666");
    private static readonly DateTimeOffset AsOf =
        new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static EvidenceRecord Measure(
        Guid id, DiagnosticSubjectId subject, string health,
        DateTimeOffset? measured = null) =>
        EvidenceRecord.Observed(id, Context, subject, "same-smart-provider",
            "disk.health", measured ?? AsOf.AddHours(-1),
            EvidenceValue.FromText(health), "synthetic",
            EvidencePrivacyClass.Equipment);

    private static DiagnosticRule DiskRule(DiagnosticSubjectId subject) =>
        new("disk-rule", "1", "Same disk is healthy",
            "Measurement does not match the expected health.",
            new[] { new EvidenceExpectation(subject, "disk.health",
                EvidenceValue.FromText("healthy")) });

    private static RepairOutcomeRule OutcomeRule() =>
        new("disk-post", "1", "disk.health", EvidenceValue.FromText("healthy"));

    [Fact]
    public void SubjectId_MustBeExplicitAndNonEmpty()
    {
        Assert.Throws<ArgumentException>(() => new DiagnosticSubjectId(Guid.Empty));
        Assert.False(default(DiagnosticSubjectId).IsValid);
        var a = DiagnosticSubjectId.NewSubject();
        var b = DiagnosticSubjectId.NewSubject();
        Assert.True(a.IsValid);
        Assert.True(b.IsValid);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ObservationsAndExpectations_RejectDefaultSubject()
    {
        Assert.Throws<ArgumentException>(() => EvidenceRecord.Observed(
            BeforeId, Context, default, "source", "disk.health",
            AsOf, EvidenceValue.FromText("healthy"), "fixture",
            EvidencePrivacyClass.Equipment));
        Assert.Throws<ArgumentException>(() => EvidenceRecord.Missing(
            BeforeId, Context, default, "source", "disk.health",
            AsOf, EvidenceAvailability.Unavailable, "fixture",
            EvidencePrivacyClass.Equipment, "Not available."));
        Assert.Throws<ArgumentException>(() => new EvidenceExpectation(
            default, "disk.health", EvidenceValue.FromText("healthy")));
    }

    [Fact]
    public void CrossDiskRecovery_NeverBecomesImprovedWithinSameCase()
    {
        var before = Measure(BeforeId, DiskA, "unhealthy", AsOf.AddHours(-2));
        var after = Measure(AfterId, DiskB, "healthy", AsOf.AddMinutes(-3));
        var result = RepairOutcomeEvaluator.Compare(OutcomeRule(), before, after);

        Assert.Equal(RepairOutcomeStatus.Indeterminate, result.Status);
        Assert.Equal(RepairOutcomeReason.IncompatibleSubject, result.Reason);
        Assert.True(result.ContextId.IsValid);
        Assert.False(result.SubjectId.IsValid);
    }

    [Fact]
    public void SameDiskRecovery_MayReportObservedImprovementNotCausation()
    {
        var before = Measure(BeforeId, DiskA, "unhealthy", AsOf.AddHours(-2));
        var after = Measure(AfterId, DiskA, "healthy", AsOf.AddMinutes(-3));
        var result = RepairOutcomeEvaluator.Compare(OutcomeRule(), before, after);

        Assert.Equal(RepairOutcomeStatus.ObservedImprovement, result.Status);
        Assert.Equal(DiskA, result.SubjectId);
    }

    [Fact]
    public void OtherDiskReading_CannotCorroborateTheSelectedDiskRule()
    {
        var other = Measure(AfterId, DiskB, "healthy");
        var result = DeterministicDiagnosticEvaluator.Evaluate(Context,
            DiskRule(DiskA), new[] { other }, AsOf);

        Assert.Equal(DiagnosticAssessmentStatus.InsufficientEvidence, result.Status);
        Assert.Empty(result.SupportingEvidenceIds);
        Assert.Equal(new[] { "disk.health" }, result.MissingFactKeys);
    }

    [Fact]
    public void ExplicitMultiDiskRule_CorroboratesBothOnlyWhenBothPresent()
    {
        var rule = new DiagnosticRule("two-disks", "1", "Both disks healthy",
            "One disk lacks an expected reading.", new[]
            {
                new EvidenceExpectation(DiskA, "disk.health",
                    EvidenceValue.FromText("healthy")),
                new EvidenceExpectation(DiskB, "disk.health",
                    EvidenceValue.FromText("healthy"))
            });
        var a = Measure(BeforeId, DiskA, "healthy");
        var b = Measure(AfterId, DiskB, "healthy");
        var full = DeterministicDiagnosticEvaluator.Evaluate(Context,
            rule, new[] { b, a }, AsOf);
        var incomplete = DeterministicDiagnosticEvaluator.Evaluate(Context,
            rule, new[] { a }, AsOf);

        Assert.Equal(DiagnosticAssessmentStatus.Corroborated, full.Status);
        Assert.Equal(2, full.SupportingEvidenceIds.Count);
        Assert.Equal(DiagnosticAssessmentStatus.InsufficientEvidence,
            incomplete.Status);
        Assert.Single(incomplete.SupportingEvidenceIds);
    }

    [Fact]
    public void DuplicateSameSubjectKeyInRule_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new DiagnosticRule(
            "duplicate", "1", "Same disk", "Expected contrast",
            new[]
            {
                new EvidenceExpectation(DiskA, "disk.health",
                    EvidenceValue.FromText("healthy")),
                new EvidenceExpectation(DiskA, "disk.health",
                    EvidenceValue.FromText("unhealthy"))
            }));
    }

    [Fact]
    public void Passport_RejectsForgedOutcomeSpanningDifferentDisks()
    {
        var a = Measure(BeforeId, DiskA, "unhealthy");
        var b = Measure(AfterId, DiskB, "healthy");
        var forged = new RepairOutcomeAssessment("fake", "1",
            "disk.health", BeforeId, AfterId,
            RepairOutcomeStatus.ObservedImprovement,
            RepairOutcomeReason.ImprovementObserved, Context, DiskA);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Context, ExportId, AsOf, new[] { a, b },
            Array.Empty<DiagnosticAssessment>(), new[] { forged }));
    }

    [Fact]
    public void Passport_RejectsOutcomeWithInvalidDeclaredSubject()
    {
        var a = Measure(BeforeId, DiskA, "unhealthy");
        var b = Measure(AfterId, DiskA, "healthy");
        var forged = new RepairOutcomeAssessment("fake", "1",
            "disk.health", BeforeId, AfterId,
            RepairOutcomeStatus.ObservedImprovement,
            RepairOutcomeReason.ImprovementObserved, Context, default);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Context, ExportId, AsOf, new[] { a, b },
            Array.Empty<DiagnosticAssessment>(), new[] { forged }));
    }

    [Fact]
    public void PublicPassport_ContainsNeitherContextNorSubjectIdentifiers()
    {
        var fact = Measure(BeforeId, DiskA, "healthy");
        var result = DeterministicDiagnosticEvaluator.Evaluate(Context,
            DiskRule(DiskA), new[] { fact }, AsOf);
        var passport = TechnicalPassportBuilder.Build(
            Context, ExportId, AsOf, new[] { fact },
            new[] { result }, Array.Empty<RepairOutcomeAssessment>());

        Assert.True(passport.HasMatchingDigest());
        Assert.DoesNotContain(Context.Value.ToString(), passport.PayloadJson,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(DiskA.Value.ToString(), passport.PayloadJson,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(BeforeId.ToString(), passport.PayloadJson,
            StringComparison.OrdinalIgnoreCase);

        using var json = JsonDocument.Parse(passport.PayloadJson);
        Assert.Equal(1, json.RootElement.GetProperty("evidence")
            .GetProperty("observed").GetInt32());
    }
}
