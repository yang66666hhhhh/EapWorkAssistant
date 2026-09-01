using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>布尔值 → DataGridSelectionMode 转换器（true=Extended 多选，false=Single 单选）。</summary>
public class SelectionModeConverter : IValueConverter
{
    public static readonly SelectionModeConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? DataGridSelectionMode.Extended : DataGridSelectionMode.Single;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
