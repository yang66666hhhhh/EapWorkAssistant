using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;

[assembly: InternalsVisibleTo("EapWorkAssistant.Tests")]

namespace EapWorkAssistant.Services;

public static class ExportService
{
    public static void CopyToClipboard(string text)
    {
        System.Windows.Clipboard.SetText(text);
    }

    public static bool SaveToFile(string content, string defaultName = "report")
    {
        var dialog = new SaveFileDialog
        {
            Filter = "文本文件|*.txt|Markdown文件|*.md|所有文件|*.*",
            FileName = $"{defaultName}_{DateTime.Now:yyyyMMdd}.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, content, Encoding.UTF8);
            return true;
        }
        return false;
    }

    public static bool SaveAsMarkdown(string title, string content, string defaultName = "report")
    {
        var markdown = ExportToMarkdown(title, content);
        var dialog = new SaveFileDialog
        {
            Filter = "Markdown文件|*.md|文本文件|*.txt|所有文件|*.*",
            FileName = $"{defaultName}_{DateTime.Now:yyyyMMdd}.md"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, markdown, Encoding.UTF8);
            return true;
        }
        return false;
    }

    public static string ExportToMarkdown(string title, string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine(content);
        return sb.ToString();
    }

    public static bool ExportToCsv(IEnumerable<WorkRecord> records, string defaultName = "工作记录")
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV文件|*.csv",
            FileName = $"{defaultName}_{DateTime.Now:yyyyMMdd}.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            var sb = new StringBuilder();
            // 添加BOM头，确保中文在Excel中正确显示
            sb.Append('\uFEFF');
            // CSV头（UniqueId 为首列，作为导入匹配键）
            sb.AppendLine("UniqueId,日期,项目,类型,内容,工作成果,工时,进度,是否亮点,问题,解决方案");
            // 数据行
            foreach (var r in records)
            {
                var uid = string.IsNullOrWhiteSpace(r.UniqueId)
                    ? WorkRecordIdentityHelper.GenerateUniqueId(r)
                    : r.UniqueId;
                sb.AppendLine($"{EscapeCsv(uid)},{EscapeCsv(r.WorkDate)},{EscapeCsv(r.ProjectName)},{EscapeCsv(r.WorkType)},{EscapeCsv(r.Content)},{EscapeCsv(r.Achievement)},{r.Hours},{r.Progress},{(r.IsHighlight == 1 ? "是" : "否")},{EscapeCsv(r.Problem)},{EscapeCsv(r.Solution)}");
            }
            File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 从 CSV 文件导入工作记录。
    /// 返回 ImportResult 以区分「用户取消 / 解析失败 / 成功」，避免把取消误报成格式错误。
    /// 使用状态机解析，正确处理引号内换行符。
    /// </summary>
    public static ImportResult<WorkRecord> ImportFromCsv()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV文件|*.csv|所有文件|*.*",
            Title = "选择要导入的 CSV 文件"
        };

        if (dialog.ShowDialog() != true)
            return new ImportResult<WorkRecord> { Canceled = true };

        try
        {
            var content = File.ReadAllText(dialog.FileName, Encoding.UTF8);
            var rows = ParseCsvRows(content);
            if (rows.Count < 1)
                return new ImportResult<WorkRecord> { Error = "文件为空，无可导入的数据" };

            // 表头驱动映射：自动识别字段位置，兼容「带 UniqueId 的新格式」与「旧 9/10 列格式」，
            // 同时兼容完全无表头的手工 CSV（退化为位置解析）。
            Dictionary<string, int>? colMap = null;
            var hasHeader = rows[0].Count > 0 &&
                (rows[0][0] == "UniqueId" || rows[0].Contains("日期") || rows[0].Contains("项目"));
            int startIdx = 0;
            if (hasHeader)
            {
                colMap = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int c = 0; c < rows[0].Count; c++)
                    if (!colMap.ContainsKey(rows[0][c])) colMap[rows[0][c]] = c;
                startIdx = 1;
            }
            // 有表头时业务列整体右移一列（首列为 UniqueId）
            int off = hasHeader ? 1 : 0;

            string GetField(List<string> f, string name, int pos)
            {
                if (colMap != null)
                    return colMap.TryGetValue(name, out var idx) && idx < f.Count ? f[idx] : "";
                return pos < f.Count ? f[pos] : "";
            }

            var records = new List<WorkRecord>();
            for (int i = startIdx; i < rows.Count; i++)
            {
                var fields = rows[i];
                if (fields.Count == 0 || (fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0])))
                    continue;

                var record = new WorkRecord
                {
                    UniqueId = GetField(fields, "UniqueId", 0).Trim(),
                    WorkDate = GetField(fields, "日期", off + 0).Trim(),
                    ProjectName = GetField(fields, "项目", off + 1).Trim(),
                    WorkType = GetField(fields, "类型", off + 2).Trim(),
                    Content = GetField(fields, "内容", off + 3).Trim(),
                };

                if (hasHeader)
                {
                    record.Achievement = GetField(fields, "工作成果", off + 4);
                    record.Hours = double.TryParse(GetField(fields, "工时", off + 5), out var h) ? h : 0;
                    record.Progress = int.TryParse(GetField(fields, "进度", off + 6), out var p) ? p : 0;
                    record.IsHighlight = GetField(fields, "是否亮点", off + 7) == "是" ? 1 : 0;
                    record.Problem = GetField(fields, "问题", off + 8);
                    record.Solution = GetField(fields, "解决方案", off + 9);
                }
                else
                {
                    // 无表头：兼容旧 9 列 / 10 列 位置布局
                    if (fields.Count >= 10)
                    {
                        record.Achievement = fields[4];
                        record.Hours = double.TryParse(fields[5], out var h) ? h : 0;
                        record.Progress = int.TryParse(fields[6], out var p) ? p : 0;
                        record.IsHighlight = fields[7] == "是" ? 1 : 0;
                        record.Problem = fields[8];
                        record.Solution = fields[9];
                    }
                    else
                    {
                        record.Hours = double.TryParse(fields[4], out var h) ? h : 0;
                        record.Progress = fields.Count > 5 && int.TryParse(fields[5], out var p) ? p : 0;
                        record.IsHighlight = fields.Count > 6 && fields[6] == "是" ? 1 : 0;
                        record.Problem = fields.Count > 7 ? fields[7] : "";
                        record.Solution = fields.Count > 8 ? fields[8] : "";
                    }
                }

                record.CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                records.Add(record);
            }

            return new ImportResult<WorkRecord> { Items = records };
        }
        catch (Exception ex)
        {
            return new ImportResult<WorkRecord> { Error = ex.Message };
        }
    }

    /// <summary>
    /// CSV 状态机解析器：正确处理引号内的逗号、换行、转义引号
    /// </summary>
    internal static List<List<string>> ParseCsvRows(string content)
    {
        var rows = new List<List<string>>();
        var currentField = new StringBuilder();
        var currentRow = new List<string>();
        bool inQuotes = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                }
                else if (c == '\r')
                {
                    // 跳过 \r，等 \n 处理行结束
                    continue;
                }
                else if (c == '\n')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                }
                else
                {
                    currentField.Append(c);
                }
            }
        }

        // 处理文件末尾没有换行的情况
        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow);
        }

        return rows;
    }

    internal static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    #region JSON 导出 / 导入（知识库 / 问题库）

    /// <summary>
    /// JSON 导入结果：区分「用户取消」「解析失败」「成功」，避免把文件损坏误报成"文件为空"。
    /// </summary>
    public sealed class ImportResult<T>
    {
        public List<T>? Items { get; init; }
        public bool Canceled { get; init; }
        public string? Error { get; init; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static bool ExportKnowledgeToJson(IEnumerable<Knowledge> items)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON 文件|*.json|所有文件|*.*",
            FileName = $"知识库_{DateTime.Now:yyyyMMdd}.json"
        };
        if (dialog.ShowDialog() != true) return false;
        var json = JsonSerializer.Serialize(items, JsonOptions);
        File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
        return true;
    }

    public static ImportResult<Knowledge> ImportKnowledgeFromJson()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON 文件|*.json|所有文件|*.*",
            Title = "选择要导入的知识库 JSON 文件"
        };
        if (dialog.ShowDialog() != true) return new ImportResult<Knowledge> { Canceled = true };
        try
        {
            var json = File.ReadAllText(dialog.FileName, Encoding.UTF8);
            var items = JsonSerializer.Deserialize<List<Knowledge>>(json);
            return new ImportResult<Knowledge> { Items = items ?? new List<Knowledge>() };
        }
        catch (Exception ex)
        {
            return new ImportResult<Knowledge> { Error = ex.Message };
        }
    }

    public static bool ExportIssuesToJson(IEnumerable<Issue> items)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON 文件|*.json|所有文件|*.*",
            FileName = $"问题库_{DateTime.Now:yyyyMMdd}.json"
        };
        if (dialog.ShowDialog() != true) return false;
        var json = JsonSerializer.Serialize(items, JsonOptions);
        File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
        return true;
    }

    public static ImportResult<Issue> ImportIssuesFromJson()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON 文件|*.json|所有文件|*.*",
            Title = "选择要导入的问题库 JSON 文件"
        };
        if (dialog.ShowDialog() != true) return new ImportResult<Issue> { Canceled = true };
        try
        {
            var json = File.ReadAllText(dialog.FileName, Encoding.UTF8);
            var items = JsonSerializer.Deserialize<List<Issue>>(json);
            return new ImportResult<Issue> { Items = items ?? new List<Issue>() };
        }
        catch (Exception ex)
        {
            return new ImportResult<Issue> { Error = ex.Message };
        }
    }

    #endregion
}

/// <summary>导入确认弹窗的数据载体：解析概览 + 三种模式的预览计数。</summary>
public sealed class ImportCsvDialogModel
{
    public int TotalParsed { get; init; }
    public int ValidCount { get; init; }
    public List<string> SkippedReasons { get; init; } = new();
    public (int Insert, int Update, int Skip) SkipPreview { get; init; }
    public (int Insert, int Update, int Skip) OverwritePreview { get; init; }
    public (int Insert, int Update, int Skip) AppendPreview { get; init; }
}
