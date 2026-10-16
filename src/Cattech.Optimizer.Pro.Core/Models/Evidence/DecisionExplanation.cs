namespace Cattech.Optimizer.Pro.Core.Models.Evidence;

/// <summary>
/// Presentation audience only. Choosing Client does not export, disclose,
/// transmit or authorize any information.
/// </summary>
public enum DecisionAudience
{
    Technician,
    Client
}

/// <summary>
/// Closed-vocabulary, immutable explanation of an already prepared case summary.
/// Contains no raw rule text, customer details, evidence, paths or identifiers.
/// Counts are deliberately suppressed for the client audience.
/// </summary>
public sealed class DecisionExplanation
{
    internal DecisionExplanation(
        DecisionAudience audience, string audienceLabel, string headline,
        string diagnosticText, string verificationText, string nextStepText,
        string evidenceCountText, string postcheckCountText,
        string safetyNotice)
    {
        Audience = audience;
        AudienceLabel = audienceLabel;
        Headline = headline;
        DiagnosticText = diagnosticText;
        VerificationText = verificationText;
        NextStepText = nextStepText;
        EvidenceCountText = evidenceCountText;
        PostcheckCountText = postcheckCountText;
        SafetyNotice = safetyNotice;
    }

    public DecisionAudience Audience { get; }
    public string AudienceLabel { get; }
    public string Headline { get; }
    public string DiagnosticText { get; }
    public string VerificationText { get; }
    public string NextStepText { get; }
    public string EvidenceCountText { get; }
    public string PostcheckCountText { get; }
    public string SafetyNotice { get; }

    public bool IsClientView => Audience == DecisionAudience.Client;
}

/// <summary>
/// Two explanations from the exact same evaluated case. No new measurement,
/// network request, copy, file I/O, authorization or repair execution occurs.
/// Never treats observed improvement as proof of a successful intervention.
/// </summary>
public static class DecisionExplanationBuilder
{
    public static DecisionExplanation Build(
        DecisionCenterSummary summary, DecisionAudience audience)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (!Enum.IsDefined(audience))
            throw new ArgumentOutOfRangeException(nameof(audience));

        if (!Enum.IsDefined(summary.DiagnosticState)
            || !Enum.IsDefined(summary.VerificationState)
            || !Enum.IsDefined(summary.NextAction)
            || summary.DiagnosticRuleCount < 0
            || summary.PostcheckCount < 0
            || summary.DiagnosticRuleCount > 256
            || summary.PostcheckCount > 256)
            throw new ArgumentException(
                "The decision summary contains an invalid state.", nameof(summary));

        if (audience == DecisionAudience.Technician)
        {
            return new DecisionExplanation(
                audience,
                "Vista técnica · análisis orientativo",
                "Qué se pudo comprobar y qué requiere revisión",
                summary.DiagnosticText,
                summary.VerificationText,
                summary.NextStepText,
                $"Hipótesis evaluadas: {summary.DiagnosticRuleCount}",
                $"Comparaciones realizadas: {summary.PostcheckCount}",
                summary.SafetyNotice);
        }

