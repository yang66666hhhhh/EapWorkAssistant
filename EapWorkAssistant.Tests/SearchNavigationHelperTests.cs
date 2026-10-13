using EapWorkAssistant.Helpers;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// 命令面板键盘导航纯逻辑：↑↓ 高亮索引的环绕与边界。
/// 对应 MainViewModel.MoveSearchSelection 的位移规则。
/// </summary>
public class SearchNavigationHelperTests
{
    [Fact]
    public void Move_空结果_恒返回无高亮()
    {
        Assert.Equal(-1, SearchNavigationHelper.Move(-1, 1, 0));
        Assert.Equal(-1, SearchNavigationHelper.Move(0, -1, 0));
    }

    [Fact]
    public void Move_无高亮时下移_落到首项()
    {
        Assert.Equal(0, SearchNavigationHelper.Move(-1, 1, 5));
    }

    [Fact]
    public void Move_无高亮时上移_落到末项()
    {
        Assert.Equal(4, SearchNavigationHelper.Move(-1, -1, 5));
    }

    [Fact]
    public void Move_末项下移_环绕回首项()
    {
        Assert.Equal(0, SearchNavigationHelper.Move(4, 1, 5));
    }

    [Fact]
    public void Move_首项上移_环绕到末项()
    {
        Assert.Equal(4, SearchNavigationHelper.Move(0, -1, 5));
    }

    [Fact]
    public void Move_普通位移_逐项移动()
    {
        Assert.Equal(3, SearchNavigationHelper.Move(2, 1, 5));
        Assert.Equal(1, SearchNavigationHelper.Move(2, -1, 5));
    }

    [Fact]
    public void Move_单项列表_上下移均停在唯一项()
    {
        Assert.Equal(0, SearchNavigationHelper.Move(0, 1, 1));
        Assert.Equal(0, SearchNavigationHelper.Move(0, -1, 1));
        Assert.Equal(0, SearchNavigationHelper.Move(-1, 1, 1));
    }

    [Fact]
    public void Move_位移为0_保持当前且夹取越界值()
    {
        Assert.Equal(-1, SearchNavigationHelper.Move(-1, 0, 5));
        Assert.Equal(3, SearchNavigationHelper.Move(3, 0, 5));
        // 脏索引（越界）被夹取回合法范围
        Assert.Equal(4, SearchNavigationHelper.Move(9, 0, 5));
    }
}
