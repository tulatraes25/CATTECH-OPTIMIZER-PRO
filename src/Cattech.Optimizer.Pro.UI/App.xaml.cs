using System.Windows;

namespace Cattech.Optimizer.Pro.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // TODO: Configurar Dependency Injection aquí
        // TODO: Configurar Serilog aquí
        // TODO: Cargar configuración inicial
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // TODO: Limpiar recursos aquí
        base.OnExit(e);
    }
}
