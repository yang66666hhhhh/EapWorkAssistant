using System.IO;

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
                File.Copy(DbPath, backupFile, true);
                ToastService.Info("数据库已自动备份", "数据安全");
            }

            CleanupOldBackups();
        }
        catch
        {
            // Backup failure should not crash the app
        }
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
        catch
        {
            // Cleanup failure should not crash the app
        }
    }
}
