using System.Data.SQLite;
using System.IO;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Data;

public static class DatabaseInitializer
{
    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant",
        "eapwork.db");

    // 仅供单元测试使用：重定向连接字符串到临时库，避免污染用户数据。
    private static string? _testConnectionString;
    public static void SetTestConnectionString(string? connectionString) => _testConnectionString = connectionString;

    public static string ConnectionString => _testConnectionString ?? $"Data Source={DbPath}";

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

            CREATE TABLE IF NOT EXISTS LeaveRecord (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Date TEXT NOT NULL,
                LeaveType TEXT NOT NULL,
                Note TEXT DEFAULT '',
                Hours REAL DEFAULT 8
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

        // 迁移：为 Issue 表添加状态和优先级字段
        try
        {
            migrateCmd.CommandText = "ALTER TABLE Issue ADD COLUMN Status TEXT DEFAULT 'Open'";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        try
        {
            migrateCmd.CommandText = "ALTER TABLE Issue ADD COLUMN Priority TEXT DEFAULT 'Medium'";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        // 迁移：为 Knowledge 表添加分类和收藏字段
        try
        {
            migrateCmd.CommandText = "ALTER TABLE Knowledge ADD COLUMN Category TEXT DEFAULT ''";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        try
        {
            migrateCmd.CommandText = "ALTER TABLE Knowledge ADD COLUMN IsFavorite INTEGER DEFAULT 0";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        // 迁移：为三张表添加软删除标记（回收站功能）
        try
        {
            migrateCmd.CommandText = "ALTER TABLE WorkRecord ADD COLUMN IsDeleted INTEGER DEFAULT 0";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        try
        {
            migrateCmd.CommandText = "ALTER TABLE Knowledge ADD COLUMN IsDeleted INTEGER DEFAULT 0";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        try
        {
            migrateCmd.CommandText = "ALTER TABLE Issue ADD COLUMN IsDeleted INTEGER DEFAULT 0";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        // 迁移：为三张表添加删除时间戳（回收站排序用）
        try
        {
            migrateCmd.CommandText = "ALTER TABLE WorkRecord ADD COLUMN DeletedAt TEXT";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        try
        {
            migrateCmd.CommandText = "ALTER TABLE Knowledge ADD COLUMN DeletedAt TEXT";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        try
        {
            migrateCmd.CommandText = "ALTER TABLE Issue ADD COLUMN DeletedAt TEXT";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        // 迁移：为 LeaveRecord 表添加软删除标记与删除时间戳（回收站功能）
        try
        {
            migrateCmd.CommandText = "ALTER TABLE LeaveRecord ADD COLUMN IsDeleted INTEGER DEFAULT 0";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        try
        {
            migrateCmd.CommandText = "ALTER TABLE LeaveRecord ADD COLUMN DeletedAt TEXT";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        // 创建索引以优化查询性能
        try
        {
            migrateCmd.CommandText = @"
                CREATE INDEX IF NOT EXISTS idx_workrecord_workdate ON WorkRecord(WorkDate);
                CREATE INDEX IF NOT EXISTS idx_workrecord_project ON WorkRecord(ProjectName);
                CREATE INDEX IF NOT EXISTS idx_workrecord_highlight ON WorkRecord(IsHighlight);
                CREATE INDEX IF NOT EXISTS idx_workrecord_deleted ON WorkRecord(IsDeleted);
                CREATE INDEX IF NOT EXISTS idx_issue_project ON Issue(ProjectName);
                CREATE INDEX IF NOT EXISTS idx_issue_status ON Issue(Status);
                CREATE INDEX IF NOT EXISTS idx_issue_deleted ON Issue(IsDeleted);
                CREATE INDEX IF NOT EXISTS idx_knowledge_category ON Knowledge(Category);
                CREATE INDEX IF NOT EXISTS idx_knowledge_favorite ON Knowledge(IsFavorite);
                CREATE INDEX IF NOT EXISTS idx_knowledge_deleted ON Knowledge(IsDeleted);
                CREATE INDEX IF NOT EXISTS idx_leaverecord_date ON LeaveRecord(Date);
                CREATE INDEX IF NOT EXISTS idx_leaverecord_deleted ON LeaveRecord(IsDeleted);
            ";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 索引创建失败不影响启动 */ }

        // 迁移：为 WorkRecord 添加 UniqueId（导入去重/覆盖的匹配键）
        try
        {
            migrateCmd.CommandText = "ALTER TABLE WorkRecord ADD COLUMN UniqueId TEXT DEFAULT ''";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 列已存在 */ }

        try
        {
            migrateCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_workrecord_uniqueid ON WorkRecord(UniqueId)";
            migrateCmd.ExecuteNonQuery();
        }
        catch { /* 索引创建失败不影响启动 */ }

        // 回填：为历史记录（空 UniqueId）生成稳定唯一标识，保证「导出后再导入」幂等、不产生重复
        try
        {
            using var backfillCmd = connection.CreateCommand();
            backfillCmd.CommandText = @"
                SELECT Id, WorkDate, ProjectName, Content, Achievement, Problem, Solution,
                       Hours, Progress, IsHighlight, HighlightNote, CreateTime
                FROM WorkRecord WHERE UniqueId IS NULL OR UniqueId = ''";
            using var reader = backfillCmd.ExecuteReader();
            var updates = new List<(int Id, string Uid)>();
            while (reader.Read())
            {
                var rec = new WorkRecord
                {
                    Id = reader.GetInt32(0),
                    WorkDate = reader.GetString(1),
                    ProjectName = reader.GetString(2),
                    Content = reader.GetString(3),
                    Achievement = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Problem = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Solution = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Hours = reader.GetDouble(7),
                    Progress = reader.GetInt32(8),
                    IsHighlight = reader.GetInt32(9),
                    HighlightNote = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    CreateTime = reader.IsDBNull(11) ? "" : reader.GetString(11)
                };
                updates.Add((rec.Id, WorkRecordIdentityHelper.GenerateUniqueId(rec)));
            }
            reader.Close();
            if (updates.Count > 0)
            {
                using var upd = connection.CreateCommand();
                upd.CommandText = "UPDATE WorkRecord SET UniqueId = @Uid WHERE Id = @Id";
                foreach (var u in updates)
                {
                    upd.Parameters.Clear();
                    upd.Parameters.AddWithValue("@Uid", u.Uid);
                    upd.Parameters.AddWithValue("@Id", u.Id);
                    upd.ExecuteNonQuery();
                }
            }
        }
        catch { /* 回填失败不阻断启动 */ }
    }
}
