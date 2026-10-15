namespace Cattech.Optimizer.Pro.Core.Models.Evidence;

/// <summary>
/// Opaque identifier for one technician-selected equipment service session.
/// Never derived from computer name, serial number, MAC/IP, or patient/client data.
/// The owning application must bind this session to the correct equipment; this
/// value alone does not authenticate the source of an observation.
/// </summary>
public readonly record struct DiagnosticContextId
{
    public DiagnosticContextId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("A diagnostic context cannot be empty.",
                nameof(value));
        Value = value;
    }

    public Guid Value { get; }

    public bool IsValid => Value != Guid.Empty;

    public static DiagnosticContextId NewSession() => new(Guid.NewGuid());
}
