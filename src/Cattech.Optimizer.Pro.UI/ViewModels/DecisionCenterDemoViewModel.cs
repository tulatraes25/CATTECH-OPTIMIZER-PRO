using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// A fully isolated product walkthrough with synthetic, disposable in-memory
/// observations. Never connects to OS services, SMART, crash dumps or clients.
/// A separate validated adapter is required before displaying real evidence.
/// </summary>
public partial class DecisionCenterDemoViewModel : ObservableObject
{
    private readonly DiagnosticContextId _demoContext =
        DiagnosticContextId.NewSession();

    private readonly DiagnosticSubjectId _demoSubject =
        DiagnosticSubjectId.NewSubject();

    private readonly DateTimeOffset _referenceTime = DateTimeOffset.UtcNow;

    [ObservableProperty]
    private string _scenarioTitle = string.Empty;

    [ObservableProperty]
    private string _scenarioDescription = string.Empty;

    [ObservableProperty]
    private DecisionCenterSummary _summary = null!;

    [ObservableProperty]
    private DecisionExplanation _explanation = null!;

    [ObservableProperty]
    private bool _isClientView;

    // The audience switch reuses the already-evaluated summary. It does
    // not recollect observations or change case identity.
    partial void OnIsClientViewChanged(bool value) => RefreshExplanation();
    partial void OnSummaryChanged(DecisionCenterSummary value) => RefreshExplanation();

    private void RefreshExplanation()
    {
        if (Summary is null)
            return;

        Explanation = DecisionExplanationBuilder.Build(
            Summary, IsClientView
                ? DecisionAudience.Client
                : DecisionAudience.Technician);
    }

    [RelayCommand]
    private void ShowTechnicianExplanation() => IsClientView = false;

    [RelayCommand]
    private void ShowClientExplanation() => IsClientView = true;

    public string DemoNotice =>
        "DEMO CON DATOS FICTICIOS — no inspecciona este equipo. " +
        "No realiza reparaciones, guarda archivos ni transmite información.";

    public string ProductPromise =>
        "De una medición a una decisión explicable: diagnosticar, " +
        "revisar riesgos, comprobar el resultado y documentar sin inventar certezas.";

    public DecisionCenterDemoViewModel()
    {
        ShowMissing();
    }

    private EvidenceRecord Reading(string factKey, string value, int minutesAgo) =>
        EvidenceRecord.Observed(
            Guid.NewGuid(), _demoContext, _demoSubject,
            "fixture-demo-only", factKey,
            _referenceTime.AddMinutes(-minutesAgo),
            EvidenceValue.FromText(value), "synthetic-demo",
            EvidencePrivacyClass.Equipment);

    private DiagnosticAssessment Check(
        string factKey, string expected,
        params EvidenceRecord[] observations)
    {
        var rule = new DiagnosticRule("demo-rule", "1",
            "A fictional technical hypothesis",
            "A contrary synthetic observation would refute it.",
            new[]
            {
                new EvidenceExpectation(_demoSubject, factKey,
                    EvidenceValue.FromText(expected))
            });
        return DeterministicDiagnosticEvaluator.Evaluate(
            _demoContext, rule, observations, _referenceTime);
    }

    private RepairOutcomeAssessment Compare(
        EvidenceRecord before, EvidenceRecord after) =>
        RepairOutcomeEvaluator.Compare(
            new RepairOutcomeRule("demo-postcheck", "1",
                "demo.disk.health", EvidenceValue.FromText("healthy")),
            before, after);

    private void Display(string title, string explanation,
        DiagnosticAssessment diagnosis,
        RepairOutcomeAssessment? outcome = null)
    {
        ScenarioTitle = title;
        ScenarioDescription = explanation;
        Summary = DecisionCenterBuilder.Build(
            _demoContext, new[] { diagnosis },
            outcome is null
                ? Array.Empty<RepairOutcomeAssessment>()
                : new[] { outcome });
    }

    [RelayCommand]
    private void ShowMissing()
    {
        Display(
            "Escenario 1 — datos insuficientes",
            "El técnico todavía no obtuvo una lectura confiable del disco ficticio.",
            Check("demo.disk.health", "healthy"));
    }

    [RelayCommand]
    private void ShowConflicting()
    {
        Display(
            "Escenario 2 — fuentes contradictorias",
            "Dos lecturas ficticias del mismo componente no coinciden.",
            Check("demo.disk.health", "healthy",
                Reading("demo.disk.health", "warning", 30),
                Reading("demo.disk.health", "healthy", 10)));
    }

    [RelayCommand]
    private void ShowImprovement()
    {
        var before = Reading("demo.disk.health", "warning", 40);
        var after = Reading("demo.disk.health", "healthy", 5);
        Display(
            "Escenario 3 — mejoría observada",
            "La segunda medición ficticia alcanzó el objetivo. " +
            "Eso no demuestra qué acción provocó el cambio.",
            Check("demo.disk.health", "healthy", after),
            Compare(before, after));
    }

    [RelayCommand]
    private void ShowUnresolved()
    {
        var before = Reading("demo.disk.health", "warning", 40);
        var after = Reading("demo.disk.health", "warning", 5);
        Display(
            "Escenario 4 — problema persistente",
            "El chequeo ficticio se realizó, pero el disco no alcanzó " +
            "la condición esperada.",
            Check("demo.check.completed", "yes",
                Reading("demo.check.completed", "yes", 3)),
            Compare(before, after));
    }
}
