using System.Globalization;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 将请假记录日期字符串转换为紧凑展示格式（"2026-09-28" → "09-28 周一"）。
/// 当月视图下年份冗余，星期比纯数字日期更易排班；解析失败时原样返回。
/// </summary>
public class LeaveDateDisplayConverter : IValueConverter
{
    public static readonly LeaveDateDisplayConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && DateTime.TryParse(s, out var d))
        {
            var weekday = d.DayOfWeek switch
            {
                DayOfWeek.Monday => "周一",
                DayOfWeek.Tuesday => "周二",
                DayOfWeek.Wednesday => "周三",
                DayOfWeek.Thursday => "周四",
                DayOfWeek.Friday => "周五",
                DayOfWeek.Saturday => "周六",
                DayOfWeek.Sunday => "周日",
                _ => ""
            };
            return $"{d:MM-dd} {weekday}";
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
