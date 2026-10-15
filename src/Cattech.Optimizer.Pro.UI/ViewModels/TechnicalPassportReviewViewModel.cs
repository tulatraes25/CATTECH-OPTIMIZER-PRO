using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// Read-only review of a frozen public passport. Copy authorization must be
/// bound to an active, explicitly selected equipment/service session.
/// Not wired to live application navigation or hardware acquisition yet.
/// </summary>
public partial class TechnicalPassportReviewViewModel : ObservableObject
{
    private readonly TechnicalPassportArtifact _snapshot;
    private readonly Action<string> _writeClipboard;
    private readonly Func<DiagnosticContextId> _activeContext;
    private bool _consumed;
    private bool _revoked;

    public TechnicalPassportReviewViewModel(
        TechnicalPassportArtifact snapshot,
        Action<string> writeClipboard,
        Func<DiagnosticContextId> activeContext)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _writeClipboard = writeClipboard ??
            throw new ArgumentNullException(nameof(writeClipboard));
        _activeContext = activeContext ??
            throw new ArgumentNullException(nameof(activeContext));

        if (!_snapshot.ContextId.IsBound || !_snapshot.HasMatchingDigest())
            throw new ArgumentException("An unbound or corrupted preview is forbidden.",
                nameof(snapshot));

        PreviewJson = _snapshot.PayloadJson;
        PreviewSha256 = _snapshot.Sha256Hex;

        if (!IsActiveScope())
            InvalidateForCaseSelectionChange();
    }

    public string PreviewJson { get; }
    public string PreviewSha256 { get; }
    public bool HasCopiedToClipboard => _consumed;

    public string PrivacyNotice =>
        "Los recuentos pueden revelar el estado del equipo. El portapapeles " +
        "puede ser leído por otras aplicaciones. SHA-256 no es una firma digital. " +
        "Cambiar de equipo o sesión revoca esta autorización.";

    [ObservableProperty]
    private bool _hasReviewedPreview;

    [ObservableProperty]
    private bool _consentsToClipboard;

    [ObservableProperty]
    private string _statusMessage =
        "Sin autorización: revisá los datos y confirmá antes de copiar.";

    partial void OnHasReviewedPreviewChanged(bool value) =>
        CopyApprovedPayloadCommand.NotifyCanExecuteChanged();

    partial void OnConsentsToClipboardChanged(bool value) =>
        CopyApprovedPayloadCommand.NotifyCanExecuteChanged();

    private bool IsActiveScope()
    {
        try
        {
            // A trusted session coordinator must supply the currently selected
            // context. A provider failure always denies clipboard disclosure.
            return _snapshot.ContextId.IsBound
                && _activeContext() == _snapshot.ContextId;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// The future navigation/controller layer MUST invoke this on EVERY
    /// equipment, customer or diagnostic-session selection change.
    /// Once revoked, returning to an earlier session does not restore consent.
    /// </summary>
    public void InvalidateForCaseSelectionChange()
    {
        _revoked = true;
        HasReviewedPreview = false;
        ConsentsToClipboard = false;
        CopyApprovedPayloadCommand.NotifyCanExecuteChanged();
        StatusMessage = "La sesión o el equipo cambió. Esta autorización " +
            "caducó; generá una nueva vista previa para el contexto actual.";
    }

    private bool CanCopyApprovedPayload() =>
        !_consumed
        && !_revoked
        && HasReviewedPreview
        && ConsentsToClipboard
        && IsActiveScope()
        && _snapshot.HasMatchingDigest();

    [RelayCommand(CanExecute = nameof(CanCopyApprovedPayload))]
    private void CopyApprovedPayload()
    {
        // Scope is re-evaluated at invocation; a cached enabled button cannot
        // authorize data from a previously selected equipment session.
        if (!IsActiveScope())
        {
            InvalidateForCaseSelectionChange();
            return;
        }

        if (!CanCopyApprovedPayload())
            return;

        try
        {
            _writeClipboard(_snapshot.PayloadJson);
        }
        catch (Exception ex) when (
            ex is ExternalException or InvalidOperationException)
        {
            StatusMessage = "No se pudo copiar al portapapeles. No se realizó " +
                "ninguna exportación. Podés volver a intentarlo.";
            return;
        }

        // Clipboard interactions can be re-entrant. If the active session
        // changed during the external callback, do not claim safe completion.
        // The OS clipboard may already contain the payload; never claim an
        // automatic rollback of external state.
        if (!IsActiveScope())
        {
            _consumed = true;
            OnPropertyChanged(nameof(HasCopiedToClipboard));
            InvalidateForCaseSelectionChange();
            StatusMessage = "Se detectó un cambio de sesión durante la copia. " +
                "El portapapeles podría contener el JSON anterior; revisalo " +
                "y borralo manualmente si corresponde.";
            return;
        }

        _consumed = true;
        OnPropertyChanged(nameof(HasCopiedToClipboard));
        HasReviewedPreview = false;
        ConsentsToClipboard = false;
        CopyApprovedPayloadCommand.NotifyCanExecuteChanged();
        StatusMessage = "JSON copiado al portapapeles con autorización. " +
            "No se envió a Internet ni se guardó un archivo.";
    }
}
