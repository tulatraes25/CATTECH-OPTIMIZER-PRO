using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// SPEC #89 — only controlled status enums may enter a generic serializer.
/// Public Passport uses its own explicit versioned payload builder.
/// All markers are purely synthetic (never from customer machines).
/// </summary>
public sealed class EvidenceAssessmentSerializationSafetyTests
{
    private static readonly DiagnosticContextId Context =
        new(Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"));
    private static readonly DiagnosticSubjectId Subject =
        new(Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"));
    private static readonly DateTimeOffset At =
        new(2026, 10, 17, 17, 0, 0, TimeSpan.Zero);
    private const string PrivateRule = "SYNTHETIC-PRIVATE-RULE-IDENTIFIER";
    private const string PrivateFact = "SYNTHETIC-PRIVATE-FACT-IDENTIFIER";
    private const string PrivateHypothesis = "SYNTHETIC-PRIVATE-HYPOTHESIS";

    [Fact]
    public void DiagnosticAssessment_GenericJsonOmittingCaseIdsRuleTextAndFactKeys()
    {
        var id = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var observed = EvidenceRecord.Observed(
            id, Context, Subject, "QA only", PrivateFact, At.AddMinutes(-30),
            EvidenceValue.FromText("synthetic-value"), "QA",
            EvidencePrivacyClass.Sensitive);
        var rule = new DiagnosticRule(PrivateRule, "SYNTHETIC-PRIVATE-VERSION",
            PrivateHypothesis, "SYNTHETIC-PRIVATE-FALSIFICATION",
            new[]
            {
                new EvidenceExpectation(Subject, PrivateFact,
                    EvidenceValue.FromText("synthetic-value")),
                new EvidenceExpectation(Subject, "SYNTHETIC-PRIVATE-MISSING-KEY",
                    EvidenceValue.FromBoolean(true))
            });
        var assessment = DeterministicDiagnosticEvaluator.Evaluate(
            Context, rule, new[] { observed }, At);
        Assert.Single(assessment.SupportingEvidenceIds);
        Assert.Single(assessment.MissingFactKeys);

        var json = JsonSerializer.Serialize(assessment);
        using var document = JsonDocument.Parse(json);
        var single = Assert.Single(document.RootElement.EnumerateObject());
        Assert.Equal("Status", single.Name);
        Assert.DoesNotContain(Context.Value.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Subject.Value.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(id.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PrivateRule, json);
        Assert.DoesNotContain(PrivateFact, json);
        Assert.DoesNotContain(PrivateHypothesis, json);
        Assert.DoesNotContain("SYNTHETIC-PRIVATE-MISSING-KEY", json);
    }

    [Fact]
    public void PostcheckAssessment_GenericJsonOmittingRuleAndMeasurementMetadata()
    {
        var beforeId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var afterId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var before = EvidenceRecord.Observed(
            beforeId, Context, Subject, "synthetic-qa", PrivateFact,
            At.AddMinutes(-20), EvidenceValue.FromBoolean(false),
            "QA", EvidencePrivacyClass.Equipment);
        var after = EvidenceRecord.Observed(
            afterId, Context, Subject, "synthetic-qa", PrivateFact,
            At.AddMinutes(-10), EvidenceValue.FromBoolean(true),
            "QA", EvidencePrivacyClass.Equipment);
        var result = RepairOutcomeEvaluator.Compare(
            new RepairOutcomeRule(PrivateRule, "SYNTHETIC-PRIVATE-VERSION",
                PrivateFact, EvidenceValue.FromBoolean(true)), before, after);
        Assert.Equal(RepairOutcomeStatus.ObservedImprovement, result.Status);

        var json = JsonSerializer.Serialize(result);
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject()
            .Select(property => property.Name).ToHashSet();
        Assert.True(names.SetEquals(new[] { "Status", "Reason" }));
        Assert.DoesNotContain(PrivateRule, json);
        Assert.DoesNotContain(PrivateFact, json);
        Assert.DoesNotContain(beforeId.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(afterId.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Context.Value.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Subject.Value.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
    }
}
