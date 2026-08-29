namespace EapWorkAssistant.Services;

/// <summary>
/// 仪表盘环比趋势计算（纯函数）。
///
/// 从 <c>DashboardViewModel.ComputeTrend</c> 中抽出。它是个无状态的小算术，
/// 但分支不少（上期无数据 / 本期也无 / 本期有则"新" / 正负百分比格式化），
/// 埋在 870 行的 ViewModel 里没人测过。抽出后可被单元测试锁定。
///
/// 方向遵循<b>中国习惯</b>：涨（IsUp=true）→ 红色，跌（IsUp=false）→ 绿色。
/// </summary>
public static class TrendCalculator
{
    /// <summary>环比趋势结果：文案 + 方向。文案为空表示不显示胶囊。</summary>
    public readonly record struct TrendResult(string Text, bool IsUp);

    /// <summary>
    /// 计算环比趋势文案与方向。
    /// 规则：
    /// <list type="bullet">
    /// <item>上期 &lt;= 0 且本期也 &lt;= 0 → 空文案（两边都没数据，不显示胶囊）。</item>
    /// <item>上期 &lt;= 0 但本期有数据 → 文案"新"，方向向上。</item>
    /// <item>否则 → "<paramref name="periodLabel"/> ±N%"，按正负加号；方向取 current ≥ previous。</item>
    /// </list>
    /// </summary>
    public static TrendResult Compute(double current, double previous, string periodLabel)
    {
        if (previous <= 0)
        {
            if (current <= 0) return new(string.Empty, true);
            return new("新", true);
        }

        var pct = (current - previous) / previous * 100;
        var sign = pct > 0 ? "+" : string.Empty;
        return new($"{periodLabel} {sign}{pct:F0}%", current >= previous);
    }
}
