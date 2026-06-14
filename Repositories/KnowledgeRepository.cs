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
                "SELECT * FROM Knowledge ORDER BY CreateTime DESC");
        });
    }

    public async Task<IEnumerable<Knowledge>> SearchAsync(string keyword)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<Knowledge>(
                "SELECT * FROM Knowledge WHERE Title LIKE @Kw OR Content LIKE @Kw OR Tags LIKE @Kw ORDER BY CreateTime DESC",
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

    public async Task<int> DeleteAsync(int id)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.ExecuteAsync(
                "DELETE FROM Knowledge WHERE Id = @Id", new { Id = id });
        });
    }

    public async Task<IEnumerable<string>> GetAllTagsAsync()
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            var rows = await connection.QueryAsync<string>("SELECT Tags FROM Knowledge WHERE Tags != '' ORDER BY CreateTime DESC");
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
                "SELECT DISTINCT Category FROM Knowledge WHERE Category != '' ORDER BY Category");
        });
    }

    public async Task<IEnumerable<Knowledge>> GetFavoritesAsync()
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await connection.QueryAsync<Knowledge>(
                "SELECT * FROM Knowledge WHERE IsFavorite = 1 ORDER BY CreateTime DESC");
        });
    }
}
