using System.Globalization;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 将逗号分隔的标签字符串拆分为 string[]，供 ItemsControl 渲染独立徽章。
/// 空字符串返回空数组。
/// </summary>
public class TagsSplitConverter : IValueConverter
{
    public static readonly TagsSplitConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string tags && !string.IsNullOrWhiteSpace(tags))
        {
            return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        return Array.Empty<string>();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
