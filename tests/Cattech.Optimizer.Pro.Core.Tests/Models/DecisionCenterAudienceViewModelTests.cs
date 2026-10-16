using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class DecisionCenterAudienceViewModelTests
{
    [Fact]
    public void Startup_RemainsTechnicalAndHasNoShareOrExportAction()
    {
        var vm = new DecisionCenterDemoViewModel();

        Assert.False(vm.IsClientView);
        Assert.Equal(DecisionAudience.Technician, vm.Explanation.Audience);
        Assert.Contains("Vista técnica", vm.Explanation.AudienceLabel);
        Assert.Contains("Hipótesis evaluadas:", vm.Explanation.EvidenceCountText);
        Assert.Contains("DEMO CON DATOS FICTICIOS", vm.DemoNotice);
        Assert.DoesNotContain("autoriza compartir", vm.Explanation.SafetyNotice);
    }

    [Fact]
    public void ChangingAudience_KeepsExactlyTheSameEvaluationObject()
    {
        var vm = new DecisionCenterDemoViewModel();
        vm.ShowConflictingCommand.Execute(null);
        var sameSummary = vm.Summary;
        var originalStatus = sameSummary.DiagnosticState;
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.ShowClientExplanationCommand.Execute(null);

        Assert.True(vm.IsClientView);
        Assert.Equal(DecisionAudience.Client, vm.Explanation.Audience);
        Assert.Same(sameSummary, vm.Summary);
        Assert.Equal(originalStatus, vm.Summary.DiagnosticState);
        Assert.Contains(nameof(vm.IsClientView), changes);
        Assert.Contains(nameof(vm.Explanation), changes);
        Assert.DoesNotContain(nameof(vm.Summary), changes);
        Assert.Empty(vm.Explanation.EvidenceCountText);
        Assert.Empty(vm.Explanation.PostcheckCountText);
        Assert.Contains("DEMO", vm.DemoNotice);

        vm.ShowTechnicianExplanationCommand.Execute(null);

        Assert.False(vm.IsClientView);
        Assert.Same(sameSummary, vm.Summary);
        Assert.Equal(DecisionAudience.Technician, vm.Explanation.Audience);
    }

    [Fact]
    public void ClientAudiencePersistsWhenFictitiousScenarioChanges()
    {
        var vm = new DecisionCenterDemoViewModel();
        vm.ShowClientExplanationCommand.Execute(null);

        vm.ShowImprovementCommand.Execute(null);
        Assert.True(vm.Explanation.IsClientView);
        Assert.Equal(DecisionVerificationState.ObservedConditionImproved,
            vm.Summary.VerificationState);
        Assert.Contains("no demuestra qué produjo el cambio",
            vm.Explanation.VerificationText);
        Assert.Equal(string.Empty, vm.Explanation.EvidenceCountText);

        vm.ShowMissingCommand.Execute(null);
        Assert.True(vm.Explanation.IsClientView);
        Assert.Equal(DecisionDiagnosticState.IncompleteEvidence,
            vm.Summary.DiagnosticState);
        Assert.Contains("La ausencia de una medición no significa que esté todo bien",
            vm.Explanation.DiagnosticText);

        vm.ShowUnresolvedCommand.Execute(null);
        Assert.True(vm.Explanation.IsClientView);
        Assert.Equal(DecisionVerificationState.UnresolvedCondition,
            vm.Summary.VerificationState);
        Assert.Contains("todavía hay una condición", vm.Explanation.VerificationText);
    }

    [Fact]
    public void ClientExplanation_DoesNotIncludeSyntheticRawFactsOrDeviceIds()
    {
        var vm = new DecisionCenterDemoViewModel();
        vm.ShowClientExplanationCommand.Execute(null);

        foreach (Action showScenario in new Action[]
        {
            () => vm.ShowMissingCommand.Execute(null),
            () => vm.ShowConflictingCommand.Execute(null),
            () => vm.ShowImprovementCommand.Execute(null),
            () => vm.ShowUnresolvedCommand.Execute(null)
        })
        {
            showScenario();
            var payload = JsonSerializer.Serialize(vm.Explanation);
            Assert.DoesNotContain("demo.disk.health", payload);
            Assert.DoesNotContain("demo.check.completed", payload);
            Assert.DoesNotContain("fixture-demo-only", payload);
            Assert.DoesNotContain("synthetic-demo", payload);
            Assert.DoesNotContain("SN-", payload);
            Assert.DoesNotContain("Comparaciones realizadas:", payload);
            Assert.DoesNotContain("Hipótesis evaluadas:", payload);
            Assert.Contains("no autoriza compartir datos", payload);
        }
    }

    [Fact]
    public void AudienceSwitch_AloneNeverChangesScenarioExplanation()
    {
        var vm = new DecisionCenterDemoViewModel();
        vm.ShowImprovementCommand.Execute(null);
        var title = vm.ScenarioTitle;
        var explanation = vm.ScenarioDescription;
        var before = JsonSerializer.Serialize(vm.Summary);

        vm.ShowClientExplanationCommand.Execute(null);
        vm.ShowTechnicianExplanationCommand.Execute(null);
        vm.ShowClientExplanationCommand.Execute(null);

        Assert.Equal(title, vm.ScenarioTitle);
        Assert.Equal(explanation, vm.ScenarioDescription);
        Assert.Equal(before, JsonSerializer.Serialize(vm.Summary));
        Assert.True(vm.IsClientView);
    }
}
