using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// bool → 选中/未选中边框色（选中时显示 Primary 色，未选中透明）
/// </summary>
public class BoolToBorderConverter : IValueConverter
{
    public static BoolToBorderConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true)
        {
            if (Application.Current.Resources["PrimaryBrush"] is SolidColorBrush brush)
                return brush;
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4F46E5"));
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// bool → 选中/未选中背景色（选中时浅色 Primary 背景）
/// </summary>
public class BoolToBgConverter : IValueConverter
{
    public static BoolToBgConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true)
        {
            if (Application.Current.Resources["PrimaryLightBrush"] is SolidColorBrush brush)
                return brush;
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEF2FF"));
        }
        if (Application.Current.Resources["SurfaceAltBrush"] is SolidColorBrush altBrush)
            return altBrush;
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFBFD"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
