using EapWorkAssistant.Helpers;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// Dashboard 图表所用的日期范围纯函数测试（周/月/季起止）。
/// 这些函数不依赖数据库或配置，是确定性的基础聚合逻辑。
/// </summary>
public class DateTimeHelperTests
{
    [Fact]
    public void GetWeekStart_周三_返回当周周一()
    {
        // 2026-08-12 是周三
        var date = new DateTime(2026, 8, 12);
        var start = DateTimeHelper.GetWeekStart(date);
        Assert.Equal(new DateTime(2026, 8, 10), start); // 周一
    }

    [Fact]
    public void GetWeekStart_周一_返回当天()
    {
        var date = new DateTime(2026, 8, 10); // 周一
        Assert.Equal(date, DateTimeHelper.GetWeekStart(date));
    }

    [Fact]
    public void GetWeekStart_周日_返回上一周周一()
    {
        // 2026-08-16 是周日，所在周的"周一"应为 2026-08-10
        var date = new DateTime(2026, 8, 16);
        Assert.Equal(new DateTime(2026, 8, 10), DateTimeHelper.GetWeekStart(date));
    }

    [Fact]
    public void GetWeekEnd_等于周一起始加六天()
    {
        var date = new DateTime(2026, 8, 12); // 周三
        var end = DateTimeHelper.GetWeekEnd(date);
        Assert.Equal(new DateTime(2026, 8, 16), end); // 周日
        Assert.Equal(6, (end - DateTimeHelper.GetWeekStart(date)).Days);
    }

    [Fact]
    public void GetMonthStart_返回当月一号()
    {
        Assert.Equal(new DateTime(2026, 2, 1), DateTimeHelper.GetMonthStart(new DateTime(2026, 2, 15)));
    }

    [Theory]
    [InlineData(2026, 1, 31)] // 大月
    [InlineData(2026, 2, 28)] // 平年二月
    [InlineData(2024, 2, 29)] // 闰年二月
    [InlineData(2026, 4, 30)] // 小月
    public void GetMonthEnd_返回当月最后一天(int year, int month, int lastDay)
    {
        var end = DateTimeHelper.GetMonthEnd(new DateTime(year, month, 1));
        Assert.Equal(new DateTime(year, month, lastDay), end);
    }

    [Theory]
    [InlineData(1, 1, 3)]   // Q1: 1/1 - 3/31（季度首月）
    [InlineData(2, 1, 3)]   // Q1: 1/1 - 3/31（2 月，非边界月）
    [InlineData(7, 7, 9)]   // Q3: 7/1 - 9/30（季度首月）
    [InlineData(11, 10, 12)] // Q4: 10/1 - 12/31（11 月，非边界月）
    public void GetQuarterRange_返回自然季度起止(int month, int startMonth, int endMonth)
    {
        var (start, end) = DateTimeHelper.GetQuarterRange(new DateTime(2026, month, 15));
        Assert.Equal(new DateTime(2026, startMonth, 1), start);
        Assert.Equal(new DateTime(2026, endMonth, DateTime.DaysInMonth(2026, endMonth)), end);
    }

    [Fact]
    public void GetWeekRange_返回起始加六天的区间字符串()
    {
        var date = new DateTime(2026, 8, 12); // 周三
        var range = DateTimeHelper.GetWeekRange(date);
        Assert.Equal("2026-08-10 ~ 2026-08-16", range);
    }
}
