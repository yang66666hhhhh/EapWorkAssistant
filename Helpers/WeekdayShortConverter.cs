using System.Globalization;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 将中文星期缩写转换为短格式（"周一" → "一"，"星期一" → "一"，"Sunday" → "Sun"）。
/// </summary>
public class WeekdayShortConverter : IValueConverter
{
    public static readonly WeekdayShortConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            // 中文格式：去掉 "周" 或 "星期" 前缀
            if (s.StartsWith("周"))
                return s.Substring(1);
            if (s.StartsWith("星期"))
                return s.Substring(2);
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}