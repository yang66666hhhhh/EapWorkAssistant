using System.Data.SQLite;
using Dapper;
using EapWorkAssistant.Data;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Repositories;

public class KnowledgeRepository
{
    public async Task<IEnumerable<Knowledge>> GetAllAsync()
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<Knowledge>(
                "SELECT * FROM Knowledge WHERE IsDeleted = 0 ORDER BY CreateTime DESC");
        });
    }

    /// <summary>仅取总条数，避免为计数全表拉取（Dashboard 统计用）</summary>
    public async Task<int> GetTotalCountAsync()
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Knowledge WHERE IsDeleted = 0");
        });
    }

    public async Task<IEnumerable<Knowledge>> SearchAsync(string keyword)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<Knowledge>(
                "SELECT * FROM Knowledge WHERE IsDeleted = 0 AND (Title LIKE @Kw OR Content LIKE @Kw OR Tags LIKE @Kw) ORDER BY CreateTime DESC",
                new { Kw = $"%{keyword}%" });
        });
    }

    public async Task<int> InsertAsync(Knowledge knowledge)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteAsync(@"
                INSERT INTO Knowledge (Title, Content, Tags, Category, IsFavorite, CreateTime)
                VALUES (@Title, @Content, @Tags, @Category, @IsFavorite, @CreateTime)",
                knowledge);
        });
    }

    public async Task<int> UpdateAsync(Knowledge knowledge)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteAsync(@"
                UPDATE Knowledge SET Title=@Title, Content=@Content, Tags=@Tags, Category=@Category, IsFavorite=@IsFavorite WHERE Id=@Id",
                knowledge);
        });
    }

    /// <summary>级联更新分类：修改配置项时同步所有知识条目</summary>
    public async Task<int> UpdateCategoryAsync(string oldCategory, string newCategory)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteAsync(
                "UPDATE Knowledge SET Category = @NewCategory WHERE Category = @OldCategory",
                new { NewCategory = newCategory, OldCategory = oldCategory });
        });
    }

    /// <summary>统计引用指定分类的未删除知识条目数（用于删除配置项前的引用提示）</summary>
    public async Task<int> GetCountByCategoryAsync(string category)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Knowledge WHERE IsDeleted = 0 AND Category = @Category",
                new { Category = category });
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
                "UPDATE Knowledge SET IsDeleted = 1, DeletedAt = datetime('now','localtime') WHERE Id = @Id", new { Id = id });
        });
    }

    public async Task<IEnumerable<string>> GetAllTagsAsync()
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            var rows = await connection.QueryAsync<string>("SELECT Tags FROM Knowledge WHERE IsDeleted = 0 AND Tags != '' ORDER BY CreateTime DESC");
            return rows.SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct();
        });
    }

    public async Task<IEnumerable<string>> GetAllCategoriesAsync()
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<string>(
                "SELECT DISTINCT Category FROM Knowledge WHERE IsDeleted = 0 AND Category != '' ORDER BY Category");
        });
    }

    public async Task<IEnumerable<Knowledge>> GetFavoritesAsync()
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<Knowledge>(
                "SELECT * FROM Knowledge WHERE IsDeleted = 0 AND IsFavorite = 1 ORDER BY CreateTime DESC");
        });
    }

    /// <summary>
    /// 带筛选和分页的查询，返回当前页知识 + 总条数（DB 级分页，避免全量载入内存）。
    /// 筛选条件：关键词（标题/内容/标签/分类）、仅收藏、分类。
    /// </summary>
    public async Task<(IEnumerable<Knowledge> Items, int Total)> GetFilteredPagedAsync(
        string? keyword, bool favoritesOnly, string? category, int offset, int limit)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            var where = new List<string> { "IsDeleted = 0" };
            var param = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                where.Add("(Title LIKE @Kw OR Content LIKE @Kw OR Tags LIKE @Kw OR Category LIKE @Kw)");
                param.Add("Kw", $"%{keyword.Trim()}%");
            }
            if (favoritesOnly)
                where.Add("IsFavorite = 1");
            if (!string.IsNullOrWhiteSpace(category))
            {
                where.Add("Category = @Category");
                param.Add("Category", category);
            }

            var whereSql = "WHERE " + string.Join(" AND ", where);
            var total = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM Knowledge {whereSql}", param);

            var dataSql = $"SELECT * FROM Knowledge {whereSql} ORDER BY CreateTime DESC LIMIT @Limit OFFSET @Offset";
            param.Add("Limit", limit);
            param.Add("Offset", offset);
            var items = await connection.QueryAsync<Knowledge>(dataSql, param);
            return (items, total);
        });
    }

    /// <summary>取回收站中的已删除知识（IsDeleted = 1），按删除时间倒序</summary>
    public async Task<IEnumerable<Knowledge>> GetDeletedAsync()
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<Knowledge>(
                "SELECT * FROM Knowledge WHERE IsDeleted = 1 ORDER BY COALESCE(DeletedAt, '') DESC, Id DESC");
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
                "UPDATE Knowledge SET IsDeleted = 0, DeletedAt = NULL WHERE Id = @Id", new { Id = id });
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
                "DELETE FROM Knowledge WHERE Id = @Id", new { Id = id });
        });
    }
}
