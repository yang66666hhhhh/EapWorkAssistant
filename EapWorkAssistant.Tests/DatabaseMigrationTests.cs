using EapWorkAssistant.Data;
using System.Data.SQLite;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// DatabaseInitializer 迁移测试，重点是 v10「工时双口径」数据搬迁。
///
/// 背景（用户口径）：填报工时 = 报给公司的，统一每天 8h；实际工时 = 真实投入，沿用历史填写值。
/// ⚠️ 一天可能有多条记录（历史最多 5 条），逐条设 8h 会把一天放大成 40h，
///    因此必须按天分摊——这是本组测试要钉住的行为。
/// </summary>
public class DatabaseMigrationTests
{
    // ===== 纯算法：按天分摊填报工时 =====

    [Fact]
    public void AllocateReportHours_单条记录_直接给满8h()
    {
        var r = DatabaseInitializer.AllocateReportHours([9.0]);

        Assert.Single(r);
        Assert.Equal(8.0, r[0], 3);
    }

    [Fact]
    public void AllocateReportHours_一天多条_按实际工时占比分摊且合计恰为8h()
    {
        // 实际 2 / 2 / 6（合计 10）→ 填报 1.6 / 1.6 / 4.8
        var r = DatabaseInitializer.AllocateReportHours([2.0, 2.0, 6.0]);

        Assert.Equal(1.6, r[0], 3);
        Assert.Equal(1.6, r[1], 3);
        Assert.Equal(4.8, r[2], 3);
        Assert.Equal(8.0, r.Sum(), 3);
    }

    [Fact]
    public void AllocateReportHours_除不尽_末条吸收舍入误差保证精确()
    {
        // 8 / 3 = 2.666…：前两条 2.67，末条吸收误差 = 8 - 5.34 = 2.66
        var r = DatabaseInitializer.AllocateReportHours([1.0, 1.0, 1.0]);

        Assert.Equal(2.67, r[0], 3);
        Assert.Equal(2.66, r[2], 3);
        Assert.Equal(8.0, r.Sum(), 3);
    }

    [Fact]
    public void AllocateReportHours_当天无实际工时_退化为平均分摊避免除零()
    {
        var r = DatabaseInitializer.AllocateReportHours([0.0, 0.0]);

        Assert.Equal(4.0, r[0], 3);
        Assert.Equal(4.0, r[1], 3);
    }

    // ===== 集成：真跑一次 v10 迁移 =====

    [Fact]
    public void 迁移v10_旧工时搬到实际工时_填报按天归8h()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eapmig_{Guid.NewGuid():N}.db");
        try
        {
            // 造一个 v9 状态的库：ActualHours 列已存在但值为 0，工时仍在 Hours
            using (var conn = new SQLiteConnection($"Data Source={path}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE WorkRecord (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        WorkDate TEXT,
                        Hours REAL,
                        ActualHours REAL DEFAULT 0,
                        IsDeleted INTEGER DEFAULT 0);
                    INSERT INTO WorkRecord (WorkDate, Hours, IsDeleted) VALUES
                        ('2026-08-21', 2, 0), ('2026-08-21', 2, 0), ('2026-08-21', 6, 0),
                        ('2026-08-24', 9, 0);
                    PRAGMA user_version = 9;";
                cmd.ExecuteNonQuery();
            }

            DatabaseInitializer.SetTestConnectionString($"Data Source={path}");
            DatabaseInitializer.Initialize();

            using var c = new SQLiteConnection($"Data Source={path}");
            c.Open();

            // 1) 实际工时 = 原填报值（2 / 2 / 6 原样保留）
            var rows = Query(c, "SELECT Hours, ActualHours FROM WorkRecord WHERE WorkDate='2026-08-21' ORDER BY Id");
            Assert.Equal(3, rows.Count);
            Assert.Equal(2.0, rows[0][1], 3);
            Assert.Equal(2.0, rows[1][1], 3);
            Assert.Equal(6.0, rows[2][1], 3);

            // 2) 填报工时被归一到「当天合计 8h」，按占比分摊
            Assert.Equal(1.6, rows[0][0], 3);
            Assert.Equal(4.8, rows[2][0], 3);

            // 3) 单条记录的日子：填报直接给 8h，实际保留原值 9h
            var single = Query(c, "SELECT Hours, ActualHours FROM WorkRecord WHERE WorkDate='2026-08-24'");
            Assert.Equal(8.0, single[0][0], 3);
            Assert.Equal(9.0, single[0][1], 3);

            // 4) schema 版本推进到最新
            Assert.Equal(10, (int)Query(c, "PRAGMA user_version")[0][0]);
        }
        finally
        {
            DatabaseInitializer.SetTestConnectionString(null);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static List<double[]> Query(SQLiteConnection conn, string sql)
    {
        var list = new List<double[]>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new double[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                row[i] = reader.IsDBNull(i) ? 0 : Convert.ToDouble(reader.GetValue(i));
            list.Add(row);
        }
        return list;
    }
}
