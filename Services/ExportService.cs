using Microsoft.Win32;
using System.IO;
using System.Text;

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
}
