using System.IO;
using EapWorkAssistant.Models;
using EapWorkAssistant.Services;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// 表单草稿服务契约：新建表单关闭后应能持久化、重启后读回；不同实体类型互不干扰；
/// 文件损坏不得抛异常拖垮主流程（草稿是"锦上添花"，绝不能成为新的崩溃点）。
/// </summary>
public class DraftServiceTests : IDisposable
{
    private readonly string _path;
    private readonly DraftService _draft;

    public DraftServiceTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"eap_draft_{Guid.NewGuid():N}.json");
        _draft = new DraftService(_path);
    }

    public void Dispose()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { /* ignore */ }
    }

    [Fact]
    public void 空服务_无草稿()
    {
        Assert.False(_draft.Has<Knowledge>());
        Assert.Null(_draft.Get<Knowledge>());
        Assert.Null(_draft.GetSavedAt<Knowledge>());
    }

    [Fact]
    public void 保存后_新实例可读回同一内容()
    {
        _draft.Save(new Knowledge { Title = "标题A", Content = "内容A", Tags = "x,y", Category = "运维" });

        var reopened = new DraftService(_path); // 模拟应用重启
        var draft = reopened.Get<Knowledge>();

        Assert.NotNull(draft);
        Assert.Equal("标题A", draft!.Title);
        Assert.Equal("内容A", draft.Content);
        Assert.Equal("x,y", draft.Tags);
        Assert.Equal("运维", draft.Category);
        Assert.True(reopened.Has<Knowledge>());
        Assert.False(string.IsNullOrWhiteSpace(reopened.GetSavedAt<Knowledge>()));
    }

    [Fact]
    public void 同键再保存_覆盖旧草稿()
    {
        _draft.Save(new Knowledge { Title = "旧" });
        _draft.Save(new Knowledge { Title = "新" });

        Assert.Equal("新", _draft.Get<Knowledge>()!.Title);
    }

    [Fact]
    public void 不同类型草稿_互不干扰()
    {
        _draft.Save(new Knowledge { Title = "知识" });
        _draft.Save(new Issue { Title = "问题", Description = "描述" });

        Assert.Equal("知识", _draft.Get<Knowledge>()!.Title);
        Assert.Equal("问题", _draft.Get<Issue>()!.Title);
        Assert.Equal("描述", _draft.Get<Issue>()!.Description);

        _draft.Clear<Knowledge>();
        Assert.False(_draft.Has<Knowledge>());
        Assert.True(_draft.Has<Issue>()); // 清一个不影响另一个
    }

    [Fact]
    public void Clear_后不再有草稿_且持久化生效()
    {
        _draft.Save(new Knowledge { Title = "待清" });
        _draft.Clear<Knowledge>();

        Assert.False(_draft.Has<Knowledge>());
        Assert.Null(new DraftService(_path).Get<Knowledge>());
    }

    [Fact]
    public void 工作记录草稿_双工时字段往返一致()
    {
        _draft.Save(new WorkRecord
        {
            ProjectName = "胜宏",
            Content = "现场调试",
            WorkType = "现场支持",
            Hours = 8,
            ActualHours = 11.5,
            Progress = 60,
            IsHighlight = 1,
            HighlightNote = "首次上线"
        });

        var draft = new DraftService(_path).Get<WorkRecord>();

        Assert.NotNull(draft);
        Assert.Equal("胜宏", draft!.ProjectName);
        Assert.Equal("现场调试", draft.Content);
        Assert.Equal(8d, draft.Hours);
        Assert.Equal(11.5d, draft.ActualHours);
        Assert.Equal(60, draft.Progress);
        Assert.Equal(1, draft.IsHighlight);
        Assert.Equal(0, draft.Id); // 草稿永远是新建态
    }

    [Fact]
    public void 顶层文件损坏_不抛异常且视为无草稿()
    {
        File.WriteAllText(_path, "{ 这不是合法 JSON");

        var service = new DraftService(_path);

        Assert.False(service.Has<Knowledge>());
        Assert.Null(service.Get<Knowledge>());
    }

    [Fact]
    public void 单条草稿内容损坏_读回为null并自动清理该键()
    {
        // 顶层合法、内层 JSON 非法：模拟手工改坏或版本更迭导致的脏条目
        File.WriteAllText(_path, "{\"Knowledge\":{\"Json\":\"{ 坏掉的负载\",\"SavedAt\":\"2026-10-13 10:00:00\"}}");

        var service = new DraftService(_path);

        Assert.True(service.Has<Knowledge>());      // 条目仍在（尚未解析）
        Assert.Null(service.Get<Knowledge>());      // 解析失败 → 返回 null
        Assert.False(service.Has<Knowledge>());     // 且顺手清理，避免下次再踩
    }

    [Fact]
    public void 目录不存在_保存时自动创建()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"eap_draft_dir_{Guid.NewGuid():N}");
        var nested = Path.Combine(dir, "drafts.json");
        var service = new DraftService(nested);

        service.Save(new Knowledge { Title = "自动建目录" });

        Assert.True(File.Exists(nested));
        Assert.Equal("自动建目录", new DraftService(nested).Get<Knowledge>()!.Title);

        try { Directory.Delete(dir, true); } catch { /* ignore */ }
    }
}
