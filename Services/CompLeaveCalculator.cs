using System;

namespace EapWorkAssistant.Services;

/// <summary>
/// 调休余额纯计算。
/// 把"哪些天算加班、已用多少调休"从数据获取与单例中解耦，便于单元测试。
/// 业务规则：周末或法定假日（且非补班日）的工作工时计入可调休加班；
/// 已用调休工时来自全年 LeaveType == "调休" 的请假记录；可用余额 = max(0, 加班 - 已用)。
/// </summary>
public static class CompLeaveCalculator
{
    /// <summary>
    /// 计算年度可调休加班工时、已用调休工时与可用余额。
    /// </summary>
    /// <param name="workRecords">全年工作记录（日期 + 工时）</param>
    /// <param name="compLeaveHours">全年"调休"类型请假工时列表</param>
    /// <param name="isHoliday">判断某日是否为法定假日</param>
    /// <param name="isMakeupWorkday">判断某日是否为补班日（周末补班算正常工作日，不计入加班）</param>
    /// <returns>(加班工时, 已用调休工时, 可用余额)</returns>
    public static (double OvertimeHours, double CompLeaveUsed, double Available) Compute(
        IEnumerable<(DateTime Date, double Hours)> workRecords,
        IEnumerable<double> compLeaveHours,
        Func<DateTime, bool> isHoliday,
        Func<DateTime, bool> isMakeupWorkday)
    {
        double overtime = 0;
        foreach (var (date, hours) in workRecords)
        {
            bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            // 周末或法定假日工作 → 计入可调休加班；补班日虽在周末但属正常工作日，不计入
            if ((isWeekend || isHoliday(date)) && !isMakeupWorkday(date))
                overtime += hours;
        }

        double used = 0;
        foreach (var h in compLeaveHours)
            used += h;

        return (overtime, used, Math.Max(0, overtime - used));
    }
}
