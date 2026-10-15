using System.Text.Json.Serialization;

namespace Cattech.Optimizer.Pro.Core.Models.Evidence;

/// <summary>
/// An in-memory selection authority for one technician-selected service case.
/// Its GUIDs are NOT hardware fingerprints, customer identifiers or proof of
/// physical identity. A trusted application adapter must establish the case.
/// </summary>
public sealed class DiagnosticSessionCoordinator
{
    private readonly object _gate = new();
    private DiagnosticContextId? _selectedCase;
    private Guid _selectionEpoch = Guid.NewGuid();

    /// <summary>
    /// Raised without case IDs after every selection operation, including
    /// choosing the same case again. A failed listener must not suppress
    /// notification of other listeners or roll back the selection.
    /// The owner of a WPF window must raise selections on its UI dispatcher.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Open an entirely new logical service session; no real hardware
    /// identity is inferred from this operation.
    /// </summary>
    public DiagnosticContextId StartNewCase()
    {
        var id = DiagnosticContextId.NewSession();
        SelectCase(id);
        return id;
    }

    /// <summary>
    /// Select an EXISTING case already validated by the owning application.
    /// Re-selection always produces a new epoch: A to B to A, or A to A,
    /// can never reactivate a lease issued before the transition.
    /// </summary>
    public void SelectCase(DiagnosticContextId caseId)
    {
        if (!caseId.IsValid)
            throw new ArgumentException("An explicit valid case is required.",
                nameof(caseId));

        lock (_gate)
        {
            _selectedCase = caseId;
            _selectionEpoch = Guid.NewGuid();
        }

        BroadcastChange();
    }

    /// <summary>
    /// Any deselection also revokes every previously captured lease.
    /// </summary>
    public void ClearSelection()
    {
        lock (_gate)
        {
            _selectedCase = null;
            _selectionEpoch = Guid.NewGuid();
        }

        BroadcastChange();
    }

    /// <summary>
    /// Capture permission to REVIEW ONLY the currently selected logical
    /// case. Returns null for an unselected or different case. The lease is
    /// local-only and never authorizes copying, reporting or repairing.
    /// </summary>
    public DiagnosticSessionLease? CaptureLeaseFor(DiagnosticContextId caseId)
    {
        if (!caseId.IsValid)
            return null;

        lock (_gate)
        {
            return _selectedCase == caseId
                ? new DiagnosticSessionLease(this, caseId, _selectionEpoch)
                : null;
        }
    }

    internal bool IsCurrent(DiagnosticSessionLease lease)
    {
        lock (_gate)
        {
            return ReferenceEquals(lease.Owner, this)
                && _selectedCase == lease.ContextId
                && _selectionEpoch == lease.Epoch;
        }
    }

    private void BroadcastChange()
    {
        var handlers = SelectionChanged;
        if (handlers is null)
            return;

        // A faulty observer must not mask the revocation from subsequent
        // observers. The epoch already changed atomically under the lock.
        foreach (EventHandler callback in handlers.GetInvocationList())
        {
            try
            {
                callback(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // Fail-safe observer isolation. A lease's IsCurrent is the
                // security gate regardless of listener delivery.
            }
        }
    }
}

/// <summary>
/// Unforgeable-by-public-construction local capability bound to one
/// coordinator, case and selection epoch. It is NOT a physical attestation
/// or a signature. No case IDs or epochs are serialized by default.
/// </summary>
public sealed class DiagnosticSessionLease
{
    private readonly DiagnosticSessionCoordinator _owner;
    private readonly Guid _epoch;

    internal DiagnosticSessionLease(
        DiagnosticSessionCoordinator owner, DiagnosticContextId contextId,
        Guid epoch)
    {
        _owner = owner;
        ContextId = contextId;
        _epoch = epoch;
    }

    [JsonIgnore]
    public DiagnosticContextId ContextId { get; }

    [JsonIgnore]
    public bool IsCurrent => _owner.IsCurrent(this);

    /// <summary>
    /// Subscribe to this particular lease becoming stale, without exposing
    /// the mutating coordinator to an arbitrary review UI. Register BEFORE
    /// inspecting IsCurrent, so an intervening selection cannot be missed.
    /// The callback runs on the thread changing the selection.
    /// </summary>
    public IDisposable SubscribeRevocation(Action onRevoked)
    {
        ArgumentNullException.ThrowIfNull(onRevoked);
        EventHandler handler = (_, _) =>
        {
            if (!IsCurrent)
                onRevoked();
        };

        _owner.SelectionChanged += handler;
        try
        {
            // Cover the transition between initial lease capture and
            // subscription registration.
            if (!IsCurrent)
                onRevoked();
        }
        catch
        {
            _owner.SelectionChanged -= handler;
            throw;
        }

        return new LeaseSubscription(_owner, handler);
    }

    internal DiagnosticSessionCoordinator Owner => _owner;
    internal Guid Epoch => _epoch;

    private sealed class LeaseSubscription : IDisposable
    {
        private DiagnosticSessionCoordinator? _source;
        private readonly EventHandler _handler;

        public LeaseSubscription(
            DiagnosticSessionCoordinator source, EventHandler handler)
        {
            _source = source;
            _handler = handler;
        }

        public void Dispose()
        {
            var coordinator =
                System.Threading.Interlocked.Exchange(ref _source, null);
            if (coordinator is not null)
                coordinator.SelectionChanged -= _handler;
        }
    }
}
