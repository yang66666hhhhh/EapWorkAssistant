using EapWorkAssistant.Services;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// TrendCalculator（仪表盘环比趋势）的单元测试。
/// 原先是 DashboardViewModel 的私有方法，分支多但无人覆盖；抽出后锁定行为。
/// 方向遵循中国习惯：涨（IsUp=true）→ 红，跌（IsUp=false）→ 绿。
/// </summary>
public class TrendCalculatorTests
{
    // ===== 上涨 =====

    [Fact]
    public void Compute_Rise_FormatsWithPlusSignAndUpDirection()
    {
        // 8 → 10：+25%
        var t = TrendCalculator.Compute(10, 8, "较昨日");

        Assert.Equal("较昨日 +25%", t.Text);
        Assert.True(t.IsUp);
    }

    [Fact]
    public void Compute_SmallRise_RoundsToNearestPercent()
    {
        // 8 → 8.3：3.75% → 四舍五入 4%
        var t = TrendCalculator.Compute(8.3, 8, "较上周");

        Assert.Equal("较上周 +4%", t.Text);
        Assert.True(t.IsUp);
    }

    // ===== 下跌 =====

    [Fact]
    public void Compute_Fall_NoLeadingSignAndDownDirection()
    {
        // 10 → 7：-30%
        var t = TrendCalculator.Compute(7, 10, "较上月");

        Assert.Equal("较上月 -30%", t.Text);
        Assert.False(t.IsUp);
    }

    // ===== 持平 =====

    [Fact]
    public void Compute_Equal_DownDirectionButZeroPercent()
    {
        // current == previous：pct=0，sign 为空，方向 current>=previous → true
        var t = TrendCalculator.Compute(8, 8, "较昨日");

        Assert.Equal("较昨日 0%", t.Text);
        Assert.True(t.IsUp);
    }

    // ===== 上期无数据（边界） =====

    [Fact]
    public void Compute_PreviousZero_CurrentHasData_LabeledNew()
    {
        // 上期没数据、本期有 → "新"，向上
        var t = TrendCalculator.Compute(8, 0, "较昨日");

        Assert.Equal("新", t.Text);
        Assert.True(t.IsUp);
    }

    [Fact]
    public void Compute_BothZero_EmptyTextNoCapsule()
    {
        // 两边都没数据 → 空文案（UI 不显示胶囊）
        var t = TrendCalculator.Compute(0, 0, "较昨日");

        Assert.Equal(string.Empty, t.Text);
    }

    [Fact]
    public void Compute_PreviousNegative_CurrentHasData_LabeledNew()
    {
        // 上期为负数（脏数据）应等价于"上期无数据"
        var t = TrendCalculator.Compute(5, -3, "较上月");

        Assert.Equal("新", t.Text);
        Assert.True(t.IsUp);
    }

    // ===== 标签透传 =====

    [Fact]
    public void Compute_PeriodLabel_IsPassedThroughToText()
    {
        var t = TrendCalculator.Compute(12, 10, "较上月");

        Assert.StartsWith("较上月 ", t.Text);
    }
}
