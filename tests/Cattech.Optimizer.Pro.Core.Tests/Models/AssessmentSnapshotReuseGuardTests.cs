using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// SPEC Kiro #43 — a GUID is a reference, not an immutable proof of the
/// measurement's content. Adversarial fixtures are fictional in-memory data.
/// </summary>
public sealed class AssessmentSnapshotReuseGuardTests
{
    private static readonly DiagnosticContextId Case =
        new(Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
    private static readonly DiagnosticSubjectId Disk =
        new(Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
    private static readonly Guid OldId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid NewId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ExportId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly DateTimeOffset At =
        new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static EvidenceRecord Read(Guid id, string value, int minutesBefore,
        string source = "synthetic-source", EvidencePrivacyClass privacy = EvidencePrivacyClass.Equipment) =>
        EvidenceRecord.Observed(id, Case, Disk, source, "disk.health",
            At.AddMinutes(-minutesBefore), EvidenceValue.FromText(value),
            "fictional QA", privacy);

    private static DiagnosticAssessment Assess(EvidenceRecord record) =>
        DeterministicDiagnosticEvaluator.Evaluate(Case,
            new DiagnosticRule("disk-health", "1", "Synthetic health expectation",
                "Non-healthy values", new[] {
                    new EvidenceExpectation(Disk, "disk.health",
                        EvidenceValue.FromText("healthy"))
                }),
            new[] { record }, At);

    private static RepairOutcomeAssessment Postcheck(
        EvidenceRecord before, EvidenceRecord after) =>
        RepairOutcomeEvaluator.Compare(
            new RepairOutcomeRule("synthetic-postcheck", "1", "disk.health",
                EvidenceValue.FromText("healthy")), before, after);

    [Fact]
    public void HealthyDiagnostic_WithSameGuidButUnhealthyPayload_IsNotExportable()
    {
        var originallyHealthy = Read(OldId, "healthy", 20);
        var assessment = Assess(originallyHealthy);
        Assert.Equal(DiagnosticAssessmentStatus.Corroborated, assessment.Status);

        var replacement = Read(OldId, "unhealthy", 20);
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Case, ExportId, At, new[] { replacement },
            new[] { assessment }, Array.Empty<RepairOutcomeAssessment>()));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("timestamp")]
    [InlineData("privacy")]
    public void SameGuid_DifferentSourceTimestampOrPrivacy_RevokesDiagnostic(string variation)
    {
        var original = Read(OldId, "healthy", 20);
        var assessment = Assess(original);
        var replacement = variation switch
        {
            "source" => Read(OldId, "healthy", 20, source: "new-source"),
            "timestamp" => Read(OldId, "healthy", 5),
            _ => Read(OldId, "healthy", 20, privacy: EvidencePrivacyClass.Sensitive)
        };

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Case, ExportId, At, new[] { replacement },
            new[] { assessment }, Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void UnavailableDiagnostic_ReusedGuidAsObserved_DoesNotPassSilentValidation()
    {
        var denied = EvidenceRecord.Missing(OldId, Case, Disk,
            "synthetic-source", "disk.health", At.AddMinutes(-20),
            EvidenceAvailability.AccessDenied, "fictional QA",
            EvidencePrivacyClass.Equipment, "No access in synthetic case");
        var assessment = Assess(denied);
        Assert.Equal(DiagnosticAssessmentStatus.InsufficientEvidence, assessment.Status);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Case, ExportId, At, new[] { Read(OldId, "healthy", 20) },
            new[] { assessment }, Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void PreviouslyImprovedPostcheck_CannotCountAlteredAfterReading()
    {
        var before = Read(OldId, "unhealthy", 30);
        var after = Read(NewId, "healthy", 5);
        var assessment = Postcheck(before, after);
        Assert.Equal(RepairOutcomeStatus.ObservedImprovement, assessment.Status);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Case, ExportId, At,
            new[] { before, Read(NewId, "unhealthy", 5) },
            Array.Empty<DiagnosticAssessment>(), new[] { assessment }));
    }

