namespace EapWorkAssistant.Models;

/// <summary>
/// 工作侧性质（与请假解耦：同一天可以既有工作记录又有请假，双标记同时展示）。
/// </summary>
public enum WorkKind
{
    /// <summary>工作日有记录（补班日也算正常上班）。</summary>
    Workday,

    /// <summary>周末（非补班日）有记录 → 周末加班，可调休。</summary>
    WeekendOvertime,

    /// <summary>法定假日有记录 → 假日加班，300% 工资不可调休。</summary>
    HolidayOvertime,

    /// <summary>当天没有工作记录。</summary>
    NoRecord
}

/// <summary>
/// 一天的出勤状态：由 <see cref="Services.DayStatusService"/> 合并
/// 工作记录、请假记录、节假日日历三个数据源生成。
///
/// 设计要点（需求确认）：同一天既有请假又有工作记录时两侧同时展示；
/// 应上班但无记录也无请假的日子标记为「未填」，方便补录。
/// </summary>
public class DayStatusItem
{
    private static readonly string[] WeekdayNames = ["日", "一", "二", "三", "四", "五", "六"];

    public required DateTime Date { get; init; }

    /// <summary>月-日，如 "10-06"（列表展示用，年份在标题/表头体现）。</summary>
    public string DateText => Date.ToString("MM-dd");

    /// <summary>星期，如 "周三"。</summary>
    public string WeekdayText => $"周{WeekdayNames[(int)Date.DayOfWeek]}";

    /// <summary>是否为周末（周六/周日；补班日也落在周末，需配合 <see cref="IsMakeupWorkday"/> 判断）。</summary>
    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>是否法定假日。</summary>
    public bool IsHolidayDate { get; init; }

    /// <summary>是否补班日（周末补班，属正常工作日）。</summary>
    public bool IsMakeupWorkday { get; init; }

    /// <summary>是否应当上班（工作日或补班日）。</summary>
    public bool IsWorkdayDue { get; init; }

    /// <summary>工作侧性质。</summary>
    public WorkKind WorkKind { get; init; }

    /// <summary>当天工作记录总工时（**填报**口径，报给公司/领导）。</summary>
    public double WorkHours { get; init; }

    /// <summary>
    /// 当天工作记录总工时（**实际**口径，真实投入）。
    /// 与 <see cref="WorkHours"/> 并存：现场填报常被限制在 8h 以内，
    /// 实际工时才是"给自己算账"（调休余额）的依据。
    /// </summary>
    public double ActualHours { get; init; }

    /// <summary>当天工作记录条数。</summary>
    public int RecordCount { get; init; }

    /// <summary>工作内容摘要（首条内容截断；多条时标注条数）。</summary>
    public string? ContentSummary { get; init; }

    /// <summary>请假类型；null 表示当天无请假。</summary>
    public string? LeaveType { get; init; }

    /// <summary>当天请假总工时。</summary>
    public double LeaveHours { get; init; }

    /// <summary>工作工时展示文本（填报口径）；0 值返回空（未填/休息日不显示 0.0h 噪音）。</summary>
    public string WorkHoursText => WorkHours > 0 ? $"{WorkHours:F1}h" : "";

    /// <summary>实际工时展示文本；未填（0）显示「—」，与工作记录页保持一致。</summary>
    public string ActualHoursText => ActualHours > 0 ? $"{ActualHours:F1}h" : "—";

    /// <summary>实际工时是否与填报工时有差异（用于提示"多干了没报"的日子）。</summary>
    public bool HasHoursDiff => ActualHours > 0 && Math.Abs(ActualHours - WorkHours) > 0.01;

    /// <summary>请假徽标文案：`请假·调休 8.0h`；无请假返回空串（整块不渲染），无工时时省略工时。</summary>
    public string LeaveBadgeText => HasLeave
        ? LeaveHours > 0 ? $"请假·{LeaveType} {LeaveHours:0.#}h" : $"请假·{LeaveType}"
        : "";

    /// <summary>是否请假（与工作记录独立，同一天可同时为 true）。</summary>
    public bool HasLeave => LeaveType is not null;

    /// <summary>是否「未填」：应上班、无工作记录、也无请假。</summary>
    public bool IsUnfilled => WorkKind == WorkKind.NoRecord && !HasLeave && IsWorkdayDue;

    /// <summary>是否普通周末/假日休息（无记录无请假且不应上班）。</summary>
    public bool IsRestDay => WorkKind == WorkKind.NoRecord && !HasLeave && !IsWorkdayDue;

