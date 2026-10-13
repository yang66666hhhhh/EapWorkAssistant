using System.Text.Json;
using EapWorkAssistant.Services;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// 配置序列化契约：任何 ConfigData 实例都必须能被 System.Text.Json 安全序列化。
/// 回归防护：窗口位置曾用 <c>double.NaN</c> 作"未记录"哨兵，而 STJ 默认禁止 NaN，
/// 导致用户一改设置（触发 Save）就抛异常——故必须保持可空类型（null 哨兵）。
/// </summary>
public class ConfigSerializationTests
{
    [Fact]
    public void 默认配置_可序列化且含窗口字段()
    {
        var json = JsonSerializer.Serialize(new ConfigData());
        Assert.Contains("WindowLeft", json);
        Assert.Contains("SidebarCollapsed", json);
    }

    [Fact]
    public void 默认配置_往返后窗口位置仍为未记录()
    {
        var back = JsonSerializer.Deserialize<ConfigData>(JsonSerializer.Serialize(new ConfigData()));

        Assert.NotNull(back);
        Assert.Null(back!.WindowLeft);
        Assert.Null(back.WindowTop);
        Assert.Equal(0d, back.WindowWidth);
        Assert.Equal(0d, back.WindowHeight);
        Assert.False(back.WindowMaximized);
        Assert.False(back.SidebarCollapsed);
    }

    [Fact]
    public void 已记录窗口几何_往返一致()
    {
        var data = new ConfigData
        {
            WindowWidth = 1280,
            WindowHeight = 800,
            WindowLeft = 120,
            WindowTop = 60,
            WindowMaximized = true,
            SidebarCollapsed = true
        };

        var back = JsonSerializer.Deserialize<ConfigData>(JsonSerializer.Serialize(data));

        Assert.NotNull(back);
        Assert.Equal(1280d, back!.WindowWidth);
        Assert.Equal(800d, back.WindowHeight);
        Assert.Equal(120d, back.WindowLeft!.Value);
        Assert.Equal(60d, back.WindowTop!.Value);
        Assert.True(back.WindowMaximized);
        Assert.True(back.SidebarCollapsed);
    }
}
