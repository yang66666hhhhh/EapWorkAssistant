namespace EapWorkAssistant.Helpers;

public static class DateTimeHelper
{
    public static string GetWeekRange(DateTime date)
    {
        var start = date.AddDays(-(int)date.DayOfWeek + 1);
        if (date.DayOfWeek == DayOfWeek.Sunday)
            start = date.AddDays(-6);
        var end = start.AddDays(6);
        return $"{start:yyyy-MM-dd} ~ {end:yyyy-MM-dd}";
    }

    public static string GetCurrentYearMonth()
    {
        return DateTime.Now.ToString("yyyy-MM");
    }

    public static DateTime GetWeekStart(DateTime date)
    {
        var start = date.AddDays(-(int)date.DayOfWeek + 1);
        return date.DayOfWeek == DayOfWeek.Sunday ? date.AddDays(-6) : start;
    }

    public static DateTime GetWeekEnd(DateTime date)
    {
        return GetWeekStart(date).AddDays(6);
    }

    public static DateTime GetMonthStart(DateTime date)
    {
        return new DateTime(date.Year, date.Month, 1);
    }

    public static DateTime GetMonthEnd(DateTime date)
    {
        return GetMonthStart(date).AddMonths(1).AddDays(-1);
    }
}