    /// <summary>
    /// 是否「法定假日休息」（无记录无请假）—— 需与普通周末区分：
    /// 日历上法定假日是紫色圆点、普通周末是灰色，出勤总览的徽标沿用同一语义。
    /// </summary>
    public bool IsHolidayRest => WorkKind == WorkKind.NoRecord && IsHolidayDate && !HasLeave;

    /// <summary>完整信息 ToolTip：日期 + 星期 + 工作侧 + 请假侧 + 内容摘要。</summary>
    public string TooltipText
    {
        get
        {
            var parts = new List<string> { $"{Date:yyyy-MM-dd} {WeekdayText}" };
            if (IsMakeupWorkday) parts.Add("补班日");
            if (IsHolidayDate) parts.Add("法定假日");
            if (WorkKind != WorkKind.NoRecord)
            {
                // 双口径有差异时把"填报 / 实际"都讲清楚，避免只看到一个数字造成误解
                var hoursText = HasHoursDiff
                    ? $"填报 {WorkHours:0.#}h / 实际 {ActualHours:0.#}h"
                    : $"{WorkHours:0.#}h";
                parts.Add($"{WorkBadgeText} {hoursText}" + (RecordCount > 1 ? $"（{RecordCount} 条）" : ""));
            }
            if (HasLeave)
                parts.Add($"请假·{LeaveType} {LeaveHours:0.#}h");
            if (IsUnfilled) parts.Add("未填记录（双击去补录）");
            if (ContentSummary is { Length: > 0 } summary) parts.Add(summary);
            return string.Join(" · ", parts);
        }
    }

    /// <summary>工作侧徽标文案。</summary>
    public string WorkBadgeText => WorkKind switch
    {
        WorkKind.Workday => IsMakeupWorkday ? "补班上班" : "正常上班",
        WorkKind.WeekendOvertime => "周末加班",
        WorkKind.HolidayOvertime => "假日加班",
        _ => IsUnfilled ? "未填" : IsHolidayDate ? "法定假日" : "周末"
    };
}

/// <summary>
/// 出勤月度热力图格子（P1 ⑦）。与 <see cref="CalendarDayItem"/> 共享同一套
/// DotColor 语义（green/red/blue/amber/purple/gray/请假类型），由
/// <see cref="AttendanceViewModel"/> 按与 CustomCalendar 一致的优先级推导。
/// </summary>
public class HeatmapCell
{
    /// <summary>日（1-31）；非当月补齐格为 0。</summary>
    public int Day { get; init; }

    public DateTime Date { get; init; }

    /// <summary>是否属于当前展示月份（false = 首尾补齐的邻月灰格）。</summary>
    public bool IsCurrentMonth { get; init; }

    public bool IsToday { get; init; }

    /// <summary>
    /// 状态色标识：green 已填 / red 未填 / blue 周末加班 / amber 假日加班 /
    /// purple 法定假日休息 / gray 普通周末 / 请假类型名；空 = 无状态（补齐格或区间外）。
    /// </summary>
    public string DotColor { get; init; } = "";

    /// <summary>完整信息 ToolTip（复用 <see cref="DayStatusItem.TooltipText"/>；无数据时给出日期）。</summary>
    public string Tooltip { get; init; } = "";

    /// <summary>对应的日状态（区间外/无数据为 null）；点击格子时据此跳转工作记录。</summary>
    public DayStatusItem? Item { get; init; }
}

/// <summary>
/// 出勤汇总统计（统计卡数据源；同一天可同时计入「正常上班」与「请假」）。
/// 工时按**填报 / 实际**双口径分别累计，供卡片展示两套数字。
/// </summary>
public sealed record AttendanceSummary(
    int NormalWorkdays,
    double NormalHours,
    int WeekendOvertimeDays,
    double WeekendOvertimeHours,
    int HolidayOvertimeDays,
    double HolidayOvertimeHours,
    int LeaveDays,
    double LeaveHours,
    int UnfilledDays,
    int RestDays)
{
    /// <summary>正常上班实际工时（合计）</summary>
    public double ActualNormalHours { get; init; }

    /// <summary>周末加班实际工时（合计）</summary>
    public double ActualWeekendOvertimeHours { get; init; }

    /// <summary>法定假日加班实际工时（合计）</summary>
    public double ActualHolidayOvertimeHours { get; init; }
}
