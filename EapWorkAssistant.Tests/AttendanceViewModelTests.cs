using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// 出勤总览 ViewModel：C1「双击某天 → 跳工作记录补录」的跳转契约。
/// 只验证事件转发与空值防御，不触发查库（LoadAsync 未调用）。
/// </summary>
public class AttendanceViewModelTests
{
    private static AttendanceViewModel NewViewModel()
        => new(new DayStatusService(new WorkRecordRepository(), new LeaveRecordRepository()),
               new CompLeaveBalanceService(new WorkRecordRepository(), new LeaveRecordRepository()));

    [Fact]
    public void OpenDay_带出当天日期()
    {
        var vm = NewViewModel();
        DateTime? captured = null;
        vm.NavigateToWorkRecord += d => captured = d;

        var day = new DateTime(2026, 10, 8);
        vm.OpenDay(new DayStatusItem { Date = day, IsWorkdayDue = true, WorkKind = WorkKind.NoRecord });

        Assert.Equal(day, captured);
    }

    [Fact]
    public void OpenDay_传入空_不触发也不抛异常()
    {
        var vm = NewViewModel();
        var fired = false;
        vm.NavigateToWorkRecord += _ => fired = true;

        vm.OpenDay(null);

        Assert.False(fired);
    }

    // ===== C3 排序契约（只验状态机，_allItems 为空时不查库）=====

    [Fact]
    public void ToggleSort_换列_套用该列默认方向()
    {
        var vm = NewViewModel();

        vm.ToggleSort("WorkHours"); // 默认降序（工时多的在前）

        Assert.Equal("WorkHours", vm.SortColumn);
        Assert.False(vm.SortAscending);
    }

    [Fact]
    public void ToggleSort_同列再次点击_反转方向()
    {
        var vm = NewViewModel();
        // 初始即按日期降序（最近的在最上面），所以点「日期」列头属同列反转
        Assert.Equal("Date", vm.SortColumn);
        Assert.False(vm.SortAscending);

        vm.ToggleSort("Date"); // 同列 → 升序
        Assert.True(vm.SortAscending);

        vm.ToggleSort("Date"); // 再点 → 回落降序
        Assert.False(vm.SortAscending);
    }

    [Fact]
    public void ToggleSort_文本列默认升序()
    {
        var vm = NewViewModel();

        vm.ToggleSort("ContentSummary");

        Assert.True(vm.SortAscending);
    }

    // ===== C2 CSV 生成（导出落盘需弹窗，故只测文本生成）=====

    [Fact]
    public void BuildDayStatusCsv_含BOM表头与行数据()
    {
        var items = new List<DayStatusItem>
        {
            new() { Date = new DateTime(2026, 10, 1), IsWorkdayDue = true, WorkKind = WorkKind.Workday,
                    WorkHours = 8, ActualHours = 10, RecordCount = 1, ContentSummary = "写代码" },
            new() { Date = new DateTime(2026, 10, 8), IsWorkdayDue = true, WorkKind = WorkKind.NoRecord },
        };

        var csv = ExportService.BuildDayStatusCsv(items);

        Assert.StartsWith("\uFEFF", csv);
        Assert.Contains("日期,星期,工作状态,填报工时,实际工时,工作记录数,请假类型,请假工时,内容摘要", csv);
        // 填报 8 / 实际 10 —— 双口径都要落进 CSV
        Assert.Contains("2026-10-01,周四,正常上班,8,10,1,,0,写代码", csv);
        Assert.Contains("2026-10-08,周四,未填,0,0,0,,0,", csv);
    }

    [Fact]
    public void BuildDayStatusCsv_含逗号的内容被引号包裹()
    {
        var items = new List<DayStatusItem>
        {
            new() { Date = new DateTime(2026, 10, 2), WorkKind = WorkKind.WeekendOvertime,
                    WorkHours = 4, ActualHours = 4, RecordCount = 1, ContentSummary = "巡检A,巡检B" },
        };

        var csv = ExportService.BuildDayStatusCsv(items);

        Assert.Contains("\"巡检A,巡检B\"", csv);
    }
}
