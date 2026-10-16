using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Services;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// 主动异常洞察分析器测试（P2 ⑨）。
///
/// 三条规则（调休余额临界 / 连续未填 / 本月填报偏离 8h）都是纯函数，
/// 这里锁定阈值、边界（今天不算漏填、请假中断连续段、周末不打断）与降级行为。
/// </summary>
public class InsightAnalyzerTests
{
    /// <summary>构造一条出勤日状态（只填本用例关心的字段）。</summary>
    private static DayStatusItem Day(
        DateTime date,
        bool workdayDue = true,
        WorkKind kind = WorkKind.NoRecord,
        double hours = 0,
        string? leaveType = null)
        => new()
        {
            Date = date,
            IsWorkdayDue = workdayDue,
            WorkKind = kind,
            WorkHours = hours,
            LeaveType = leaveType,
            LeaveHours = leaveType is null ? 0 : 8
        };

    // ==================== 规则 1：调休余额临界 / 透支 ====================

    [Fact]
    public void AnalyzeCompLeave_余额充足_不产出()
    {
        var item = InsightAnalyzer.AnalyzeCompLeave(available: 16, used: 8, weekendOvertimeReport: 24);
        Assert.Null(item);
    }

    [Fact]
    public void AnalyzeCompLeave_从未加班也从未调休_不产出()
    {
        var item = InsightAnalyzer.AnalyzeCompLeave(available: 0, used: 0, weekendOvertimeReport: 0);
        Assert.Null(item);
    }

    [Fact]
    public void AnalyzeCompLeave_余额不足一天_告警()
    {
        var item = InsightAnalyzer.AnalyzeCompLeave(available: 4, used: 20, weekendOvertimeReport: 24);

        Assert.NotNull(item);
        Assert.Equal(InsightAnalyzer.KindCompLeaveLow, item!.Kind);
        Assert.Equal(InsightSeverity.Warning, item.Severity);
        Assert.Equal("调休余额仅剩 4h（不足 1 天）", item.Title);
        Assert.Equal(ViewNames.Attendance, item.ActionTarget);
    }

    [Fact]
    public void AnalyzeCompLeave_已用超出周末加班_透支告警()
    {
        var item = InsightAnalyzer.AnalyzeCompLeave(available: 0, used: 30, weekendOvertimeReport: 24);

        Assert.NotNull(item);
        Assert.Equal(InsightAnalyzer.KindCompLeaveOverdrawn, item!.Kind);
        Assert.Equal(InsightSeverity.Danger, item.Severity);
        Assert.Equal("调休已透支 6h", item.Title);
    }

    [Fact]
    public void AnalyzeCompLeave_余额恰好用尽_提示用尽而非透支()
    {
        var item = InsightAnalyzer.AnalyzeCompLeave(available: 0, used: 24, weekendOvertimeReport: 24);

        Assert.NotNull(item);
        Assert.Equal(InsightAnalyzer.KindCompLeaveLow, item!.Kind);
        Assert.Equal(InsightSeverity.Warning, item.Severity);
        Assert.Equal("调休余额已用尽", item.Title);
    }

    // ==================== 规则 2：连续未填 ====================

    [Fact]
    public void AnalyzeConsecutiveUnfilled_不足三天_不产出()
    {
        var today = new DateTime(2026, 10, 16); // 周五
        var items = new[]
        {
            Day(new DateTime(2026, 10, 15)), // 周四 未填
            Day(new DateTime(2026, 10, 14)), // 周三 未填
        };

        Assert.Null(InsightAnalyzer.AnalyzeConsecutiveUnfilled(items, today));
    }

    [Fact]
    public void AnalyzeConsecutiveUnfilled_连续三天_告警并给出区间()
    {
        var today = new DateTime(2026, 10, 19); // 周一（本身未填也不计入）
        var items = new[]
        {
            Day(new DateTime(2026, 10, 14)),                      // 周三 未填
            Day(new DateTime(2026, 10, 15)),                      // 周四 未填
            Day(new DateTime(2026, 10, 16)),                      // 周五 未填
            Day(new DateTime(2026, 10, 17), workdayDue: false),   // 周六 休息：应被跳过，不得打断连续段
            Day(new DateTime(2026, 10, 18), workdayDue: false),   // 周日 休息
        };

        var item = InsightAnalyzer.AnalyzeConsecutiveUnfilled(items, today);

        Assert.NotNull(item);
        Assert.Equal(InsightAnalyzer.KindConsecutiveUnfilled, item!.Kind);
        Assert.Equal(InsightSeverity.Warning, item.Severity);
        Assert.Equal("连续 3 个工作日未填工作记录", item.Title);
        Assert.Contains("10-14 ~ 10-16", item.Detail);
        Assert.Equal(ViewNames.WorkRecord, item.ActionTarget);
    }

