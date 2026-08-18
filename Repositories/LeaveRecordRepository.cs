using System.Data.SQLite;
using Dapper;
using EapWorkAssistant.Data;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Repositories;

public class LeaveRecordRepository
{
    /// <summary>获取指定月份的请假记录（已过滤软删除）</summary>
    public async Task<IEnumerable<LeaveRecord>> GetByMonthAsync(int year, int month)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            var yearMonth = $"{year:D4}-{month:D2}";
            return await connection.QueryAsync<LeaveRecord>(
                "SELECT Id, Date, LeaveType, Note, Hours FROM LeaveRecord WHERE IsDeleted = 0 AND strftime('%Y-%m', Date) = @YearMonth ORDER BY Date",
                new { YearMonth = yearMonth });
        });
    }

    /// <summary>新增请假记录</summary>
    public async Task<int> InsertAsync(LeaveRecord record)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteAsync(
                "INSERT INTO LeaveRecord (Date, LeaveType, Note, Hours) VALUES (@Date, @LeaveType, @Note, @Hours)",
                record);
        });
    }

    /// <summary>更新请假记录</summary>
    public async Task<int> UpdateAsync(LeaveRecord record)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteAsync(
                "UPDATE LeaveRecord SET Date=@Date, LeaveType=@LeaveType, Note=@Note, Hours=@Hours WHERE Id=@Id",
                record);
        });
    }

    /// <summary>获取指定年份的所有请假记录（用于调休余额计算，已过滤软删除）</summary>
    public async Task<IEnumerable<LeaveRecord>> GetByYearAsync(int year)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<LeaveRecord>(
                "SELECT Id, Date, LeaveType, Note, Hours FROM LeaveRecord WHERE IsDeleted = 0 AND strftime('%Y', Date) = @Year ORDER BY Date",
                new { Year = year.ToString() });
        });
    }

    /// <summary>软删除（移入回收站）</summary>
    public async Task<int> DeleteAsync(int id)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteAsync(
                "UPDATE LeaveRecord SET IsDeleted = 1, DeletedAt = datetime('now','localtime') WHERE Id = @Id", new { Id = id });
        });
    }

    /// <summary>取回收站中的已删除请假记录，按删除时间倒序</summary>
    public async Task<IEnumerable<LeaveRecord>> GetDeletedAsync()
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<LeaveRecord>(
                "SELECT Id, Date, LeaveType, Note, Hours, DeletedAt FROM LeaveRecord WHERE IsDeleted = 1 ORDER BY COALESCE(DeletedAt, '') DESC, Id DESC");
        });
    }

    /// <summary>从回收站恢复（软删除还原）</summary>
    public async Task<int> RestoreAsync(int id)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteAsync(
                "UPDATE LeaveRecord SET IsDeleted = 0, DeletedAt = NULL WHERE Id = @Id", new { Id = id });
        });
    }

    /// <summary>彻底删除（回收站清空 / 单条永久删除用）</summary>
    public async Task<int> HardDeleteAsync(int id)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteAsync(
                "DELETE FROM LeaveRecord WHERE Id = @Id", new { Id = id });
        });
    }
}
