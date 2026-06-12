using System.Data.SQLite;
using System.IO;

namespace EapWorkAssistant.Data;

public static class DatabaseInitializer
{
    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant",
        "eapwork.db");

    public static string ConnectionString => $"Data Source={DbPath}";

    public static void Initialize()
    {
        var dir = Path.GetDirectoryName(DbPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var connection = new SQLiteConnection(ConnectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS WorkRecord (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                WorkDate TEXT,
                ProjectName TEXT,
                WorkType TEXT,
                Content TEXT,
                Achievement TEXT,
                Problem TEXT,
                Solution TEXT,
                Hours REAL,
                Progress INTEGER,
                CreateTime TEXT
            );

            CREATE TABLE IF NOT EXISTS Knowledge (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT,
                Content TEXT,
                Tags TEXT,
                CreateTime TEXT
            );

            CREATE TABLE IF NOT EXISTS Issue (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProjectName TEXT,
                Title TEXT,
                Description TEXT,
                RootCause TEXT,
                Solution TEXT,
                Keywords TEXT,
                CreateTime TEXT
            );
        ";
        cmd.ExecuteNonQuery();

        // 迁移：为 WorkRecord 表添加亮点字段
        using var migrateCmd = connection.CreateCommand();
        try
        {
            migrateCmd.CommandText = "ALTER TABLE WorkRecord ADD COLUMN IsHighlight INTEGER DEFAULT 0";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        try
        {
            migrateCmd.CommandText = "ALTER TABLE WorkRecord ADD COLUMN HighlightNote TEXT DEFAULT ''";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }
    }
}
