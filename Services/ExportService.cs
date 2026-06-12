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

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
