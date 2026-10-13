using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// DayStatusRules 纯规则单测：加班/应上班判定的唯一事实来源。
/// </summary>
public class DayStatusRulesTests
{
    private static readonly Func<DateTime, bool> NoHoliday = _ => false;
    private static readonly Func<DateTime, bool> NoMakeup = _ => false;
    private static readonly Func<DateTime, bool> IsMakeup = d => d == new DateTime(2026, 10, 10);

    [Fact]
    public void Classify_工作日有记录_算正常上班()
    {
        var kind = DayStatusRules.Classify(new DateTime(2026, 10, 8), true, false, false);
        Assert.Equal(WorkKind.Workday, kind);
    }

    [Fact]
    public void Classify_周末有记录_算周末加班()
    {
        var kind = DayStatusRules.Classify(new DateTime(2026, 10, 11), true, false, false);
        Assert.Equal(WorkKind.WeekendOvertime, kind);
    }

    [Fact]
    public void Classify_法定假日有记录_算假日加班()
    {
        // 10-06 是国庆法定假日（周二）
        var kind = DayStatusRules.Classify(new DateTime(2026, 10, 6), true, true, false);
        Assert.Equal(WorkKind.HolidayOvertime, kind);
    }

    [Fact]
    public void Classify_补班日有记录_算正常上班而非加班()
    {
        // 10-10 是补班日（周六），有记录 = 正常上班
        var kind = DayStatusRules.Classify(new DateTime(2026, 10, 10), true, false, true);
        Assert.Equal(WorkKind.Workday, kind);
    }

    [Fact]
    public void Classify_无记录_一律NoRecord()
    {
        Assert.Equal(WorkKind.NoRecord, DayStatusRules.Classify(new DateTime(2026, 10, 11), false, false, false));
        Assert.Equal(WorkKind.NoRecord, DayStatusRules.Classify(new DateTime(2026, 10, 6), false, true, false));
        Assert.Equal(WorkKind.NoRecord, DayStatusRules.Classify(new DateTime(2026, 10, 8), false, false, false));
    }

    [Fact]
    public void IsWorkdayDue_补班日算应上班_法定假日不算()
    {
        Assert.True(DayStatusRules.IsWorkdayDue(new DateTime(2026, 10, 8), false, false));   // 周四工作日
        Assert.True(DayStatusRules.IsWorkdayDue(new DateTime(2026, 10, 10), false, true));   // 补班周六
        Assert.False(DayStatusRules.IsWorkdayDue(new DateTime(2026, 10, 11), false, false)); // 普通周日

        // 法定假日即使落在工作日也不应上班（否则出勤总览会把国庆误报成「未填」）
        Assert.False(DayStatusRules.IsWorkdayDue(new DateTime(2026, 10, 1), true, false));   // 国庆（周四）
        Assert.False(DayStatusRules.IsWorkdayDue(new DateTime(2026, 10, 2), true, false));   // 国庆（周五）
        Assert.False(DayStatusRules.IsWorkdayDue(new DateTime(2026, 10, 5), true, false));   // 国庆（周一）
    }

    /// <summary>
    /// 回归：法定假日落在工作日时，无记录必须判为「休息」而不是「未填」，
    /// 徽标文案应为「法定假日」。这是 2026-10-13 出勤总览国庆标签错乱的根因。
    /// </summary>
    [Fact]
    public void 法定假日落在工作日_无记录不判未填()
    {
        // 2026-10-05 是周一（工作日）+ 法定假日 + 无记录
        var holiday = new DayStatusItem
        {
            Date = new DateTime(2026, 10, 5),
            IsHolidayDate = true,
            IsWorkdayDue = DayStatusRules.IsWorkdayDue(new DateTime(2026, 10, 5), true, false),
            WorkKind = WorkKind.NoRecord
        };

        Assert.False(holiday.IsUnfilled);
        Assert.True(holiday.IsRestDay);
        Assert.Equal("法定假日", holiday.WorkBadgeText);
    }

    [Fact]
    public void Classify_与CompLeaveCalculator口径一致_周末与假日拆分()
    {
        // 同一份规则驱动两处消费：周末→可调休，假日→不可调休
        var weekend = DayStatusRules.Classify(new DateTime(2026, 10, 11), true, false, false);
        var holiday = DayStatusRules.Classify(new DateTime(2026, 10, 6), true, true, false);
        Assert.Equal(WorkKind.WeekendOvertime, weekend);
        Assert.Equal(WorkKind.HolidayOvertime, holiday);
        Assert.NotEqual(weekend, holiday);
    }
}

