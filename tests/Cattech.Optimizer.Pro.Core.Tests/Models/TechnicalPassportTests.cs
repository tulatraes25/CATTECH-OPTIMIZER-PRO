using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class TechnicalPassportTests
{
    private static readonly Guid ExportId =
        Guid.Parse("92929292-2222-4222-8222-929292929292");
    private static readonly Guid FirstId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SecondId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid SensitiveId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 10, 15, 6, 0, 0, TimeSpan.Zero);

    private static readonly DiagnosticContextId Context =
        DiagnosticContextId.FromGuid(
            Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"));

    private static EvidenceRecord Observed(Guid id, string value,
        EvidencePrivacyClass privacy = EvidencePrivacyClass.Equipment,
        string source = "synthetic", DateTimeOffset? observedAt = null,
        DiagnosticContextId? context = null) =>
        EvidenceRecord.Observed(id, source, "windows.health",
            observedAt ?? IssuedAt.AddHours(-2), EvidenceValue.FromText(value),
            "synthetic-test-only", privacy,
            "Only fixture data.", contextId: context ?? Context);

    private static EvidenceRecord Missing(Guid id) =>
        EvidenceRecord.Missing(id, "synthetic", "windows.health",
            IssuedAt.AddHours(-1), EvidenceAvailability.AccessDenied,
            "fixture", EvidencePrivacyClass.Equipment,
            "Synthetic access denied.", contextId: Context);

    private static DiagnosticAssessment Diagnostic(EvidenceRecord fact) =>
        DeterministicDiagnosticEvaluator.Evaluate(
            new DiagnosticRule("diagnostic-rule", "1", "A diagnostic hypothesis",
                "A contradicting observation", new[]
                {
                    new EvidenceExpectation("windows.health",
                        EvidenceValue.FromText("healthy"))
                }), new[] { fact }, IssuedAt);

    private static RepairOutcomeAssessment Outcome(
        EvidenceRecord before, EvidenceRecord after) =>
        RepairOutcomeEvaluator.Compare(
            new RepairOutcomeRule("outcome-rule", "1", "windows.health",
                EvidenceValue.FromText("healthy")), before, after);

    [Fact]
    public void Export_IsCanonical_RegardlessOfInputOrder()
    {
        var healthy = Observed(SecondId, "healthy",
            observedAt: IssuedAt.AddHours(-1));
        var unhealthy = Observed(FirstId, "unhealthy");
        var missing = Missing(SensitiveId);
        var diagnostic = Diagnostic(healthy);
        var outcome = Outcome(unhealthy, healthy);

        var first = TechnicalPassportBuilder.Build(ExportId, IssuedAt,
            new[] { healthy, missing, unhealthy }, new[] { diagnostic },
            new[] { outcome });
        var second = TechnicalPassportBuilder.Build(ExportId, IssuedAt,
            new[] { unhealthy, healthy, missing }, new[] { diagnostic },
            new[] { outcome });

        Assert.Equal(first.PayloadJson, second.PayloadJson);
        Assert.Equal(first.Sha256Hex, second.Sha256Hex);
        Assert.True(first.HasMatchingDigest());
        Assert.Equal(64, first.Sha256Hex.Length);

        using var json = JsonDocument.Parse(first.PayloadJson);
        var root = json.RootElement;
        Assert.Equal("cattech.technical-passport.public.v1",
            root.GetProperty("schemaVersion").GetString());
        Assert.Equal(2, root.GetProperty("evidence").GetProperty("observed").GetInt32());
        Assert.Equal(1, root.GetProperty("evidence").GetProperty("accessDenied").GetInt32());
        Assert.Equal("Corroborated", root.GetProperty("diagnosticAssessments")[0]
            .GetProperty("status").GetString());
        Assert.Equal("ObservedImprovement", root.GetProperty("outcomeAssessments")[0]
            .GetProperty("status").GetString());
        Assert.DoesNotContain("windows.health", first.PayloadJson);
        Assert.DoesNotContain("diagnostic-rule", first.PayloadJson);
        Assert.DoesNotContain("outcome-rule", first.PayloadJson);
    }

    [Fact]
    public void Export_ExcludesSensitiveRawDataAndRelatedConclusions()
    {
        const string marker = "PRIVATE-EMAIL-user@example.invalid-DEVICE-SERIAL-123";
        const string path = @"C:\Users\PrivatePerson\Documents\confidential.dmp";
        var sensitive = EvidenceRecord.Observed(SensitiveId, marker,
            "windows.health", IssuedAt.AddHours(-2),
            EvidenceValue.FromText(path), marker,
            EvidencePrivacyClass.Sensitive, marker, contextId: Context);
        var safe = Observed(FirstId, "unhealthy", source: marker);
        var second = Observed(SecondId, "healthy",
            observedAt: IssuedAt.AddHours(-1));
        var assessment = DeterministicDiagnosticEvaluator.Evaluate(
            new DiagnosticRule(marker, "1", marker, marker,
                new[] { new EvidenceExpectation("windows.health",
                    EvidenceValue.FromText(path)) }),
            new[] { sensitive }, IssuedAt);
        var sensitiveOutcome = Outcome(sensitive, second);
        var safeOutcome = Outcome(safe, second);

        var report = TechnicalPassportBuilder.Build(ExportId, IssuedAt,
            new[] { safe, sensitive, second }, new[] { assessment },
            new[] { sensitiveOutcome, safeOutcome });

        Assert.DoesNotContain(marker, report.PayloadJson);
        Assert.DoesNotContain(path, report.PayloadJson);
        Assert.DoesNotContain(SensitiveId.ToString(), report.PayloadJson);
        Assert.DoesNotContain(FirstId.ToString(), report.PayloadJson);
        using var json = JsonDocument.Parse(report.PayloadJson);
        Assert.Equal(2, json.RootElement.GetProperty("evidence")
            .GetProperty("observed").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("diagnosticAssessments")
            .GetArrayLength());
        Assert.Equal(1, json.RootElement.GetProperty("outcomeAssessments")
            .GetArrayLength());
    }

    [Fact]
    public void MissingEvidence_IsNotReportedAsObserved()
    {
        var report = TechnicalPassportBuilder.Build(ExportId, IssuedAt,
            new[] { Missing(FirstId) }, Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>());

        using var json = JsonDocument.Parse(report.PayloadJson);
        Assert.Equal(0, json.RootElement.GetProperty("evidence")
            .GetProperty("observed").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("evidence")
            .GetProperty("accessDenied").GetInt32());
        Assert.Empty(json.RootElement.GetProperty("outcomeAssessments")
            .EnumerateArray());
    }

    [Fact]
    public void DigestDetectsPayloadEdits_ButIsNotAnAuthenticitySignature()
    {
        var report = TechnicalPassportBuilder.Build(ExportId, IssuedAt,
            new[] { Observed(FirstId, "healthy") },
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>());
        Assert.True(TechnicalPassportArtifact.MatchesDigest(
            report.PayloadJson, report.Sha256Hex));
        Assert.False(TechnicalPassportArtifact.MatchesDigest(
            report.PayloadJson + " ", report.Sha256Hex));
        Assert.False(TechnicalPassportArtifact.MatchesDigest(
            report.PayloadJson, "NOT-A-DIGEST"));
        Assert.False(TechnicalPassportArtifact.MatchesDigest(
            null, report.Sha256Hex));
        Assert.Contains("No signature", report.PayloadJson);
    }

    [Fact]
    public void Export_NormalizesIssuedAtToUtc()
    {
        var issuedLocal = IssuedAt.ToOffset(TimeSpan.FromHours(-3));
        var report = TechnicalPassportBuilder.Build(ExportId, issuedLocal,
            new[] { Observed(FirstId, "healthy") },
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>());

        using var json = JsonDocument.Parse(report.PayloadJson);
        var time = json.RootElement.GetProperty("issuedAtUtc").GetDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, time.Offset);
        Assert.Equal(IssuedAt, time);
    }

    [Fact]
    public void DuplicateEvidenceIdsAndReusedExportIdentity_FailClosed()
    {
        var record = Observed(FirstId, "healthy");
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            ExportId, IssuedAt, new[] { record, record },
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>()));
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            FirstId, IssuedAt, new[] { record },
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>()));
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            Guid.Empty, IssuedAt, new[] { record },
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void MissingLinkedEvidence_FailsClosed()
    {
        var first = Observed(FirstId, "unhealthy");
        var second = Observed(SecondId, "healthy");
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            ExportId, IssuedAt, new[] { first },
            Array.Empty<DiagnosticAssessment>(),
            new[] { Outcome(first, second) }));
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            ExportId, IssuedAt, new[] { first },
            new[] { Diagnostic(second) },
            Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void FutureDatedObservation_IsNotExportedAsVerified()
    {
        var future = EvidenceRecord.Observed(FirstId, "fixture", "windows.health",
            IssuedAt.AddMinutes(10), EvidenceValue.FromText("healthy"),
            "fixture", EvidencePrivacyClass.Equipment, contextId: Context);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            ExportId, IssuedAt, new[] { future },
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void OutcomesCannotBeConstructedWithArbitraryStatusesByCallers()
    {
        // A public constructor could fabricate ObservedImprovement without
        // any pre/post rule evaluation. Only the evaluator constructs outcomes.
        Assert.Empty(typeof(RepairOutcomeAssessment).GetConstructors());

        var before = Observed(FirstId, "unhealthy");
        var after = Observed(SecondId, "healthy",
            observedAt: IssuedAt.AddHours(-1));
        var measured = Outcome(before, after);
        Assert.Equal(RepairOutcomeStatus.ObservedImprovement, measured.Status);

        var passport = TechnicalPassportBuilder.Build(ExportId, IssuedAt,
            new[] { before, after }, Array.Empty<DiagnosticAssessment>(),
            new[] { measured });
        Assert.Contains("ObservedImprovement", passport.PayloadJson);
    }

    [Fact]
    public void TooManyEvidenceRows_RefuseUnboundedSnapshot()
    {
        var record = Observed(FirstId, "healthy");
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            ExportId, IssuedAt, Enumerable.Repeat(record, 10_001),
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void IncompleteDiagnostics_NeverLeakThroughPublicStatusCounts()
    {
        var safe = Observed(FirstId, "healthy");

        // This diagnostic includes safe corroboration, but also an unlinked
        // missing fact that could describe sensitive customer information.
        var partial = DeterministicDiagnosticEvaluator.Evaluate(
            new DiagnosticRule("private-gap", "1", "synthetic", "synthetic", new[]
            {
                new EvidenceExpectation("windows.health", EvidenceValue.FromText("healthy")),
                new EvidenceExpectation("private.customer", EvidenceValue.FromText("present"))
            }),
            new[] { safe }, IssuedAt);
        var missingOnly = DeterministicDiagnosticEvaluator.Evaluate(
            new DiagnosticRule("missing-only", "1", "synthetic", "synthetic", new[]
            {
                new EvidenceExpectation("private.customer", EvidenceValue.FromText("present"))
            }),
            Array.Empty<EvidenceRecord>(), IssuedAt, Context);

        Assert.Equal(DiagnosticAssessmentStatus.InsufficientEvidence, partial.Status);
        Assert.Single(partial.SupportingEvidenceIds);
        Assert.Single(partial.MissingFactKeys);
        Assert.Empty(missingOnly.SupportingEvidenceIds);
        Assert.Single(missingOnly.MissingFactKeys);

        var passport = TechnicalPassportBuilder.Build(ExportId, IssuedAt,
            new[] { safe }, new[] { partial, missingOnly },
            Array.Empty<RepairOutcomeAssessment>());
        using var json = JsonDocument.Parse(passport.PayloadJson);

        Assert.Equal(0, json.RootElement.GetProperty("diagnosticAssessments")
            .GetArrayLength());
        Assert.Equal(1, json.RootElement.GetProperty("evidence")
            .GetProperty("observed").GetInt32());
    }



    [Fact]
    public void MixedEquipmentObservations_CannotBecomeOnePublicPassport()
    {
        var different = DiagnosticContextId.FromGuid(
            Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"));
        var a = Observed(FirstId, "unhealthy");
        var b = Observed(SecondId, "healthy", context: different);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            ExportId, IssuedAt, new[] { a, b },
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void AssessmentFromAnotherCase_IsRejectedEvenWhenReferencesExist()
    {
        var different = DiagnosticContextId.FromGuid(
            Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"));
        var a = Observed(FirstId, "unhealthy");
        var b = Observed(SecondId, "healthy");
        var foreign = Observed(SecondId, "healthy", context: different);
        var foreignAssessment = Diagnostic(foreign);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            ExportId, IssuedAt, new[] { a, b }, new[] { foreignAssessment },
            Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void PublicJsonOmitsOpaqueContextIdentifier()
    {
        var record = Observed(FirstId, "healthy");
        var passport = TechnicalPassportBuilder.Build(ExportId, IssuedAt,
            new[] { record }, Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>());

        Assert.Equal(Context, passport.ContextId);
        Assert.DoesNotContain(Context.Value.ToString(), passport.PayloadJson,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contextId", passport.PayloadJson,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contextId", JsonSerializer.Serialize(passport),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacyUnboundEvidence_IsNotExported()
    {
        var old = EvidenceRecord.Observed(FirstId, "synthetic", "windows.health",
            IssuedAt.AddHours(-2), EvidenceValue.FromText("healthy"),
            "legacy-fixture", EvidencePrivacyClass.Equipment);

        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            ExportId, IssuedAt, new[] { old },
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void CrossContextOutcome_IsNotExportedEvenWithKnownEvidenceIds()
    {
        var different = DiagnosticContextId.FromGuid(
            Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"));
        var before = Observed(FirstId, "unhealthy");
        var after = Observed(SecondId, "healthy");
        var foreign = Observed(SecondId, "healthy", context: different);
        var badOutcome = Outcome(before, foreign);

        Assert.Equal(RepairOutcomeStatus.Indeterminate, badOutcome.Status);
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            ExportId, IssuedAt, new[] { before, after },
            Array.Empty<DiagnosticAssessment>(), new[] { badOutcome }));
    }


    [Fact]
    public void EmptyPassport_IsRejectedWithoutInventingAnEquipmentContext()
    {
        Assert.Throws<ArgumentException>(() => TechnicalPassportBuilder.Build(
            ExportId, IssuedAt, Array.Empty<EvidenceRecord>(),
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>()));
    }

}
