using System.Globalization;
using System.Windows.Data;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 将请假记录转换为列表行 ToolTip 的完整信息（"2026-09-28 周一 · 调休 8h"，有备注时追加备注行）。
/// </summary>
public class LeaveRowTooltipConverter : IValueConverter
{
    public static readonly LeaveRowTooltipConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not LeaveRecord r) return value;
        var text = $"{r.Date} {r.LeaveType} {r.Hours:0.#}h";
        if (!string.IsNullOrWhiteSpace(r.Note))
            text += $"\n备注：{r.Note}";
        return text;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
