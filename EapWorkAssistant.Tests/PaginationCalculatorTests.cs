using EapWorkAssistant.Services;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// PaginationCalculator（分页算术）的单元测试。
/// 这段逻辑原先是 WorkRecordViewModel 里的三个私有方法，无法单测；
/// 页码算错是分页控件最容易回归的地方，抽出后在此锁定行为。
/// </summary>
public class PaginationCalculatorTests
{
    // ===== CalculateTotalPages =====

    [Theory]
    [InlineData(0, 20, 1)]      // 无记录 → 至少 1 页，避免"第 1/0 页"
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]     // 正好一页，不产生空页
    [InlineData(21, 20, 2)]     // 边界：多一条就多一页
    [InlineData(100, 20, 5)]
    [InlineData(101, 20, 6)]
    public void CalculateTotalPages_VariousInputs(int totalCount, int pageSize, int expected)
        => Assert.Equal(expected, PaginationCalculator.CalculateTotalPages(totalCount, pageSize));

    [Theory]
    [InlineData(100, 0)]
    [InlineData(100, -1)]
    public void CalculateTotalPages_InvalidPageSize_ReturnsOne(int totalCount, int pageSize)
        => Assert.Equal(1, PaginationCalculator.CalculateTotalPages(totalCount, pageSize));

    // ===== ClampPage =====

    [Fact]
    public void ClampPage_ExceedsTotal_ClampsToLastPage()
        // 筛选后记录变少、当前页越界的典型场景
        => Assert.Equal(3, PaginationCalculator.ClampPage(9, 3));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ClampPage_BelowOne_ClampsToOne(int currentPage)
        => Assert.Equal(1, PaginationCalculator.ClampPage(currentPage, 5));

    [Fact]
    public void ClampPage_WithinRange_Unchanged()
        => Assert.Equal(2, PaginationCalculator.ClampPage(2, 5));

    [Fact]
    public void ClampPage_ZeroTotalPages_ReturnsOne()
        => Assert.Equal(1, PaginationCalculator.ClampPage(3, 0));

    // ===== BuildPageText =====

    [Fact]
    public void BuildPageText_HasRecords_ShowsPageNumbers()
        => Assert.Equal("第 2 / 5 页", PaginationCalculator.BuildPageText(2, 5, 100));

    [Fact]
    public void BuildPageText_NoRecords_ShowsNoRecords()
        => Assert.Equal("无记录", PaginationCalculator.BuildPageText(1, 1, 0));

    // ===== BuildVisiblePageNumbers =====

    [Fact]
    public void BuildVisiblePageNumbers_FewPages_FlattensAll()
    {
        var pages = PaginationCalculator.BuildVisiblePageNumbers(1, 5);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, pages);
    }

    [Fact]
    public void BuildVisiblePageNumbers_ExactlySevenPages_FlattensAll()
    {
        var pages = PaginationCalculator.BuildVisiblePageNumbers(4, 7);

        Assert.Equal(7, pages.Count);
        Assert.DoesNotContain(PaginationCalculator.Ellipsis, pages);
    }

    [Fact]
    public void BuildVisiblePageNumbers_NearStart_ShowsLeadingWindow()
    {
        var pages = PaginationCalculator.BuildVisiblePageNumbers(1, 20);

        // 首末页始终显示；当前页在开头时不该有前置省略号
        //   current=1,total=20 → [1,2,3,0,20]（0 为省略号）
        Assert.Equal(1, pages[0]);
        Assert.Equal(20, pages[^1]);
        Assert.DoesNotContain(PaginationCalculator.Ellipsis, pages.Take(3));
        Assert.Contains(PaginationCalculator.Ellipsis, pages);
    }

    [Fact]
    public void BuildVisiblePageNumbers_InMiddle_HasBothEllipses()
    {
        var pages = PaginationCalculator.BuildVisiblePageNumbers(10, 20);

        Assert.Equal(1, pages[0]);
        Assert.Equal(20, pages[^1]);
        // 中间页两侧都需要折叠
        Assert.Equal(2, pages.Count(p => p == PaginationCalculator.Ellipsis));
        Assert.Contains(10, pages);
    }

    [Fact]
    public void BuildVisiblePageNumbers_NearEnd_ShowsTrailingWindow()
    {
        var pages = PaginationCalculator.BuildVisiblePageNumbers(20, 20);

        Assert.Equal(1, pages[0]);
        Assert.Equal(20, pages[^1]);
        //   current=20,total=20 → [1,0,18,19,20]（0 为省略号）
        Assert.Contains(PaginationCalculator.Ellipsis, pages);
        // 末页附近不应出现后置省略号（末 3 项为 18,19,20）
        Assert.DoesNotContain(PaginationCalculator.Ellipsis, pages[^3..]);
    }

    [Fact]
    public void BuildVisiblePageNumbers_SinglePage_ReturnsOne()
    {
        var pages = PaginationCalculator.BuildVisiblePageNumbers(1, 1);

        Assert.Equal(new[] { 1 }, pages);
    }
}
