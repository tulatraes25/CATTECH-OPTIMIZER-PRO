namespace Cattech.Optimizer.Pro.Core.Models.Evidence;

/// <summary>
/// Whether a measurement is present. Missing evidence is never a healthy result.
/// </summary>
public enum EvidenceAvailability
{
    Observed,
    Unavailable,
    AccessDenied,
    Unsupported,
    InvalidData
}

public enum EvidencePrivacyClass
{
    Public,
    Equipment,
    Sensitive
}

public enum EvidenceValueKind
{
    Text,
    Number,
    Boolean
}

/// <summary>
/// A small, immutable typed fact value. No inferred values for unavailable sources.
/// </summary>
public sealed record EvidenceValue
{
    private EvidenceValue(EvidenceValueKind kind, string? text, decimal? number, bool? boolean)
    {
        Kind = kind;
        Text = text;
        Number = number;
        Boolean = boolean;
    }

    public EvidenceValueKind Kind { get; }
    public string? Text { get; }
    public decimal? Number { get; }
    public bool? Boolean { get; }

    public static EvidenceValue FromText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("An observed text value cannot be blank.", nameof(value));

        return new EvidenceValue(EvidenceValueKind.Text, value, null, null);
    }

    public static EvidenceValue FromNumber(decimal value) =>
        new(EvidenceValueKind.Number, null, value, null);

    public static EvidenceValue FromBoolean(bool value) =>
        new(EvidenceValueKind.Boolean, null, null, value);
}

/// <summary>
/// One observation with explicit provenance, availability and privacy classification.
/// This is a data model only: no I/O, collection, repair or automatic export.
/// </summary>
public sealed record EvidenceRecord
{
    private EvidenceRecord(
        Guid id,
        string sourceId,
        string factKey,
        DateTimeOffset observedAtUtc,
        EvidenceAvailability availability,
        EvidenceValue? value,
        string provenance,
        EvidencePrivacyClass privacyClass,
        string limitations,
        DiagnosticContextId contextId)
    {
        ContextId = contextId;
        Id = id;
        SourceId = sourceId;
        FactKey = factKey;
        ObservedAtUtc = observedAtUtc.ToUniversalTime();
        Availability = availability;
        Value = value;
        Provenance = provenance;
        PrivacyClass = privacyClass;
        Limitations = limitations;
    }

    /// <summary>
    /// The diagnostic session, not a serial number or hardware fingerprint.
    /// Unbound legacy records are inert for correlation and public export.
    /// </summary>
    public DiagnosticContextId ContextId { get; }

    public Guid Id { get; }
    public string SourceId { get; }
    public string FactKey { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public EvidenceAvailability Availability { get; }
    public EvidenceValue? Value { get; }
    public string Provenance { get; }
    public EvidencePrivacyClass PrivacyClass { get; }
    public string Limitations { get; }

    public static EvidenceRecord Observed(
        Guid id, string sourceId, string factKey, DateTimeOffset observedAt,
        EvidenceValue value, string provenance, EvidencePrivacyClass privacyClass,
        string limitations = "", DiagnosticContextId contextId = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        Validate(id, sourceId, factKey, provenance, privacyClass, limitations);
        return new EvidenceRecord(id, sourceId, factKey, observedAt,
            EvidenceAvailability.Observed, value, provenance, privacyClass,
            limitations, contextId);
    }

    public static EvidenceRecord Missing(
        Guid id, string sourceId, string factKey, DateTimeOffset observedAt,
        EvidenceAvailability reason, string provenance,
        EvidencePrivacyClass privacyClass, string limitations,
        DiagnosticContextId contextId = default)
    {
        if (reason == EvidenceAvailability.Observed
            || !Enum.IsDefined(reason))
            throw new ArgumentOutOfRangeException(nameof(reason),
                "Missing observations require a known non-observed status.");

        Validate(id, sourceId, factKey, provenance, privacyClass, limitations);
        if (string.IsNullOrWhiteSpace(limitations))
            throw new ArgumentException("Unavailable evidence requires an explanation.",
                nameof(limitations));

        return new EvidenceRecord(id, sourceId, factKey, observedAt,
            reason, null, provenance, privacyClass, limitations, contextId);
    }

    private static void Validate(
        Guid id, string sourceId, string factKey, string provenance,
        EvidencePrivacyClass privacyClass, string limitations)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Evidence must have a stable non-empty ID.", nameof(id));
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("Source is required.", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(factKey))
            throw new ArgumentException("Fact key is required.", nameof(factKey));
        if (string.IsNullOrWhiteSpace(provenance))
            throw new ArgumentException("Provenance is required.", nameof(provenance));
        if (!Enum.IsDefined(privacyClass))
            throw new ArgumentOutOfRangeException(nameof(privacyClass));
        ArgumentNullException.ThrowIfNull(limitations);
    }
}
