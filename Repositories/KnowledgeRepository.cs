using System.Data.SQLite;
using Dapper;
using EapWorkAssistant.Data;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Repositories;

public class KnowledgeRepository
{
    public async Task<IEnumerable<Knowledge>> GetAllAsync()
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryAsync<Knowledge>(
            "SELECT * FROM Knowledge ORDER BY CreateTime DESC");
    }

    public async Task<IEnumerable<Knowledge>> SearchAsync(string keyword)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryAsync<Knowledge>(
            "SELECT * FROM Knowledge WHERE Title LIKE @Kw OR Content LIKE @Kw OR Tags LIKE @Kw ORDER BY CreateTime DESC",
            new { Kw = $"%{keyword}%" });
    }

    public async Task<int> InsertAsync(Knowledge knowledge)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.ExecuteAsync(@"
            INSERT INTO Knowledge (Title, Content, Tags, CreateTime)
            VALUES (@Title, @Content, @Tags, @CreateTime)",
            knowledge);
    }

    public async Task<int> UpdateAsync(Knowledge knowledge)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.ExecuteAsync(@"
            UPDATE Knowledge SET Title=@Title, Content=@Content, Tags=@Tags WHERE Id=@Id",
            knowledge);
    }

    public async Task<int> DeleteAsync(int id)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.ExecuteAsync(
            "DELETE FROM Knowledge WHERE Id = @Id", new { Id = id });
    }
}
