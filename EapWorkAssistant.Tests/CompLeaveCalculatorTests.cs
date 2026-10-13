using EapWorkAssistant.Models;
using EapWorkAssistant.Services;
using System;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// 调休余额纯计算测试：不依赖网络/数据库/单例，用判定委托模拟法定假日与补班日。
/// 业务规则：法定假日加班（300% 工资、不可调休）单独统计、不计入余额；
/// 周末加班（非补班日）才计入可调休余额。
///
/// 双口径：填报工时（对外展示）与实际工时（自己算账）分别累计，
/// **余额一律按实际工时算** —— 现场填报常被限制在 8h 以内。
/// </summary>
public class CompLeaveCalculatorTests
{
    // 默认：没有法定假日、也没有补班日
    private static Func<DateTime, bool> NoHoliday => _ => false;
    private static Func<DateTime, bool> NoMakeup => _ => false;

    [Fact]
    public void Compute_工作日加班不计入()
    {
        // 2026-08-12 是周三（工作日），不应计入加班
        var r = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 12), 8.0, 8.0) },
            Array.Empty<double>(),
            NoHoliday, NoMakeup);

        Assert.Equal(0, r.WeekendOvertimeActualHours);
        Assert.Equal(0, r.HolidayOvertimeActualHours);
        Assert.Equal(0, r.CompLeaveUsed);
        Assert.Equal(0, r.CompLeaveAvailable);
    }

    [Fact]
    public void Compute_周末工作计入可调休加班()
    {
        // 2026-08-15 是周六
        var r = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 15), 6.0, 6.0) },
            Array.Empty<double>(),
            NoHoliday, NoMakeup);

        Assert.Equal(6.0, r.WeekendOvertimeActualHours, 3);
        Assert.Equal(0, r.HolidayOvertimeActualHours, 3);
        Assert.Equal(6.0, r.OvertimeActualHours, 3);
        Assert.Equal(6.0, r.CompLeaveAvailable, 3);
    }

    [Fact]
    public void Compute_法定假日工作单独统计且不计入可调休余额()
    {
        // 用判定委托把 2026-08-01 标记为法定假日
        var isHoliday = new Func<DateTime, bool>(d => d == new DateTime(2026, 8, 1));
        var r = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 1), 8.0, 8.0) },
            Array.Empty<double>(),
            isHoliday, NoMakeup);

        Assert.Equal(0, r.WeekendOvertimeActualHours, 3);
        Assert.Equal(8.0, r.HolidayOvertimeActualHours, 3);
        Assert.Equal(8.0, r.OvertimeActualHours, 3);   // 合计用于展示
        Assert.Equal(0, r.CompLeaveAvailable, 3);      // 法定假日加班不可调休，余额为 0
    }

    [Fact]
    public void Compute_补班日虽在周末但不计入加班()
    {
        // 2026-08-15 是周六，但标记为补班日 → 视为正常工作日，不计入
        var isMakeup = new Func<DateTime, bool>(d => d == new DateTime(2026, 8, 15));
        var r = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 15), 8.0, 8.0) },
            Array.Empty<double>(),
            NoHoliday, isMakeup);

        Assert.Equal(0, r.WeekendOvertimeActualHours);
        Assert.Equal(0, r.HolidayOvertimeActualHours);
        Assert.Equal(0, r.CompLeaveAvailable);
    }

    [Fact]
    public void Compute_已用调休只扣减周末加班余额()
    {
        var records = new[]
        {
            (new DateTime(2026, 8, 15), 6.0, 6.0), // 周六 +6（可调休）
            (new DateTime(2026, 10, 1), 8.0, 8.0), // 法定假日 +8（不可调休）
        };
        var isHoliday = new Func<DateTime, bool>(d => d == new DateTime(2026, 10, 1));
        var r = CompLeaveCalculator.Compute(
            records,
            new[] { 5.0 }, // 已用调休 5h
            isHoliday, NoMakeup);

        Assert.Equal(6.0, r.WeekendOvertimeActualHours, 3);
        Assert.Equal(8.0, r.HolidayOvertimeActualHours, 3);
        Assert.Equal(14.0, r.OvertimeActualHours, 3);
        Assert.Equal(5.0, r.CompLeaveUsed, 3);
        Assert.Equal(1.0, r.CompLeaveAvailable, 3); // 6 - 5，假日加班 8h 不参与抵扣
    }

    [Fact]
    public void Compute_已用超过周末加班时余额不为负()
    {
        var r = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 15), 3.0, 3.0) },
            new[] { 10.0 }, // 用超了
            NoHoliday, NoMakeup);

        Assert.Equal(10.0, r.CompLeaveUsed, 3);
        Assert.Equal(0, r.CompLeaveAvailable); // max(0, 3-10)
    }

    [Fact]
    public void Compute_空输入返回全零()
    {
        var r = CompLeaveCalculator.Compute(
            Array.Empty<(DateTime, double, double)>(),
            Array.Empty<double>(),
            NoHoliday, NoMakeup);

        Assert.Equal(0, r.WeekendOvertimeActualHours);
        Assert.Equal(0, r.HolidayOvertimeActualHours);
        Assert.Equal(0, r.OvertimeActualHours);
        Assert.Equal(0, r.CompLeaveUsed);
        Assert.Equal(0, r.CompLeaveAvailable);
    }

    /// <summary>
    /// 核心场景：现场填报被限制在 8h（报多了公司不认），实际干了 12h。
    /// 填报 8h 既是展示值、**也是调休余额的依据**（调休是公司给的权利，只认填报口径）；
    /// 实际 12h 仅记录真实投入，不参与余额计算。
    /// </summary>
    [Fact]
    public void Compute_余额按填报工时算_实际工时只记录不参与()
    {
        var r = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 15), 8.0, 12.0) }, // 填报 8h / 实际 12h
            Array.Empty<double>(),
            NoHoliday, NoMakeup);

        Assert.Equal(8.0, r.WeekendOvertimeReportHours, 3);   // 填报口径（给领导看，也是余额依据）
        Assert.Equal(12.0, r.WeekendOvertimeActualHours, 3);  // 实际口径（自己看）
        Assert.Equal(8.0, r.OvertimeReportHours, 3);
        Assert.Equal(12.0, r.OvertimeActualHours, 3);
        Assert.Equal(8.0, r.CompLeaveAvailable, 3);           // 余额 = 填报 8h，不是实际 12h
    }

    /// <summary>实际工时未填写时展示「—」，**不回落**成填报值（用户明确要求区分两个口径，避免"没填却显示有"）。</summary>
    [Fact]
    public void ActualHoursText_未填写时显示破折号_不回落为填报工时()
    {
        var notFilled = new WorkRecord { Hours = 8.5, ActualHours = 0 };
        Assert.Equal("—", notFilled.ActualHoursText);
        Assert.False(notFilled.HasExplicitActualHours);

        var filled = new WorkRecord { Hours = 8.5, ActualHours = 12 };
        Assert.Equal("12.0h", filled.ActualHoursText);
        Assert.True(filled.HasExplicitActualHours);
    }
}
