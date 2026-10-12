using System;

namespace EapWorkAssistant.Services;

/// <summary>
/// 调休余额纯计算。
/// 把"哪些天算加班、已用多少调休"从数据获取与单例中解耦，便于单元测试。
/// 业务规则（依据《劳动法》第四十四条）：
/// - 法定假日加班：按 300% 支付加班工资，**不得以调休替代**，单独统计、不计入可调休余额；
/// - 周末加班（且非补班日）：可安排补休，计入可调休余额；
/// - 补班日虽在周末但属正常工作日，不计入加班；
/// - 可用余额 = max(0, 周末加班 - 已用调休)。
/// </summary>
public static class CompLeaveCalculator
{
    /// <summary>
    /// 计算年度周末加班、法定假日加班、已用调休与可用余额。
    /// </summary>
    /// <param name="workRecords">全年工作记录（日期 + 工时）</param>
    /// <param name="compLeaveHours">全年"调休"类型请假工时列表</param>
    /// <param name="isHoliday">判断某日是否为法定假日</param>
    /// <param name="isMakeupWorkday">判断某日是否为补班日（周末补班算正常工作日，不计入加班）</param>
    /// <returns>(周末加班工时, 法定假日加班工时, 加班合计, 已用调休工时, 可用余额)</returns>
    public static (double WeekendOvertimeHours, double HolidayOvertimeHours, double OvertimeHours,
        double CompLeaveUsed, double Available) Compute(
        IEnumerable<(DateTime Date, double Hours)> workRecords,
        IEnumerable<double> compLeaveHours,
        Func<DateTime, bool> isHoliday,
        Func<DateTime, bool> isMakeupWorkday)
    {
        double weekendOvertime = 0;
        double holidayOvertime = 0;
        foreach (var (date, hours) in workRecords)
        {
            // 补班日属正常工作日，任何情况下都不计加班
            if (isMakeupWorkday(date)) continue;

            if (isHoliday(date))
            {
                // 法定假日加班：300% 工资、不可调休，单独累计
                holidayOvertime += hours;
            }
            else if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                // 周末加班：可调休
                weekendOvertime += hours;
            }
        }

        double used = 0;
        foreach (var h in compLeaveHours)
            used += h;

        return (weekendOvertime, holidayOvertime, weekendOvertime + holidayOvertime,
            used, Math.Max(0, weekendOvertime - used));
    }
}
