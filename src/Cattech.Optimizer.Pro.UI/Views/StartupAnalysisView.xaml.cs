using System.Windows;
using System.Windows.Controls;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.UI.Views;

/// <summary>
/// Vista de análisis de programas de inicio.
/// El code-behind solo conecta el diálogo de confirmación con el ViewModel.
/// </summary>
public partial class StartupAnalysisView : UserControl
{
    public StartupAnalysisView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) => AttachConfirmation(e.NewValue as StartupAnalysisViewModel);
        Loaded += (_, _) =>
        {
            // La pantalla ya no abre en blanco: analiza una vez al entrar.
            if (DataContext is StartupAnalysisViewModel viewModel
                && !viewModel.IsRunning && !viewModel.HasResults)
            {
                viewModel.AnalyzeCommand.Execute(null);
            }
        };
    }

    private void AttachConfirmation(StartupAnalysisViewModel? viewModel)
    {
        if (viewModel is null)
            return;

        viewModel.ConfirmDisableRequest = message =>
            MessageBox.Show(
                Window.GetWindow(this),
                message,
                "Confirmar desactivación de programas de inicio",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) == MessageBoxResult.Yes;
    }
}
