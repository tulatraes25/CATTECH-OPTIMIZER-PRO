using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>Cross-checks security semantics from historical parallel PR #34.</summary>
public class ParallelPassportBranchParityTests
{
    private static readonly DiagnosticContextId CaseA =
        new(Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
    private static readonly DiagnosticContextId CaseB =
        new(Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
    private static readonly DiagnosticSubjectId Disk =
        new(Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"));
    private static readonly Guid BeforeId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid AfterId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ExportId =
        Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
    private static readonly DateTimeOffset At =
        new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static EvidenceRecord Observation(
        Guid id, DiagnosticContextId context, string state, DateTimeOffset time) =>
        EvidenceRecord.Observed(id, context, Disk, "synthetic-reader",
            "disk.health", time, EvidenceValue.FromText(state), "synthetic",
            EvidencePrivacyClass.Equipment);

    [Fact]
    public void Passport_RejectsMixedServiceCaseObservationsEvenWithMatchingDisk()
    {
        var recordA = Observation(BeforeId, CaseA, "warning", At.AddMinutes(-20));
        var recordB = Observation(AfterId, CaseB, "healthy", At.AddMinutes(-10));

        Assert.Throws<ArgumentException>(() =>
            TechnicalPassportBuilder.Build(CaseA, ExportId, At,
                new[] { recordA, recordB },
                Array.Empty<DiagnosticAssessment>(),
                Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void Passport_RejectsDiagnosticAssessmentFromDifferentCase()
    {
        var record = Observation(AfterId, CaseB, "healthy", At.AddMinutes(-10));
        var rule = new DiagnosticRule("health", "1", "synthetic", "synthetic",
            new[] { new EvidenceExpectation(Disk, "disk.health",
                EvidenceValue.FromText("healthy")) });
        var assessment = DeterministicDiagnosticEvaluator.Evaluate(
            CaseB, rule, new[] { record }, At);
        var local = Observation(BeforeId, CaseA, "healthy", At.AddMinutes(-10));

        Assert.Throws<ArgumentException>(() =>
            TechnicalPassportBuilder.Build(CaseA, ExportId, At,
                new[] { local }, new[] { assessment },
                Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void Passport_RejectsOutcomeFromDifferentServiceCase()
    {
        var old = Observation(BeforeId, CaseB, "warning", At.AddMinutes(-20));
        var newer = Observation(AfterId, CaseB, "healthy", At.AddMinutes(-10));
        var outcome = RepairOutcomeEvaluator.Compare(
            new RepairOutcomeRule("repair", "1", "disk.health",
                EvidenceValue.FromText("healthy")), old, newer);

        var local = Observation(BeforeId, CaseA, "warning", At.AddMinutes(-20));
        Assert.Throws<ArgumentException>(() =>
            TechnicalPassportBuilder.Build(CaseA, ExportId, At,
                new[] { local }, Array.Empty<DiagnosticAssessment>(),
                new[] { outcome }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Passport_RejectsEmptyEvidence_RegardlessOfCaseSelection(bool hasCase)
    {
        var context = hasCase ? CaseA : default;
        Assert.Throws<ArgumentException>(() =>
            TechnicalPassportBuilder.Build(context, ExportId, At,
                Array.Empty<EvidenceRecord>(),
                Array.Empty<DiagnosticAssessment>(),
                Array.Empty<RepairOutcomeAssessment>()));
    }
}