    [Fact]
    public void PreviouslyImprovedPostcheck_CannotCountAlteredBeforeReading()
    {
        var before = Read(OldId, "unhealthy", 30);
        var after = Read(NewId, "healthy", 5);
        var assessment = Postcheck(before, after);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Case, ExportId, At,
            new[] { Read(OldId, "healthy", 30), after },
            Array.Empty<DiagnosticAssessment>(), new[] { assessment }));
    }

    [Fact]
    public void UntouchedDiagnosticAndPostcheck_ExportValidCountsWithoutRawValues()
    {
        const string sensitiveValue = "SYNTHETIC-PRIVATE-READING";
        var before = Read(OldId, "unhealthy", 30);
        var after = Read(NewId, "healthy", 5);
        var diagnostic = Assess(after);
        var postcheck = Postcheck(before, after);
        var passport = TechnicalPassportBuilder.Build(Case, ExportId, At,
            new[] { after, before }, new[] { diagnostic }, new[] { postcheck });

        using var doc = JsonDocument.Parse(passport.PayloadJson);
        var d = Assert.Single(doc.RootElement.GetProperty("diagnosticAssessments").EnumerateArray());
        var o = Assert.Single(doc.RootElement.GetProperty("outcomeAssessments").EnumerateArray());
        Assert.Equal("Corroborated", d.GetProperty("status").GetString());
        Assert.Equal("ObservedImprovement", o.GetProperty("status").GetString());
        Assert.Equal(1, d.GetProperty("count").GetInt32());
        Assert.Equal(1, o.GetProperty("count").GetInt32());
        Assert.True(passport.HasMatchingDigest());
        Assert.DoesNotContain(Case.Value.ToString(), passport.PayloadJson,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(OldId.ToString(), passport.PayloadJson,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sensitiveValue, JsonSerializer.Serialize(diagnostic));
        Assert.DoesNotContain(sensitiveValue, JsonSerializer.Serialize(postcheck));
    }
    [Fact]
    public void PreviouslyCorroboratedDiagnostic_RejectsAddedContradictoryReading()
    {
        var original = Read(OldId, "healthy", 20);
        var assessment = Assess(original);
        var later = Read(NewId, "unhealthy", 5);
        Assert.Equal(DiagnosticAssessmentStatus.Corroborated, assessment.Status);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Case, ExportId, At, new[] { original, later },
            new[] { assessment }, Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void PreviouslyCorroboratedDiagnostic_RejectsAddedUnavailableReading()
    {
        var original = Read(OldId, "healthy", 20);
        var assessment = Assess(original);
        var later = EvidenceRecord.Missing(NewId, Case, Disk,
            "synthetic-source", "disk.health", At.AddMinutes(-5),
            EvidenceAvailability.AccessDenied, "fictional QA",
            EvidencePrivacyClass.Equipment, "synthetic access denied");

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Case, ExportId, At, new[] { original, later },
            new[] { assessment }, Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void PreviouslyMissingDiagnostic_RejectsLaterFirstObservation()
    {
        var unrelated = EvidenceRecord.Observed(OldId, Case, Disk,
            "synthetic-source", "disk.temperature", At.AddMinutes(-20),
            EvidenceValue.FromNumber(35), "fictional QA",
            EvidencePrivacyClass.Equipment);
        var assessment = DeterministicDiagnosticEvaluator.Evaluate(Case,
            new DiagnosticRule("disk-health", "1", "Synthetic expectation",
                "Synthetic contrary evidence", new[] {
                    new EvidenceExpectation(Disk, "disk.health",
                        EvidenceValue.FromText("healthy"))
                }), new[] { unrelated }, At);
        Assert.Equal(DiagnosticAssessmentStatus.InsufficientEvidence, assessment.Status);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Case, ExportId, At, new[] { unrelated, Read(NewId, "healthy", 5) },
            new[] { assessment }, Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void IndependentFactAndOtherDisk_DoNotInvalidateScopedDiagnostic()
    {
        var original = Read(OldId, "healthy", 20);
        var assessment = Assess(original);
        var otherDisk = new DiagnosticSubjectId(
            Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"));
        var unrelated = EvidenceRecord.Observed(NewId, Case, otherDisk,
            "synthetic-source", "disk.health", At.AddMinutes(-5),
            EvidenceValue.FromText("unhealthy"), "fictional QA",
            EvidencePrivacyClass.Equipment);

        var passport = TechnicalPassportBuilder.Build(
            Case, ExportId, At, new[] { unrelated, original },
            new[] { assessment }, Array.Empty<RepairOutcomeAssessment>());
        using var doc = JsonDocument.Parse(passport.PayloadJson);
        var status = Assert.Single(doc.RootElement
            .GetProperty("diagnosticAssessments").EnumerateArray());
        Assert.Equal("Corroborated", status.GetProperty("status").GetString());
        Assert.Equal(1, status.GetProperty("count").GetInt32());
    }

}
