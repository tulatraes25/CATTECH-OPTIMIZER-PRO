namespace Cattech.Optimizer.Pro.Core.Models.Reports;

/// <summary>
/// Necessary but not sufficient identity match for case-linked snapshots.
/// The association is NOT authenticated merely because two GUIDs are equal.
/// Stored provenance and source integrity must be verified before export.
/// </summary>
public static class ReportSourceCasePolicy
{
    public static bool HasMatchingExplicitCase(Guid? selectedCaseId, Guid? sourceCaseId)
        => selectedCaseId is { } current && current != Guid.Empty
            && sourceCaseId is { } source && source != Guid.Empty
            && current == source;

    /// <summary>
    /// Source attribution is a hard boundary for named-client exports.
    /// This checks a necessary case-link condition, not cryptographic
    /// attestation. A separate audited association writer remains required.
    /// Returns only controlled section labels, never customer/device values.
    /// </summary>
    public static string? FindUnassociatedClientSource(ReportGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IncludeClient || options.ServiceReport is null)
            return null;
        // SPEC #92 P0 interim containment: a mutable ServiceCaseId GUID,
        // including an equal GUID, is not authenticated source provenance.
        // Independent measurements cannot be attributed to a named client
        // until C2 verifies an audited binding to immutable source bytes.
        // Historical files and client-free standalone exports remain readable.
        if (options.IncludeDiagnostic && options.DiagnosticReport is not null)
            return "Diagnóstico";
        if (options.IncludeStartup && options.StartupAnalysis is not null)
            return "Inicio";
        if (options.IncludeSmart && options.SmartAnalysis is not null)
            return "SMART";
        if (options.IncludeSmartTests && options.SmartTestSessions.Count > 0)
            return "SMART Self-Tests";

        // History-only cleanup/restore and the current visual optimization
        // object have no service-case provenance contract. They must not be
        // attributed to a named customer's device by direct service callers.
        if (options.IncludeCleanup && options.CleanupResult is not null)
            return "Limpieza";
        if (options.IncludeRestorePoint && options.RestorePointResult is not null)
            return "Restauración";
        if (options.IncludeVisualOptimization && options.VisualOptimizationResult is not null)
            return "Optimización";
        return null;
    }
}
