using System.Data.SQLite;
using Dapper;
using EapWorkAssistant.Data;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Repositories;

public class LeaveRecordRepository
{
    /// <summary>获取指定月份的请假记录</summary>
    public async Task<IEnumerable<LeaveRecord>> GetByMonthAsync(int year, int month)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            var yearMonth = $"{year:D4}-{month:D2}";
            return await connection.QueryAsync<LeaveRecord>(
                "SELECT Id, Date, LeaveType, Note, Hours FROM LeaveRecord WHERE strftime('%Y-%m', Date) = @YearMonth ORDER BY Date",
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

    /// <summary>获取指定年份的所有请假记录（用于调休余额计算）</summary>
    public async Task<IEnumerable<LeaveRecord>> GetByYearAsync(int year)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<LeaveRecord>(
                "SELECT Id, Date, LeaveType, Note, Hours FROM LeaveRecord WHERE strftime('%Y', Date) = @Year ORDER BY Date",
                new { Year = year.ToString() });
        });
    }

    /// <summary>删除请假记录</summary>
    public async Task<int> DeleteAsync(int id)
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
