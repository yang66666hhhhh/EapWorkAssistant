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

    /// <summary>草稿文件所在目录（测试隔离目录，%TEMP%）。</summary>
    private string Dir => Path.GetDirectoryName(_path)!;

    /// <summary>损坏备份前缀，与 DraftService.BackupPrefix 同构。</summary>
    private string CorruptPrefix => Path.GetFileNameWithoutExtension(_path) + ".corrupt-";

    public DraftServiceTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"eap_draft_{Guid.NewGuid():N}.json");
        _draft = new DraftService(_path);
    }

    public void Dispose()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { /* ignore */ }
        try
        {
            foreach (var f in Directory.GetFiles(Dir, CorruptPrefix + "*"))
                File.Delete(f);
        }
        catch { /* ignore */ }
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
    public void 顶层文件损坏_重置前备份坏文件()
    {
        const string broken = "{ 这不是合法 JSON";
        File.WriteAllText(_path, broken);

        _ = new DraftService(_path); // 触发 Load 失败分支

        var backup = Assert.Single(Directory.GetFiles(Dir, CorruptPrefix + "*"));
        Assert.Equal(broken, File.ReadAllText(backup));      // 原样保留，人工可抢救
        Assert.Contains(".file.bak", backup);                // 顶层文件标记为 file
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
    public void 单条草稿损坏_移出前备份该条原始内容()
    {
        File.WriteAllText(_path, "{\"Knowledge\":{\"Json\":\"{ 坏掉的负载\",\"SavedAt\":\"2026-10-13 10:00:00\"}}");

        var service = new DraftService(_path);
        Assert.Null(service.Get<Knowledge>());

        var backup = Assert.Single(Directory.GetFiles(Dir, CorruptPrefix + "*"));
        Assert.Equal("{ 坏掉的负载", File.ReadAllText(backup));  // 原始负载原样落盘
        Assert.Contains(".Knowledge.bak", backup);               // 标记来自哪个键

        // 清理已落盘：重开后该键不存在，文件仍是合法 JSON
        Assert.False(new DraftService(_path).Has<Knowledge>());
    }

    [Fact]
    public void 损坏备份_超出上限时保留最近的()
    {
        // 预置 12 个"很久以前"的备份，再触发一次真实备份 → 总数应被截到 10
        for (int i = 0; i < 12; i++)
        {
            var old = Path.Combine(Dir, $"{CorruptPrefix}20200101_0000{i:D2}_000.old.bak");
            File.WriteAllText(old, "old");
            File.SetLastWriteTimeUtc(old, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }
        const string broken = "{ 坏";
        File.WriteAllText(_path, broken);

        _ = new DraftService(_path);

        var backups = Directory.GetFiles(Dir, CorruptPrefix + "*");
        Assert.Equal(10, backups.Length);
        Assert.Contains(backups, f => File.ReadAllText(f) == broken); // 最新那条没被误删
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
