using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using EapWorkAssistant.Services;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// bool → 选中/未选中边框色（选中时显示 Primary 色，未选中透明）
/// </summary>
public class BoolToBorderConverter : IValueConverter
{
    public static BoolToBorderConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true)
        {
            if (Application.Current.Resources["PrimaryBrush"] is SolidColorBrush brush)
                return brush;
            // 回退：从 ThemeService 获取当前强调色
            var hex = ThemeService.GetAccentPreviewColor(ThemeService.Instance.AccentColor);
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// bool → 选中/未选中背景色（选中时浅色 Primary 背景，深色模式下用半透明主色）
/// </summary>
public class BoolToBgConverter : IValueConverter
{
    public static BoolToBgConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true)
        {
            var isDark = ThemeService.Instance.IsDarkMode;
            if (isDark)
            {
                // 深色模式：用主色 20% 不透明度做选中背景
                if (Application.Current.Resources["PrimaryBrush"] is SolidColorBrush primary)
                    return new SolidColorBrush(Color.FromArgb(51, primary.Color.R, primary.Color.G, primary.Color.B));
                // 回退
                var hex = ThemeService.GetAccentPreviewColor(ThemeService.Instance.AccentColor);
                var c = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(Color.FromArgb(51, c.R, c.G, c.B));
            }
            // 浅色模式：用 PrimaryLight 柔和底色
            if (Application.Current.Resources["PrimaryLightBrush"] is SolidColorBrush brush)
                return brush;
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEF2FF"));
        }
        if (Application.Current.Resources["SurfaceAltBrush"] is SolidColorBrush altBrush)
            return altBrush;
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFBFD"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
