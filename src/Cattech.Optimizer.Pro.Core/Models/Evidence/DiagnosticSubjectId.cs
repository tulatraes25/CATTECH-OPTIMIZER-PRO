namespace Cattech.Optimizer.Pro.Core.Models.Evidence;

/// <summary>
/// Opaque identity of a host or individual component INSIDE an equipment service
/// context. Must be assigned by a trusted inventory adapter; it is not a disk
/// serial, MAC address, physical fingerprint or hardware authenticity proof.
/// </summary>
public readonly record struct DiagnosticSubjectId
{
    public DiagnosticSubjectId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("A diagnostic subject cannot be empty.",
                nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public bool IsValid => Value != Guid.Empty;

    public static DiagnosticSubjectId NewSubject() => new(Guid.NewGuid());
}
