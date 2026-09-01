using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 非空字符串 → Visible，空/null → Collapsed。
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public static readonly StringToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string;
        var visible = !string.IsNullOrEmpty(str);
        if (parameter is "Inverse") visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