    [Fact]
    public void AnalyzeConsecutiveUnfilled_连续五天_升级为风险()
    {
        var today = new DateTime(2026, 10, 19); // 周一
        var items = new[]
        {
            Day(new DateTime(2026, 10, 12)), // 周一
            Day(new DateTime(2026, 10, 13)), // 周二
            Day(new DateTime(2026, 10, 14)), // 周三
            Day(new DateTime(2026, 10, 15)), // 周四
            Day(new DateTime(2026, 10, 16)), // 周五
        };

        var item = InsightAnalyzer.AnalyzeConsecutiveUnfilled(items, today);

        Assert.NotNull(item);
        Assert.Equal(InsightSeverity.Danger, item!.Severity);
        Assert.Equal("连续 5 个工作日未填工作记录", item.Title);
    }

    [Fact]
    public void AnalyzeConsecutiveUnfilled_今天未填不计入()
    {
        var today = new DateTime(2026, 10, 16); // 周五
        var items = new[]
        {
            Day(new DateTime(2026, 10, 13)), // 周二 未填
            Day(new DateTime(2026, 10, 14)), // 周三 未填
            Day(new DateTime(2026, 10, 15)), // 周四 未填
            Day(new DateTime(2026, 10, 16)), // 周五 = 今天，不得计入
        };

        var item = InsightAnalyzer.AnalyzeConsecutiveUnfilled(items, today);

        Assert.NotNull(item);
        // 若把今天算进去会变成 4 天
        Assert.Equal("连续 3 个工作日未填工作记录", item!.Title);
    }

    [Fact]
    public void AnalyzeConsecutiveUnfilled_遇请假即中断()
    {
        var today = new DateTime(2026, 10, 16); // 周五
        var items = new[]
        {
            Day(new DateTime(2026, 10, 15)),                                // 周四 未填
            Day(new DateTime(2026, 10, 14), leaveType: "年假"),              // 周三 请假：中断
            Day(new DateTime(2026, 10, 13)),                                // 周二 未填
        };

        Assert.Null(InsightAnalyzer.AnalyzeConsecutiveUnfilled(items, today));
    }

    [Fact]
    public void AnalyzeConsecutiveUnfilled_遇有记录即中断()
    {
        var today = new DateTime(2026, 10, 16); // 周五
        var items = new[]
        {
            Day(new DateTime(2026, 10, 15), kind: WorkKind.Workday, hours: 8), // 周四 有记录：最先遇到 → 断
            Day(new DateTime(2026, 10, 14)),
            Day(new DateTime(2026, 10, 13)),
        };

        Assert.Null(InsightAnalyzer.AnalyzeConsecutiveUnfilled(items, today));
    }

    [Fact]
    public void AnalyzeConsecutiveUnfilled_超出回溯窗口不计入()
    {
        var today = new DateTime(2026, 10, 16); // 周五
        // 45 天前起连续未填：整段都在 30 天窗口之外
        var start = today.AddDays(-45);
        var items = Enumerable.Range(0, 3)
            .Select(i => Day(start.AddDays(i)))
            .ToList();

        Assert.Null(InsightAnalyzer.AnalyzeConsecutiveUnfilled(items, today));
    }

    // ==================== 规则 3：本月填报偏离 8h ====================

    [Fact]
    public void AnalyzeMonthHours_全部满八小时_不产出()
    {
        var today = new DateTime(2026, 10, 16);
        var items = new[]
        {
            Day(new DateTime(2026, 10, 13), kind: WorkKind.Workday, hours: 8),
            Day(new DateTime(2026, 10, 14), kind: WorkKind.Workday, hours: 8),
        };

        Assert.Empty(InsightAnalyzer.AnalyzeMonthHours(items, today));
    }

