using System.Data.SQLite;
using Dapper;
using EapWorkAssistant.Data;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Repositories;

public class WorkRecordRepository
{
    public async Task<IEnumerable<WorkRecord>> GetAllAsync()
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryAsync<WorkRecord>(
            "SELECT * FROM WorkRecord ORDER BY WorkDate DESC, Id DESC");
    }

    public async Task<IEnumerable<WorkRecord>> GetByDateAsync(string date)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryAsync<WorkRecord>(
            "SELECT * FROM WorkRecord WHERE WorkDate = @Date ORDER BY Id",
            new { Date = date });
    }

    public async Task<IEnumerable<WorkRecord>> GetByDateRangeAsync(string startDate, string endDate)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryAsync<WorkRecord>(
            "SELECT * FROM WorkRecord WHERE WorkDate BETWEEN @Start AND @End ORDER BY WorkDate",
            new { Start = startDate, End = endDate });
    }

    public async Task<IEnumerable<WorkRecord>> GetByMonthAsync(string yearMonth)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryAsync<WorkRecord>(
            "SELECT * FROM WorkRecord WHERE WorkDate LIKE @Month ORDER BY WorkDate",
            new { Month = $"{yearMonth}%" });
    }

    public async Task<int> InsertAsync(WorkRecord record)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.ExecuteAsync(@"
            INSERT INTO WorkRecord (WorkDate, ProjectName, WorkType, Content, Achievement, Problem, Solution, Hours, Progress, IsHighlight, HighlightNote, CreateTime)
            VALUES (@WorkDate, @ProjectName, @WorkType, @Content, @Achievement, @Problem, @Solution, @Hours, @Progress, @IsHighlight, @HighlightNote, @CreateTime)",
            record);
    }

    public async Task<int> UpdateAsync(WorkRecord record)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.ExecuteAsync(@"
            UPDATE WorkRecord SET WorkDate=@WorkDate, ProjectName=@ProjectName, WorkType=@WorkType,
            Content=@Content, Achievement=@Achievement, Problem=@Problem, Solution=@Solution,
            Hours=@Hours, Progress=@Progress, IsHighlight=@IsHighlight, HighlightNote=@HighlightNote WHERE Id=@Id",
            record);
    }

    public async Task<int> DeleteAsync(int id)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.ExecuteAsync(
            "DELETE FROM WorkRecord WHERE Id = @Id", new { Id = id });
    }

    public async Task<double> GetTotalHoursAsync(string startDate, string endDate)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.ExecuteScalarAsync<double>(
            "SELECT COALESCE(SUM(Hours), 0) FROM WorkRecord WHERE WorkDate BETWEEN @Start AND @End",
            new { Start = startDate, End = endDate });
    }

    public async Task<IEnumerable<dynamic>> GetProjectStatsAsync(string startDate, string endDate)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryAsync(
            "SELECT ProjectName, SUM(Hours) as TotalHours, COUNT(*) as RecordCount FROM WorkRecord WHERE WorkDate BETWEEN @Start AND @End GROUP BY ProjectName",
            new { Start = startDate, End = endDate });
    }

    public async Task<IEnumerable<dynamic>> GetTypeStatsAsync(string startDate, string endDate)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryAsync(
            "SELECT WorkType, SUM(Hours) as TotalHours FROM WorkRecord WHERE WorkDate BETWEEN @Start AND @End GROUP BY WorkType",
            new { Start = startDate, End = endDate });
    }

    public async Task<IEnumerable<WorkRecord>> GetHighlightsAsync(string startDate, string endDate)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryAsync<WorkRecord>(
            "SELECT * FROM WorkRecord WHERE IsHighlight = 1 AND WorkDate BETWEEN @Start AND @End ORDER BY WorkDate DESC",
            new { Start = startDate, End = endDate });
    }

    public async Task<IEnumerable<dynamic>> GetDailyStatsAsync(string startDate, string endDate)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryAsync(
            "SELECT WorkDate, SUM(Hours) as TotalHours, COUNT(*) as RecordCount FROM WorkRecord WHERE WorkDate BETWEEN @Start AND @End GROUP BY WorkDate ORDER BY WorkDate",
            new { Start = startDate, End = endDate });
    }

    public async Task<WorkRecord?> GetLatestBeforeOrOnAsync(string date)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryFirstOrDefaultAsync<WorkRecord>(
            "SELECT * FROM WorkRecord WHERE WorkDate <= @Date ORDER BY WorkDate DESC, Id DESC LIMIT 1",
            new { Date = date });
    }

    public async Task<int> GetRecordedDaysCountAsync(string startDate, string endDate)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(DISTINCT WorkDate) FROM WorkRecord WHERE WorkDate BETWEEN @Start AND @End",
            new { Start = startDate, End = endDate });
    }
}
