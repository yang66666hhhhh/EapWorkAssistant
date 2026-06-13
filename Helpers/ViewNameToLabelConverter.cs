using System.Globalization;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 英文视图名 → 中文标签（用于下拉框显示）
/// </summary>
public class ViewNameToLabelConverter : IValueConverter
{
    public static ViewNameToLabelConverter Instance { get; } = new();

    private static readonly Dictionary<string, string> Labels = new()
    {
        ["Dashboard"]  = "工作台",
        ["WorkRecord"] = "工作记录",
        ["Knowledge"]  = "知识库",
        ["Issue"]      = "问题跟踪",
        ["Settings"]   = "设置",
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string key && Labels.TryGetValue(key, out var label) ? label : value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
