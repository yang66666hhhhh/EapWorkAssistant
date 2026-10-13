namespace EapWorkAssistant.Helpers;

/// <summary>
/// 命令面板（Spotlight 搜索）键盘导航的**纯逻辑**：由 ↑↓ 的位移量推算新的高亮索引（环绕、不越界）。
/// 抽成无副作用的静态方法便于单测，ViewModel 只负责状态写入与可见性判断。
/// </summary>
public static class SearchNavigationHelper
{
    /// <summary>
    /// 计算移动后的高亮索引。
    /// </summary>
    /// <param name="current">当前索引（-1 表示无高亮）。</param>
    /// <param name="delta">位移量（+1 下移 / -1 上移）。</param>
    /// <param name="count">结果总数。</param>
    /// <returns>
    /// 新索引；<paramref name="count"/> &lt;= 0 时返回 -1。
    /// 无高亮（current &lt; 0）时：下移落到首项、上移落到末项；否则按环绕规则移动。
    /// </returns>
    public static int Move(int current, int delta, int count)
    {
        if (count <= 0) return -1;
        if (delta == 0) return current < 0 ? -1 : Math.Clamp(current, 0, count - 1);

        if (current < 0)
            return delta > 0 ? 0 : count - 1;

        var next = (current + delta) % count;
        if (next < 0) next += count;
        return next;
    }
}
