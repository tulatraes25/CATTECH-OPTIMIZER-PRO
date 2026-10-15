using System.Windows;
using System.Windows.Controls;
using Cattech.Optimizer.Pro.Core.Models.Evidence;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.UI.Views;

/// <summary>
/// EE-05B reusable review surface. It accepts only an already-built snapshot.
/// Not registered in MainViewModel navigation until a validated live evidence
/// provider is available; never substitutes a guessed customer/equipment case.
/// </summary>
public partial class TechnicalPassportReviewView : UserControl
{
    public TechnicalPassportReviewView(
        TechnicalPassportArtifact passport,
        DiagnosticSessionLease selectedCaseLease)
    {
        ArgumentNullException.ThrowIfNull(selectedCaseLease);
        InitializeComponent();
        DataContext = new TechnicalPassportReviewViewModel(
            passport, payload => Clipboard.SetText(payload),
            selectedCaseLease);

        // Navigating away must not leave old previews/consents subscribed
        // to the coordinator, even if the same WPF object is later reused.
        Unloaded += (_, _) =>
            (DataContext as IDisposable)?.Dispose();
    }
}
