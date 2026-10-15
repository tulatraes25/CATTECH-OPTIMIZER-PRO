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
    private readonly Guid _baselineCaptureToken;
    private readonly Guid _currentCaptureToken;
    private readonly int _baselineIndex;

    internal SmartInventoryMatchPreview(int currentIndex, SmartInventoryMatchStatus status,
        DiagnosticSubjectId? previousSubjectId, int baselineIndex,
        Guid baselineCaptureToken, Guid currentCaptureToken)
    {
        CurrentIndex = currentIndex;
        Status = status;
        PreviousSubjectId = previousSubjectId;
        _baselineIndex = baselineIndex;
        _baselineCaptureToken = baselineCaptureToken;
        _currentCaptureToken = currentCaptureToken;
    }

    public int CurrentIndex { get; }
    public SmartInventoryMatchStatus Status { get; }
    [JsonIgnore]
    public DiagnosticSubjectId? PreviousSubjectId { get; }

    /// <summary>
    /// Advisory freshness check for the exact two captured inventories and
    /// baseline subject map. It does not authenticate a physical disk,
    /// authorize evidence, or constitute technician consent.
    /// </summary>
    public bool IsCurrentCandidateFor(
        SmartInventorySnapshot baseline, SmartInventorySnapshot current,
        IReadOnlyList<DiagnosticSubjectId> baselineSubjects,
        int selectedCurrentIndex)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(baselineSubjects);

        if (Status != SmartInventoryMatchStatus.CandidateRequiresReview
            || !PreviousSubjectId.HasValue || _baselineIndex < 0
            || selectedCurrentIndex != CurrentIndex
            || _baselineCaptureToken != baseline.CaptureToken
            || _currentCaptureToken != current.CaptureToken
            || baseline.ContextId != current.ContextId
            || _baselineIndex >= baseline.DeviceCount
            || CurrentIndex < 0 || CurrentIndex >= current.DeviceCount)
            return false;

        var subjects = baselineSubjects.ToArray();
        if (subjects.Length != baseline.DeviceCount
            || subjects.Any(id => !id.IsValid)
            || subjects.Distinct().Count() != subjects.Length)
            return false;

        return subjects[_baselineIndex] == PreviousSubjectId.Value;
    }
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
    private readonly string?[] _claimedSerials;

    private SmartInventorySnapshot(
        DiagnosticContextId contextId, DateTimeOffset capturedAtUtc,
        DiskIdentity?[] identities, string?[] claimedSerials)
    {
        ContextId = contextId;
        CapturedAtUtc = capturedAtUtc.ToUniversalTime();
        CaptureToken = Guid.NewGuid();
        _identities = identities;
        _claimedSerials = claimedSerials;
    }

    [JsonIgnore]
    public DiagnosticContextId ContextId { get; }
    public DateTimeOffset CapturedAtUtc { get; }
    public int DeviceCount => _identities.Length;
    // Local in-memory capture identity, not a hardware fingerprint.
    [JsonIgnore]
    internal Guid CaptureToken { get; }

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
            snapshot.Select(ExtractIdentity).ToArray(),
            snapshot.Select(ExtractClaimedSerial).ToArray());
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
        // Freeze the potentially mutable input subject map for this preview.
        var subjectSnapshot = baselineSubjects.ToArray();
        if (subjectSnapshot.Length != baseline.DeviceCount
            || subjectSnapshot.Any(id => !id.IsValid)
            || subjectSnapshot.Distinct().Count() != subjectSnapshot.Length)
            throw new ArgumentException(
                "Baseline subjects must be unique and bound one-to-one.",
                nameof(baselineSubjects));

        // Include any usable serial claim, even from a failed/incomplete report.
        // Otherwise an untrusted duplicate could hide behind a null identity
        // while a second record with that serial is matched as unique.
        var baselineSerialCounts = CountSerials(baseline._claimedSerials);
        var currentSerialCounts = CountSerials(current._claimedSerials);
        var previews = new SmartInventoryMatchPreview[current.DeviceCount];
        SmartInventoryMatchPreview ForCurrent(int index,
            SmartInventoryMatchStatus status,
            DiagnosticSubjectId? previousSubject = null, int baselineIndex = -1) =>
            new(index, status, previousSubject, baselineIndex,
                baseline.CaptureToken, current.CaptureToken);

        for (var i = 0; i < current._identities.Length; i++)
        {
            var identity = current._identities[i];
            if (identity is null)
            {
                previews[i] = ForCurrent(
                    i, SmartInventoryMatchStatus.UnverifiableIdentity, null);
                continue;
            }

            // A duplicated serial in either inventory destroys one-to-one
            // physical-device attribution, even if model/capacity differ.
            if (currentSerialCounts[identity.Serial] != 1
                || (baselineSerialCounts.TryGetValue(identity.Serial, out var count)
                    && count != 1))
            {
                previews[i] = ForCurrent(
                    i, SmartInventoryMatchStatus.AmbiguousSerial, null);
                continue;
            }

            var priorIndex = Array.FindIndex(baseline._identities,
                prior => prior is not null && prior.Serial == identity.Serial);
            if (priorIndex < 0)
            {
                previews[i] = ForCurrent(
                    i, SmartInventoryMatchStatus.NewDevice, null);
                continue;
            }

            if (baseline._identities[priorIndex] != identity)
            {
                previews[i] = ForCurrent(
                    i, SmartInventoryMatchStatus.ChangedIdentity, null);
                continue;
            }

            previews[i] = ForCurrent(
                i, SmartInventoryMatchStatus.CandidateRequiresReview,
                subjectSnapshot[priorIndex], priorIndex);
        }

        return Array.AsReadOnly(previews);
    }

    private static Dictionary<string, int> CountSerials(string?[] items)
    {
        return items.Where(serial => serial is not null)
            .Select(serial => serial!)
            .GroupBy(serial => serial, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.Ordinal);
    }

    private static DiskIdentity? ExtractIdentity(SmartDiskReport report)
    {
        if (!report.IsAnalysisSuccessful || string.IsNullOrWhiteSpace(report.Device))
            return null;

        var serial = ExtractClaimedSerial(report);
        var model = Normalize(report.ModelName);
        var transport = Normalize(
            string.IsNullOrWhiteSpace(report.SmartctlDeviceType)
                ? report.Protocol : report.SmartctlDeviceType);

        if (serial is null || model is null || transport is null
            || report.CapacityBytes <= 0)
            return null;

        return new DiskIdentity(serial, model, report.CapacityBytes, transport);
    }

    private static string? ExtractClaimedSerial(SmartDiskReport report)
    {
        // Deliberately do not rely on IsAnalysisSuccessful here: a failed
        // entry may still claim the same serial as a complete entry, making
        // the overall inventory ambiguous rather than one-to-one.
        var serial = Normalize(report.SerialNumber);
        if (serial is null
            or "UNKNOWN" or "UNKNOWN SERIAL" or "N/D" or "N/A" or "NONE"
            or "NULL" or "NOT AVAILABLE" or "NOT APPLICABLE"
            or "TO BE FILLED BY O.E.M." or "TO BE FILLED BY OEM"
            or "DEFAULT STRING" or "SYSTEM SERIAL NUMBER")
            return null;

        // Devices may report all-zero serials of varying lengths rather
        // than the two common placeholders. None are reliable identifiers.
        if (serial.All(ch => ch == '0'))
            return null;

        // Uniform dummy identifiers (e.g. FFFFFFFFFFFF or XXXXXXXXXXXX)
        // cannot justify continuity of a physical drive. Only reject longer
        // repeated patterns; a short real vendor serial may legitimately repeat.
        if (serial.Length >= 4 && serial.All(ch => ch == serial[0]))
            return null;

        return serial;
    }

    private static string? Normalize(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxFieldLength)
            return null;

        // SMART strings are untrusted firmware output. Control, Unicode
        // formatting/bidi and unpaired surrogate characters must not be used
        // as identity evidence, even when two reports happen to match.
        if (value.Any(ch => char.IsControl(ch) || char.IsSurrogate(ch)
            || char.GetUnicodeCategory(ch)
                == System.Globalization.UnicodeCategory.Format))
            return null;

        return value.ToUpperInvariant();
    }

    // Private by design: raw SMART identity is neither returned nor logged.
    private sealed record DiskIdentity(
        string Serial, string Model, long CapacityBytes, string Transport);
}
