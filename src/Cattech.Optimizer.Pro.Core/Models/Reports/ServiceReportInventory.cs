namespace Cattech.Optimizer.Pro.Core.Models.Reports;

/// <summary>
/// Immutable, privacy-minimized snapshot of report-store health.
/// Not a file identity guarantee, consent, or authorization for read/delete.
/// </summary>
public sealed record ServiceReportInventory(
    int TotalFiles,
    int UniqueEntries,
    int AmbiguousEntries,
    int InvalidEntries,
    bool ScanFailed)
{
    public bool RequiresReview => ScanFailed || AmbiguousEntries > 0 || InvalidEntries > 0;
}
