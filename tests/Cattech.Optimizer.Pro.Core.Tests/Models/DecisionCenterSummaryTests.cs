using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class DecisionCenterSummaryTests
{
    private static readonly DiagnosticContextId CaseA =
        new(Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
    private static readonly DiagnosticContextId CaseB =
        new(Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
    private static readonly DiagnosticSubjectId DiskA =
        new(Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"));
    private static readonly DateTimeOffset Now =
        new(2026, 10, 15, 20, 0, 0, TimeSpan.Zero);
    private static readonly Guid BeforeId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid AfterId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OtherId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");

    private static EvidenceRecord Fact(
        Guid id, string value, DiagnosticContextId? scope = null,
        DateTimeOffset? time = null, string key = "disk.health") =>
        EvidenceRecord.Observed(
            id, scope ?? CaseA, DiskA, "synthetic-provider", key,
            time ?? Now.AddMinutes(-2), EvidenceValue.FromText(value),
            "fixture-provenance", EvidencePrivacyClass.Equipment);

    private static DiagnosticAssessment Diagnose(
        string expected = "healthy",
        IEnumerable<EvidenceRecord>? records = null,
        DiagnosticContextId? scope = null, string key = "disk.health",
        string ruleId = "rule-1") =>
        DeterministicDiagnosticEvaluator.Evaluate(
            scope ?? CaseA,
            new DiagnosticRule(ruleId, "1", "synthetic hypothesis",
                "contrary reading", new[]
                {
                    new EvidenceExpectation(DiskA, key,
                        EvidenceValue.FromText(expected))
                }),
            records ?? new[] { Fact(AfterId, expected, scope, key: key) }, Now);

    private static RepairOutcomeAssessment Postcheck(
        string before = "warning", string after = "healthy",
        DiagnosticContextId? scope = null) =>
        RepairOutcomeEvaluator.Compare(
            new RepairOutcomeRule("post", "1", "disk.health",
                EvidenceValue.FromText("healthy")),
            Fact(BeforeId, before, scope, Now.AddHours(-1)),
            Fact(AfterId, after, scope, Now.AddMinutes(-2)));

    private static DecisionCenterSummary Build(
        IEnumerable<DiagnosticAssessment>? assessments = null,
        IEnumerable<RepairOutcomeAssessment>? postchecks = null,
        DiagnosticContextId? scope = null) =>
        DecisionCenterBuilder.Build(scope ?? CaseA,
            assessments ?? Array.Empty<DiagnosticAssessment>(),
            postchecks ?? Array.Empty<RepairOutcomeAssessment>());

    [Fact]
    public void EmptyCase_DoesNotImplyHealthyComputer()
    {
        var result = Build();

        Assert.Equal(DecisionDiagnosticState.NotEvaluated, result.DiagnosticState);
        Assert.Equal(DecisionVerificationState.NotMeasured, result.VerificationState);
        Assert.Equal(DecisionNextAction.GatherInitialEvidence, result.NextAction);
        Assert.Equal(0, result.DiagnosticRuleCount);
        Assert.Equal(0, result.PostcheckCount);
        Assert.Contains("Sin hipótesis", result.DiagnosticText);
        Assert.Contains("no ejecuta reparaciones", result.SafetyNotice);
    }

    [Fact]
    public void MatchingEvidence_IsConsistentWithRuleNotProofOfCause()
    {
        var summary = Build(new[] { Diagnose() });

        Assert.Equal(DecisionDiagnosticState.ConsistentWithRule, summary.DiagnosticState);
        Assert.Equal(DecisionNextAction.ReviewRepairPlan, summary.NextAction);
        Assert.Contains("no prueban la causa", summary.DiagnosticText);
        Assert.Equal(DecisionVerificationState.NotMeasured, summary.VerificationState);
    }

    [Fact]
    public void MissingAndFutureDatedMeasurements_BlockPositiveDiagnostic()
    {
        var missing = Diagnose(records: Array.Empty<EvidenceRecord>());
        var future = Diagnose(records: new[]
        {
            Fact(AfterId, "healthy", time: Now.AddMinutes(2))
        }, ruleId: "future");
        var summary = Build(new[] { missing, future });

        Assert.Equal(DecisionDiagnosticState.IncompleteEvidence, summary.DiagnosticState);
        Assert.Equal(DecisionNextAction.CompleteMissingMeasurements, summary.NextAction);
        Assert.Equal(2, summary.DiagnosticRuleCount);
    }

    [Fact]
    public void ConflictingReadings_TakePriorityOverConsistentAndIncompleteRules()
    {
        var conflict = Diagnose(records: new[]
        {
            Fact(BeforeId, "warning"),
            Fact(AfterId, "healthy")
        });
        var summary = Build(new[]
        {
            Diagnose(ruleId: "clean"),
            Diagnose(records: Array.Empty<EvidenceRecord>(), ruleId: "missing"),
            conflict
        });

        Assert.Equal(DecisionDiagnosticState.ConflictingEvidence, summary.DiagnosticState);
        Assert.Equal(DecisionNextAction.ResolveContradictions, summary.NextAction);
        Assert.Contains("contradicen", summary.DiagnosticText);
    }

    [Fact]
    public void RefutedHypothesis_DoesNotTriggerRepairSuggestion()
    {
        var summary = Build(new[]
        {
            Diagnose(records: new[] { Fact(AfterId, "warning") })
        });

        Assert.Equal(DecisionDiagnosticState.RefutedRule, summary.DiagnosticState);
        Assert.Equal(DecisionNextAction.RevisitHypothesis, summary.NextAction);
    }

    [Fact]
    public void ObservedImprovement_DoesNotClaimCausalRepair()
    {
        var result = Build(new[] { Diagnose() }, new[] { Postcheck() });

        Assert.Equal(DecisionDiagnosticState.ConsistentWithRule, result.DiagnosticState);
        Assert.Equal(DecisionVerificationState.ObservedConditionImproved,
            result.VerificationState);
        Assert.Equal(DecisionNextAction.MonitorAndDocument, result.NextAction);
        Assert.Contains("no demuestra su causa", result.VerificationText);
    }

    [Fact]
    public void UnresolvedCondition_RequiresFurtherInvestigation()
    {
        var unrelatedCorroboration = Diagnose(
            expected: "present", key: "windows.log", ruleId: "event-observed");
        var result = Build(
            new[] { unrelatedCorroboration },
            new[] { Postcheck(after: "warning") });

        Assert.Equal(DecisionVerificationState.UnresolvedCondition,
            result.VerificationState);
        Assert.Equal(DecisionNextAction.InvestigatePersistence, result.NextAction);
        Assert.Contains("no informar reparación exitosa", result.NextStepText);
    }

    [Fact]
    public void IndeterminatePostcheck_RequiresRepeatNotCertification()
    {
        var result = Build(new[] { Diagnose() },
            new[] { Postcheck(before: "healthy", after: "healthy") });

        Assert.Equal(DecisionVerificationState.Indeterminate,
            result.VerificationState);
        Assert.Equal(DecisionNextAction.RepeatPostcheck, result.NextAction);
    }

    [Fact]
    public void IncompleteDiagnostic_OverridesPositivePostcheck()
    {
        var result = Build(
            new[] { Diagnose(records: Array.Empty<EvidenceRecord>()) },
            new[] { Postcheck() });

        Assert.Equal(DecisionDiagnosticState.IncompleteEvidence, result.DiagnosticState);
        Assert.Equal(DecisionVerificationState.ObservedConditionImproved,
            result.VerificationState);
        Assert.Equal(DecisionNextAction.CompleteMissingMeasurements,
            result.NextAction);
    }

    [Fact]
    public void CrossEquipmentDiagnosticsAndPostchecks_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => Build(
            new[] { Diagnose(scope: CaseB) }));
        Assert.Throws<ArgumentException>(() => Build(
            postchecks: new[] { Postcheck(scope: CaseB) }));
        Assert.Throws<ArgumentException>(() => DecisionCenterBuilder.Build(
            default, Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void DuplicateRuleOrPostcheck_IsNotCountedAsAdditionalProof()
    {
        var diagnostic = Diagnose();
        var postcheck = Postcheck();

        Assert.Throws<ArgumentException>(() =>
            Build(new[] { diagnostic, diagnostic }));
        Assert.Throws<ArgumentException>(() =>
            Build(postchecks: new[] { postcheck, postcheck }));
    }

    [Fact]
    public void OversizeOrNullInputs_FailClosed()
    {
        var diagnostic = Diagnose();
        Assert.Throws<ArgumentException>(() =>
            Build(Enumerable.Repeat(diagnostic, 257)));
        Assert.Throws<ArgumentException>(() =>
            Build(postchecks: Enumerable.Repeat(Postcheck(), 257)));
        Assert.Throws<ArgumentNullException>(() =>
            DecisionCenterBuilder.Build(CaseA, null!,
                Array.Empty<RepairOutcomeAssessment>()));
    }

    [Fact]
    public void StableSummary_DoesNotDependOnRuleEnumerationOrder()
    {
        var first = Diagnose(ruleId: "first");
        var second = Diagnose(records: Array.Empty<EvidenceRecord>(), ruleId: "second");
        var a = Build(new[] { first, second });
        var b = Build(new[] { second, first });

        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.Equal(DecisionDiagnosticState.IncompleteEvidence, a.DiagnosticState);
    }

    [Fact]
    public void PublicSummary_ExcludesSerialsPathsGuidsAndArbitraryHypotheses()
    {
        const string secret = "PRIVATE-CUSTOMER-PRIVATE-SERIAL-XYZ";
        const string privatePath = @"C:\Users\PRIVATE\Documents\sensitive.dmp";
        var observation = EvidenceRecord.Observed(
            OtherId, CaseA, DiskA, privatePath, "private.fact",
            Now.AddMinutes(-1), EvidenceValue.FromText(secret),
            secret, EvidencePrivacyClass.Sensitive, secret);
        var rule = new DiagnosticRule(secret, "1", secret, privatePath,
            new[] { new EvidenceExpectation(DiskA, "private.fact",
                EvidenceValue.FromText(secret)) });
        var assessment = DeterministicDiagnosticEvaluator.Evaluate(
            CaseA, rule, new[] { observation }, Now);

        var summary = Build(new[] { assessment });
        var json = JsonSerializer.Serialize(summary);

        Assert.DoesNotContain(secret, json);
        Assert.DoesNotContain(privatePath, json);
        Assert.DoesNotContain(OtherId.ToString(), json);
        Assert.DoesNotContain(CaseA.Value.ToString(), json);
        Assert.DoesNotContain(DiskA.Value.ToString(), json);
        Assert.Contains("no prueban la causa", json);
        Assert.Empty(typeof(DecisionCenterSummary).GetConstructors());
    }
}