/// <summary>
/// DayStatusService 集成测试：三源合并（工作记录 + 请假 + 节假日日历）。
/// 使用临时 SQLite 库 + HolidayService 内置兜底数据（2026 年节假日）。
/// </summary>
public class DayStatusServiceTests
{
    private static DayStatusService NewService()
        => new(new WorkRecordRepository(), new LeaveRecordRepository());

    [Fact]
    public async Task GetRangeAsync_国庆周_状态与真实节假日对齐()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var recordRepo = new WorkRecordRepository();
            var leaveRepo = new LeaveRecordRepository();

            // 10-06/07 法定假日加班；10-10 补班日上班；10-11 周末加班
            await recordRepo.InsertAsync(Rec("2026-10-06", 8));
            await recordRepo.InsertAsync(Rec("2026-10-07", 9));
            await recordRepo.InsertAsync(Rec("2026-10-10", 4));
            await recordRepo.InsertAsync(Rec("2026-10-11", 9));
            // 09-30 请假（调休），当天也有工作记录 → 双标记
            await recordRepo.InsertAsync(Rec("2026-09-30", 4));
            await leaveRepo.InsertAsync(new LeaveRecord { Date = "2026-09-30", LeaveType = "调休", Hours = 8 });

            var items = await NewService().GetRangeAsync(
                new DateTime(2026, 9, 30), new DateTime(2026, 10, 11));

            var byDate = items.ToDictionary(i => i.Date);

            Assert.Equal(WorkKind.HolidayOvertime, byDate[new DateTime(2026, 10, 6)].WorkKind);
            Assert.Equal("假日加班", byDate[new DateTime(2026, 10, 6)].WorkBadgeText);
            Assert.Equal(17, byDate[new DateTime(2026, 10, 6)].WorkHours + byDate[new DateTime(2026, 10, 7)].WorkHours, 3);

            Assert.Equal(WorkKind.Workday, byDate[new DateTime(2026, 10, 10)].WorkKind);
            Assert.Equal("补班上班", byDate[new DateTime(2026, 10, 10)].WorkBadgeText);

            Assert.Equal(WorkKind.WeekendOvertime, byDate[new DateTime(2026, 10, 11)].WorkKind);
            Assert.Equal("周末加班", byDate[new DateTime(2026, 10, 11)].WorkBadgeText);

            // 双标记：工作 + 请假同时展示
            var dual = byDate[new DateTime(2026, 9, 30)];
            Assert.True(dual.HasLeave);
            Assert.Equal("调休", dual.LeaveType);
            Assert.Equal(8, dual.LeaveHours, 3);
            Assert.Equal(4, dual.WorkHours, 3);
            Assert.Equal(WorkKind.Workday, dual.WorkKind);
            // 请假徽标文案（只读计算属性，仅经 TextBlock.Text/显式 OneWay 绑定，不得直接绑 Run.Text）
            Assert.Equal("请假·调休 8h", dual.LeaveBadgeText);

            // 10-08 工作日无记录无请假 → 未填
            var unfilled = byDate[new DateTime(2026, 10, 8)];
            Assert.True(unfilled.IsUnfilled);
            Assert.Equal("未填", unfilled.WorkBadgeText);
            Assert.Equal("", unfilled.LeaveBadgeText);

            // 10-03/04 法定假日无记录 → 休息（非未填）
            var rest = byDate[new DateTime(2026, 10, 3)];
            Assert.True(rest.IsRestDay);
            Assert.False(rest.IsUnfilled);

