using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 排序箭头可见性转换器（IMultiValueConverter）。
/// 接收 3 个绑定值：(SortColumn, SortAscending, ColumnKey)，
/// 当当前排序列匹配 ColumnKey 且方向匹配时返回 Visible，否则 Collapsed。
/// </summary>
public class SortArrowConverter : IMultiValueConverter
{
    /// <summary>升序箭头实例：当 SortColumn==ColumnKey 且 SortAscending==true 时显示</summary>
    public static readonly SortArrowConverter AscInstance = new(true);

    /// <summary>降序箭头实例：当 SortColumn==ColumnKey 且 SortAscending==false 时显示</summary>
    public static readonly SortArrowConverter DescInstance = new(false);

    private readonly bool _targetDirection;

    private SortArrowConverter(bool targetDirection) => _targetDirection = targetDirection;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3) return Visibility.Collapsed;
        var sortColumn = values[0] as string;
        var sortAscending = values[1] is true;
        var columnKey = values[2] as string;

        return sortColumn == columnKey && sortAscending == _targetDirection
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
