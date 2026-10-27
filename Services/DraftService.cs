using EapWorkAssistant.Helpers;
using System.IO;
using System.Linq;
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
/// - 损坏内容**先备份再清理**（P3 §5.3-a）：顶层文件损坏 → 备份整个文件；
///   单条草稿损坏 → 备份该条原始 JSON。备份失败也不阻断，退化为原来的「丢弃 + 记日志」。
/// </summary>
public sealed class DraftService
{
    /// <summary>损坏备份保留上限（按前缀计数），超出删除最旧的，避免长期运行堆积。</summary>
    private const int MaxCorruptBackups = 10;

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
                // 先备份该条原始 JSON，再从内存移出——否则 Persist 会把它永久写没
                var backup = BackupCorrupt(entry.Json, KeyOf<T>());
                _entries.Remove(KeyOf<T>());
                Persist();
                Logger.Error(
                    $"草稿解析失败（{KeyOf<T>()}），已移出"
                    + (backup != null ? $"，原始内容备份至 {backup}" : "（备份失败，内容已丢失）"),
                    ex);
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

    /// <summary>备份文件名前缀：与草稿文件同目录，便于按前缀计数与批量清理。</summary>
    private string BackupPrefix => Path.GetFileNameWithoutExtension(_path) + ".corrupt-";

    /// <summary>
    /// 把损坏内容原样落盘备份，返回备份路径；任何失败返回 null 且**不抛**。
    /// <paramref name="rawJson"/> 非空 → 备份这段字符串（单条草稿损坏）；
    /// 为 null → 复制整个草稿文件（顶层文件损坏）。
    /// </summary>
    private string? BackupCorrupt(string? rawJson, string? entryKey = null)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            Directory.CreateDirectory(dir);

            var tag = string.IsNullOrEmpty(entryKey) ? "file" : entryKey;
            // 带毫秒，避免同一秒内多次损坏互相覆盖
            var target = Path.Combine(dir, $"{BackupPrefix}{DateTime.Now:yyyyMMdd_HHmmss_fff}.{tag}.bak");

            if (rawJson != null)
                File.WriteAllText(target, rawJson);
            else if (File.Exists(_path))
                File.Copy(_path, target, overwrite: true);
            else
                return null;

            PruneCorruptBackups(dir);
            return target;
        }
        catch (Exception ex)
        {
            Logger.Error("备份损坏草稿失败（不影响主流程）", ex);
            return null;
        }
    }

    /// <summary>只保留最近 <see cref="MaxCorruptBackups"/> 个损坏备份，删除更旧的。</summary>
    private void PruneCorruptBackups(string dir)
    {
        try
        {
            var stale = Directory.GetFiles(dir, BackupPrefix + "*")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Skip(MaxCorruptBackups);
            foreach (var file in stale) File.Delete(file);
        }
        catch { /* 清理失败无害，下次备份时再试 */ }
    }

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
            // 文件损坏不应阻断启动：先备份坏文件（人工仍可抢救内容），再重置为空。
            // ⚠️ 必须在任何 Persist 之前备份——后续 Save/Clear 会覆盖原文件。
            var backup = BackupCorrupt(rawJson: null);
            _entries = new Dictionary<string, DraftEntry>();
            Logger.Error(
                "加载表单草稿失败，已重置"
                + (backup != null ? $"，坏文件已备份至 {backup}" : "（备份失败，原文件将在下次保存时被覆盖）"),
                ex);
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
