using System.Windows.Controls;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.UI.Views;

/// <summary>
/// Vista de discos SMART.
/// </summary>
public partial class SmartDiskView : UserControl
{
    public SmartDiskView()
    {
        InitializeComponent();
    }

    private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SmartDiskViewModel viewModel &&
            sender is DataGrid dataGrid &&
            dataGrid.SelectedItem is Core.Models.Smart.SmartDiskReport report)
        {
            viewModel.SelectReportCommand.Execute(report);
        }
    }
}