    [Fact]
    public void AnalyzeMonthHours_少报_告警()
    {
        var today = new DateTime(2026, 10, 16);
        var items = new[]
        {
            Day(new DateTime(2026, 10, 13), kind: WorkKind.Workday, hours: 6),
            Day(new DateTime(2026, 10, 14), kind: WorkKind.Workday, hours: 7),
        };

        var results = InsightAnalyzer.AnalyzeMonthHours(items, today);

        var item = Assert.Single(results);
        Assert.Equal(InsightAnalyzer.KindMonthHoursShort, item.Kind);
        Assert.Equal(InsightSeverity.Warning, item.Severity);
        Assert.Equal("本月 2 天填报不足 8h，合计少报 3h", item.Title);
    }

    [Fact]
    public void AnalyzeMonthHours_超填_提示()
    {
        var today = new DateTime(2026, 10, 16);
        var items = new[]
        {
            Day(new DateTime(2026, 10, 13), kind: WorkKind.Workday, hours: 8),
            Day(new DateTime(2026, 10, 14), kind: WorkKind.Workday, hours: 10),
        };

        var results = InsightAnalyzer.AnalyzeMonthHours(items, today);

        var item = Assert.Single(results);
        Assert.Equal(InsightAnalyzer.KindMonthHoursOver, item.Kind);
        Assert.Equal(InsightSeverity.Info, item.Severity);
        Assert.Equal("本月 1 天填报超过 8h，合计多出 2h", item.Title);
    }

    [Fact]
    public void AnalyzeMonthHours_不计入周末加班与未填日()
    {
        var today = new DateTime(2026, 10, 16);
        var items = new[]
        {
            // 周末加班：本就不是标准 8h 工作日，且非「应上班」
            Day(new DateTime(2026, 10, 10), workdayDue: false, kind: WorkKind.WeekendOvertime, hours: 10),
            // 应上班但未填：由"连续未填"规则负责，此处不重复提示
            Day(new DateTime(2026, 10, 13)),
        };

        Assert.Empty(InsightAnalyzer.AnalyzeMonthHours(items, today));
    }

    [Fact]
    public void AnalyzeMonthHours_只统计本月_上月记录不参与()
    {
        var today = new DateTime(2026, 10, 16);
        var items = new[]
        {
            Day(new DateTime(2026, 9, 30), kind: WorkKind.Workday, hours: 6), // 上月：不计
            Day(new DateTime(2026, 10, 13), kind: WorkKind.Workday, hours: 8),
        };

        Assert.Empty(InsightAnalyzer.AnalyzeMonthHours(items, today));
    }

    // ==================== 聚合入口 ====================

    private static InsightInput Input(
        IReadOnlyList<DayStatusItem> dayStatus,
        DateTime today,
        bool holidayAvailable = true,
        double available = 16,
        double used = 8,
        double weekend = 24)
        => new(holidayAvailable, available, used, weekend, dayStatus, today);

    [Fact]
    public void Analyze_节假日数据不可用_降级为单条提示()
    {
        var today = new DateTime(2026, 10, 16);
        var results = InsightAnalyzer.Analyze(Input(Array.Empty<DayStatusItem>(), today, holidayAvailable: false));

        var item = Assert.Single(results);
        Assert.Equal(InsightAnalyzer.KindHolidayDataUnavailable, item.Kind);
        Assert.Equal(InsightSeverity.Info, item.Severity);
    }

    [Fact]
    public void Analyze_按严重度降序排列()
    {
        var today = new DateTime(2026, 10, 16); // 周五
        var items = new[]
        {
            Day(new DateTime(2026, 10, 13)), // 周二 未填
            Day(new DateTime(2026, 10, 14)), // 周三 未填
            Day(new DateTime(2026, 10, 15)), // 周四 未填
        };

        // 调休透支（Danger） + 连续 3 天未填（Warning）
        var results = InsightAnalyzer.Analyze(
            Input(items, today, available: 0, used: 30, weekend: 24));

        Assert.Equal(2, results.Count);
        Assert.Equal(InsightSeverity.Danger, results[0].Severity);
        Assert.Equal(InsightAnalyzer.KindCompLeaveOverdrawn, results[0].Kind);
        Assert.Equal(InsightSeverity.Warning, results[1].Severity);
        Assert.Equal(InsightAnalyzer.KindConsecutiveUnfilled, results[1].Kind);
    }

    [Fact]
    public void Analyze_日常健康状态_无洞察()
    {
        var today = new DateTime(2026, 10, 16);
        var items = new[]
        {
            Day(new DateTime(2026, 10, 15), kind: WorkKind.Workday, hours: 8),
        };

        Assert.Empty(InsightAnalyzer.Analyze(Input(items, today)));
    }
}
