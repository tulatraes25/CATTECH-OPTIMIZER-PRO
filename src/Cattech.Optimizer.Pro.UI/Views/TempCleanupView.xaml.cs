using System.Windows;
using System.Windows.Controls;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.UI.Views;

/// <summary>
/// Vista de limpieza segura de temporales.
/// El code-behind solo conecta el diálogo de confirmación con el ViewModel:
/// la lógica, la validación y el borrado viven en el ViewModel/servicio.
/// </summary>
public partial class TempCleanupView : UserControl
{
    public TempCleanupView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) => AttachConfirmation(e.NewValue as TempCleanupViewModel);
        Loaded += (_, _) =>
        {
            // Escaneo automático al entrar: la pantalla arranca con datos reales.
            if (DataContext is TempCleanupViewModel viewModel
                && !viewModel.IsRunning && !viewModel.HasScanned)
            {
                viewModel.ScanCommand.Execute(null);
            }
        };
    }

    private void AttachConfirmation(TempCleanupViewModel? viewModel)
    {
        if (viewModel is null)
            return;

        viewModel.ConfirmCleanupRequest = message =>
            MessageBox.Show(
                Window.GetWindow(this),
                message,
                "Confirmar limpieza de temporales",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) == MessageBoxResult.Yes;
    }
}
