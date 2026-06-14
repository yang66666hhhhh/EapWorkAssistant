using Microsoft.Win32;
using System.IO;
using System.Text;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Services;

public static class ExportService
{
    public static void CopyToClipboard(string text)
    {
        System.Windows.Clipboard.SetText(text);
    }

    public static void SaveToFile(string content, string defaultName = "report")
    {
        var dialog = new SaveFileDialog
        {
            Filter = "文本文件|*.txt|Markdown文件|*.md|所有文件|*.*",
            FileName = $"{defaultName}_{DateTime.Now:yyyyMMdd}.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, content, Encoding.UTF8);
        }
    }

    public static void SaveAsMarkdown(string title, string content, string defaultName = "report")
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
        }
    }

    public static string ExportToMarkdown(string title, string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine(content);
        return sb.ToString();
    }

    public static void ExportToCsv(IEnumerable<WorkRecord> records, string defaultName = "工作记录")
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
            // CSV头
            sb.AppendLine("日期,项目,类型,内容,工时,进度,是否亮点,问题,解决方案");
            // 数据行
            foreach (var r in records)
            {
                sb.AppendLine($"{EscapeCsv(r.WorkDate)},{EscapeCsv(r.ProjectName)},{EscapeCsv(r.WorkType)},{EscapeCsv(r.Content)},{r.Hours},{r.Progress},{(r.IsHighlight == 1 ? "是" : "否")},{EscapeCsv(r.Problem)},{EscapeCsv(r.Solution)}");
            }
            File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
        }
    }

    /// <summary>
    /// 从 CSV 文件导入工作记录，返回解析后的记录列表。
    /// 使用状态机解析，正确处理引号内换行符。
    /// </summary>
    public static List<WorkRecord>? ImportFromCsv()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV文件|*.csv|所有文件|*.*",
            Title = "选择要导入的 CSV 文件"
        };

        if (dialog.ShowDialog() != true) return null;

        try
        {
            var content = File.ReadAllText(dialog.FileName, Encoding.UTF8);
            var rows = ParseCsvRows(content);
            if (rows.Count < 2) return null; // 至少需要标题行 + 1行数据

            var records = new List<WorkRecord>();
            // 跳过标题行（第一行）
            for (int i = 1; i < rows.Count; i++)
            {
                var fields = rows[i];
                if (fields.Count == 0 || (fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0])))
                    continue;
                if (fields.Count < 5) continue;

                var record = new WorkRecord
                {
                    WorkDate = fields[0],
                    ProjectName = fields[1],
                    WorkType = fields[2],
                    Content = fields[3],
                    Hours = double.TryParse(fields[4], out var h) ? h : 0,
                    Progress = fields.Count > 5 && int.TryParse(fields[5], out var p) ? p : 0,
                    IsHighlight = fields.Count > 6 && fields[6] == "是" ? 1 : 0,
                    Problem = fields.Count > 7 ? fields[7] : "",
                    Solution = fields.Count > 8 ? fields[8] : "",
                    CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                records.Add(record);
            }

            return records;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// CSV 状态机解析器：正确处理引号内的逗号、换行、转义引号
    /// </summary>
    private static List<List<string>> ParseCsvRows(string content)
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

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
