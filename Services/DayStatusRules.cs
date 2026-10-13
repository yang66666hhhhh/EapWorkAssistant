using EapWorkAssistant.Models;

namespace EapWorkAssistant.Services;

/// <summary>
/// 出勤判定纯规则（无 IO、无状态，可单测）。
///
/// 定位：「是否加班 / 是否应上班」等业务规则的**唯一事实来源**。
/// 消费方：
///   - <see cref="DayStatusService"/>：生成出勤总览的日状态；
///   - <see cref="CompLeaveCalculator"/>：拆分周末加班（可调休）与假日加班（300% 不可调休）；
///   - <c>CustomCalendar.CreateDayItem</c>：日历圆点颜色。
/// 三处共用本类，避免规则漂移（历史上曾因节假日 schema 变更导致判定失效）。
/// </summary>
public static class DayStatusRules
{
    /// <summary>是否周末（周六/周日）。补班日也落在周末，需配合 isMakeup 判断。</summary>
    public static bool IsWeekend(DateTime date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>
    /// 是否应当上班：非法定假日的工作日，或周末的补班日。
    /// ⚠️ 必须排除法定假日 —— 国庆/春节里有若干天落在工作日（如 2026-10-01 周四、10-05 周一），
    /// 只判「非周末」会把这些假日算成应上班，进而在出勤总览里误报「未填」（历史上踩过）。
    /// </summary>
    /// <param name="date">日期</param>
    /// <param name="isHoliday">是否法定假日（假日一律不上班）</param>
    /// <param name="isMakeup">是否补班日（周末补班属正常工作日）</param>
    public static bool IsWorkdayDue(DateTime date, bool isHoliday, bool isMakeup) =>
        !isHoliday && (!IsWeekend(date) || isMakeup);

    /// <summary>
    /// 解析某一天的工作侧性质。
    /// </summary>
    /// <param name="date">日期</param>
    /// <param name="hasRecord">当天是否有工作记录</param>
    /// <param name="isHoliday">是否法定假日</param>
    /// <param name="isMakeup">是否补班日（周末补班属正常工作日，不算加班）</param>
    public static WorkKind Classify(DateTime date, bool hasRecord, bool isHoliday, bool isMakeup)
    {
        if (!hasRecord)
            return WorkKind.NoRecord;
        if (isHoliday)
            return WorkKind.HolidayOvertime;
        if (IsWeekend(date) && !isMakeup)
            return WorkKind.WeekendOvertime;
        return WorkKind.Workday;
    }
}
