using System.Windows;

namespace Cattech.Optimizer.Pro.UI;

/// <summary>
/// Punto de entrada de la aplicación. La composición de servicios ocurre en
/// <see cref="ViewModels.MainViewModel"/>, que se enlaza como DataContext en MainWindow.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}
