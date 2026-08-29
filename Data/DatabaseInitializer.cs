using System.Data.SQLite;
using System.IO;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Data;

/// <summary>
/// 数据库初始化与迁移。
/// 迁移采用「版本表 + 幂等前置检查」策略：
/// - 版本号记录在 SQLite 的 PRAGMA user_version 中，随库走，天然原子。
/// - 每个迁移步骤执行前先检查目标列/索引是否已存在，因此可重复执行（幂等），
///   不再依赖 try/catch 吞异常来判断「列已存在」。
/// - 迁移失败会写入日志并中止后续迁移，版本号停留在最后一个成功版本，
///   保证问题可见、可追溯，而不是被静默吞掉。
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>当前代码期望的最新 schema 版本。新增迁移时必须递增此常量。</summary>
    private const int LatestSchemaVersion = 8;

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

        // 必须在建表之前判断：建表后表一定存在，届时再判断就失去意义。
        // 全新库 → 建表（含完整 schema）后直接标记为最新版本，无需跑任何 ALTER。
        var isFreshDatabase = IsEmptyDatabase(connection);
        CreateTables(connection);

        if (isFreshDatabase)
        {
            SetSchemaVersion(connection, LatestSchemaVersion);
            return;
        }

        // 已存在的库：按版本补齐缺失列/索引。
        ApplyMigrations(connection);
    }

    // ===== 建表（最新 schema） =====

    private const string CreateTablesSql = @"
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
            CreateTime TEXT,
            IsHighlight INTEGER DEFAULT 0,
            HighlightNote TEXT DEFAULT '',
            IsDeleted INTEGER DEFAULT 0,
            DeletedAt TEXT,
            UniqueId TEXT DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS Knowledge (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT,
            Content TEXT,
            Tags TEXT,
            CreateTime TEXT,
            Category TEXT DEFAULT '',
            IsFavorite INTEGER DEFAULT 0,
            IsDeleted INTEGER DEFAULT 0,
            DeletedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS Issue (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProjectName TEXT,
            Title TEXT,
            Description TEXT,
            RootCause TEXT,
            Solution TEXT,
            Keywords TEXT,
            CreateTime TEXT,
            Status TEXT DEFAULT 'Open',
            Priority TEXT DEFAULT 'Medium',
            IsDeleted INTEGER DEFAULT 0,
            DeletedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS LeaveRecord (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Date TEXT NOT NULL,
            LeaveType TEXT NOT NULL,
            Note TEXT DEFAULT '',
            Hours REAL DEFAULT 8,
            IsDeleted INTEGER DEFAULT 0,
            DeletedAt TEXT
        );
    ";

    private static void CreateTables(SQLiteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = CreateTablesSql;
        cmd.ExecuteNonQuery();
    }

    // ===== 版本管理 =====

    private static int GetSchemaVersion(SQLiteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version";
        var value = cmd.ExecuteScalar();
        return value is long l ? (int)l : Convert.ToInt32(value ?? 0);
    }

    /// <summary>设置版本号。参数为本模块内部常量整数，无注入风险（PRAGMA 不支持参数化）。</summary>
    private static void SetSchemaVersion(SQLiteConnection connection, int version)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version}";
        cmd.ExecuteNonQuery();
    }

    /// <summary>判断表中是否已存在某列（用 PRAGMA table_info，避免在业务路径里靠异常判断）。</summary>
    private static bool ColumnExists(SQLiteConnection connection, string table, string column)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader["name"]?.ToString(), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void AddColumnIfMissing(SQLiteConnection connection, string table, string column, string definition)
    {
        if (ColumnExists(connection, table, column)) return;
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        cmd.ExecuteNonQuery();
    }

    // ===== 迁移定义 =====

    private sealed class Migration
    {
        public required int Version { get; init; }
        public required string Name { get; init; }
        public required Action<SQLiteConnection> Apply { get; init; }
    }

    /// <summary>
    /// 按版本号升序排列的迁移列表。
    /// 新增迁移：复制一份条目，Version 取 LatestSchemaVersion+1，并同步递增 LatestSchemaVersion。
    /// 已发布的迁移**只能追加、不得修改**，否则历史库会与新库产生结构分叉。
    /// </summary>
    private static readonly List<Migration> Migrations =
    [
        new Migration
        {
            Version = 1,
            Name = "WorkRecord 亮点字段",
            Apply = c =>
            {
                AddColumnIfMissing(c, "WorkRecord", "IsHighlight", "INTEGER DEFAULT 0");
                AddColumnIfMissing(c, "WorkRecord", "HighlightNote", "TEXT DEFAULT ''");
            }
        },
        new Migration
        {
            Version = 2,
            Name = "Issue 状态与优先级",
            Apply = c =>
            {
                AddColumnIfMissing(c, "Issue", "Status", "TEXT DEFAULT 'Open'");
                AddColumnIfMissing(c, "Issue", "Priority", "TEXT DEFAULT 'Medium'");
            }
        },
        new Migration
        {
            Version = 3,
            Name = "Knowledge 分类与收藏",
            Apply = c =>
            {
                AddColumnIfMissing(c, "Knowledge", "Category", "TEXT DEFAULT ''");
                AddColumnIfMissing(c, "Knowledge", "IsFavorite", "INTEGER DEFAULT 0");
            }
        },
        new Migration
        {
            Version = 4,
            Name = "软删除标记（工作记录/知识库/问题跟踪）",
            Apply = c =>
            {
                AddColumnIfMissing(c, "WorkRecord", "IsDeleted", "INTEGER DEFAULT 0");
                AddColumnIfMissing(c, "Knowledge", "IsDeleted", "INTEGER DEFAULT 0");
                AddColumnIfMissing(c, "Issue", "IsDeleted", "INTEGER DEFAULT 0");
            }
        },
        new Migration
        {
            Version = 5,
            Name = "删除时间戳（工作记录/知识库/问题跟踪）",
            Apply = c =>
            {
                AddColumnIfMissing(c, "WorkRecord", "DeletedAt", "TEXT");
                AddColumnIfMissing(c, "Knowledge", "DeletedAt", "TEXT");
                AddColumnIfMissing(c, "Issue", "DeletedAt", "TEXT");
            }
        },
        new Migration
        {
            Version = 6,
            Name = "LeaveRecord 软删除标记与时间戳",
            Apply = c =>
            {
                AddColumnIfMissing(c, "LeaveRecord", "IsDeleted", "INTEGER DEFAULT 0");
                AddColumnIfMissing(c, "LeaveRecord", "DeletedAt", "TEXT");
            }
        },
        new Migration
        {
            Version = 7,
            Name = "查询性能索引",
            Apply = c => ExecuteNonQuery(c, @"
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
                CREATE INDEX IF NOT EXISTS idx_leaverecord_deleted ON LeaveRecord(IsDeleted);")
        },
        new Migration
        {
            Version = 8,
            Name = "WorkRecord UniqueId 与历史数据回填",
            Apply = c =>
            {
                AddColumnIfMissing(c, "WorkRecord", "UniqueId", "TEXT DEFAULT ''");
                ExecuteNonQuery(c, "CREATE INDEX IF NOT EXISTS idx_workrecord_uniqueid ON WorkRecord(UniqueId)");
                BackfillUniqueIds(c);
            }
        },
    ];

    private static void ApplyMigrations(SQLiteConnection connection)
    {
        var current = GetSchemaVersion(connection);

        foreach (var migration in Migrations.OrderBy(m => m.Version))
        {
            if (migration.Version <= current) continue;

            try
            {
                using var transaction = connection.BeginTransaction();
                migration.Apply(connection);
                SetSchemaVersion(connection, migration.Version);
                transaction.Commit();
            }
            catch (Exception ex)
            {
                // 不再静默吞掉：完整记录异常，并停止后续迁移（版本号停留在最后成功版本）。
                // 不向上抛出是为了避免启动即崩溃；问题会留在日志里可供排查。
                try { transactionRollbackSafe(connection); } catch { /* 回滚失败也只记录 */ }
                Logger.Error($"数据库迁移 v{migration.Version}（{migration.Name}）执行失败，已中止后续迁移。" +
                             $"当前库版本停留在 v{GetSchemaVersion(connection)}。", ex);
                break;
            }
        }
    }

    /// <summary>回滚辅助：迁移失败时尽量回滚，避免留下半截 schema。</summary>
    private static void transactionRollbackSafe(SQLiteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "ROLLBACK";
        try { cmd.ExecuteNonQuery(); } catch { /* 无活动事务时忽略 */ }
    }

    private static void ExecuteNonQuery(SQLiteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>判断是否为全新空库（不含任何已知业务表）。</summary>
    private static bool IsEmptyDatabase(SQLiteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN " +
                          "('WorkRecord','Knowledge','Issue','LeaveRecord')";
        var count = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        return count == 0;
    }

    // ===== UniqueId 回填 =====

    /// <summary>
    /// 为历史记录（空 UniqueId）生成稳定唯一标识，保证「导出后再导入」幂等、不产生重复。
    /// 所有列读取均做 DBNull 防护：这些 TEXT 列在建表时允许 NULL，
    /// 历史脏数据可能为 NULL，直接 GetString 会抛 InvalidCastException。
    /// </summary>
    private static void BackfillUniqueIds(SQLiteConnection connection)
    {
        var updates = new List<(int Id, string Uid)>();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT Id, WorkDate, ProjectName, Content, Achievement, Problem, Solution,
                       Hours, Progress, IsHighlight, HighlightNote, CreateTime
                FROM WorkRecord WHERE UniqueId IS NULL OR UniqueId = ''";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var rec = new WorkRecord
                {
                    Id = SafeInt(reader, 0),
                    WorkDate = SafeString(reader, 1),
                    ProjectName = SafeString(reader, 2),
                    Content = SafeString(reader, 3),
                    Achievement = SafeString(reader, 4),
                    Problem = SafeString(reader, 5),
                    Solution = SafeString(reader, 6),
                    Hours = SafeDouble(reader, 7),
                    Progress = SafeInt(reader, 8),
                    IsHighlight = SafeInt(reader, 9),
                    HighlightNote = SafeString(reader, 10),
                    CreateTime = SafeString(reader, 11)
                };
                updates.Add((rec.Id, WorkRecordIdentityHelper.GenerateUniqueId(rec)));
            }
        }

        if (updates.Count == 0) return;

        using var upd = connection.CreateCommand();
        upd.CommandText = "UPDATE WorkRecord SET UniqueId = @Uid WHERE Id = @Id";
        var uidParam = upd.Parameters.Add("@Uid", System.Data.DbType.String);
        var idParam = upd.Parameters.Add("@Id", System.Data.DbType.Int32);
        foreach (var (id, uid) in updates)
        {
            uidParam.Value = uid;
            idParam.Value = id;
            upd.ExecuteNonQuery();
        }
    }

    // ===== 安全读取（DBNull 防护） =====

    private static string SafeString(SQLiteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static double SafeDouble(SQLiteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0d : reader.GetDouble(ordinal);

    private static int SafeInt(SQLiteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
}
