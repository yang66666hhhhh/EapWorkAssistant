using EapWorkAssistant.Helpers;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// 命令面板静态条目源（页面 / 操作）的匹配逻辑。
/// 对应 MainViewModel.SearchAsync 里 CommandPaletteProvider.Match 的合并段。
/// </summary>
public class CommandPaletteProviderTests
{
    [Fact]
    public void 空白关键词_不返回任何静态条目()
    {
        Assert.Empty(CommandPaletteProvider.Match(""));
        Assert.Empty(CommandPaletteProvider.Match("   "));
        Assert.Empty(CommandPaletteProvider.Match(null!));
    }

    [Fact]
    public void 页面标题精确命中()
    {
        var hits = CommandPaletteProvider.Match("设置");
        var entry = Assert.Single(hits);
        Assert.Equal("设置", entry.Title);
        Assert.Equal(ViewNames.Settings, entry.NavigateTo);
        Assert.False(entry.OpenNewForm);
    }

    [Fact]
    public void 别名命中_考勤映射到出勤总览()
    {
        var hits = CommandPaletteProvider.Match("考勤");
        var entry = Assert.Single(hits);
        Assert.Equal(ViewNames.Attendance, entry.NavigateTo);
    }

    [Fact]
    public void 副标题参与匹配_热力图命中出勤总览()
    {
        var entry = Assert.Single(CommandPaletteProvider.Match("热力图"));
        Assert.Equal(ViewNames.Attendance, entry.NavigateTo);
    }

    [Fact]
    public void 匹配大小写不敏感()
    {
        Assert.Contains(CommandPaletteProvider.Match("SETTINGS"), e => e.NavigateTo == ViewNames.Settings);
        Assert.Contains(CommandPaletteProvider.Match("iSsUe"), e => e.NavigateTo == ViewNames.Issue);
    }

    [Fact]
    public void 多关键词_全部命中才算匹配()
    {
        var hits = CommandPaletteProvider.Match("出勤 热力图");
        Assert.Single(hits);
        Assert.Equal(ViewNames.Attendance, hits[0].NavigateTo);

        // "知识" 不出现在出勤总览的命中范围 → 部分命中应落空
        Assert.Empty(CommandPaletteProvider.Match("出勤 知识"));
    }

    [Fact]
    public void 全角空格也可分隔多关键词()
    {
        var hits = CommandPaletteProvider.Match("工作　记录");
        Assert.Contains(hits, e => e.Title == "工作记录");
        Assert.Contains(hits, e => e.Title == "新增工作记录");
    }

    [Fact]
    public void 新增工作记录_是带开抽屉标记的操作项()
    {
        // 用独有别名「记一笔」——「新增」也会命中「工作记录」页面的副标题（Ctrl+N 快速新增），那是另一条用例
        var entry = Assert.Single(CommandPaletteProvider.Match("记一笔"));
        Assert.Equal("操作", entry.Type);
        Assert.True(entry.OpenNewForm);
        Assert.Equal(ViewNames.WorkRecord, entry.NavigateTo);
    }

    [Fact]
    public void 新增_同时命中页面与操作_两者都返回()
    {
        // 副标题参与匹配的必然结果：「工作记录」页面文案含"快速新增"，操作项含"新增"
        var hits = CommandPaletteProvider.Match("新增");
        Assert.Contains(hits, e => e.Title == "工作记录");
        Assert.Contains(hits, e => e.Title == "新增工作记录");
    }

    [Fact]
    public void 七个页面入口齐全且导航键稳定()
    {
        var expected = new[]
        {
            ViewNames.Dashboard, ViewNames.WorkRecord, ViewNames.Attendance,
            ViewNames.Knowledge, ViewNames.Issue, ViewNames.Settings, ViewNames.RecycleBin
        };
        var pages = CommandPaletteProvider.Entries.Where(e => e.Type == "页面").ToList();

        Assert.Equal(7, pages.Count);
        foreach (var name in expected)
            Assert.Contains(pages, p => p.NavigateTo == name);
        // 导航键不得重复（否则计数与跳转都会错位）
        Assert.Equal(pages.Count, pages.Select(p => p.NavigateTo).Distinct().Count());
    }

    [Fact]
    public void 关键词命中操作与页面_操作同样参与匹配且互不排斥()
    {
        // "记录" 同时命中「工作记录」页面与「新增工作记录」操作——两者都应返回
        var hits = CommandPaletteProvider.Match("记录");
        Assert.Contains(hits, e => e.Title == "工作记录");
        Assert.Contains(hits, e => e.Title == "新增工作记录");
    }
}
