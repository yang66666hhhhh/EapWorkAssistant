using System.Globalization;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// MultiValue 转换器：接收 [百分比, 父容器宽度]，返回像素宽度值。
/// 用于进度条等需要按比例填充的场景。
/// </summary>
public class PercentToWidthConverter : IMultiValueConverter
{
    public static readonly PercentToWidthConverter Instance = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is double percent && values[1] is double parentWidth && parentWidth > 0)
        {
            // 减去 padding (约 32px)
            var availableWidth = parentWidth - 32;
            return Math.Max(0, availableWidth * percent / 100.0);
        }
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
