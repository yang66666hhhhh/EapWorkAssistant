using EapWorkAssistant.Services;
using System.IO;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// LocalDataService 单测：只覆盖**纯逻辑与单文件查询**。
/// 备份摘要依赖真实用户目录（%LOCALAPPDATA%），不可在单测里假定，故不测。
/// </summary>
public class LocalDataServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public LocalDataServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"eap_data_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "sample.db");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }

    // ===== FormatSize =====

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1, "1 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(10240, "10 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(172032, "168 KB")]
    public void 字节数_格式化为人类可读(long bytes, string expected)
    {
        Assert.Equal(expected, LocalDataService.FormatSize(bytes));
    }

    [Fact]
    public void 负数大小_返回占位符而非负数文本()
    {
        Assert.Equal("—", LocalDataService.FormatSize(-1));
    }

    // ===== GetSnapshot =====

    [Fact]
    public void 文件存在_返回真实大小与修改时间()
    {
        File.WriteAllBytes(_file, new byte[2048]);
        var expectedMtime = new DateTime(2026, 10, 20, 9, 30, 0);
        File.SetLastWriteTime(_file, expectedMtime);

        var snapshot = LocalDataService.GetSnapshot(_file);

        Assert.NotNull(snapshot);
        Assert.Equal(2048, snapshot!.SizeBytes);
        Assert.Equal(expectedMtime, snapshot.LastWriteTime);
        Assert.Equal(Path.GetFullPath(_file), snapshot.Path);
    }

    [Fact]
    public void 文件不存在_返回null而不是零字节快照()
    {
        // 语义区分很重要：null → 界面显示「未创建」，0 B → 会误导成「空数据库」
        Assert.Null(LocalDataService.GetSnapshot(Path.Combine(_dir, "missing.db")));
    }

    // ===== 路径关系 =====

    [Fact]
    public void 路径常量_全部位于应用数据目录之下()
    {
        var root = LocalDataService.DataDirectory;

        Assert.EndsWith("EapWorkAssistant", root);
        Assert.Equal(root, Path.GetDirectoryName(LocalDataService.DatabasePath));
        Assert.Equal(root, Path.GetDirectoryName(LocalDataService.BackupDirectory));
        Assert.Equal(root, Path.GetDirectoryName(LocalDataService.LogDirectory));
        Assert.Equal("eapwork.db", Path.GetFileName(LocalDataService.DatabasePath));
    }

    [Fact]
    public void 备份与日志目录_各自独立不嵌套()
    {
        Assert.NotEqual(LocalDataService.BackupDirectory, LocalDataService.LogDirectory);
        Assert.False(LocalDataService.BackupDirectory.StartsWith(LocalDataService.LogDirectory));
        Assert.False(LocalDataService.LogDirectory.StartsWith(LocalDataService.BackupDirectory));
    }

    // ===== OpenFolder =====

    [Fact]
    public void 打开不存在的目录_返回false且不抛异常()
    {
        // 只验证失败分支（成功分支会真的弹资源管理器，不能进单测）
        Assert.False(LocalDataService.OpenFolder(Path.Combine(_dir, "no_such_dir")));
    }
}
