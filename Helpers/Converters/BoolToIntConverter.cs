using System.Globalization;
using System.Windows.Data;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// bool 与 int (0/1) 的双向转换器。
/// </summary>
public class BoolToIntConverter : IValueConverter
{
    public static readonly BoolToIntConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i && i != 0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? 1 : 0;
}
