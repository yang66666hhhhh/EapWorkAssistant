namespace EapWorkAssistant.Services;

/// <summary>
/// 试用期记录覆盖率纯函数计算。
/// </summary>
public static class CoverageCalculator
{
    /// <summary>
    /// 从已记录的日期字符串中，排除休息日后得到「有记录的工作日」数量。
    /// </summary>
    /// <param name="recordedDates">yyyy-MM-dd 格式的日期集合</param>
    /// <param name="restDays">每周休息日的 DayOfWeek 数值（0=周日，6=周六）</param>
    public static int CountRecordedWorkingDays(IEnumerable<string> recordedDates, List<int> restDays)
    {
        return recordedDates.Count(d => DateTime.TryParse(d, out var dt) && !restDays.Contains((int)dt.DayOfWeek));
    }
}