            // 10-01/02/05 是「落在工作日上」的国庆假日 → 同样必须是「法定假日」而非「未填」
            // （回归：IsWorkdayDue 曾漏判法定假日，导致国庆工作日整片显示为未填）
            foreach (var day in new[] { 1, 2, 5 })
            {
                var d = byDate[new DateTime(2026, 10, day)];
                Assert.True(d.IsHolidayDate);
                Assert.False(d.IsUnfilled);
                Assert.True(d.IsRestDay);
                Assert.True(d.IsHolidayRest);   // 紫标（区别于普通周末）
                Assert.Equal("法定假日", d.WorkBadgeText);
            }
        }
        finally
        {
            TestDb.Cleanup(db);
        }
    }

    /// <summary>
    /// 双口径聚合：同一天多条记录时，填报工时与实际工时**分别**累加。
    /// 场景：周末加班实际干了 12h，但公司只让报 8h。
    /// </summary>
    [Fact]
    public async Task GetRangeAsync_双工时口径分别聚合()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var recordRepo = new WorkRecordRepository();
            // 2026-10-11 是周日（周末加班）：两条记录 → 填报 4+4=8h，实际 6+6=12h
            await recordRepo.InsertAsync(RecWithActual("2026-10-11", 4, 6));
            await recordRepo.InsertAsync(RecWithActual("2026-10-11", 4, 6));
            // 2026-10-10 是补班周六：只填了填报工时，未填实际工时 → 实际口径为 0（**不回落**）
            await recordRepo.InsertAsync(Rec("2026-10-10", 4));

            var items = await NewService().GetRangeAsync(
                new DateTime(2026, 10, 10), new DateTime(2026, 10, 11));
            var byDate = items.ToDictionary(i => i.Date);

            var weekend = byDate[new DateTime(2026, 10, 11)];
            Assert.Equal(WorkKind.WeekendOvertime, weekend.WorkKind);
            Assert.Equal(8.0, weekend.WorkHours, 3);      // 填报口径
            Assert.Equal(12.0, weekend.ActualHours, 3);   // 实际口径
            Assert.True(weekend.HasHoursDiff);            // 两个口径不一致
            Assert.Equal(2, weekend.RecordCount);

            // 未填实际工时 → 实际口径为 0（不再回落成填报值），也不算"口径差异"
            var makeup = byDate[new DateTime(2026, 10, 10)];
            Assert.Equal(4.0, makeup.WorkHours, 3);    // 填报口径照常
            Assert.Equal(0.0, makeup.ActualHours, 3);  // 实际未填 = 0
            Assert.False(makeup.HasHoursDiff);

            // 汇总卡要能同时给出两个口径
            var summary = DayStatusService.Summarize(items);
            Assert.Equal(8.0, summary.WeekendOvertimeHours, 3);
            Assert.Equal(12.0, summary.ActualWeekendOvertimeHours, 3);
        }
        finally
        {
            TestDb.Cleanup(db);
        }
    }

    [Fact]
    public async Task GetRangeAsync_软删除记录不进入总览()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var recordRepo = new WorkRecordRepository();
            var rec = Rec("2026-10-08", 8);
            await recordRepo.InsertAsync(rec);
            await recordRepo.DeleteAsync(rec.Id);

            var items = await NewService().GetRangeAsync(
                new DateTime(2026, 10, 8), new DateTime(2026, 10, 8));

            var item = Assert.Single(items);
            Assert.Equal(WorkKind.NoRecord, item.WorkKind);
            Assert.True(item.IsUnfilled);
        }
        finally
        {
            TestDb.Cleanup(db);
        }
    }

    [Fact]
    public async Task Summarize_同一天双标记_同时计入两类统计()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var recordRepo = new WorkRecordRepository();
            var leaveRepo = new LeaveRecordRepository();
            await recordRepo.InsertAsync(Rec("2026-09-30", 4));
            await leaveRepo.InsertAsync(new LeaveRecord { Date = "2026-09-30", LeaveType = "调休", Hours = 8 });

            var items = await NewService().GetRangeAsync(
                new DateTime(2026, 9, 30), new DateTime(2026, 9, 30));
            var s = DayStatusService.Summarize(items);

            // 09-30 既是「正常上班」又是「请假」，双计（与界面双标记口径一致）
            Assert.Equal(1, s.NormalWorkdays);
            Assert.Equal(4, s.NormalHours, 3);
            Assert.Equal(1, s.LeaveDays);
            Assert.Equal(8, s.LeaveHours, 3);
        }
        finally
        {
            TestDb.Cleanup(db);
        }
    }

    private static WorkRecord Rec(string date, double hours) => new()
    {
        WorkDate = date,
        Hours = hours,
        ProjectName = "测试项目",
        WorkType = "调试",
        Content = "单元测试内容"
    };

    private static WorkRecord RecWithActual(string date, double hours, double actualHours) => new()
    {
        WorkDate = date,
        Hours = hours,
        ActualHours = actualHours,
        ProjectName = "测试项目",
        WorkType = "调试",
        Content = "单元测试内容"
    };
}
