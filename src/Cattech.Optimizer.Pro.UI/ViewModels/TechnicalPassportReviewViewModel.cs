using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// EE-05B: explicit human review of one immutable public export snapshot.
/// Clipboard output is injected and happens only through the authorized command.
/// A new passport requires a new view model and fresh acknowledgments.
/// This is a reusable UI component; it is not wired to live diagnostics yet.
/// </summary>
public partial class TechnicalPassportReviewViewModel : ObservableObject
{
    private readonly TechnicalPassportArtifact _snapshot;
    private readonly Action<string> _writeClipboard;
    private bool _consumed;

    public TechnicalPassportReviewViewModel(
        TechnicalPassportArtifact snapshot, Action<string> writeClipboard)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _writeClipboard = writeClipboard ??
            throw new ArgumentNullException(nameof(writeClipboard));

        if (!_snapshot.HasMatchingDigest())
            throw new ArgumentException("The preview checksum did not match.",
                nameof(snapshot));

        PreviewJson = _snapshot.PayloadJson;
        PreviewSha256 = _snapshot.Sha256Hex;
    }

    public string PreviewJson { get; }
    public string PreviewSha256 { get; }
    public bool HasCopiedToClipboard => _consumed;

    public string PrivacyNotice =>
        "Los recuentos pueden revelar el estado del equipo. El portapapeles " +
        "puede ser leído por otras aplicaciones. SHA-256 no es una firma digital.";

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

    private bool CanCopyApprovedPayload() =>
        !_consumed
        && HasReviewedPreview
        && ConsentsToClipboard
        && _snapshot.HasMatchingDigest();

    [RelayCommand(CanExecute = nameof(CanCopyApprovedPayload))]
    private void CopyApprovedPayload()
    {
        // Recheck on invocation, not just button binding.
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

        // One-time approval for the exact displayed snapshot.
        // Never store user confirmation for the next passport.
        _consumed = true;
        OnPropertyChanged(nameof(HasCopiedToClipboard));
        HasReviewedPreview = false;
        ConsentsToClipboard = false;
        CopyApprovedPayloadCommand.NotifyCanExecuteChanged();
        StatusMessage = "JSON copiado al portapapeles con autorización. " +
            "No se envió a Internet ni se guardó un archivo.";
    }
}
