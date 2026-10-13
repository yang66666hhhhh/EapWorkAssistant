using System;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Services;

/// <summary>
/// 调休余额计算结果（双口径）。
///
/// 为什么分两个口径（工控现场常限制单日**填报**工时不超过 8 小时）：
///   - 填报口径（Report）：报给公司/领导，**也是调休余额的计算依据**（公司只认这个）；
///   - 实际口径（Actual）：真实投入时长，通常大于填报值，仅供自己查看"到底干了多少"。
/// </summary>
/// <param name="WeekendOvertimeReportHours">周末加班（填报口径，**计入余额**）</param>
/// <param name="WeekendOvertimeActualHours">周末加班（实际口径，仅展示）</param>
/// <param name="HolidayOvertimeReportHours">法定假日加班（填报口径）</param>
/// <param name="HolidayOvertimeActualHours">法定假日加班（实际口径，不可调休，单独统计）</param>
/// <param name="CompLeaveUsed">已使用调休工时</param>
/// <param name="CompLeaveAvailable">可用调休余额 = max(0, 周末加班【填报】 - 已用调休)</param>
public sealed record CompLeaveResult(
    double WeekendOvertimeReportHours,
    double WeekendOvertimeActualHours,
    double HolidayOvertimeReportHours,
    double HolidayOvertimeActualHours,
    double CompLeaveUsed,
    double CompLeaveAvailable)
{
    /// <summary>加班合计（填报口径），用于对外展示</summary>
    public double OvertimeReportHours => WeekendOvertimeReportHours + HolidayOvertimeReportHours;

    /// <summary>加班合计（实际口径）</summary>
    public double OvertimeActualHours => WeekendOvertimeActualHours + HolidayOvertimeActualHours;
}

/// <summary>
/// 调休余额纯计算。
/// 把"哪些天算加班、已用多少调休"从数据获取与单例中解耦，便于单元测试。
/// 业务规则（依据《劳动法》第四十四条）：
/// - 法定假日加班：按 300% 支付加班工资，**不得以调休替代**，单独统计、不计入可调休余额；
/// - 周末加班（且非补班日）：可安排补休，计入可调休余额；
/// - 补班日虽在周末但属正常工作日，不计入加班；
/// - 可用余额 = max(0, 周末加班【填报工时】 - 已用调休)。
///
/// ⚠️ 余额口径是**填报工时**而非实际工时：调休是公司给的权利，只能按公司认可的口径算；
/// 实际工时通常更多（现场不让报超 8h），若按实际算余额会虚高、公司不认。
/// 实际工时仅用于自己查看真实投入。
///
/// 加班判定统一走 <see cref="DayStatusRules.Classify"/>（与出勤总览、日历圆点同一份规则）。
/// </summary>
public static class CompLeaveCalculator
{
    /// <summary>
    /// 计算年度周末加班、法定假日加班、已用调休与可用余额（加班工时按填报/实际双口径分别累计）。
    /// </summary>
    /// <param name="workRecords">全年工作记录（日期 + 填报工时 + 实际工时）</param>
    /// <param name="compLeaveHours">全年"调休"类型请假工时列表</param>
    /// <param name="isHoliday">判断某日是否为法定假日</param>
    /// <param name="isMakeupWorkday">判断某日是否为补班日（周末补班算正常工作日，不计入加班）</param>
    public static CompLeaveResult Compute(
        IEnumerable<(DateTime Date, double ReportHours, double ActualHours)> workRecords,
        IEnumerable<double> compLeaveHours,
        Func<DateTime, bool> isHoliday,
        Func<DateTime, bool> isMakeupWorkday)
    {
        double weekendReport = 0, weekendActual = 0;
        double holidayReport = 0, holidayActual = 0;

        foreach (var (date, reportHours, actualHours) in workRecords)
        {
            // 补班日属正常工作日，任何情况下都不计加班
            if (isMakeupWorkday(date)) continue;

            switch (DayStatusRules.Classify(date, true, isHoliday(date), isMakeupWorkday(date)))
            {
                case WorkKind.HolidayOvertime:
                    // 法定假日加班：300% 工资、不可调休，单独累计
                    holidayReport += reportHours;
                    holidayActual += actualHours;
                    break;
                case WorkKind.WeekendOvertime:
                    // 周末加班：可调休
                    weekendReport += reportHours;
                    weekendActual += actualHours;
                    break;
            }
        }

        double used = 0;
        foreach (var h in compLeaveHours)
            used += h;

        // 余额按**填报工时**计算（公司口径）：调休是公司给的权利，只能按公司认可的填报值算。
        // 实际工时通常大于填报值，若按它算余额会虚高、公司不认；实际工时仅供自己查看。
        return new CompLeaveResult(
            weekendReport, weekendActual,
            holidayReport, holidayActual,
            used, Math.Max(0, weekendReport - used));
    }
}
