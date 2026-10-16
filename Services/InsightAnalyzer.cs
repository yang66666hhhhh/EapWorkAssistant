using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Services;

/// <summary>
/// 洞察分析输入。由 ViewModel 取数后组装，使分析器本身**无 IO、无单例依赖**，可纯函数单测。
/// </summary>
/// <param name="IsHolidayDataAvailable">节假日日历是否可用；不可用时无法判定「应上班」，需整体降级。</param>
/// <param name="CompLeaveAvailable">调休可用余额（填报口径，来自 <see cref="CompLeaveCalculator"/>）</param>
/// <param name="CompLeaveUsed">已使用调休工时</param>
/// <param name="WeekendOvertimeReportHours">周末加班工时（填报口径，余额的唯一来源）</param>
/// <param name="DayStatus">区间内逐日出勤状态（含「应上班无记录」的未填日）</param>
/// <param name="Today">今天（用于排除"今天还没结束不算漏填"的边界）</param>
public sealed record InsightInput(
    bool IsHolidayDataAvailable,
    double CompLeaveAvailable,
    double CompLeaveUsed,
    double WeekendOvertimeReportHours,
    IReadOnlyList<DayStatusItem> DayStatus,
    DateTime Today);

/// <summary>
/// 主动异常洞察分析（P2 ⑨，纯静态）。
///
/// 从仪表盘的统计数字里再往前一步：不等用户去翻页发现问题，而是主动算出来并给出可点击的处理入口。
/// 当前三条规则：
///   1. **调休余额临界/透支** —— 余额不足 1 天提醒，已用超过周末加班则告警；
///   2. **连续未填** —— 连续 N 个「应上班」工作日无记录无请假（漏填 → 出勤与调休统计不准）；
///   3. **本月填报偏离 8h** —— 少报压低调休余额，超填可能触碰现场填报上限。
///
/// 设计约束（与项目红线一致）：
///   - 「应上班 / 加班」判定**只信** <see cref="DayStatusRules"/> 与 <c>DayStatusService</c> 产出的
///     <see cref="DayStatusItem"/>，本类不自行判断节假日；
///   - 工时口径为**填报工时**（公司口径），与实际工时无关；
///   - 节假日日历不可用时**整体降级**为一条 Info 提示，而不是在错误日历上误报未填。
/// </summary>
public static class InsightAnalyzer
{
    // ===== 分类标识（Kind）=====
    public const string KindHolidayDataUnavailable = "holiday-data-unavailable";
    public const string KindCompLeaveOverdrawn = "comp-leave-overdrawn";
    public const string KindCompLeaveLow = "comp-leave-low";
    public const string KindConsecutiveUnfilled = "consecutive-unfilled";
    public const string KindMonthHoursShort = "month-hours-short";
    public const string KindMonthHoursOver = "month-hours-over";

    /// <summary>公司口径标准日工时（现场填报上限即 8h）。</summary>
    public const double StandardDailyHours = 8.0;

    /// <summary>调休余额低于一天（8h）即视为临界。</summary>
    public const double LowBalanceThreshold = 8.0;

    /// <summary>连续未填：达到 3 天提醒、5 天升级为风险。</summary>
    public const int UnfilledWarnDays = 3;
    public const int UnfilledDangerDays = 5;

    /// <summary>连续未填的回溯窗口（自然日）。</summary>
    public const int UnfilledLookbackDays = 30;

    /// <summary>工时比较容差（浮点误差）。</summary>
    public const double HoursTolerance = 0.01;

    /// <summary>
    /// 聚合入口：产出全部洞察，按严重度降序（同级保持规则顺序：调休 → 未填 → 工时偏离）。
    /// </summary>
    public static List<InsightItem> Analyze(InsightInput input)
    {
        var list = new List<InsightItem>();

        // 节假日日历不可用时，"哪天该上班"本身就判不准（法定假日会被误当作工作日 → 凭空多出未填）。
        // 此时宁可不报，只提示原因，避免用错误的日历制造假警报。
        if (!input.IsHolidayDataAvailable)
        {
            list.Add(new InsightItem
            {
                Kind = KindHolidayDataUnavailable,
                Severity = InsightSeverity.Info,
                Title = "节假日日历未就绪",
                Detail = "联网获取节假日 / 补班日历后，将自动提示调休余额与出勤填报异常。"
            });
            return list;
        }

        if (AnalyzeCompLeave(input.CompLeaveAvailable, input.CompLeaveUsed,
                input.WeekendOvertimeReportHours) is { } comp)
            list.Add(comp);

        if (AnalyzeConsecutiveUnfilled(input.DayStatus, input.Today) is { } unfilled)
            list.Add(unfilled);

        list.AddRange(AnalyzeMonthHours(input.DayStatus, input.Today));

        return list.OrderByDescending(i => i.Severity).ToList();
    }

