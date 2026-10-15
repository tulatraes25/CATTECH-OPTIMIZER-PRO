using System.Text.Json.Serialization;
using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.Core.Models.Evidence;

/// <summary>
/// Preview states are advisory only: an exact candidate never means the same
/// physical disk has been proven or automatically authorized for comparison.
/// </summary>
public enum SmartInventoryMatchStatus
{
    CandidateRequiresReview,
    NewDevice,
    UnverifiableIdentity,
    AmbiguousSerial,
    ChangedIdentity
}

/// <summary>
/// Privacy-minimized comparison result. No serial, path, model or raw SMART value
/// is copied into the result. A candidate ID is NOT an approved binding.
/// </summary>
public sealed class SmartInventoryMatchPreview
{
    internal SmartInventoryMatchPreview(int currentIndex, SmartInventoryMatchStatus status,
        DiagnosticSubjectId? previousSubjectId)
    {
        CurrentIndex = currentIndex;
        Status = status;
        PreviousSubjectId = previousSubjectId;
    }

    public int CurrentIndex { get; }
    public SmartInventoryMatchStatus Status { get; }
    [JsonIgnore]
    public DiagnosticSubjectId? PreviousSubjectId { get; }
}

/// <summary>
/// In-memory snapshot of identifiers from existing SMART reports. Raw identity
/// strings remain private and are never emitted from the comparison results.
/// Capture must be invoked only for a selected, verified service context.
/// </summary>
public sealed class SmartInventorySnapshot
{
    private const int MaxDisks = 128;
    private const int MaxFieldLength = 256;

    private readonly DiskIdentity?[] _identities;

    private SmartInventorySnapshot(
        DiagnosticContextId contextId, DateTimeOffset capturedAtUtc,
        DiskIdentity?[] identities)
    {
        ContextId = contextId;
        CapturedAtUtc = capturedAtUtc.ToUniversalTime();
        _identities = identities;
    }

    [JsonIgnore]
    public DiagnosticContextId ContextId { get; }
    public DateTimeOffset CapturedAtUtc { get; }
    public int DeviceCount => _identities.Length;

    public static SmartInventorySnapshot Capture(
        DiagnosticContextId contextId, DateTimeOffset capturedAt,
        IEnumerable<SmartDiskReport> reports)
    {
        if (!contextId.IsValid)
            throw new ArgumentException("A known equipment service context is required.",
                nameof(contextId));
        ArgumentNullException.ThrowIfNull(reports);

        var snapshot = reports.Take(MaxDisks + 1).ToArray();
        if (snapshot.Length > MaxDisks || snapshot.Any(report => report is null))
            throw new ArgumentException("Invalid or oversized disk inventory.",
                nameof(reports));

        // Snapshot source models are mutable; extract just the identity fields
        // now so caller edits cannot silently change a preview after review.
        return new SmartInventorySnapshot(contextId, capturedAt,
            snapshot.Select(ExtractIdentity).ToArray());
    }

    /// <summary>
    /// Calculate only candidates for a human equipment/drive identity review.
    /// The method never returns an approved binding nor creates evidence.
    /// </summary>
    public static IReadOnlyList<SmartInventoryMatchPreview> Preview(
        SmartInventorySnapshot baseline, SmartInventorySnapshot current,
        IReadOnlyList<DiagnosticSubjectId> baselineSubjects,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(baselineSubjects);

        if (baseline.ContextId != current.ContextId)
            throw new ArgumentException("Inventories belong to different service cases.",
                nameof(current));
        if (baseline.CapturedAtUtc >= current.CapturedAtUtc
            || current.CapturedAtUtc > evaluatedAt.ToUniversalTime())
            throw new ArgumentException("Inventory chronology requires review.",
                nameof(current));
        if (baselineSubjects.Count != baseline.DeviceCount
            || baselineSubjects.Any(id => !id.IsValid)
            || baselineSubjects.Distinct().Count() != baselineSubjects.Count)
            throw new ArgumentException(
                "Baseline subjects must be unique and bound one-to-one.",
                nameof(baselineSubjects));

        var baselineSerialCounts = CountSerials(baseline._identities);
        var currentSerialCounts = CountSerials(current._identities);
        var previews = new SmartInventoryMatchPreview[current.DeviceCount];

        for (var i = 0; i < current._identities.Length; i++)
        {
            var identity = current._identities[i];
            if (identity is null)
            {
                previews[i] = new SmartInventoryMatchPreview(
                    i, SmartInventoryMatchStatus.UnverifiableIdentity, null);
                continue;
            }

            // A duplicated serial in either inventory destroys one-to-one
            // physical-device attribution, even if model/capacity differ.
            if (currentSerialCounts[identity.Serial] != 1
                || (baselineSerialCounts.TryGetValue(identity.Serial, out var count)
                    && count != 1))
            {
                previews[i] = new SmartInventoryMatchPreview(
                    i, SmartInventoryMatchStatus.AmbiguousSerial, null);
                continue;
            }

            var priorIndex = Array.FindIndex(baseline._identities,
                prior => prior is not null && prior.Serial == identity.Serial);
            if (priorIndex < 0)
            {
                previews[i] = new SmartInventoryMatchPreview(
                    i, SmartInventoryMatchStatus.NewDevice, null);
                continue;
            }

            if (baseline._identities[priorIndex] != identity)
            {
                previews[i] = new SmartInventoryMatchPreview(
                    i, SmartInventoryMatchStatus.ChangedIdentity, null);
                continue;
            }

            previews[i] = new SmartInventoryMatchPreview(
                i, SmartInventoryMatchStatus.CandidateRequiresReview,
                baselineSubjects[priorIndex]);
        }

        return Array.AsReadOnly(previews);
    }

    private static Dictionary<string, int> CountSerials(DiskIdentity?[] items)
    {
        return items.Where(identity => identity is not null)
            .Select(identity => identity!.Serial)
            .GroupBy(serial => serial, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.Ordinal);
    }

    private static DiskIdentity? ExtractIdentity(SmartDiskReport report)
    {
        if (!report.IsAnalysisSuccessful || string.IsNullOrWhiteSpace(report.Device))
            return null;

        var serial = Normalize(report.SerialNumber);
        var model = Normalize(report.ModelName);
        var transport = Normalize(
            string.IsNullOrWhiteSpace(report.SmartctlDeviceType)
                ? report.Protocol : report.SmartctlDeviceType);

        if (serial is null || model is null || transport is null
            || report.CapacityBytes <= 0
            || serial is "UNKNOWN" or "N/D" or "N/A" or "NONE" or "NULL"
                or "NOT AVAILABLE" or "TO BE FILLED BY O.E.M."
                or "00000000" or "000000000000")
            return null;

        return new DiskIdentity(serial, model, report.CapacityBytes, transport);
    }

    private static string? Normalize(string? raw)
    {
        var value = raw?.Trim();
        return string.IsNullOrWhiteSpace(value) || value.Length > MaxFieldLength
            ? null : value.ToUpperInvariant();
    }

    // Private by design: raw SMART identity is neither returned nor logged.
    private sealed record DiskIdentity(
        string Serial, string Model, long CapacityBytes, string Transport);
}
