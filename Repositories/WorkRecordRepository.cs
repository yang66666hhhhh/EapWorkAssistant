using System.Data.SQLite;
using Dapper;
using EapWorkAssistant.Data;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Repositories;

public class WorkRecordRepository
{
    public async Task<IEnumerable<WorkRecord>> GetAllAsync()
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<WorkRecord>(
                "SELECT * FROM WorkRecord ORDER BY WorkDate DESC, Id DESC");
        });
    }

    public async Task<IEnumerable<WorkRecord>> GetByDateAsync(string date)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<WorkRecord>(
                "SELECT * FROM WorkRecord WHERE WorkDate = @Date ORDER BY Id",
                new { Date = date });
        });
    }

    public async Task<IEnumerable<WorkRecord>> GetByDateRangeAsync(string startDate, string endDate)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<WorkRecord>(
                "SELECT * FROM WorkRecord WHERE WorkDate BETWEEN @Start AND @End ORDER BY WorkDate",
                new { Start = startDate, End = endDate });
        });
    }

    public async Task<IEnumerable<WorkRecord>> GetByMonthAsync(string yearMonth)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<WorkRecord>(
                "SELECT * FROM WorkRecord WHERE WorkDate LIKE @Month ORDER BY WorkDate",
                new { Month = $"{yearMonth}%" });
        });
    }

    public async Task<int> InsertAsync(WorkRecord record)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            var id = await connection.QuerySingleAsync<int>(@"
                INSERT INTO WorkRecord (WorkDate, ProjectName, WorkType, Content, Achievement, Problem, Solution, Hours, Progress, IsHighlight, HighlightNote, CreateTime)
                VALUES (@WorkDate, @ProjectName, @WorkType, @Content, @Achievement, @Problem, @Solution, @Hours, @Progress, @IsHighlight, @HighlightNote, @CreateTime);
                SELECT last_insert_rowid();",
                record);
            record.Id = id;
            return id;
        });
    }

    public async Task<int> UpdateAsync(WorkRecord record)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteAsync(@"
                UPDATE WorkRecord SET WorkDate=@WorkDate, ProjectName=@ProjectName, WorkType=@WorkType,
                Content=@Content, Achievement=@Achievement, Problem=@Problem, Solution=@Solution,
                Hours=@Hours, Progress=@Progress, IsHighlight=@IsHighlight, HighlightNote=@HighlightNote WHERE Id=@Id",
                record);
        });
    }

    public async Task<int> DeleteAsync(int id)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteAsync(
                "DELETE FROM WorkRecord WHERE Id = @Id", new { Id = id });
        });
    }

    public async Task<int> BatchInsertAsync(IEnumerable<WorkRecord> records)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                var count = await connection.ExecuteAsync(@"
                    INSERT INTO WorkRecord (WorkDate, ProjectName, WorkType, Content, Achievement, Problem, Solution, Hours, Progress, IsHighlight, HighlightNote, CreateTime)
                    VALUES (@WorkDate, @ProjectName, @WorkType, @Content, @Achievement, @Problem, @Solution, @Hours, @Progress, @IsHighlight, @HighlightNote, @CreateTime)",
                    records, transaction);
                transaction.Commit();
                return count;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public async Task<double> GetTotalHoursAsync(string startDate, string endDate)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteScalarAsync<double>(
                "SELECT COALESCE(SUM(Hours), 0) FROM WorkRecord WHERE WorkDate BETWEEN @Start AND @End",
                new { Start = startDate, End = endDate });
        });
    }

    public async Task<IEnumerable<dynamic>> GetProjectStatsAsync(string startDate, string endDate)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync(
                "SELECT ProjectName, SUM(Hours) as TotalHours, COUNT(*) as RecordCount FROM WorkRecord WHERE WorkDate BETWEEN @Start AND @End GROUP BY ProjectName",
                new { Start = startDate, End = endDate });
        });
    }

    public async Task<IEnumerable<dynamic>> GetTypeStatsAsync(string startDate, string endDate)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync(
                "SELECT WorkType, SUM(Hours) as TotalHours FROM WorkRecord WHERE WorkDate BETWEEN @Start AND @End GROUP BY WorkType",
                new { Start = startDate, End = endDate });
        });
    }

    public async Task<IEnumerable<WorkRecord>> GetHighlightsAsync(string startDate, string endDate)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<WorkRecord>(
                "SELECT * FROM WorkRecord WHERE IsHighlight = 1 AND WorkDate BETWEEN @Start AND @End ORDER BY WorkDate DESC",
                new { Start = startDate, End = endDate });
        });
    }

    public async Task<IEnumerable<dynamic>> GetDailyStatsAsync(string startDate, string endDate)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync(
                "SELECT WorkDate, SUM(Hours) as TotalHours, COUNT(*) as RecordCount FROM WorkRecord WHERE WorkDate BETWEEN @Start AND @End GROUP BY WorkDate ORDER BY WorkDate",
                new { Start = startDate, End = endDate });
        });
    }

    public async Task<WorkRecord?> GetLatestBeforeOrOnAsync(string date)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryFirstOrDefaultAsync<WorkRecord>(
                "SELECT * FROM WorkRecord WHERE WorkDate <= @Date ORDER BY WorkDate DESC, Id DESC LIMIT 1",
                new { Date = date });
        });
    }

    public async Task<int> GetRecordedDaysCountAsync(string startDate, string endDate)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(DISTINCT WorkDate) FROM WorkRecord WHERE WorkDate BETWEEN @Start AND @End",
                new { Start = startDate, End = endDate });
        });
    }

    public async Task<IEnumerable<WorkRecord>> SearchAsync(string keyword)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            // 支持多关键词空格分隔搜索
            var keywords = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (keywords.Length <= 1)
            {
                return await connection.QueryAsync<WorkRecord>(
                    "SELECT * FROM WorkRecord WHERE Content LIKE @Kw OR ProjectName LIKE @Kw OR Problem LIKE @Kw OR Solution LIKE @Kw OR Achievement LIKE @Kw OR HighlightNote LIKE @Kw ORDER BY WorkDate DESC, Id DESC LIMIT 50",
                    new { Kw = $"%{keyword}%" });
            }

            var where = string.Join(" AND ", keywords.Select((_, i) =>
                $"(Content LIKE @Kw{i} OR ProjectName LIKE @Kw{i} OR Problem LIKE @Kw{i} OR Solution LIKE @Kw{i} OR Achievement LIKE @Kw{i} OR HighlightNote LIKE @Kw{i})"));
            var param = new DynamicParameters();
            for (int i = 0; i < keywords.Length; i++)
                param.Add($"Kw{i}", $"%{keywords[i]}%");
            return await connection.QueryAsync<WorkRecord>(
                $"SELECT * FROM WorkRecord WHERE {where} ORDER BY WorkDate DESC, Id DESC LIMIT 50", param);
        });
    }

    /// <summary>
    /// 带筛选和分页的查询，返回当前页记录 + 统计信息（总条数、总工时、亮点数）
    /// </summary>
    public async Task<(IEnumerable<WorkRecord> Records, int TotalCount, double TotalHours, int HighlightCount)>
        GetFilteredPagedAsync(string? keyword, string? project, string? workType,
            string? startDate, string? endDate, int offset, int limit)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            var where = new List<string>();
            var param = new DynamicParameters();

            if (!string.IsNullOrEmpty(keyword))
            {
                // 支持多关键词空格分隔
                var keywords = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (keywords.Length > 1)
                {
                    var kwConditions = new List<string>();
                    for (int i = 0; i < keywords.Length; i++)
                    {
                        kwConditions.Add($"(ProjectName LIKE @Kw{i} OR WorkType LIKE @Kw{i} OR Content LIKE @Kw{i} OR Achievement LIKE @Kw{i} OR Problem LIKE @Kw{i} OR Solution LIKE @Kw{i} OR HighlightNote LIKE @Kw{i})");
                        param.Add($"Kw{i}", $"%{keywords[i]}%");
                    }
                    where.Add($"({string.Join(" AND ", kwConditions)})");
                }
                else
                {
                    where.Add("(ProjectName LIKE @Kw OR WorkType LIKE @Kw OR Content LIKE @Kw OR Achievement LIKE @Kw OR Problem LIKE @Kw OR Solution LIKE @Kw OR HighlightNote LIKE @Kw)");
                    param.Add("Kw", $"%{keyword}%");
                }
            }
            if (!string.IsNullOrEmpty(project))
            {
                where.Add("ProjectName = @Project");
                param.Add("Project", project);
            }
            if (!string.IsNullOrEmpty(workType))
            {
                where.Add("WorkType = @WorkType");
                param.Add("WorkType", workType);
            }
            if (!string.IsNullOrEmpty(startDate))
            {
                where.Add("WorkDate >= @StartDate");
                param.Add("StartDate", startDate);
            }
            if (!string.IsNullOrEmpty(endDate))
            {
                where.Add("WorkDate <= @EndDate");
                param.Add("EndDate", endDate);
            }

            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

            // 统计查询（跨全量匹配记录）
            var statsSql = $@"SELECT COALESCE(SUM(Hours),0) AS TotalHours, COUNT(*) AS TotalCount,
                              COALESCE(SUM(CASE WHEN IsHighlight=1 THEN 1 ELSE 0 END),0) AS HighlightCount
                              FROM WorkRecord {whereSql}";
            var stats = await connection.QuerySingleAsync(statsSql, param);

            // 分页查询
            var dataSql = $@"SELECT * FROM WorkRecord {whereSql}
                             ORDER BY WorkDate DESC, Id DESC LIMIT @Limit OFFSET @Offset";
            param.Add("Limit", limit);
            param.Add("Offset", offset);
            var records = await connection.QueryAsync<WorkRecord>(dataSql, param);

            return (records, (int)stats.TotalCount, (double)stats.TotalHours, (int)stats.HighlightCount);
        });
    }
}
