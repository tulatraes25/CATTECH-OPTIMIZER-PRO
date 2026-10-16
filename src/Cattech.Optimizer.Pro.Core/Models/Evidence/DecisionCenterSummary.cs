namespace Cattech.Optimizer.Pro.Core.Models.Evidence;

/// <summary>
/// A conservative summary of one technical service case. These states describe
/// *evidence about configured rules*, not general hardware health or causality.
/// </summary>
public enum DecisionDiagnosticState
{
    NotEvaluated,
    ConsistentWithRule,
    RefutedRule,
    IncompleteEvidence,
    ConflictingEvidence
}

public enum DecisionVerificationState
{
    NotMeasured,
    ObservedConditionImproved,
    UnresolvedCondition,
    Indeterminate
}

public enum DecisionNextAction
{
    GatherInitialEvidence,
    ResolveContradictions,
    CompleteMissingMeasurements,
    RevisitHypothesis,
    ReviewRepairPlan,
    RepeatPostcheck,
    InvestigatePersistence,
    MonitorAndDocument
}

/// <summary>
/// Local advisory model with a fixed vocabulary. Does not contain machine/client
/// identity, arbitrary rule text, evidence IDs or raw measurement values.
/// The constructor is deliberately inaccessible to application consumers.
/// </summary>
public sealed class DecisionCenterSummary
{
    internal DecisionCenterSummary(
        DecisionDiagnosticState diagnosticState,
        DecisionVerificationState verificationState,
        DecisionNextAction nextAction,
        int diagnosticRuleCount,
        int postcheckCount)
    {
        DiagnosticState = diagnosticState;
        VerificationState = verificationState;
        NextAction = nextAction;
        DiagnosticRuleCount = diagnosticRuleCount;
        PostcheckCount = postcheckCount;
    }

    public DecisionDiagnosticState DiagnosticState { get; }
    public DecisionVerificationState VerificationState { get; }
    public DecisionNextAction NextAction { get; }
    public int DiagnosticRuleCount { get; }
    public int PostcheckCount { get; }

    public string DiagnosticText => DiagnosticState switch
    {
        DecisionDiagnosticState.NotEvaluated =>
            "Sin hipótesis evaluadas. No hay diagnóstico concluido.",
        DecisionDiagnosticState.ConsistentWithRule =>
            "Las observaciones coinciden con las reglas comprobadas; no prueban la causa.",
        DecisionDiagnosticState.RefutedRule =>
            "Al menos una hipótesis fue refutada por las observaciones.",
        DecisionDiagnosticState.IncompleteEvidence =>
            "Faltan mediciones, permisos o datos temporalmente consistentes.",
        DecisionDiagnosticState.ConflictingEvidence =>
            "Las observaciones se contradicen: es necesario investigar las fuentes.",
        _ => "No fue posible evaluar el diagnóstico."
    };

    public string VerificationText => VerificationState switch
    {
        DecisionVerificationState.NotMeasured =>
            "No existe una verificación comparable antes y después.",
        DecisionVerificationState.ObservedConditionImproved =>
            "Se observó una mejoría en las condiciones medidas; no demuestra su causa.",
        DecisionVerificationState.UnresolvedCondition =>
            "Al menos una condición evaluada continúa sin alcanzar el objetivo.",
        DecisionVerificationState.Indeterminate =>
            "La verificación no permite confirmar mejoría: hay datos incompatibles o faltantes.",
        _ => "El estado de verificación es desconocido."
    };

    public string NextStepText => NextAction switch
    {
        DecisionNextAction.GatherInitialEvidence =>
            "Iniciar una captura de evidencias verificables antes de planificar acciones.",
        DecisionNextAction.ResolveContradictions =>
            "Comprobar fuentes, componente, hora y consistencia de las mediciones.",
        DecisionNextAction.CompleteMissingMeasurements =>
            "Completar las mediciones faltantes; nunca interpretar ausencia como salud.",
        DecisionNextAction.RevisitHypothesis =>
            "Revisar o descartar la hipótesis refutada antes de intervenir.",
        DecisionNextAction.ReviewRepairPlan =>
            "Examinar un plan de cambios y sus riesgos antes de solicitar autorización.",
        DecisionNextAction.RepeatPostcheck =>
            "Repetir la verificación comparable en el mismo equipo y componente.",
        DecisionNextAction.InvestigatePersistence =>
            "Investigar por qué la condición persiste; no informar reparación exitosa.",
        DecisionNextAction.MonitorAndDocument =>
            "Documentar la mejoría observada y vigilar su estabilidad sin afirmar causalidad.",
        _ => "Revisar el caso con un técnico."
    };

    public string SafetyNotice =>
        "Asesoramiento técnico basado en reglas; no ejecuta reparaciones, no " +
        "certifica causalidad ni prueba por sí solo la identidad física del equipo.";
}

