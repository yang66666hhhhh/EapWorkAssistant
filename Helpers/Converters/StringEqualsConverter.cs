using System.Globalization;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 字符串等值比较转换器：当绑定值等于 ConverterParameter 时返回 true，否则 false。
/// 用于区间切换等需要按值高亮选中态的场景。
/// </summary>
public class StringEqualsConverter : IValueConverter
{
    public static readonly StringEqualsConverter Instance = new();

    public object Convert(object? value, System.Type? targetType, object? parameter, CultureInfo? culture)
    {
        return string.Equals(value?.ToString(), parameter?.ToString(), System.StringComparison.Ordinal);
    }

    public object ConvertBack(object? value, System.Type? targetType, object? parameter, CultureInfo? culture)
    {
        return System.Windows.DependencyProperty.UnsetValue;
    }
}
