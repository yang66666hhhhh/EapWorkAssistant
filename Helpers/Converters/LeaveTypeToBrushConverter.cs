using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EapWorkAssistant.Helpers;

/// <summary>将假期类型中文名转为对应的彩色画笔（用于列表标签着色）</summary>
public class LeaveTypeToBrushConverter : IValueConverter
{
    public static readonly LeaveTypeToBrushConverter Instance = new();

    private static readonly Dictionary<string, string> BrushKeyMap = new()
    {
        ["年假"] = "LeaveAnnualBrush",
        ["事假"] = "LeavePersonalBrush",
        ["病假"] = "LeaveSickBrush",
        ["调休"] = "LeaveCompBrush",
        ["出差"] = "LeaveTravelBrush",
        ["婚假"] = "LeaveMarriageBrush",
    };

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var leaveType = value as string ?? "";
        if (BrushKeyMap.TryGetValue(leaveType, out var key))
            return Application.Current.TryFindResource(key);
        return Application.Current.TryFindResource("TextSecondaryBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
