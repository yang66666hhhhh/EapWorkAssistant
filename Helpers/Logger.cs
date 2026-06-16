using System.IO;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 轻量日志工具：写入 %LOCALAPPDATA%\EapWorkAssistant\logs\ 目录。
/// 用于不需要 Toast 打扰但需要留痕的场景（如启动时配置加载失败）。
/// </summary>
public static class Logger
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant", "logs");

    public static void Error(string message, Exception? ex = null)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var file = Path.Combine(LogDir, $"error_{DateTime.Now:yyyyMMdd}.log");
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (ex != null) line += $"\n  {ex.GetType().Name}: {ex.Message}";
            File.AppendAllText(file, line + Environment.NewLine);
        }
        catch { /* 日志写入失败不应影响主流程 */ }
    }
}
