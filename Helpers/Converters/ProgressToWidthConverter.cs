using System.Globalization;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 将进度百分比 (0-100) 转换为进度条像素宽度。
/// 基于固定轨道宽度计算，用于 DataGrid 单元格内的迷你进度条。
/// </summary>
public class ProgressToWidthConverter : IValueConverter
{
    public static readonly ProgressToWidthConverter Instance = new();

    /// <summary>进度条轨道最大宽度（像素）</summary>
    private const double TrackWidth = 48;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int percent)
            return TrackWidth * Math.Clamp(percent, 0, 100) / 100.0;
        if (value is double dPercent)
            return TrackWidth * Math.Clamp(dPercent, 0, 100) / 100.0;
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
