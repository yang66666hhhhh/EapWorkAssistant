using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// bool → Visibility 转换器（true = Visible, false = Collapsed）。
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (parameter is "Inverse") flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}
