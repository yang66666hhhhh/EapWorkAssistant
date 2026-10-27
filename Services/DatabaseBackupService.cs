using System.Data.SQLite;
using System.IO;
using EapWorkAssistant.Helpers;

namespace EapWorkAssistant.Services;

public static class DatabaseBackupService
{
    // 路径统一取自 LocalDataService，避免两处各拼一份导致设置页显示与实际不一致
    private static string BackupDir => LocalDataService.BackupDirectory;

    private static string DbPath => LocalDataService.DatabasePath;

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
                var isFirstBackup = !Directory.Exists(BackupDir)
                    || Directory.GetFiles(BackupDir, "eapwork_*.db").Length == 0;
                SafeBackup(backupFile);
                // 日常备份静默进行；仅首次备份提示一次，避免每次启动打扰
                if (isFirstBackup)
                    ToastService.Info("已创建首次数据库备份", "数据安全");
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
    /// 立即生成一份手动备份（设置页「立即备份」入口）。
    /// 与每日备份分开命名（eapwork_manual_*），同日多次点击不会互相覆盖；
    /// 仍走 `eapwork_*.db` 通配，因此同样受 30 天保留策略约束。
    /// 成功返回备份文件全路径；数据库不存在或写盘失败返回 null（已记日志）。
    /// </summary>
    public static string? BackupNow()
    {
        try
        {
            if (!File.Exists(DbPath))
                return null;

            if (!Directory.Exists(BackupDir))
                Directory.CreateDirectory(BackupDir);

            // 带毫秒，避免同一秒内连点两次互相覆盖
            var file = Path.Combine(BackupDir, $"eapwork_manual_{DateTime.Now:yyyyMMdd_HHmmss_fff}.db");
            SafeBackup(file);
            CleanupOldBackups();
            return file;
        }
        catch (Exception ex)
        {
            Logger.Error("手动备份失败", ex);
            return null;
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
