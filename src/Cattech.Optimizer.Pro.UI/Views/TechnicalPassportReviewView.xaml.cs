using System.Windows;
using System.Windows.Controls;
using Cattech.Optimizer.Pro.Core.Models.Evidence;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.UI.Views;

/// <summary>
/// EE-05B reusable consent surface. Its trusted caller must provide the active
/// context and signal case selection changes. Not yet registered in navigation.
/// </summary>
public partial class TechnicalPassportReviewView : UserControl
{
    public TechnicalPassportReviewView(
        TechnicalPassportArtifact passport,
        Func<DiagnosticContextId> activeContext)
    {
        InitializeComponent();
        DataContext = new TechnicalPassportReviewViewModel(
            passport, payload => Clipboard.SetText(payload), activeContext);
    }

    public void InvalidateForCaseSelectionChange()
    {
        if (DataContext is TechnicalPassportReviewViewModel model)
            model.InvalidateForCaseSelectionChange();
    }
}
