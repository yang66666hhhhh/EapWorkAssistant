using System.Globalization;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>将英文状态值转为中文标签</summary>
public class IssueStatusConverter : IValueConverter
{
    public static readonly IssueStatusConverter Instance = new();

    private static readonly Dictionary<string, string> Map = new()
    {
        ["Open"] = "待处理",
        ["InProgress"] = "进行中",
        ["Resolved"] = "已解决",
        ["Closed"] = "已关闭"
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string str && Map.TryGetValue(str, out var label) ? label : (object)(value?.ToString() ?? "");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string label)
        {
            foreach (var kvp in Map)
                if (kvp.Value == label) return kvp.Key;
        }
        return "Open";
    }
}

/// <summary>将英文优先级转为中文标签</summary>
public class IssuePriorityConverter : IValueConverter
{
    public static readonly IssuePriorityConverter Instance = new();

    private static readonly Dictionary<string, string> Map = new()
    {
        ["Low"] = "低",
        ["Medium"] = "中",
        ["High"] = "高",
        ["Critical"] = "紧急"
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && Map.TryGetValue(s, out var label) ? label : (object)(value?.ToString() ?? "");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string label)
        {
            foreach (var kvp in Map)
                if (kvp.Value == label) return kvp.Key;
        }
        return "Medium";
    }
}
