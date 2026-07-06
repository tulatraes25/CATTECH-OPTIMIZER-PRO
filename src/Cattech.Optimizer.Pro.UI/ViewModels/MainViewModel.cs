using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel principal para la ventana de la aplicación.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // Estilos para botones del sidebar (se inicializan en código)
    public static string HomeButtonStyle { get; set; } = "SidebarButton";
    public static string HardwareButtonStyle { get; set; } = "SidebarButton";
    public static string DiagnosticsButtonStyle { get; set; } = "SidebarButton";
    public static string OptimizationButtonStyle { get; set; } = "SidebarButton";
    public static string ReportsButtonStyle { get; set; } = "SidebarButton";
    public static string SettingsButtonStyle { get; set; } = "SidebarButton";

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentSection = "Home";

    public MainViewModel()
    {
        // Cargar vista inicial
        NavigateTo("Home");
    }

    [RelayCommand]
    private void Navigate(string? section)
    {
        if (!string.IsNullOrEmpty(section))
        {
            NavigateTo(section);
        }
    }

    private void NavigateTo(string section)
    {
        CurrentSection = section;

        // TODO: Implementar navegación real con vistas
        // Por ahora, mostrar placeholder
        CurrentView = section switch
        {
            "Home" => CreatePlaceholder("Inicio - Panel principal"),
            "Hardware" => CreatePlaceholder("Información de Hardware"),
            "Diagnostics" => CreatePlaceholder("Diagnóstico del Sistema"),
            "Optimization" => CreatePlaceholder("Optimización"),
            "Reports" => CreatePlaceholder("Generación de Informes"),
            "Settings" => CreatePlaceholder("Configuración de Empresa/Técnico"),
            _ => CreatePlaceholder("Sección no encontrada")
        };
    }

    private static ContentControl CreatePlaceholder(string text)
    {
        var border = new Border
        {
            Background = System.Windows.Media.Brushes.White,
            Padding = new Thickness(30)
        };

        var stackPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Top
        };

        var title = new TextBlock
        {
            Text = text,
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)),
            Margin = new Thickness(0, 20, 0, 10)
        };

        var subtitle = new TextBlock
        {
            Text = "Esta sección será implementada en futuras iteraciones del MVP.",
            FontSize = 14,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x66, 0x66, 0x66)),
            Margin = new Thickness(0, 0, 0, 20)
        };

        var version = new TextBlock
        {
            Text = "CATTECH OPTIMIZER PRO v0.1.0 MVP",
            FontSize = 12,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x99, 0x99, 0x99))
        };

        stackPanel.Children.Add(title);
        stackPanel.Children.Add(subtitle);
        stackPanel.Children.Add(version);

        border.Child = stackPanel;

        return new ContentControl { Content = border };
    }
}
