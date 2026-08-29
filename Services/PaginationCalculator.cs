namespace EapWorkAssistant.Services;

/// <summary>
/// 分页计算的纯函数集合（无状态、无 WPF 依赖）。
///
/// 从 WorkRecordViewModel 中抽出：原先 `UpdatePagination` / `UpdateVisiblePageNumbers` /
/// `CalculateTotalPages` 是三处混在 ViewModel 里的算术逻辑，无法单测，
/// 而"页码算错"恰恰是分页控件最容易回归的地方。
///
/// 约定：<c>0</c> 在页码列表中表示<b>省略号</b>（由 View 渲染为 "…"）。
/// </summary>
public static class PaginationCalculator
{
    /// <summary>页码列表中代表"省略号"的哨兵值。</summary>
    public const int Ellipsis = 0;

    /// <summary>总页数小于等于该值时，直接平铺全部页码，不加省略号。</summary>
    private const int MaxFlatPages = 7;

    /// <summary>
    /// 计算总页数。无记录或页大小非法时返回 1（保证 UI 至少有一页，避免出现"第 1/0 页"）。
    /// </summary>
    public static int CalculateTotalPages(int totalCount, int pageSize)
        => totalCount > 0 && pageSize > 0
            ? (totalCount + pageSize - 1) / pageSize
            : 1;

    /// <summary>
    /// 把当前页收敛到 [1, totalPages] 区间。
    /// 典型场景：筛选条件变化导致总记录数变少，当前页可能越界。
    /// </summary>
    public static int ClampPage(int currentPage, int totalPages)
    {
        if (totalPages <= 0) return 1;
        if (currentPage > totalPages) return totalPages;
        if (currentPage < 1) return 1;
        return currentPage;
    }

    /// <summary>生成分页状态文案；无记录时给出"无记录"而不是"第 1 / 1 页"。</summary>
    public static string BuildPageText(int currentPage, int totalPages, int totalCount)
        => totalCount > 0 ? $"第 {currentPage} / {totalPages} 页" : "无记录";

    /// <summary>
    /// 生成要显示的页码序列（首末页始终显示，中间以省略号折叠）。
    /// </summary>
    /// <param name="currentPage">当前页码（1 起）。</param>
    /// <param name="totalPages">总页数。</param>
    /// <returns>页码列表，其中 <see cref="Ellipsis"/> 代表省略号。</returns>
    public static List<int> BuildVisiblePageNumbers(int currentPage, int totalPages)
    {
        var pages = new List<int>();

        // 总页数很少（含为 0/1 的边界）时直接平铺，无需折叠
        if (totalPages <= MaxFlatPages)
        {
            for (int i = 1; i <= totalPages; i++)
                pages.Add(i);
            return pages;
        }

        pages.Add(1);

        int start = Math.Max(2, currentPage - 2);
        int end = Math.Min(totalPages - 1, currentPage + 2);

        if (start > 2) pages.Add(Ellipsis);
        for (int i = start; i <= end; i++) pages.Add(i);
        if (end < totalPages - 1) pages.Add(Ellipsis);

        pages.Add(totalPages);
        return pages;
    }
}
