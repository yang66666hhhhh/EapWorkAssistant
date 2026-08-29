using System;
using System.Collections.Generic;
using System.Data.SQLite;
using Dapper;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Repositories;

/// <summary>
/// 知识库仓储。连接生命周期样板由 <see cref="SqliteRepository.ExecuteAsync"/> 统一管理。
/// </summary>
public class KnowledgeRepository : SqliteRepository
{
    public Task<IEnumerable<Knowledge>> GetAllAsync()
        => ExecuteAsync(c => c.QueryAsync<Knowledge>(
            "SELECT * FROM Knowledge WHERE IsDeleted = 0 ORDER BY CreateTime DESC"));

    /// <summary>仅取总条数，避免为计数全表拉取（Dashboard 统计用）</summary>
    public Task<int> GetTotalCountAsync()
        => ExecuteAsync(c => c.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Knowledge WHERE IsDeleted = 0"));

    /// <summary>统计指定创建日期范围内的未删除知识条目数（Dashboard 计数卡环比用）</summary>
    public Task<int> GetCountByDateRangeAsync(string startDate, string endDate)
        => ExecuteAsync(c => c.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Knowledge WHERE IsDeleted = 0 AND date(CreateTime) BETWEEN @Start AND @End",
            new { Start = startDate, End = endDate }));

    public Task<IEnumerable<Knowledge>> SearchAsync(string keyword)
        => ExecuteAsync(c => c.QueryAsync<Knowledge>(
            "SELECT * FROM Knowledge WHERE IsDeleted = 0 AND (Title LIKE @Kw OR Content LIKE @Kw OR Tags LIKE @Kw) ORDER BY CreateTime DESC",
            new { Kw = $"%{keyword}%" }));

    public Task<int> InsertAsync(Knowledge knowledge)
        => ExecuteAsync(c => c.ExecuteAsync(@"
            INSERT INTO Knowledge (Title, Content, Tags, Category, IsFavorite, CreateTime)
            VALUES (@Title, @Content, @Tags, @Category, @IsFavorite, @CreateTime)",
            knowledge));

    public Task<int> UpdateAsync(Knowledge knowledge)
        => ExecuteAsync(c => c.ExecuteAsync(@"
            UPDATE Knowledge SET Title=@Title, Content=@Content, Tags=@Tags, Category=@Category, IsFavorite=@IsFavorite WHERE Id=@Id",
            knowledge));

    /// <summary>级联更新分类：修改配置项时同步所有知识条目</summary>
    public Task<int> UpdateCategoryAsync(string oldCategory, string newCategory)
        => ExecuteAsync(c => c.ExecuteAsync(
            "UPDATE Knowledge SET Category = @NewCategory WHERE Category = @OldCategory",
            new { NewCategory = newCategory, OldCategory = oldCategory }));

    /// <summary>统计引用指定分类的未删除知识条目数（用于删除配置项前的引用提示）</summary>
    public Task<int> GetCountByCategoryAsync(string category)
        => ExecuteAsync(c => c.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Knowledge WHERE IsDeleted = 0 AND Category = @Category",
            new { Category = category }));

    /// <summary>软删除（移入回收站）</summary>
    public Task<int> DeleteAsync(int id)
        => ExecuteAsync(c => c.ExecuteAsync(
            "UPDATE Knowledge SET IsDeleted = 1, DeletedAt = datetime('now','localtime') WHERE Id = @Id", new { Id = id }));

    public Task<IEnumerable<string>> GetAllTagsAsync()
        => ExecuteAsync(async c =>
        {
            var rows = await c.QueryAsync<string>("SELECT Tags FROM Knowledge WHERE IsDeleted = 0 AND Tags != '' ORDER BY CreateTime DESC");
            return rows.SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct();
        });

    public Task<IEnumerable<string>> GetAllCategoriesAsync()
        => ExecuteAsync(c => c.QueryAsync<string>(
            "SELECT DISTINCT Category FROM Knowledge WHERE IsDeleted = 0 AND Category != '' ORDER BY Category"));

    public Task<IEnumerable<Knowledge>> GetFavoritesAsync()
        => ExecuteAsync(c => c.QueryAsync<Knowledge>(
            "SELECT * FROM Knowledge WHERE IsDeleted = 0 AND IsFavorite = 1 ORDER BY CreateTime DESC"));

    /// <summary>
    /// 带筛选和分页的查询，返回当前页知识 + 总条数（DB 级分页，避免全量载入内存）。
    /// 筛选条件：关键词（标题/内容/标签/分类）、仅收藏、分类。
    /// </summary>
    public Task<(IEnumerable<Knowledge> Items, int Total)> GetFilteredPagedAsync(
        string? keyword, bool favoritesOnly, string? category, int offset, int limit)
        => ExecuteAsync(async c =>
        {
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
            var total = await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM Knowledge {whereSql}", param);

            var dataSql = $"SELECT * FROM Knowledge {whereSql} ORDER BY CreateTime DESC LIMIT @Limit OFFSET @Offset";
            param.Add("Limit", limit);
            param.Add("Offset", offset);
            var items = await c.QueryAsync<Knowledge>(dataSql, param);
            return (items, total);
        });

    /// <summary>取回收站中的已删除知识（IsDeleted = 1），按删除时间倒序</summary>
    public Task<IEnumerable<Knowledge>> GetDeletedAsync()
        => ExecuteAsync(c => c.QueryAsync<Knowledge>(
            "SELECT * FROM Knowledge WHERE IsDeleted = 1 ORDER BY COALESCE(DeletedAt, '') DESC, Id DESC"));

    /// <summary>从回收站恢复（软删除还原）</summary>
    public Task<int> RestoreAsync(int id)
        => ExecuteAsync(c => c.ExecuteAsync(
            "UPDATE Knowledge SET IsDeleted = 0, DeletedAt = NULL WHERE Id = @Id", new { Id = id }));

    /// <summary>彻底删除（回收站清空 / 单条永久删除用）</summary>
    public Task<int> HardDeleteAsync(int id)
        => ExecuteAsync(c => c.ExecuteAsync(
            "DELETE FROM Knowledge WHERE Id = @Id", new { Id = id }));
}
