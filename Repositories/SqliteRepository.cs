using System.Data.SQLite;
using EapWorkAssistant.Data;

namespace EapWorkAssistant.Repositories;

/// <summary>
/// 仓储基类：封装 SQLite 连接的样板（建连接 → 打开 → 执行体 → 释放）。
///
/// 各仓储方法原先都长这样：
/// <code>
/// return await Task.Run(async () =>
/// {
///     using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
///     await connection.OpenAsync();
///     return await connection.QueryAsync&lt;T&gt;("SELECT ...");
/// });
/// </code>
/// 4 个仓储、约 30 个方法里这段重复了 30 遍。抽到基类后，子类只提供"查询体"，
/// 连接生命周期由基类统一管理，样板消除、错误处理一致。
/// </summary>
public abstract class SqliteRepository
{
    /// <summary>
    /// 在后台线程创建并打开连接，执行 <paramref name="body"/>，返回结果。
    /// 连接由 using 保证释放；body 内可对该连接做任意操作（含事务、CreateCommand）。
    /// </summary>
    protected static async Task<T> ExecuteAsync<T>(Func<SQLiteConnection, Task<T>> body)
    {
        return await Task.Run(async () =>
        {
            using var connection = new SQLiteConnection(DatabaseInitializer.ConnectionString);
            await connection.OpenAsync();
            return await body(connection);
        });
    }
}
