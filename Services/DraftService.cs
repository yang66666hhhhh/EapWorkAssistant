using EapWorkAssistant.Helpers;
using System.IO;
using System.Text.Json;

namespace EapWorkAssistant.Services;

/// <summary>
/// 表单草稿服务：把「新建表单」里未保存的内容持久化到本地，下次打开同一模块的
/// 新建抽屉时由 View 询问是否恢复，避免误关抽屉丢失长文本（项目描述 / 问题描述等）。
///
/// - 存储：%LOCALAPPDATA%\EapWorkAssistant\drafts.json（与 config.json 同级）。
/// - 键：取实体类型名（Knowledge / Issue / WorkRecord），调用方无需维护字符串常量。
/// - 只保存「新增」草稿；编辑态（Id &gt; 0）数据本就在库中，重开即可，不占草稿位。
/// - 所有 IO 与反序列化失败都吞掉并写日志，草稿能力绝不能影响主流程。
/// </summary>
public sealed class DraftService
{
    private static readonly string DraftDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant");

    private static readonly string DefaultDraftPath = Path.Combine(DraftDir, "drafts.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>进程内单例：View 层直接取用（与 ConfigService.Instance 同范式）。</summary>
    public static DraftService Instance { get; } = new();

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, DraftEntry> _entries = new();

    private sealed class DraftEntry
    {
        public string Json { get; set; } = string.Empty;
        public string SavedAt { get; set; } = string.Empty;
    }

    /// <summary>默认构造：使用 %LOCALAPPDATA% 下的标准路径。</summary>
    public DraftService() : this(DefaultDraftPath) { }

    /// <summary>显式路径构造：供单元测试使用隔离目录，避免污染真实草稿文件。</summary>
    public DraftService(string filePath)
    {
        _path = filePath;
        Load();
    }

    private static string KeyOf<T>() => typeof(T).Name;

    /// <summary>保存（或覆盖）某类型的新建草稿。</summary>
    public void Save<T>(T item) where T : class
    {
        if (item == null) return;
        try
        {
            var json = JsonSerializer.Serialize(item, JsonOptions);
            lock (_lock)
            {
                _entries[KeyOf<T>()] = new DraftEntry
                {
                    Json = json,
                    SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                Persist();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("保存表单草稿失败", ex);
        }
    }

    /// <summary>读取草稿（不移除）。不存在或内容已损坏时返回 null 并顺手清理脏条目。</summary>
    public T? Get<T>() where T : class
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(KeyOf<T>(), out var entry)) return null;
            try
            {
                return JsonSerializer.Deserialize<T>(entry.Json, JsonOptions);
            }
            catch (Exception ex)
            {
                Logger.Error($"草稿解析失败（{KeyOf<T>()}），已丢弃", ex);
                _entries.Remove(KeyOf<T>());
                Persist();
                return null;
            }
        }
    }

    /// <summary>草稿保存时间文本（无草稿返回 null），用于恢复提示文案。</summary>
    public string? GetSavedAt<T>() where T : class
    {
        lock (_lock)
            return _entries.TryGetValue(KeyOf<T>(), out var e) ? e.SavedAt : null;
    }

    /// <summary>是否存在草稿。</summary>
    public bool Has<T>() where T : class
    {
        lock (_lock) return _entries.ContainsKey(KeyOf<T>());
    }

    /// <summary>清除草稿（保存成功、或用户选择不恢复时调用）。</summary>
    public void Clear<T>() where T : class
    {
        lock (_lock)
        {
            if (_entries.Remove(KeyOf<T>()))
                Persist();
        }
    }

    // ===== IO（调用方已持锁）=====

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json)) return;
            _entries = JsonSerializer.Deserialize<Dictionary<string, DraftEntry>>(json, JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            // 文件损坏不应阻断启动：重置为空并记录
            _entries = new Dictionary<string, DraftEntry>();
            Logger.Error("加载表单草稿失败，已重置", ex);
        }
    }

    private void Persist()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(_entries, JsonOptions));
        }
        catch (Exception ex)
        {
            Logger.Error("写入表单草稿失败", ex);
        }
    }
}
