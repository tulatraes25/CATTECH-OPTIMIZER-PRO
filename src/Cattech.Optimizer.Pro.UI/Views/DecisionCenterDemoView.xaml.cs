using System.Windows.Controls;

namespace Cattech.Optimizer.Pro.UI.Views;

/// <summary>
/// Display-only synthetic workflow demo. Does not start hardware services,
/// repairs, exports or network operations.
/// </summary>
public partial class DecisionCenterDemoView : UserControl
{
    public DecisionCenterDemoView()
    {
        InitializeComponent();
    }
}