        return new DecisionExplanation(
            audience,
            "Vista para el cliente · explicación ilustrativa",
            ClientHeadline(summary.DiagnosticState),
            ClientDiagnostic(summary.DiagnosticState),
            ClientVerification(summary.DiagnosticState, summary.VerificationState),
            ClientNextStep(summary.NextAction),
            string.Empty,
            string.Empty,
            "Esta explicación no garantiza que el equipo esté sano, no " +
            "certifica una reparación y no autoriza compartir datos. " +
            "En un caso real, el técnico debe revisar los resultados con vos.");
    }

    private static string ClientHeadline(DecisionDiagnosticState state) =>
        state switch
        {
            DecisionDiagnosticState.NotEvaluated =>
                "Todavía no contamos con un diagnóstico comprobado",
            DecisionDiagnosticState.ConsistentWithRule =>
                "Las mediciones disponibles coinciden con lo que se evaluó",
            DecisionDiagnosticState.RefutedRule =>
                "Una explicación posible no coincidió con las pruebas",
            DecisionDiagnosticState.IncompleteEvidence =>
                "Necesitamos completar algunas comprobaciones",
            DecisionDiagnosticState.ConflictingEvidence =>
                "Encontramos mediciones que debemos aclarar",
            _ => "No hay un diagnóstico confirmado"
        };

    private static string ClientDiagnostic(DecisionDiagnosticState state) =>
        state switch
        {
            DecisionDiagnosticState.NotEvaluated =>
                "Todavía no se realizaron comprobaciones suficientes para informar el estado del equipo.",
            DecisionDiagnosticState.ConsistentWithRule =>
                "Algunas observaciones coinciden con lo esperado para la prueba realizada. " +
                "Eso no garantiza el estado general del equipo ni identifica por sí solo una causa.",
            DecisionDiagnosticState.RefutedRule =>
                "Los datos disponibles no respaldan una de las posibles explicaciones. " +
                "Es necesario revisar otras alternativas antes de intervenir.",
            DecisionDiagnosticState.IncompleteEvidence =>
                "Faltan resultados o hay datos que no permiten una conclusión. " +
                "La ausencia de una medición no significa que esté todo bien.",
            DecisionDiagnosticState.ConflictingEvidence =>
                "Algunos resultados no coinciden entre sí. " +
                "Primero debemos aclarar esas diferencias.",
            _ => "Es necesario volver a revisar las mediciones."
        };

    private static string ClientVerification(
        DecisionDiagnosticState diagnosis, DecisionVerificationState verification)
    {
        return verification switch
        {
            DecisionVerificationState.NotMeasured =>
                "Todavía no contamos con una comparación confiable antes y después.",
            DecisionVerificationState.ObservedConditionImproved
                when diagnosis != DecisionDiagnosticState.ConsistentWithRule =>
                "Una medición tuvo una variación favorable, pero el resto de " +
                "las pruebas todavía no permite interpretar el resultado como resuelto.",
            DecisionVerificationState.ObservedConditionImproved =>
                "La medición posterior mejoró respecto de la inicial. " +
                "Eso no demuestra qué produjo el cambio ni garantiza que sea permanente.",
            DecisionVerificationState.UnresolvedCondition =>
                "En las mediciones disponibles todavía hay una condición " +
                "que no alcanzó el resultado esperado.",
            DecisionVerificationState.Indeterminate =>
                "La información disponible no permite comprobar si hubo una mejoría.",
            _ => "La verificación necesita revisión técnica."
        };
    }

    private static string ClientNextStep(DecisionNextAction action) =>
        action switch
        {
            DecisionNextAction.GatherInitialEvidence =>
                "Primero vamos a reunir mediciones confiables antes de decidir qué hacer.",
            DecisionNextAction.ResolveContradictions =>
                "Vamos a revisar las diferencias entre los resultados antes de proponer acciones.",
            DecisionNextAction.CompleteMissingMeasurements =>
                "Vamos a completar las pruebas necesarias para comprender mejor el problema.",
            DecisionNextAction.RevisitHypothesis =>
                "Vamos a reconsiderar la explicación inicial y revisar otras posibilidades.",
            DecisionNextAction.ReviewRepairPlan =>
                "El técnico podrá explicar las alternativas, los riesgos y los " +
                "respaldos recomendados antes de pedir tu autorización.",
            DecisionNextAction.RepeatPostcheck =>
                "Vamos a repetir la comprobación para obtener un resultado comparable.",
            DecisionNextAction.InvestigatePersistence =>
                "Vamos a revisar por qué sigue presente la condición observada.",
            DecisionNextAction.MonitorAndDocument =>
                "Conviene registrar lo observado y comprobar que se mantenga estable.",
            _ => "El técnico debe revisar los próximos pasos."
        };
}
