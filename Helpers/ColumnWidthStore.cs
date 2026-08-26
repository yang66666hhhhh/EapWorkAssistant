using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// DataGrid 列宽记忆的本地持久化存储。
/// 以 TableKey 为维度保存每张表各列的像素宽度，落盘到
/// %LOCALAPPDATA%/EapWorkAssistant/columnwidths.json，带 400ms 防抖，全程 try/catch 容错。
/// </summary>
internal static class ColumnWidthStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant", "columnwidths.json");

    private static Dictionary<string, Dictionary<string, double>> _cache = Load();
    private static readonly object _lock = new();
    private static Timer? _flushTimer;

    private static Dictionary<string, Dictionary<string, double>> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(json);
                if (dict != null) return dict;
            }
        }
        catch
        {
            // 损坏或不可读时回退到空表，不阻断 UI
        }
        return new Dictionary<string, Dictionary<string, double>>();
    }

    public static bool TryGet(string tableKey, string columnKey, out double width)
    {
        width = 0;
        lock (_lock)
        {
            if (_cache.TryGetValue(tableKey, out var table) &&
                table.TryGetValue(columnKey, out width))
            {
                return true;
            }
        }
        return false;
    }

    public static void SetWidth(string tableKey, string columnKey, double width)
    {
        lock (_lock)
        {
            if (!_cache.TryGetValue(tableKey, out var table))
                _cache[tableKey] = table = new Dictionary<string, double>();
            table[columnKey] = width;
        }
        ScheduleFlush();
    }

    private static void ScheduleFlush()
    {
        // 拖拽过程中高频写入，合并为 400ms 一次的落盘
        if (_flushTimer == null)
            _flushTimer = new Timer(_ => Flush(), null, 400, Timeout.Infinite);
        else
            _flushTimer.Change(400, Timeout.Infinite);
    }

    private static void Flush()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (dir != null) Directory.CreateDirectory(dir);
            string json;
            lock (_lock)
            {
                json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
            }
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // 持久化失败不应影响主流程
        }
    }
}
