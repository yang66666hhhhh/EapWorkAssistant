using System.Data.SQLite;
using Dapper;
using EapWorkAssistant.Data;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Repositories;

public class IssueRepository
{
    public async Task<IEnumerable<Issue>> GetAllAsync()
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryAsync<Issue>(
            "SELECT * FROM Issue ORDER BY CreateTime DESC");
    }

    public async Task<IEnumerable<Issue>> SearchAsync(string keyword)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryAsync<Issue>(
            "SELECT * FROM Issue WHERE Title LIKE @Kw OR Description LIKE @Kw OR Keywords LIKE @Kw OR RootCause LIKE @Kw ORDER BY CreateTime DESC",
            new { Kw = $"%{keyword}%" });
    }

    public async Task<IEnumerable<Issue>> GetByProjectAsync(string project)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.QueryAsync<Issue>(
            "SELECT * FROM Issue WHERE ProjectName = @Project ORDER BY CreateTime DESC",
            new { Project = project });
    }

    public async Task<int> InsertAsync(Issue issue)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.ExecuteAsync(@"
            INSERT INTO Issue (ProjectName, Title, Description, RootCause, Solution, Keywords, CreateTime)
            VALUES (@ProjectName, @Title, @Description, @RootCause, @Solution, @Keywords, @CreateTime)",
            issue);
    }

    public async Task<int> UpdateAsync(Issue issue)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.ExecuteAsync(@"
            UPDATE Issue SET ProjectName=@ProjectName, Title=@Title, Description=@Description,
            RootCause=@RootCause, Solution=@Solution, Keywords=@Keywords WHERE Id=@Id",
            issue);
    }

    public async Task<int> DeleteAsync(int id)
    {
        using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        return await connection.ExecuteAsync(
            "DELETE FROM Issue WHERE Id = @Id", new { Id = id });
    }
}
