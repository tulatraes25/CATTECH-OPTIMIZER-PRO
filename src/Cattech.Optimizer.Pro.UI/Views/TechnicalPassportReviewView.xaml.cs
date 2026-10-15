using System.Windows;
using System.Windows.Controls;
using Cattech.Optimizer.Pro.Core.Models.Evidence;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.UI.Views;

/// <summary>
/// EE-05B reusable review surface. It accepts only an already-built snapshot.
/// Not registered in MainViewModel navigation until a validated live evidence
/// provider is available; never substitutes synthetic or empty patient data.
/// </summary>
public partial class TechnicalPassportReviewView : UserControl
{
    public TechnicalPassportReviewView(
        TechnicalPassportArtifact passport,
        Func<DiagnosticContextId> activeContext)
    {
        ArgumentNullException.ThrowIfNull(activeContext);
        InitializeComponent();
        DataContext = new TechnicalPassportReviewViewModel(
            passport, payload => Clipboard.SetText(payload), activeContext);
    }
}
