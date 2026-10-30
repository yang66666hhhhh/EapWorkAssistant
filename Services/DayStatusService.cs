using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;

namespace EapWorkAssistant.Services;

/// <summary>
/// 出勤日状态服务：按日期区间合并三个数据源，产出每天一条 <see cref="DayStatusItem"/>。
///
/// 数据源：
///   1. WorkRecord（工作记录：工时/内容，已过滤软删除）
///   2. LeaveRecord（请假记录：类型/工时，已过滤软删除）
///   3. HolidayService（法定假日/补班日日历，含本地缓存与内置兜底）
///
/// 定位：**「是否加班」等判定规则的唯一事实来源**。
/// 原先规则散落在 CustomCalendar（圆点颜色）与 CompLeaveCalculator（调休统计）两处，
/// 现统一收敛到本服务，消费方只读结果，避免规则漂移。
///
/// 业务规则（与需求确认一致）：
///   - 同一天既有请假又有工作记录：双标记同时展示，互不覆盖；
///   - 应上班（工作日或补班日）但无记录也无请假：标记「未填」；
///   - 法定假日有记录 = 假日加班（300% 不可调休）；周末（非补班）有记录 = 周末加班（可调休）。
/// </summary>
public class DayStatusService
{
    private readonly WorkRecordRepository _recordRepo;
    private readonly LeaveRecordRepository _leaveRepo;

    public DayStatusService(WorkRecordRepository recordRepo, LeaveRecordRepository leaveRepo)
    {
        _recordRepo = recordRepo;
        _leaveRepo = leaveRepo;
    }

    /// <summary>指定年份的节假日数据是否已可用。</summary>
    public bool IsHolidayDataAvailable(int year) => HolidayService.Instance.IsYearAvailable(year);

    /// <summary>
    /// 库里是否存在**任何**工作记录或请假记录（不限当前区间，含历史）。
    /// 出勤总览用它区分两种「空」：
    ///   - 全新用户（无任何记录）→ 显示引导空状态，而不是让 18 个「未填」红格把人淹掉；
    ///   - 已在使用但本月没填 → 保留「未填」提醒，这才是有效告警。
    /// </summary>
    public async Task<bool> HasAnyRecordAsync()
    {
        if (await _recordRepo.GetTotalCountAsync() > 0)
            return true;
        return await _leaveRepo.GetTotalCountAsync() > 0;
    }

