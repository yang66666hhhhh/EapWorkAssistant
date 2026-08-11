using EapWorkAssistant.Data;
using System.IO;

namespace EapWorkAssistant.Tests;

/// <summary>为每个测试提供独立的临时 SQLite 库（通过 DatabaseInitializer 的连接串覆盖实现）。</summary>
internal static class TestDb
{
    public static string NewTempDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eaptest_{System.Guid.NewGuid():N}.db");
        if (File.Exists(path)) File.Delete(path);
        DatabaseInitializer.SetTestConnectionString($"Data Source={path}");
        DatabaseInitializer.Initialize();
        return path;
    }

    public static void Cleanup(string path)
    {
        DatabaseInitializer.SetTestConnectionString(null);
        if (File.Exists(path)) File.Delete(path);
    }
}
