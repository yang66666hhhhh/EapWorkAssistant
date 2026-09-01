using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 将问题「状态」英文值转为对应的彩色画笔（用于 IssueView 状态徽章着色）。
/// 与 <see cref="IssueStatusConverter"/>（英文 → 中文标签）配套使用：
/// 一个负责文字，一个负责底色。
/// </summary>
public class IssueStatusToBrushConverter : IValueConverter
{
    public static readonly IssueStatusToBrushConverter Instance = new();

    private static readonly Dictionary<string, string> BrushKeyMap = new()
    {
        ["Open"] = "WarningLightBrush",        // 待处理 - 黄
        ["InProgress"] = "InfoLightBrush",     // 进行中 - 蓝
        ["Resolved"] = "SuccessLightBrush",    // 已解决 - 绿
        ["Closed"] = "TextSecondaryBrush"      // 已关闭 - 中性灰
    };

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value as string ?? "";
        if (BrushKeyMap.TryGetValue(status, out var key))
            return Application.Current.TryFindResource(key);
        return Application.Current.TryFindResource("TextSecondaryBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 将问题「优先级」英文值转为对应的彩色画笔：低 → 中 → 高 → 紧急，颜色逐级加重。
/// 与 <see cref="IssuePriorityConverter"/>（英文 → 中文标签）配套使用。
/// </summary>
public class IssuePriorityToBrushConverter : IValueConverter
{
    public static readonly IssuePriorityToBrushConverter Instance = new();

    private static readonly Dictionary<string, string> BrushKeyMap = new()
    {
        ["Low"] = "SuccessLightBrush",         // 低 - 绿
        ["Medium"] = "InfoLightBrush",         // 中 - 蓝
        ["High"] = "WarningLightBrush",        // 高 - 黄
        ["Critical"] = "DangerLightBrush"      // 紧急 - 红
    };

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var priority = value as string ?? "";
        if (BrushKeyMap.TryGetValue(priority, out var key))
            return Application.Current.TryFindResource(key);
        return Application.Current.TryFindResource("TextSecondaryBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
