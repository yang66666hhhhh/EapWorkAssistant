using System.Data.SQLite;
using System.IO;
using EapWorkAssistant.Helpers;

namespace EapWorkAssistant.Services;

public static class DatabaseBackupService
{
    private static readonly string BackupDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant",
        "backups");

    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant",
        "eapwork.db");

    private const int MaxBackupDays = 30;

    public static void BackupIfNeeded()
    {
        try
        {
            if (!File.Exists(DbPath))
                return;

            if (!Directory.Exists(BackupDir))
                Directory.CreateDirectory(BackupDir);

            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var backupFile = Path.Combine(BackupDir, $"eapwork_{today}.db");

            if (!File.Exists(backupFile))
            {
                SafeBackup(backupFile);
                ToastService.Info("数据库已自动备份", "数据安全");
            }

            CleanupOldBackups();
        }
        catch (Exception ex)
        {
            Logger.Error("数据库备份失败", ex);
            ToastService.Error("数据库备份失败，数据安全存在风险");
        }
    }

    /// <summary>
    /// 安全备份：先 checkpoint WAL，再使用 SQLite backup API
    /// </summary>
    private static void SafeBackup(string backupFile)
    {
        using var srcConn = new SQLiteConnection($"Data Source={DbPath};Version=3;");
        srcConn.Open();

        // 将 WAL 日志刷入主数据库文件
        using (var cmd = new SQLiteCommand("PRAGMA wal_checkpoint(FULL);", srcConn))
            cmd.ExecuteNonQuery();

        using var dstConn = new SQLiteConnection($"Data Source={backupFile};Version=3;");
        dstConn.Open();

        // 使用 SQLite 内置 backup API，确保一致性
        srcConn.BackupDatabase(dstConn, "main", "main", -1, null, 0);
    }

    private static void CleanupOldBackups()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-MaxBackupDays);
            var files = Directory.GetFiles(BackupDir, "eapwork_*.db");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("清理旧备份失败", ex);
        }
    }
}
