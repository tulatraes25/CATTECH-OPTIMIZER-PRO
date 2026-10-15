using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// Preview-only clipboard consent bound to one immutable passport and the
/// currently selected diagnostic context. NOT wired to production navigation.
/// The owning session coordinator must call InvalidateForCaseSelectionChange
/// on every equipment/client/session change; the provider check also runs
/// immediately before copying. No actual clipboard service is created here.
/// </summary>
public partial class TechnicalPassportReviewViewModel : ObservableObject
{
    private readonly TechnicalPassportArtifact _snapshot;
    private readonly Action<string> _writeClipboard;
    private readonly Func<DiagnosticContextId> _activeContext;
    private bool _consumed;
    private bool _revoked;
    private bool _copyInProgress;

    public TechnicalPassportReviewViewModel(
        TechnicalPassportArtifact snapshot, Action<string> writeClipboard,
        Func<DiagnosticContextId> activeContext)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _writeClipboard = writeClipboard ??
            throw new ArgumentNullException(nameof(writeClipboard));
        _activeContext = activeContext ??
            throw new ArgumentNullException(nameof(activeContext));

        if (!_snapshot.ContextId.IsValid || !_snapshot.HasMatchingDigest())
            throw new ArgumentException(
                "A bound, intact passport snapshot is required.",
                nameof(snapshot));

        PreviewJson = _snapshot.PayloadJson;
        PreviewSha256 = _snapshot.Sha256Hex;

        if (!IsActiveScope())
            InvalidateForCaseSelectionChange();
    }

    public string PreviewJson { get; }
    public string PreviewSha256 { get; }
    public bool HasCopiedToClipboard => _consumed;
    public bool IsConsentRevoked => _revoked;

    public string PrivacyNotice =>
        "Los recuentos pueden revelar el estado del equipo. El portapapeles " +
        "puede ser leído por otras aplicaciones y no se borra automáticamente " +
        "al cambiar de sesión. SHA-256 no es una firma digital. " +
        "Cambiar de equipo, cliente o sesión revoca esta autorización.";

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
            return _snapshot.ContextId.IsValid
                && _activeContext() == _snapshot.ContextId;
        }
        catch (Exception)
        {
            // A failing, missing or revoked coordinator never authorizes data.
            return false;
        }
    }

    /// <summary>
    /// Must be invoked on ANY real selection change, even when the old case
    /// is selected again later. Revocation cannot be undone by checking boxes.
    /// This never clears clipboard data previously written by the OS.
    /// </summary>
    public void InvalidateForCaseSelectionChange()
    {
        _revoked = true;
        HasReviewedPreview = false;
        ConsentsToClipboard = false;
        CopyApprovedPayloadCommand.NotifyCanExecuteChanged();
        StatusMessage = "Cambió el equipo, cliente o sesión. La autorización " +
            "caducó: generá una vista previa nueva para el contexto actual.";
    }

    private bool CanCopyApprovedPayload() =>
        !_consumed
        && !_revoked
        && !_copyInProgress
        && HasReviewedPreview
        && ConsentsToClipboard
        && IsActiveScope()
        && _snapshot.HasMatchingDigest();

    [RelayCommand(CanExecute = nameof(CanCopyApprovedPayload))]
    private void CopyApprovedPayload()
    {
        if (!IsActiveScope())
        {
            InvalidateForCaseSelectionChange();
            return;
        }

        // Also enforce guards if a caller invokes the command directly
        // without respecting button CanExecute.
        if (!CanCopyApprovedPayload())
            return;

        _copyInProgress = true;
        CopyApprovedPayloadCommand.NotifyCanExecuteChanged();
        try
        {
            _writeClipboard(_snapshot.PayloadJson);

            // Once an external write occurred, the consent is spent even if
            // navigation changed during a re-entrant clipboard callback.
            _consumed = true;
            OnPropertyChanged(nameof(HasCopiedToClipboard));

            if (_revoked || !IsActiveScope())
            {
                InvalidateForCaseSelectionChange();
                StatusMessage = "La sesión cambió durante la copia. " +
                    "El portapapeles podría contener el JSON anterior; " +
                    "revisalo y borralo manualmente si corresponde.";
                return;
            }

            HasReviewedPreview = false;
            ConsentsToClipboard = false;
            StatusMessage = "JSON copiado con autorización para el caso activo. " +
                "No se envió a Internet ni se guardó ningún archivo.";
        }
        catch (Exception ex) when (
            ex is ExternalException or InvalidOperationException)
        {
            // An external clipboard writer can throw after changing the OS
            // clipboard; retrying with old consent would be unsafe.
            InvalidateForCaseSelectionChange();
            StatusMessage = "No se pudo confirmar la copia al portapapeles. " +
                "Es posible que su contenido haya cambiado; revisalo. " +
                "Para reintentar necesitás una vista previa nueva.";
        }
        finally
        {
            _copyInProgress = false;
            CopyApprovedPayloadCommand.NotifyCanExecuteChanged();
        }
    }
}
