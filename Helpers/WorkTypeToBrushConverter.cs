using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 将工作类型名称映射为对应的彩色画刷（用于标签背景）。
/// </summary>
public class WorkTypeToBrushConverter : IValueConverter
{
    public static readonly WorkTypeToBrushConverter Instance = new();

    private static readonly Dictionary<string, SolidColorBrush> BrushMap = new()
    {
        ["开发"] = new SolidColorBrush(Color.FromRgb(67, 56, 202)),    // 紫
        ["运维"] = new SolidColorBrush(Color.FromRgb(14, 165, 233)),    // 蓝
        ["会议"] = new SolidColorBrush(Color.FromRgb(245, 158, 11)),    // 橙
        ["学习"] = new SolidColorBrush(Color.FromRgb(16, 185, 129)),    // 绿
        ["调试"] = new SolidColorBrush(Color.FromRgb(239, 68, 68)),     // 红
        ["文档"] = new SolidColorBrush(Color.FromRgb(99, 102, 241)),    // 靛蓝
        ["出差"] = new SolidColorBrush(Color.FromRgb(236, 72, 153)),    // 粉
    };

    private static readonly SolidColorBrush DefaultBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string workType && BrushMap.TryGetValue(workType, out var brush))
            return brush;
        return DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
