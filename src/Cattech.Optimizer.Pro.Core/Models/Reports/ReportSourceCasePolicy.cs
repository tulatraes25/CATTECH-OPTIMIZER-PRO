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
}
