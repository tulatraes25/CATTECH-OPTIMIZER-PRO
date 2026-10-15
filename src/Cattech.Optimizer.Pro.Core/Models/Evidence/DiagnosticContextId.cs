namespace Cattech.Optimizer.Pro.Core.Models.Evidence;

/// <summary>
/// Opaque identity of one explicitly selected equipment/service diagnostic session.
/// It is not a hardware fingerprint, serial, MAC, hostname or proof of physical identity.
/// Never put this value in the public Technical Passport JSON.
/// </summary>
public readonly record struct DiagnosticContextId
{
    private DiagnosticContextId(Guid value) => Value = value;

    public Guid Value { get; }

    // default(DiagnosticContextId) is a deliberately unbound legacy value.
    // It must never be treated as permission to combine observations.
    public bool IsBound => Value != Guid.Empty;

    public static DiagnosticContextId NewSession() => new(Guid.NewGuid());

    public static DiagnosticContextId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("A bound diagnostic context needs a non-empty ID.",
                nameof(value));

        return new DiagnosticContextId(value);
    }
}
