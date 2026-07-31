using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 字符串相等判断 → Visible / Collapsed。
/// 绑定值与 ConverterParameter 相等时显示，否则隐藏。
/// </summary>
public class StringEqualsToVisibilityConverter : IValueConverter
{
    public static readonly StringEqualsToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string;
        var param = parameter as string;
        return string.Equals(str, param, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
