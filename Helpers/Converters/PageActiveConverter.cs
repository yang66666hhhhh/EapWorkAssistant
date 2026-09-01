using System.Globalization;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 多值转换器：比较两个值是否相等，返回 bool。
/// 用于分页按钮高亮当前页。
/// </summary>
public class PageActiveConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        // values[0] = page number (from ItemsControl item)
        // values[1] = CurrentPage (from ViewModel)
        if (values[0] is int page && values[1] is int current)
            return page == current && page > 0;
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
