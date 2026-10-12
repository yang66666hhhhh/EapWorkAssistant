using EapWorkAssistant.Services;
using System;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// 调休余额纯计算测试：不依赖网络/数据库/单例，用判定委托模拟法定假日与补班日。
/// 业务规则：法定假日加班（300% 工资、不可调休）单独统计、不计入余额；
/// 周末加班（非补班日）才计入可调休余额。
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
        var (weekend, holiday, _, used, avail) = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 12), 8.0) },
            Array.Empty<double>(),
            NoHoliday, NoMakeup);

        Assert.Equal(0, weekend);
        Assert.Equal(0, holiday);
        Assert.Equal(0, used);
        Assert.Equal(0, avail);
    }

    [Fact]
    public void Compute_周末工作计入可调休加班()
    {
        // 2026-08-15 是周六
        var (weekend, holiday, total, _, avail) = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 15), 6.0) },
            Array.Empty<double>(),
            NoHoliday, NoMakeup);

        Assert.Equal(6.0, weekend, 3);
        Assert.Equal(0, holiday, 3);
        Assert.Equal(6.0, total, 3);
        Assert.Equal(6.0, avail, 3);
    }

    [Fact]
    public void Compute_法定假日工作单独统计且不计入可调休余额()
    {
        // 用判定委托把 2026-08-01 标记为法定假日
        var isHoliday = new Func<DateTime, bool>(d => d == new DateTime(2026, 8, 1));
        var (weekend, holiday, total, used, avail) = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 1), 8.0) },
            Array.Empty<double>(),
            isHoliday, NoMakeup);

        Assert.Equal(0, weekend, 3);
        Assert.Equal(8.0, holiday, 3);
        Assert.Equal(8.0, total, 3);   // 合计用于展示
        Assert.Equal(0, avail, 3);     // 法定假日加班不可调休，余额为 0
    }

    [Fact]
    public void Compute_补班日虽在周末但不计入加班()
    {
        // 2026-08-15 是周六，但标记为补班日 → 视为正常工作日，不计入
        var isMakeup = new Func<DateTime, bool>(d => d == new DateTime(2026, 8, 15));
        var (weekend, holiday, _, _, avail) = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 15), 8.0) },
            Array.Empty<double>(),
            NoHoliday, isMakeup);

        Assert.Equal(0, weekend);
        Assert.Equal(0, holiday);
        Assert.Equal(0, avail);
    }

    [Fact]
    public void Compute_已用调休只扣减周末加班余额()
    {
        var records = new[]
        {
            (new DateTime(2026, 8, 15), 6.0), // 周六 +6（可调休）
            (new DateTime(2026, 10, 1), 8.0), // 法定假日 +8（不可调休）
        };
        var isHoliday = new Func<DateTime, bool>(d => d == new DateTime(2026, 10, 1));
        var (weekend, holiday, total, used, avail) = CompLeaveCalculator.Compute(
            records,
            new[] { 5.0 }, // 已用调休 5h
            isHoliday, NoMakeup);

        Assert.Equal(6.0, weekend, 3);
        Assert.Equal(8.0, holiday, 3);
        Assert.Equal(14.0, total, 3);
        Assert.Equal(5.0, used, 3);
        Assert.Equal(1.0, avail, 3); // 6 - 5，假日加班 8h 不参与抵扣
    }

    [Fact]
    public void Compute_已用超过周末加班时余额不为负()
    {
        var (_, _, _, used, avail) = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 15), 3.0) },
            new[] { 10.0 }, // 用超了
            NoHoliday, NoMakeup);

        Assert.Equal(10.0, used, 3);
        Assert.Equal(0, avail); // max(0, 3-10)
    }

    [Fact]
    public void Compute_空输入返回全零()
    {
        var (weekend, holiday, total, used, avail) = CompLeaveCalculator.Compute(
            Array.Empty<(DateTime, double)>(),
            Array.Empty<double>(),
            NoHoliday, NoMakeup);

        Assert.Equal(0, weekend);
        Assert.Equal(0, holiday);
        Assert.Equal(0, total);
        Assert.Equal(0, used);
        Assert.Equal(0, avail);
    }
}
