using System.Data.SQLite;
using Dapper;
using EapWorkAssistant.Data;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Repositories;

public class IssueRepository
{
    public async Task<IEnumerable<Issue>> GetAllAsync()
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<Issue>(
                "SELECT * FROM Issue WHERE IsDeleted = 0 ORDER BY CreateTime DESC");
        });
    }

    /// <summary>仅取总条数，避免为计数全表拉取（Dashboard 统计用）</summary>
    public async Task<int> GetTotalCountAsync()
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Issue WHERE IsDeleted = 0");
        });
    }

    public async Task<IEnumerable<Issue>> SearchAsync(string keyword)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<Issue>(
                "SELECT * FROM Issue WHERE IsDeleted = 0 AND (Title LIKE @Kw OR Description LIKE @Kw OR Keywords LIKE @Kw OR RootCause LIKE @Kw OR Solution LIKE @Kw) ORDER BY CreateTime DESC",
                new { Kw = $"%{keyword}%" });
        });
    }

    public async Task<IEnumerable<Issue>> GetByProjectAsync(string project)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<Issue>(
                "SELECT * FROM Issue WHERE IsDeleted = 0 AND ProjectName = @Project ORDER BY CreateTime DESC",
                new { Project = project });
        });
    }

    public async Task<int> InsertAsync(Issue issue)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteAsync(@"
                INSERT INTO Issue (ProjectName, Title, Description, RootCause, Solution, Keywords, Status, Priority, CreateTime)
                VALUES (@ProjectName, @Title, @Description, @RootCause, @Solution, @Keywords, @Status, @Priority, @CreateTime)",
                issue);
        });
    }

    public async Task<int> UpdateAsync(Issue issue)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteAsync(@"
                UPDATE Issue SET ProjectName=@ProjectName, Title=@Title, Description=@Description,
                RootCause=@RootCause, Solution=@Solution, Keywords=@Keywords, Status=@Status, Priority=@Priority WHERE Id=@Id",
                issue);
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
                "UPDATE Issue SET IsDeleted = 1, DeletedAt = datetime('now','localtime') WHERE Id = @Id", new { Id = id });
        });
    }

    /// <summary>
    /// 带筛选和分页的查询，返回当前页问题 + 总条数（DB 级分页，避免全量载入内存）。
    /// 筛选条件：关键词（标题/描述/关键词/根因/方案）、状态、优先级。
    /// </summary>
    public async Task<(IEnumerable<Issue> Items, int Total)> GetFilteredPagedAsync(
        string? keyword, string? status, string? priority, int offset, int limit)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            var where = new List<string> { "IsDeleted = 0" };
            var param = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                where.Add("(Title LIKE @Kw OR Description LIKE @Kw OR Keywords LIKE @Kw OR RootCause LIKE @Kw OR Solution LIKE @Kw)");
                param.Add("Kw", $"%{keyword.Trim()}%");
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                where.Add("Status = @Status");
                param.Add("Status", status);
            }
            if (!string.IsNullOrWhiteSpace(priority))
            {
                where.Add("Priority = @Priority");
                param.Add("Priority", priority);
            }

            var whereSql = "WHERE " + string.Join(" AND ", where);
            var total = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM Issue {whereSql}", param);

            var dataSql = $"SELECT * FROM Issue {whereSql} ORDER BY CreateTime DESC LIMIT @Limit OFFSET @Offset";
            param.Add("Limit", limit);
            param.Add("Offset", offset);
            var items = await connection.QueryAsync<Issue>(dataSql, param);
            return (items, total);
        });
    }

    /// <summary>取回收站中的已删除问题（IsDeleted = 1），按删除时间倒序</summary>
    public async Task<IEnumerable<Issue>> GetDeletedAsync()
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<Issue>(
                "SELECT * FROM Issue WHERE IsDeleted = 1 ORDER BY COALESCE(DeletedAt, '') DESC, Id DESC");
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
                "UPDATE Issue SET IsDeleted = 0, DeletedAt = NULL WHERE Id = @Id", new { Id = id });
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
                "DELETE FROM Issue WHERE Id = @Id", new { Id = id });
        });
    }
}