    /// <summary>
    /// 获取 [start, end] 闭区间内每一天的出勤状态（含无记录的休息日/未填日）。
    /// </summary>
    public async Task<List<DayStatusItem>> GetRangeAsync(DateTime start, DateTime end)
    {
        if (end < start)
            (start, end) = (end, start);

        var startStr = start.ToString("yyyy-MM-dd");
        var endStr = end.ToString("yyyy-MM-dd");

        // 区间可能跨年，逐日补拉所缺年份的节假日数据（LoadYearAsync 幂等）
        for (var y = start.Year; y <= end.Year; y++)
            await HolidayService.Instance.LoadYearAsync(y);

        // 1. 工作记录按天聚合（同一天多条 → 合计工时、拼接摘要）。
        //    工时按填报（r.Hours）与实际（r.ActualHours，未填即 0，不回落）双口径分别累加。
        var workByDay = new Dictionary<DateTime, (double Hours, double ActualHours, int Count, string Summary)>();
        foreach (var r in await _recordRepo.GetByDateRangeAsync(startStr, endStr))
        {
            if (!DateTime.TryParse(r.WorkDate, out var date))
                continue; // 脏数据防御：日期不可解析则跳过
            var day = date.Date;
            var summary = (r.Content ?? string.Empty).Trim();
            var actual = r.ActualHours;
            if (workByDay.TryGetValue(day, out var acc))
            {
                workByDay[day] = (
                    acc.Hours + r.Hours,
                    acc.ActualHours + actual,
                    acc.Count + 1,
                    string.IsNullOrEmpty(summary) ? acc.Summary : acc.Summary);
            }
            else
            {
                workByDay[day] = (r.Hours, actual, 1, summary);
            }
        }

        // 2. 请假记录按天取首条（同一天多条请假极少见；工时仍合计）
        var leaveByDay = new Dictionary<DateTime, (string Type, double Hours)>();
        foreach (var l in await _leaveRepo.GetByDateRangeAsync(startStr, endStr))
        {
            if (!DateTime.TryParse(l.Date, out var date))
                continue;
            var day = date.Date;
            if (leaveByDay.TryGetValue(day, out var acc))
                leaveByDay[day] = (acc.Type, acc.Hours + l.Hours);
            else
                leaveByDay[day] = (l.LeaveType, l.Hours);
        }

        // 3. 逐日生成状态
        var items = new List<DayStatusItem>();
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
        {
            var isHoliday = HolidayService.Instance.IsHoliday(d);
            var isMakeup = HolidayService.Instance.IsMakeupWorkday(d);

            var hasWork = workByDay.TryGetValue(d, out var work);
            var hasLeave = leaveByDay.TryGetValue(d, out var leave);

            var kind = DayStatusRules.Classify(d, hasWork, isHoliday, isMakeup);

            items.Add(new DayStatusItem
            {
                Date = d,
                IsHolidayDate = isHoliday,
                IsMakeupWorkday = isMakeup,
                IsWorkdayDue = DayStatusRules.IsWorkdayDue(d, isHoliday, isMakeup),
                WorkKind = kind,
                WorkHours = hasWork ? work.Hours : 0,
                ActualHours = hasWork ? work.ActualHours : 0,
                RecordCount = hasWork ? work.Count : 0,
                ContentSummary = hasWork ? Truncate(work.Summary, 24) : null,
                LeaveType = hasLeave ? leave.Type : null,
                LeaveHours = hasLeave ? leave.Hours : 0
            });
        }

        return items;
    }

    /// <summary>汇总统计（统计卡用）。同一天可同时计入「正常上班」与「请假」，与双标记展示口径一致。
    /// 工时按填报/实际双口径分别累计（月度-本年视图下两者常不同：现场填报受 8h 上限压制）。</summary>
    public static AttendanceSummary Summarize(IEnumerable<DayStatusItem> items)
    {
        int normalDays = 0, weekendDays = 0, holidayDays = 0, leaveDays = 0, unfilledDays = 0, restDays = 0;
        double normalHours = 0, weekendHours = 0, holidayHours = 0, leaveHours = 0;
        double actualNormalHours = 0, actualWeekendHours = 0, actualHolidayHours = 0;

        foreach (var i in items)
        {
            switch (i.WorkKind)
            {
                case WorkKind.Workday:
                    normalDays++;
                    normalHours += i.WorkHours;
                    actualNormalHours += i.ActualHours;
                    break;
                case WorkKind.WeekendOvertime:
                    weekendDays++;
                    weekendHours += i.WorkHours;
                    actualWeekendHours += i.ActualHours;
                    break;
                case WorkKind.HolidayOvertime:
                    holidayDays++;
                    holidayHours += i.WorkHours;
                    actualHolidayHours += i.ActualHours;
                    break;
            }

            if (i.HasLeave)
            {
                leaveDays++;
                leaveHours += i.LeaveHours;
            }

            if (i.IsUnfilled) unfilledDays++;
            else if (i.IsRestDay) restDays++;
        }

        return new AttendanceSummary(
            normalDays, normalHours,
            weekendDays, weekendHours,
            holidayDays, holidayHours,
            leaveDays, leaveHours,
            unfilledDays, restDays)
        {
            ActualNormalHours = actualNormalHours,
            ActualWeekendOvertimeHours = actualWeekendHours,
            ActualHolidayOvertimeHours = actualHolidayHours
        };
    }

    private static string? Truncate(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.ReplaceLineEndings(" ").Trim();
        return text.Length <= max ? text : text[..max] + "…";
    }
}
