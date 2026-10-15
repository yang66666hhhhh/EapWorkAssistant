using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// P1 ⑦ 出勤月度热力图：DotColor 推导优先级（与 CustomCalendar 同一套语义）、
/// 格子点击跳转契约、月份导航边界，以及 BuildHeatmap 集成（临时 SQLite 库 + LoadAsync）。
/// </summary>
public class AttendanceHeatmapTests
{
    private static AttendanceViewModel NewViewModel()
        => new(new DayStatusService(new WorkRecordRepository(), new LeaveRecordRepository()),
               new CompLeaveBalanceService(new WorkRecordRepository(), new LeaveRecordRepository()));

    private static DayStatusItem Day(
        DateTime date,
        WorkKind kind = WorkKind.NoRecord,
        bool isWorkdayDue = false,
        bool isHoliday = false,
        bool isMakeup = false,
        string? leaveType = null)
        => new()
        {
            Date = date,
            WorkKind = kind,
            IsWorkdayDue = isWorkdayDue,
            IsHolidayDate = isHoliday,
            IsMakeupWorkday = isMakeup,
            LeaveType = leaveType
        };

    // ===== DotColor 推导优先级（复刻 CustomCalendar 圆点语义）=====

    [Fact]
    public void ToDotColor_假日加班_琥珀()
        => Assert.Equal("amber", AttendanceViewModel.ToDotColor(
            Day(new DateTime(2026, 10, 1), WorkKind.HolidayOvertime, isHoliday: true)));

    [Fact]
    public void ToDotColor_工作日已填_绿()
        => Assert.Equal("green", AttendanceViewModel.ToDotColor(
            Day(new DateTime(2026, 10, 8), WorkKind.Workday, isWorkdayDue: true)));

    [Fact]
    public void ToDotColor_补班日已填_也是绿()
        => Assert.Equal("green", AttendanceViewModel.ToDotColor(
            Day(new DateTime(2026, 10, 10), WorkKind.Workday, isWorkdayDue: true, isMakeup: true)));

    [Fact]
    public void ToDotColor_周末加班无请假_蓝()
        => Assert.Equal("blue", AttendanceViewModel.ToDotColor(
            Day(new DateTime(2026, 10, 11), WorkKind.WeekendOvertime)));

    [Fact]
    public void ToDotColor_周末加班有请假_请假色优先()
        => Assert.Equal("调休", AttendanceViewModel.ToDotColor(
            Day(new DateTime(2026, 10, 11), WorkKind.WeekendOvertime, leaveType: "调休")));

    [Fact]
    public void ToDotColor_无记录有请假_请假色()
        => Assert.Equal("年假", AttendanceViewModel.ToDotColor(
            Day(new DateTime(2026, 10, 8), WorkKind.NoRecord, leaveType: "年假")));

    [Fact]
    public void ToDotColor_补班日未填_红()
        => Assert.Equal("red", AttendanceViewModel.ToDotColor(
            Day(new DateTime(2026, 10, 10), WorkKind.NoRecord, isWorkdayDue: true, isMakeup: true)));

    [Fact]
    public void ToDotColor_普通工作日未填_红()
        => Assert.Equal("red", AttendanceViewModel.ToDotColor(
            Day(new DateTime(2026, 10, 8), WorkKind.NoRecord, isWorkdayDue: true)));

    [Fact]
    public void ToDotColor_法定假日无记录_紫()
        => Assert.Equal("purple", AttendanceViewModel.ToDotColor(
            Day(new DateTime(2026, 10, 1), WorkKind.NoRecord, isHoliday: true)));

    [Fact]
    public void ToDotColor_法定假日落在周末_仍是紫()
        => Assert.Equal("purple", AttendanceViewModel.ToDotColor(
            Day(new DateTime(2026, 10, 4), WorkKind.NoRecord, isHoliday: true)));

    [Fact]
    public void ToDotColor_普通周末无记录_灰()
        => Assert.Equal("gray", AttendanceViewModel.ToDotColor(
            Day(new DateTime(2026, 10, 4), WorkKind.NoRecord)));

