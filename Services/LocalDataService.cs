using System.Diagnostics;
using System.IO;
using EapWorkAssistant.Helpers;

namespace EapWorkAssistant.Services;

/// <summary>
/// 本地数据位置与占用的**只读**查询，外加「打开目录 / 立即备份」两个动作。
/// 设置页「数据管理」区块的唯一数据来源。
///
/// 职责边界：
/// - 只读查询 + 打开资源管理器，**不删除任何文件**（清理属用户自己的决定）
/// - 路径常量集中在此，DatabaseBackupService 与其他服务引用同一来源，避免各拼各的导致不一致
/// </summary>
public static class LocalDataService
{
    /// <summary>应用数据根目录（%LOCALAPPDATA%\EapWorkAssistant）。</summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant");

    /// <summary>主数据库文件。</summary>
    public static string DatabasePath { get; } = Path.Combine(DataDirectory, "eapwork.db");

    /// <summary>每日自动备份与手动备份目录。</summary>
    public static string BackupDirectory { get; } = Path.Combine(DataDirectory, "backups");

    /// <summary>错误日志目录。</summary>
    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "logs");

    /// <summary>单个文件的快照信息。</summary>
    public sealed record FileSnapshot(string Path, long SizeBytes, DateTime LastWriteTime);

    /// <summary>
    /// 读取文件快照；文件不存在返回 null（调用方据此显示「未创建」而不是 0 B）。
    /// 只读属性读取，不打开文件句柄。
    /// </summary>
    public static FileSnapshot? GetSnapshot(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists
                ? new FileSnapshot(info.FullName, info.Length, info.LastWriteTime)
                : null;
        }
        catch (Exception ex)
        {
            Logger.Error($"读取文件信息失败：{path}", ex);
            return null;
        }
    }

    /// <summary>备份摘要：份数与最近一份的时间（无备份时 LatestForDisplay 为 null）。</summary>
    public sealed record BackupSummary(int Count, DateTime? Latest);

    /// <summary>
    /// 统计备份目录下 `eapwork_*.db`。
    /// 目录不存在视为 0 份（首次使用尚未备份的常态，不算错误）。
    /// </summary>
    public static BackupSummary GetBackupSummary()
    {
        try
        {
            if (!Directory.Exists(BackupDirectory))
                return new BackupSummary(0, null);

            var files = new DirectoryInfo(BackupDirectory).GetFiles("eapwork_*.db");
            if (files.Length == 0)
                return new BackupSummary(0, null);

            return new BackupSummary(files.Length, files.Max(f => f.LastWriteTime));
        }
        catch (Exception ex)
        {
            Logger.Error("统计备份目录失败", ex);
            return new BackupSummary(0, null);
        }
    }

    /// <summary>
    /// 人类可读的字节数。1024 进制，保留一位小数并去掉多余的 .0。
    /// 负数与 0 分别返回「—」与「0 B」，避免界面出现 "-1 B"。
    /// </summary>
    public static string FormatSize(long bytes)
    {
        if (bytes < 0) return "—";
        if (bytes < 1024) return $"{bytes} B";

        double value = bytes;
        foreach (var unit in new[] { "KB", "MB", "GB" })
        {
            value /= 1024;
            if (value < 1024)
                return value.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) + " " + unit;
        }
        return value.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) + " TB";
    }

    /// <summary>
    /// 在资源管理器中打开目录。目录不存在返回 false（调用方提示，不代为创建）。
    /// </summary>
    public static bool OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return false;

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"打开目录失败：{path}", ex);
            return false;
        }
    }
}
