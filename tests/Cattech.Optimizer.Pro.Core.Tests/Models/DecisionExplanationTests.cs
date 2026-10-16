using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class DecisionExplanationTests
{
    private static readonly DiagnosticContextId Context =
        new(Guid.Parse("11111111-aaaa-4111-8111-aaaaaaaaaaaa"));
    private static readonly DiagnosticSubjectId Subject =
        new(Guid.Parse("22222222-bbbb-4222-8222-bbbbbbbbbbbb"));
    private static readonly DateTimeOffset Now =
        new(2026, 10, 15, 21, 0, 0, TimeSpan.Zero);

    private static EvidenceRecord Fact(Guid id, string value,
        DateTimeOffset? at = null, string factKey = "case.health",
        string source = "synthetic") =>
        EvidenceRecord.Observed(id, Context, Subject, source, factKey,
            at ?? Now.AddMinutes(-8),
            EvidenceValue.FromText(value), "synthetic-provenance",
            EvidencePrivacyClass.Equipment);

    private static DiagnosticAssessment Diagnostic(DecisionDiagnosticState status)
    {
        const string sensitive = "SYNTHETIC-SERIAL-CLIENT-SECRET-123";
        var rule = new DiagnosticRule(sensitive, "1", sensitive, sensitive,
            new[]
            {
                new EvidenceExpectation(Subject, "case.health",
                    EvidenceValue.FromText("healthy"))
            });

        var input = status switch
        {
            DecisionDiagnosticState.ConsistentWithRule => new[]
            {
                Fact(Guid.Parse("10101010-1010-4010-8010-101010101010"),
                    "healthy", source: sensitive)
            },
            DecisionDiagnosticState.RefutedRule => new[]
            {
                Fact(Guid.Parse("20202020-2020-4020-8020-202020202020"), "warning")
            },
            DecisionDiagnosticState.ConflictingEvidence => new[]
            {
                Fact(Guid.Parse("30303030-3030-4030-8030-303030303030"), "warning"),
                Fact(Guid.Parse("40404040-4040-4040-8040-404040404040"), "healthy")
            },
            DecisionDiagnosticState.IncompleteEvidence =>
                Array.Empty<EvidenceRecord>(),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
        return DeterministicDiagnosticEvaluator.Evaluate(
            Context, rule, input, Now);
    }

    private static RepairOutcomeAssessment Outcome(
        DecisionVerificationState status)
    {
        var before = status == DecisionVerificationState.Indeterminate
            ? "healthy" : "warning";
        var after = status switch
        {
            DecisionVerificationState.ObservedConditionImproved => "healthy",
            DecisionVerificationState.UnresolvedCondition => "warning",
            DecisionVerificationState.Indeterminate => "healthy",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
        return RepairOutcomeEvaluator.Compare(
            new RepairOutcomeRule("post", "1", "case.health",
                EvidenceValue.FromText("healthy")),
            Fact(Guid.Parse("50505050-5050-4050-8050-505050505050"),
                before, Now.AddHours(-2)),
            Fact(Guid.Parse("60606060-6060-4060-8060-606060606060"),
                after, Now.AddMinutes(-2)));
    }

    private static DecisionCenterSummary Make(
        DecisionDiagnosticState diag, DecisionVerificationState outcome)
    {
        var diagnostics = diag == DecisionDiagnosticState.NotEvaluated
            ? Array.Empty<DiagnosticAssessment>()
            : new[] { Diagnostic(diag) };
        var postchecks = outcome == DecisionVerificationState.NotMeasured
            ? Array.Empty<RepairOutcomeAssessment>()
            : new[] { Outcome(outcome) };

        var summary = DecisionCenterBuilder.Build(Context, diagnostics, postchecks);
        Assert.Equal(diag, summary.DiagnosticState);
        Assert.Equal(outcome, summary.VerificationState);
        return summary;
    }

    [Fact]
    public void AllTwentyStatusCombinations_ProduceBoundedSafeMessages()
    {
        foreach (var diagnosis in Enum.GetValues<DecisionDiagnosticState>())
        foreach (var verification in Enum.GetValues<DecisionVerificationState>())
        {
            var summary = Make(diagnosis, verification);
            var before = JsonSerializer.Serialize(summary);
            var technician = DecisionExplanationBuilder.Build(
                summary, DecisionAudience.Technician);
            var client = DecisionExplanationBuilder.Build(
                summary, DecisionAudience.Client);

            Assert.False(technician.IsClientView);
            Assert.True(client.IsClientView);
            Assert.NotEqual(technician.Headline, client.Headline);
            Assert.NotEmpty(client.DiagnosticText);
            Assert.NotEmpty(client.VerificationText);
            Assert.NotEmpty(client.NextStepText);
            Assert.NotEmpty(technician.DiagnosticText);
            Assert.NotEmpty(technician.VerificationText);
            Assert.Contains("no certifica una reparación", client.SafetyNotice);
            Assert.Contains("no ejecuta reparaciones", technician.SafetyNotice);
            Assert.Equal(string.Empty, client.EvidenceCountText);
            Assert.Equal(string.Empty, client.PostcheckCountText);
            Assert.Equal(before, JsonSerializer.Serialize(summary));

            var allText = JsonSerializer.Serialize(client)
                + JsonSerializer.Serialize(technician);
            Assert.DoesNotContain("SYNTHETIC-SERIAL-CLIENT-SECRET-123", allText);
            Assert.DoesNotContain(Context.Value.ToString(), allText,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Subject.Value.ToString(), allText,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("causa confirmada", allText,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("reparado", allText,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ConflictWithPartialImprovement_NeverTellsClientProblemWasResolved()
    {
        var summary = Make(DecisionDiagnosticState.ConflictingEvidence,
            DecisionVerificationState.ObservedConditionImproved);
        var client = DecisionExplanationBuilder.Build(summary, DecisionAudience.Client);

        Assert.Contains("debemos aclarar", client.Headline);
        Assert.Contains("no permite interpretar el resultado como resuelto",
            client.VerificationText);
        Assert.Contains("revisar las diferencias", client.NextStepText);
    }

    [Theory]
    [InlineData(DecisionDiagnosticState.NotEvaluated)]
    [InlineData(DecisionDiagnosticState.RefutedRule)]
    [InlineData(DecisionDiagnosticState.IncompleteEvidence)]
    [InlineData(DecisionDiagnosticState.ConflictingEvidence)]
    public void NonCorroboratedHypotheses_OverridePartialImprovementInClientCopy(
        DecisionDiagnosticState diagnosticState)
    {
        var result = DecisionExplanationBuilder.Build(
            Make(diagnosticState, DecisionVerificationState.ObservedConditionImproved),
            DecisionAudience.Client);

        Assert.Contains("no permite interpretar el resultado como resuelto",
            result.VerificationText);
    }

    [Fact]
    public void TechnicianCounts_AreReadOnlyAggregatesNotPrivateEvidence()
    {
        var summary = Make(DecisionDiagnosticState.ConsistentWithRule,
            DecisionVerificationState.ObservedConditionImproved);
        var expert = DecisionExplanationBuilder.Build(summary,
            DecisionAudience.Technician);

        Assert.Equal("Hipótesis evaluadas: 1", expert.EvidenceCountText);
        Assert.Equal("Comparaciones realizadas: 1", expert.PostcheckCountText);
        Assert.Contains("no demues", expert.VerificationText);
        Assert.Contains("Documentar la mejoría", expert.NextStepText);
    }

    [Fact]
    public void NullOrUnknownAudience_IsRejected()
    {
        var summary = Make(DecisionDiagnosticState.NotEvaluated,
            DecisionVerificationState.NotMeasured);

        Assert.Throws<ArgumentNullException>(() =>
            DecisionExplanationBuilder.Build(null!, DecisionAudience.Client));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DecisionExplanationBuilder.Build(summary, (DecisionAudience)777));
    }

    [Fact]
    public void PresentationIsPureAndNoPublicConstructorAllowsForgedReports()
    {
        Assert.Empty(typeof(DecisionExplanation).GetConstructors());
        var summary = Make(DecisionDiagnosticState.IncompleteEvidence,
            DecisionVerificationState.NotMeasured);

        var first = DecisionExplanationBuilder.Build(summary, DecisionAudience.Client);
        var second = DecisionExplanationBuilder.Build(summary, DecisionAudience.Client);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.All(typeof(DecisionExplanation).GetProperties()
            .Where(p => p.Name != nameof(DecisionExplanation.IsClientView)),
            p => Assert.False(p.CanWrite));
    }
}