    // ===== 点击格子跳转契约 =====

    [Fact]
    public void OpenHeatmapDay_带Item_转发当天日期()
    {
        var vm = NewViewModel();
        DateTime? captured = null;
        vm.NavigateToWorkRecord += d => captured = d;

        var target = new DateTime(2026, 10, 8);
        vm.OpenHeatmapDay(new HeatmapCell { Day = 8, Date = target, IsCurrentMonth = true, Item = Day(target) });

        Assert.Equal(target, captured);
    }

    [Fact]
    public void OpenHeatmapDay_区间外灰格无Item_不转发()
    {
        var vm = NewViewModel();
        var fired = false;
        vm.NavigateToWorkRecord += _ => fired = true;

        vm.OpenHeatmapDay(new HeatmapCell { Day = 30, Date = new DateTime(2026, 9, 30), IsCurrentMonth = false });

        Assert.False(fired);
    }

    [Fact]
    public void OpenHeatmapDay_传入空_不抛异常()
    {
        var vm = NewViewModel();
        vm.OpenHeatmapDay(null); // 不抛即通过
    }

    // ===== 月份导航边界（未加载数据时全区不可翻）=====

    [Fact]
    public void 月份导航_未加载_两个方向都不可点()
    {
        var vm = NewViewModel();

        Assert.False(vm.CanPrevMonth);
        Assert.False(vm.CanNextMonth);

        vm.PrevHeatmapMonthCommand.Execute(null);
        vm.NextHeatmapMonthCommand.Execute(null);

        Assert.False(vm.CanPrevMonth);
        Assert.False(vm.CanNextMonth);
    }

    // ===== BuildHeatmap 集成（临时库 + LoadAsync 全链路）=====

    [Fact]
    public async Task BuildHeatmap_本月模式_格子铺满整周且状态正确()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new WorkRecordRepository();
            var today = DateTime.Today;

            // 先加载当年节假日数据再选日期（HolidayService 未加载时 IsHoliday 恒 false，
            // 会选中 10-01 这种法定假日，导致分类落在 HolidayOvertime 而非 Workday）
            await HolidayService.Instance.LoadYearAsync(today.Year);

            // 当月第一个「普通工作日」（排除周末/法定假日/补班日）插一条 8h 记录 → 应为 green
            var firstWorkday = new DateTime(today.Year, today.Month, 1);
            while (firstWorkday.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                   || HolidayService.Instance.IsHoliday(firstWorkday)
                   || HolidayService.Instance.IsMakeupWorkday(firstWorkday))
                firstWorkday = firstWorkday.AddDays(1);

            // 当月第一个「非假日非补班」周六（10 月 3 日是假日周六、10 日是补班周六，都要排除；
            // 若整月没有这样的周六（极端）则跳过 blue 断言）插一条 4h 记录 → 应为 blue
            var monthEnd = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
            var firstSaturday = new DateTime(today.Year, today.Month, 1);
            while (firstSaturday <= monthEnd
                   && (firstSaturday.DayOfWeek != DayOfWeek.Saturday
                       || HolidayService.Instance.IsHoliday(firstSaturday)
                       || HolidayService.Instance.IsMakeupWorkday(firstSaturday)))
                firstSaturday = firstSaturday.AddDays(1);

            await repo.InsertAsync(Make(firstWorkday.ToString("yyyy-MM-dd"), hours: 8));
            if (firstSaturday <= monthEnd)
                await repo.InsertAsync(Make(firstSaturday.ToString("yyyy-MM-dd"), hours: 4));

            var vm = NewViewModel();
            await vm.LoadCommand.ExecuteAsync(null);

            // 格子数：整周铺满（42 格上限内的 7 的倍数）
            Assert.Equal(0, vm.HeatmapCells.Count % 7);
            Assert.True(vm.HeatmapCells.Count >= 35);
            Assert.True(vm.HeatmapCells.Count <= 42);

            // 当月格子数 = 当月天数
            int daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
            Assert.Equal(daysInMonth, vm.HeatmapCells.Count(c => c.IsCurrentMonth));

