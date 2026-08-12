using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// Dashboard 统计聚合层测试：直接针对 WorkRecordRepository 的 GROUP BY / SUM 查询，
/// 用临时 SQLite 库插数据验证，并确认软删除记录被排除在统计之外。
/// </summary>
public class DashboardAggregationTests
{
    private static WorkRecord Make(string date, string project, string type, double hours, int highlight = 0)
        => new WorkRecord
        {
            WorkDate = date,
            ProjectName = project,
            WorkType = type,
            Content = "c",
            Achievement = "",
            Problem = "",
            Solution = "",
            Hours = hours,
            Progress = 0,
            IsHighlight = highlight,
            HighlightNote = "",
            CreateTime = "2026-08-01 00:00:00"
        };

    [Fact]
    public async Task GetTotalHoursAsync_汇总区间内工时()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new WorkRecordRepository();
            await repo.InsertAsync(Make("2026-08-01", "P1", "开发", 8));
            await repo.InsertAsync(Make("2026-08-02", "P1", "开发", 7.5));
            await repo.InsertAsync(Make("2026-09-01", "P1", "开发", 10)); // 区间外

            var total = await repo.GetTotalHoursAsync("2026-08-01", "2026-08-31");
            Assert.Equal(15.5, total, 3);
        }
        finally { TestDb.Cleanup(db); }
    }

    [Fact]
    public async Task GetProjectStatsAsync_按项目分组汇总工时与条数()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new WorkRecordRepository();
            await repo.InsertAsync(Make("2026-08-01", "P1", "开发", 8));
            await repo.InsertAsync(Make("2026-08-02", "P1", "运维", 4));
            await repo.InsertAsync(Make("2026-08-03", "P2", "开发", 6));

            var stats = (await repo.GetProjectStatsAsync("2026-08-01", "2026-08-31"))
                .ToDictionary(r => (string)r.ProjectName);

            Assert.Equal(2, stats.Count);
            Assert.Equal(12.0, Convert.ToDouble(stats["P1"].TotalHours), 3);
            Assert.Equal(2, Convert.ToInt32(stats["P1"].RecordCount));
            Assert.Equal(6.0, Convert.ToDouble(stats["P2"].TotalHours), 3);
            Assert.Equal(1, Convert.ToInt32(stats["P2"].RecordCount));
        }
        finally { TestDb.Cleanup(db); }
    }

    [Fact]
    public async Task GetTypeStatsAsync_按工作类型分组汇总工时()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new WorkRecordRepository();
            await repo.InsertAsync(Make("2026-08-01", "P1", "开发", 8));
            await repo.InsertAsync(Make("2026-08-02", "P2", "开发", 2));
            await repo.InsertAsync(Make("2026-08-03", "P1", "会议", 3));

            var stats = (await repo.GetTypeStatsAsync("2026-08-01", "2026-08-31"))
                .ToDictionary(r => (string)r.WorkType);

            Assert.Equal(2, stats.Count);
            Assert.Equal(10.0, Convert.ToDouble(stats["开发"].TotalHours), 3);
            Assert.Equal(3.0, Convert.ToDouble(stats["会议"].TotalHours), 3);
        }
        finally { TestDb.Cleanup(db); }
    }

    [Fact]
    public async Task GetDailyStatsAsync_按日期分组汇总()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new WorkRecordRepository();
            await repo.InsertAsync(Make("2026-08-01", "P1", "开发", 8));
            await repo.InsertAsync(Make("2026-08-01", "P2", "运维", 2)); // 同日第二条
            await repo.InsertAsync(Make("2026-08-02", "P1", "开发", 5));

            var stats = (await repo.GetDailyStatsAsync("2026-08-01", "2026-08-31"))
                .ToDictionary(r => (string)r.WorkDate);

            Assert.Equal(2, stats.Count);
            Assert.Equal(10.0, Convert.ToDouble(stats["2026-08-01"].TotalHours), 3);
            Assert.Equal(2, Convert.ToInt32(stats["2026-08-01"].RecordCount));
            Assert.Equal(5.0, Convert.ToDouble(stats["2026-08-02"].TotalHours), 3);
        }
        finally { TestDb.Cleanup(db); }
    }

    [Fact]
    public async Task GetRecordedDaysCountAsync_统计不重复工作日()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new WorkRecordRepository();
            await repo.InsertAsync(Make("2026-08-01", "P1", "开发", 8));
            await repo.InsertAsync(Make("2026-08-01", "P2", "运维", 2)); // 同日
            await repo.InsertAsync(Make("2026-08-03", "P1", "开发", 5));

            var days = await repo.GetRecordedDaysCountAsync("2026-08-01", "2026-08-31");
            Assert.Equal(2, days); // 8-01 与 8-03 两个不同工作日
        }
        finally { TestDb.Cleanup(db); }
    }

    [Fact]
    public async Task 聚合查询_排除软删除记录()
    {
        var db = TestDb.NewTempDb();
        try
        {
            var repo = new WorkRecordRepository();
            var keep = await repo.InsertAsync(Make("2026-08-01", "P1", "开发", 8));
            var drop = await repo.InsertAsync(Make("2026-08-02", "P1", "开发", 5)); // 将被软删除

            await repo.DeleteAsync(drop); // 软删除

            // 区间总工时仅含保留的 8h
            var total = await repo.GetTotalHoursAsync("2026-08-01", "2026-08-31");
            Assert.Equal(8.0, total, 3);

            // 项目统计中 P1 仅剩 8h、1 条（被删的 5h/1 条已排除）
            var stats = (await repo.GetProjectStatsAsync("2026-08-01", "2026-08-31"))
                .ToDictionary(r => (string)r.ProjectName);
            Assert.Equal(1, stats.Count);
            Assert.Equal(8.0, Convert.ToDouble(stats["P1"].TotalHours), 3);
            Assert.Equal(1, Convert.ToInt32(stats["P1"].RecordCount));
        }
        finally { TestDb.Cleanup(db); }
    }
}