/// <summary>
/// Deterministic, offline and read-only decision gate over already-evaluated
/// evidence. No sensor acquisition, repairs, commands, serialization of raw facts
/// or network calls. A result cannot be created from mixed/unscoped cases.
/// </summary>
public static class DecisionCenterBuilder
{
    private const int MaximumAssessments = 256;

    public static DecisionCenterSummary Build(
        DiagnosticContextId selectedContext,
        IEnumerable<DiagnosticAssessment> diagnostics,
        IEnumerable<RepairOutcomeAssessment> postchecks)
    {
        if (!selectedContext.IsValid)
            throw new ArgumentException("A selected equipment case is required.",
                nameof(selectedContext));

        var rules = Snapshot(diagnostics, nameof(diagnostics));
        var results = Snapshot(postchecks, nameof(postchecks));

        if (rules.Any(x => x.ContextId != selectedContext))
            throw new ArgumentException(
                "Diagnostic assessments belong to another equipment case.",
                nameof(diagnostics));
        if (results.Any(x => x.ContextId != selectedContext))
            throw new ArgumentException(
                "Postchecks belong to another equipment case.", nameof(postchecks));

        if (rules.Any(x => !Enum.IsDefined(x.Status))
            || results.Any(x => !Enum.IsDefined(x.Status)
                || !Enum.IsDefined(x.Reason)))
            throw new ArgumentException("An assessment has an undefined state.");

        // Duplicate results would inflate the apparent level of evidence.
        if (rules.Select(x => (x.RuleId, x.RuleVersion))
            .Distinct().Count() != rules.Length)
            throw new ArgumentException("Repeated diagnostic assessments.",
                nameof(diagnostics));
        if (results.Select(x => (x.BeforeEvidenceId, x.AfterEvidenceId, x.FactKey))
            .Distinct().Count() != results.Length)
            throw new ArgumentException("Repeated postcheck evidence pair.",
                nameof(postchecks));

        var diagnosticsState = rules.Length == 0
            ? DecisionDiagnosticState.NotEvaluated
            : rules.Any(x => x.Status == DiagnosticAssessmentStatus.ConflictingEvidence)
                ? DecisionDiagnosticState.ConflictingEvidence
                : rules.Any(x => x.Status == DiagnosticAssessmentStatus.InsufficientEvidence)
                    ? DecisionDiagnosticState.IncompleteEvidence
                    : rules.Any(x => x.Status == DiagnosticAssessmentStatus.Refuted)
                        ? DecisionDiagnosticState.RefutedRule
                        : DecisionDiagnosticState.ConsistentWithRule;

        var postcheckState = results.Length == 0
            ? DecisionVerificationState.NotMeasured
            : results.Any(x => x.Status == RepairOutcomeStatus.Unresolved)
                ? DecisionVerificationState.UnresolvedCondition
                : results.Any(x => x.Status == RepairOutcomeStatus.Indeterminate)
                    ? DecisionVerificationState.Indeterminate
                    : DecisionVerificationState.ObservedConditionImproved;

        var next = ChooseNextStep(diagnosticsState, postcheckState);
        return new DecisionCenterSummary(
            diagnosticsState, postcheckState, next, rules.Length, results.Length);
    }

    private static DecisionNextAction ChooseNextStep(
        DecisionDiagnosticState diagnosis, DecisionVerificationState postcheck)
    {
        if (diagnosis == DecisionDiagnosticState.NotEvaluated)
            return DecisionNextAction.GatherInitialEvidence;
        if (diagnosis == DecisionDiagnosticState.ConflictingEvidence)
            return DecisionNextAction.ResolveContradictions;
        if (diagnosis == DecisionDiagnosticState.IncompleteEvidence)
            return DecisionNextAction.CompleteMissingMeasurements;
        if (diagnosis == DecisionDiagnosticState.RefutedRule)
            return DecisionNextAction.RevisitHypothesis;

        return postcheck switch
        {
            DecisionVerificationState.UnresolvedCondition =>
                DecisionNextAction.InvestigatePersistence,
            DecisionVerificationState.Indeterminate =>
                DecisionNextAction.RepeatPostcheck,
            DecisionVerificationState.ObservedConditionImproved =>
                DecisionNextAction.MonitorAndDocument,
            _ => DecisionNextAction.ReviewRepairPlan
        };
    }

    private static T[] Snapshot<T>(IEnumerable<T> input, string paramName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(input);
        var snapshot = input.Take(MaximumAssessments + 1).ToArray();
        if (snapshot.Length > MaximumAssessments || snapshot.Any(x => x is null))
            throw new ArgumentException("Assessment list is invalid or too large.",
                paramName);
        return snapshot;
    }
}