            // 有记录的工作日格子：green；有记录的周六格子：blue
            var wd = vm.HeatmapCells.First(c => c.IsCurrentMonth && c.Day == firstWorkday.Day);
            Assert.Equal("green", wd.DotColor);
            Assert.NotNull(wd.Item);
            Assert.False(string.IsNullOrEmpty(wd.Tooltip));

            if (firstSaturday <= monthEnd)
            {
                var sat = vm.HeatmapCells.First(c => c.IsCurrentMonth && c.Day == firstSaturday.Day);
                Assert.Equal("blue", sat.DotColor);
            }

            // 今天格子有标记（今天必然在当月格子中）
            Assert.Contains(vm.HeatmapCells, c => c.IsCurrentMonth && c.IsToday);

            // 「本月」区间只覆盖一个月：两个翻页方向都不可点，标题为当前年月
            Assert.False(vm.CanPrevMonth);
            Assert.False(vm.CanNextMonth);
            Assert.Equal($"{today:yyyy年M月}", vm.HeatmapTitle);
        }
        finally { TestDb.Cleanup(db); }
    }

    [Fact]
    public async Task BuildHeatmap_本年模式_默认展示末月且可向前翻月()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var vm = NewViewModel();
            await vm.SetRangeCommand.ExecuteAsync("本年");

            var today = DateTime.Today;

            // 本年区间覆盖 1-12 月，但默认展示「今天所在月」而非区间末月（12 月是未来月份，无数据）
            Assert.Equal(new DateTime(today.Year, today.Month, 1), vm.HeatmapMonth);
            Assert.Equal($"{today:yyyy年M月}", vm.HeatmapTitle);

            // 当前月可向后翻（到 12 月）；1 月时不可再向前翻
            Assert.True(vm.CanNextMonth);
            if (today.Month == 1)
                Assert.False(vm.CanPrevMonth);
            else
            {
                Assert.True(vm.CanPrevMonth);
                vm.PrevHeatmapMonthCommand.Execute(null);
                Assert.Equal(today.Month - 1, vm.HeatmapMonth.Month);
                Assert.Equal(DateTime.DaysInMonth(today.Year, today.Month - 1),
                    vm.HeatmapCells.Count(c => c.IsCurrentMonth));
                // 翻回当前月
                vm.NextHeatmapMonthCommand.Execute(null);
                Assert.Equal(today.Month, vm.HeatmapMonth.Month);
            }
        }
        finally { TestDb.Cleanup(db); }
    }

    [Fact]
    public async Task BuildHeatmap_跨月自定义区间_月份边界正确()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var vm = NewViewModel();
            // 固定区间 2026-09-10 ~ 2026-10-20（不依赖 today，避免跨年边界）
            vm.RangePreset = "自定义";
            vm.StartDate = new DateTime(2026, 9, 10);
            vm.EndDate = new DateTime(2026, 10, 20);
            await vm.LoadCommand.ExecuteAsync(null);

            // 默认展示区间末月（10 月）；可向前翻到 9 月，9 月不可再向前
            Assert.Equal(new DateTime(2026, 10, 1), vm.HeatmapMonth);
            Assert.True(vm.CanPrevMonth);
            Assert.False(vm.CanNextMonth);

            vm.PrevHeatmapMonthCommand.Execute(null);
            Assert.Equal(new DateTime(2026, 9, 1), vm.HeatmapMonth);
            Assert.False(vm.CanPrevMonth); // 已到区间首月
            Assert.True(vm.CanNextMonth);
        }
        finally { TestDb.Cleanup(db); }
    }

    private static WorkRecord Make(string date, double hours)
        => new WorkRecord
        {
            WorkDate = date,
            ProjectName = "P1",
            WorkType = "开发",
            Content = "热力图测试",
            Achievement = "",
            Problem = "",
            Solution = "",
            Hours = hours,
            Progress = 0,
            IsHighlight = 0,
            HighlightNote = "",
            CreateTime = "2026-10-01 00:00:00"
        };
}
