using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using EapWorkAssistant.Services;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 趋势方向 → 语义色画刷（涨红跌绿，遵循中国视觉习惯）。
/// 用于 StatCard 趋势胶囊：TrendUp=true（增长）→ 红，false（下降）→ 绿。
/// 暗色模式下使用更亮的同色系，保证在深色卡片背景上可读。
/// </summary>
public class TrendBrushConverter : IValueConverter
{
    public static readonly TrendBrushConverter Instance = new();

    private static readonly SolidColorBrush LightUp = new(Color.FromRgb(220, 38, 38));   // 涨·红
    private static readonly SolidColorBrush LightDown = new(Color.FromRgb(5, 150, 105)); // 跌·绿
    private static readonly SolidColorBrush DarkUp = new(Color.FromRgb(248, 113, 113));
    private static readonly SolidColorBrush DarkDown = new(Color.FromRgb(52, 211, 153));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isUp = value is bool b && b;
        var isDark = ThemeService.Instance.IsDarkMode;
        return isUp ? (isDark ? DarkUp : LightUp) : (isDark ? DarkDown : LightDown);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
