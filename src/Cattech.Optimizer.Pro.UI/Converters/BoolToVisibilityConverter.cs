using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Core.Models.VisualOptimization;

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

/// <summary>
/// Invierte bool y convierte a Visibility. true → Collapsed, false → Visible.
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Collapsed;
    }
}

/// <summary>
/// Convierte string a Visibility. Null/vacío → Collapsed, con valor → Visible.
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string;
        return string.IsNullOrWhiteSpace(str) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convierte int a Visibility. 0 → Collapsed, !=0 → Visible.
/// </summary>
public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convierte bool (CanRestore) a Color de fondo. true → verde, false → gris.
/// </summary>
public class RestoreColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true
            ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
            : new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convierte bool (CanRestore) a texto. true → "Restaurable", false → "Restaurado".
/// </summary>
public class RestoreTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "Restaurable" : "Restaurado";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convierte CleanupRiskLevel a Color de fondo. Low → verde, Medium → amarillo.
/// </summary>
public class RiskColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CleanupRiskLevel risk)
        {
            return risk switch
            {
                CleanupRiskLevel.Low => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                CleanupRiskLevel.Medium => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                _ => new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convierte VisualRiskLevel a Color de fondo. Low → verde, Medium → amarillo.
/// </summary>
public class VisualRiskColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VisualRiskLevel risk)
        {
            return risk switch
            {
                VisualRiskLevel.Low => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                VisualRiskLevel.Medium => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                _ => new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convierte bool a string "Sí"/"No".
/// </summary>
public class BoolToVisYesNoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "Sí" : "No";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
