using EapWorkAssistant.Services;
using System;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// 调休余额纯计算测试：不依赖网络/数据库/单例，用判定委托模拟法定假日与补班日。
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
        var (ot, used, avail) = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 12), 8.0) },
            Array.Empty<double>(),
            NoHoliday, NoMakeup);

        Assert.Equal(0, ot);
        Assert.Equal(0, used);
        Assert.Equal(0, avail);
    }

    [Fact]
    public void Compute_周末工作计入加班()
    {
        // 2026-08-15 是周六
        var (ot, _, _) = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 15), 6.0) },
            Array.Empty<double>(),
            NoHoliday, NoMakeup);

        Assert.Equal(6.0, ot, 3);
    }

    [Fact]
    public void Compute_法定假日工作计入加班()
    {
        // 用判定委托把 2026-08-01 标记为法定假日（即便它是周六也无所谓）
        var isHoliday = new Func<DateTime, bool>(d => d == new DateTime(2026, 8, 1));
        var (ot, _, _) = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 1), 8.0) },
            Array.Empty<double>(),
            isHoliday, NoMakeup);

        Assert.Equal(8.0, ot, 3);
    }

    [Fact]
    public void Compute_补班日虽在周末但不计入加班()
    {
        // 2026-08-15 是周六，但标记为补班日 → 视为正常工作日，不计入
        var isMakeup = new Func<DateTime, bool>(d => d == new DateTime(2026, 8, 15));
        var (ot, _, _) = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 15), 8.0) },
            Array.Empty<double>(),
            NoHoliday, isMakeup);

        Assert.Equal(0, ot);
    }

    [Fact]
    public void Compute_已用调休扣减余额()
    {
        var records = new[]
        {
            (new DateTime(2026, 8, 15), 6.0), // 周六 +6
            (new DateTime(2026, 8, 16), 4.0), // 周日 +4
        };
        var (ot, used, avail) = CompLeaveCalculator.Compute(
            records,
            new[] { 5.0 }, // 已用调休 5h
            NoHoliday, NoMakeup);

        Assert.Equal(10.0, ot, 3);
        Assert.Equal(5.0, used, 3);
        Assert.Equal(5.0, avail, 3);
    }

    [Fact]
    public void Compute_已用超过加班时余额不为负()
    {
        var (_, used, avail) = CompLeaveCalculator.Compute(
            new[] { (new DateTime(2026, 8, 15), 3.0) },
            new[] { 10.0 }, // 用超了
            NoHoliday, NoMakeup);

        Assert.Equal(10.0, used, 3);
        Assert.Equal(0, avail); // max(0, 3-10)
    }

    [Fact]
    public void Compute_空输入返回全零()
    {
        var (ot, used, avail) = CompLeaveCalculator.Compute(
            Array.Empty<(DateTime, double)>(),
            Array.Empty<double>(),
            NoHoliday, NoMakeup);

        Assert.Equal(0, ot);
        Assert.Equal(0, used);
        Assert.Equal(0, avail);
    }
}
