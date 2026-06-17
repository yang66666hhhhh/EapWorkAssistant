using System.Globalization;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 将 SelectedIndex (int) 与 ConverterParameter (string "0"~"4") 比较，
/// 相等时返回 true。用于导航栏 RadioButton 选中状态同步。
/// </summary>
public class IndexToBoolConverter : IValueConverter
{
    public static readonly IndexToBoolConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string param && int.TryParse(param, out var target))
            return index == target;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string param && int.TryParse(param, out var target))
            return target;
        return Binding.DoNothing;
    }
}
