using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using EapWorkAssistant.Services;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 将工作类型名称映射为对应的彩色画刷（用于标签背景）。
/// 暗色模式下使用稍亮/柔和的配色，与深色卡片背景协调。
/// </summary>
public class WorkTypeToBrushConverter : IValueConverter
{
    public static readonly WorkTypeToBrushConverter Instance = new();

    private static readonly Dictionary<string, SolidColorBrush> LightBrushMap = new()
    {
        ["开发"] = new SolidColorBrush(Color.FromRgb(67, 56, 202)),    // Indigo
        ["运维"] = new SolidColorBrush(Color.FromRgb(14, 165, 233)),    // Sky
        ["会议"] = new SolidColorBrush(Color.FromRgb(245, 158, 11)),    // Amber
        ["学习"] = new SolidColorBrush(Color.FromRgb(16, 185, 129)),    // Emerald
        ["调试"] = new SolidColorBrush(Color.FromRgb(239, 68, 68)),     // Red
        ["文档"] = new SolidColorBrush(Color.FromRgb(99, 102, 241)),    // Indigo-light
        ["出差"] = new SolidColorBrush(Color.FromRgb(236, 72, 153)),    // Pink
    };

    private static readonly Dictionary<string, SolidColorBrush> DarkBrushMap = new()
    {
        ["开发"] = new SolidColorBrush(Color.FromRgb(99, 102, 241)),    // 更亮的靛蓝
        ["运维"] = new SolidColorBrush(Color.FromRgb(56, 189, 248)),    // 更亮的天蓝
        ["会议"] = new SolidColorBrush(Color.FromRgb(251, 191, 36)),    // 更亮的琥珀
        ["学习"] = new SolidColorBrush(Color.FromRgb(52, 211, 153)),    // 更亮的翠绿
        ["调试"] = new SolidColorBrush(Color.FromRgb(248, 113, 113)),   // 更亮的红
        ["文档"] = new SolidColorBrush(Color.FromRgb(129, 140, 248)),   // 更亮的靛蓝浅
        ["出差"] = new SolidColorBrush(Color.FromRgb(244, 114, 182)),   // 更亮的粉
    };

    private static readonly SolidColorBrush LightDefaultBrush = new(Color.FromRgb(107, 114, 128));
    private static readonly SolidColorBrush DarkDefaultBrush = new(Color.FromRgb(148, 163, 184));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isDark = ThemeService.Instance.IsDarkMode;
        var map = isDark ? DarkBrushMap : LightBrushMap;
        var defaultBrush = isDark ? DarkDefaultBrush : LightDefaultBrush;

        if (value is string workType && map.TryGetValue(workType, out var brush))
            return brush;
        return defaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
