using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Cattech.Optimizer.Pro.UI.Converters;

/// <summary>
/// Convierte bool a Visibility. true = Visible, false = Collapsed.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}

/// <summary>
/// Convierte string a SolidColorBrush para el campo de logo.
/// Si la ruta está vacía, muestra gris. Si tiene valor, muestra azul.
/// </summary>
public class LogoPathColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var path = value as string;
        return string.IsNullOrWhiteSpace(path)
            ? new SolidColorBrush(Colors.Gray)
            : new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convierte string hex (#RRGGBB) a Color para mostrar preview del color.
/// </summary>
public class HexToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            var hex = value as string;
            if (string.IsNullOrWhiteSpace(hex))
                return Colors.LightGray;

            hex = hex.TrimStart('#');

            if (hex.Length == 6)
            {
                var r = System.Convert.ToByte(hex[..2], 16);
                var g = System.Convert.ToByte(hex[2..4], 16);
                var b = System.Convert.ToByte(hex[4..6], 16);
                return Color.FromRgb(r, g, b);
            }

            if (hex.Length == 8)
            {
                var a = System.Convert.ToByte(hex[..2], 16);
                var r = System.Convert.ToByte(hex[2..4], 16);
                var g = System.Convert.ToByte(hex[4..6], 16);
                var b = System.Convert.ToByte(hex[6..8], 16);
                return Color.FromArgb(a, r, g, b);
            }

            return Colors.LightGray;
        }
        catch
        {
            return Colors.LightGray;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Invierte un valor booleano. true → false, false → true.
/// </summary>
public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? !b : value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? !b : value;
    }
}
