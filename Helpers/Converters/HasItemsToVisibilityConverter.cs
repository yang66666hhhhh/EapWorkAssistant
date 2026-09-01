using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 当集合/数值不为空（Count &gt; 0）时返回 Visible，否则 Collapsed。
/// 与 CountToVisibilityConverter 相反：用于显示"有数据时"的 UI 元素（如角标、操作栏）。
/// </summary>
public class HasItemsToVisibilityConverter : IValueConverter
{
    public static readonly HasItemsToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            int i => i > 0 ? Visibility.Visible : Visibility.Collapsed,
            ICollection c => c.Count > 0 ? Visibility.Visible : Visibility.Collapsed,
            _ => Visibility.Collapsed
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