    /// <summary>
    /// 调休余额临界 / 透支。
    /// 余额 = max(0, 周末加班【填报】- 已用调休)：已用超出周末加班即为透支。
    /// 从未加过班也从未用过调休时返回 null（无话可说，不给噪音）。
    /// </summary>
    public static InsightItem? AnalyzeCompLeave(
        double available, double used, double weekendOvertimeReport)
    {
        if (used <= HoursTolerance && available <= HoursTolerance)
            return null; // 既没加班也没用过调休

        if (available <= HoursTolerance)
        {
            var deficit = used - weekendOvertimeReport;
            return deficit > HoursTolerance
                ? new InsightItem
                {
                    Kind = KindCompLeaveOverdrawn,
                    Severity = InsightSeverity.Danger,
                    Title = $"调休已透支 {deficit:0.#}h",
                    Detail = $"全年周末加班 {weekendOvertimeReport:0.#}h，已用调休 {used:0.#}h，超出部分无余额可抵。",
                    ActionText = "查看出勤",
                    ActionTarget = ViewNames.Attendance
                }
                : new InsightItem
                {
                    Kind = KindCompLeaveLow,
                    Severity = InsightSeverity.Warning,
                    Title = "调休余额已用尽",
                    Detail = $"全年周末加班 {weekendOvertimeReport:0.#}h 已全部用于调休，后续请假请留意。",
                    ActionText = "查看出勤",
                    ActionTarget = ViewNames.Attendance
                };
        }

        if (available < LowBalanceThreshold)
        {
            return new InsightItem
            {
                Kind = KindCompLeaveLow,
                Severity = InsightSeverity.Warning,
                Title = $"调休余额仅剩 {available:0.#}h（不足 1 天）",
                Detail = $"全年周末加班 {weekendOvertimeReport:0.#}h，已用 {used:0.#}h。",
                ActionText = "查看出勤",
                ActionTarget = ViewNames.Attendance
            };
        }

        return null;
    }

    /// <summary>
    /// 连续未填：从最近一个「应上班」工作日往前数，连续多少天既无工作记录也无请假。
    ///
    /// 边界处理：
    ///   - **今天不计入**（当天还没过完，不算漏填）；
    ///   - 只数「应上班」日，周末/法定假日自然跳过，不打断连续段；
    ///   - 遇到第一个有记录或有请假的应上班日即停止计数；
    ///   - 仅保留最近 <see cref="UnfilledLookbackDays"/> 天窗口内的数据。
    /// </summary>
    public static InsightItem? AnalyzeConsecutiveUnfilled(
        IEnumerable<DayStatusItem> dayStatus, DateTime today, int lookbackDays = UnfilledLookbackDays)
    {
        var todayDate = today.Date;
        var from = todayDate.AddDays(-lookbackDays);

        var workdays = dayStatus
            .Where(i => i.IsWorkdayDue && i.Date.Date < todayDate && i.Date.Date >= from)
            .OrderByDescending(i => i.Date)
            .ToList();
        if (workdays.Count == 0) return null;

        var streak = new List<DateTime>();
        foreach (var day in workdays)
        {
            if (!day.IsUnfilled) break;
            streak.Add(day.Date);
        }

        if (streak.Count < UnfilledWarnDays) return null;

        var first = streak.Min();
        var last = streak.Max();
        var range = first == last ? $"{first:MM-dd}" : $"{first:MM-dd} ~ {last:MM-dd}";
        var severity = streak.Count >= UnfilledDangerDays
            ? InsightSeverity.Danger
            : InsightSeverity.Warning;

        return new InsightItem
        {
            Kind = KindConsecutiveUnfilled,
            Severity = severity,
            Title = $"连续 {streak.Count} 个工作日未填工作记录",
            Detail = $"{range} 期间无记录也无请假，补录后出勤与调休统计才准确。",
            ActionText = "去补录",
            ActionTarget = ViewNames.WorkRecord
        };
    }

    /// <summary>
    /// 本月填报偏离 8h：统计本月内已填的「应上班」工作日里，填报工时不足 / 超过 8h 的天数。
    /// 少报会压低调休余额（公司按填报口径计算），超填则可能触碰现场填报上限，故分开提示。
    /// </summary>
    public static IReadOnlyList<InsightItem> AnalyzeMonthHours(
        IEnumerable<DayStatusItem> dayStatus, DateTime today)
    {
        var todayDate = today.Date;
        var monthStart = new DateTime(todayDate.Year, todayDate.Month, 1);

        // 只统计「应上班 + 已填 + 属正常工作日」的日期：
        // 周末/假日加班不计入（那本就不是标准 8h 的工作日）；
        // 未填日不在此处重复提示（连续未填规则已负责）。
        var filled = dayStatus
            .Where(i => i.IsWorkdayDue
                        && i.WorkKind == WorkKind.Workday
                        && i.Date.Date >= monthStart
                        && i.Date.Date <= todayDate)
            .ToList();

        var results = new List<InsightItem>();

        var shortDays = filled
            .Where(i => i.WorkHours < StandardDailyHours - HoursTolerance)
            .ToList();
        if (shortDays.Count > 0)
        {
            var gap = shortDays.Sum(i => StandardDailyHours - i.WorkHours);
            results.Add(new InsightItem
            {
                Kind = KindMonthHoursShort,
                Severity = InsightSeverity.Warning,
                Title = $"本月 {shortDays.Count} 天填报不足 8h，合计少报 {gap:0.#}h",
                Detail = "调休余额按填报工时计算，少报会让余额偏低；若属实可忽略。",
                ActionText = "查看出勤",
                ActionTarget = ViewNames.Attendance
            });
        }

        var overDays = filled
            .Where(i => i.WorkHours > StandardDailyHours + HoursTolerance)
            .ToList();
        if (overDays.Count > 0)
        {
            var extra = overDays.Sum(i => i.WorkHours - StandardDailyHours);
            results.Add(new InsightItem
            {
                Kind = KindMonthHoursOver,
                Severity = InsightSeverity.Info,
                Title = $"本月 {overDays.Count} 天填报超过 8h，合计多出 {extra:0.#}h",
                Detail = "若现场有每日 8h 填报上限，超填部分可能需调整。",
                ActionText = "查看出勤",
                ActionTarget = ViewNames.Attendance
            });
        }

        return results;
    }
}
