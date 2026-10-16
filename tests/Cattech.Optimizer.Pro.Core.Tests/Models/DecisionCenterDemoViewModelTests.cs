using Cattech.Optimizer.Pro.Core.Models.Evidence;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class DecisionCenterDemoViewModelTests
{
    [Fact]
    public void OpeningDemo_StartsWithClearlyLabeledIncompleteSyntheticCase()
    {
        var vm = new DecisionCenterDemoViewModel();

        Assert.Contains("DEMO CON DATOS FICTICIOS", vm.DemoNotice);
        Assert.Contains("no inspecciona este equipo", vm.DemoNotice);
        Assert.Contains("datos insuficientes", vm.ScenarioTitle);
        Assert.Equal(DecisionDiagnosticState.IncompleteEvidence,
            vm.Summary.DiagnosticState);
        Assert.Equal(DecisionVerificationState.NotMeasured,
            vm.Summary.VerificationState);
        Assert.Equal(DecisionNextAction.CompleteMissingMeasurements,
            vm.Summary.NextAction);
    }

    [Fact]
    public void ScenarioCommands_ActuallyChangeExplanationAndNextStep()
    {
        var vm = new DecisionCenterDemoViewModel();
        var initial = vm.Summary;

        vm.ShowConflictingCommand.Execute(null);
        Assert.NotSame(initial, vm.Summary);
        Assert.Equal(DecisionDiagnosticState.ConflictingEvidence,
            vm.Summary.DiagnosticState);
        Assert.Equal(DecisionNextAction.ResolveContradictions,
            vm.Summary.NextAction);
        Assert.Contains("contradictorias", vm.ScenarioTitle);

        vm.ShowImprovementCommand.Execute(null);
        Assert.Equal(DecisionDiagnosticState.ConsistentWithRule,
            vm.Summary.DiagnosticState);
        Assert.Equal(DecisionVerificationState.ObservedConditionImproved,
            vm.Summary.VerificationState);
        Assert.Equal(DecisionNextAction.MonitorAndDocument,
            vm.Summary.NextAction);
        Assert.Contains("no demuestra", vm.ScenarioDescription);

        vm.ShowUnresolvedCommand.Execute(null);
        Assert.Equal(DecisionVerificationState.UnresolvedCondition,
            vm.Summary.VerificationState);
        Assert.Equal(DecisionNextAction.InvestigatePersistence,
            vm.Summary.NextAction);
        Assert.Contains("problema persistente", vm.ScenarioTitle);

        vm.ShowMissingCommand.Execute(null);
        Assert.Equal(DecisionDiagnosticState.IncompleteEvidence,
            vm.Summary.DiagnosticState);
        Assert.Equal(DecisionVerificationState.NotMeasured,
            vm.Summary.VerificationState);
    }

    [Fact]
    public void ViewModel_RaisesBindableNotificationsWhenScenarioChanges()
    {
        var vm = new DecisionCenterDemoViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        vm.ShowImprovementCommand.Execute(null);

        Assert.Contains(nameof(vm.Summary), changed);
        Assert.Contains(nameof(vm.ScenarioTitle), changed);
        Assert.Contains(nameof(vm.ScenarioDescription), changed);
    }

    [Fact]
    public void EveryDemoScenario_ReportsNoRealRepairCapability()
    {
        var vm = new DecisionCenterDemoViewModel();
        foreach (Action show in new Action[]
        {
            () => vm.ShowMissingCommand.Execute(null),
            () => vm.ShowConflictingCommand.Execute(null),
            () => vm.ShowImprovementCommand.Execute(null),
            () => vm.ShowUnresolvedCommand.Execute(null)
        })
        {
            show();
            Assert.Equal(1, vm.Summary.DiagnosticRuleCount);
            Assert.Contains("no ejecuta reparaciones", vm.Summary.SafetyNotice);
            Assert.Contains("no", vm.DemoNotice);
            Assert.DoesNotContain("Causa confirmada", vm.Summary.VerificationText);
        }
    }

    [Fact]
    public void Demo_IsIsolatedFromExternalDataAndDoesNotExposeDeviceId()
    {
        var vm = new DecisionCenterDemoViewModel();
        vm.ShowImprovementCommand.Execute(null);

        var json = System.Text.Json.JsonSerializer.Serialize(vm.Summary);
        Assert.DoesNotContain("fixture-demo-only", json);
        Assert.DoesNotContain("demo.disk.health", json);
        Assert.DoesNotContain("synthetic-demo", json);
        Assert.DoesNotContain("SerialNumber", json);
    }
}
