using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 当集合为空（Count == 0 或 null）时返回 Visible，否则返回 Collapsed。
/// 用于显示空状态提示。
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public static readonly CountToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (value is ICollection collection)
            return collection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
