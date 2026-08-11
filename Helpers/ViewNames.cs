namespace EapWorkAssistant.Helpers;

/// <summary>
/// 各导航视图的稳定名称常量。原散落在 MainViewModel / MainWindow / SettingsViewModel /
/// DashboardView / ViewNameToLabelConverter / ConfigData 等多处，统一收口到此避免拼写错误。
/// 取值与持久化配置（ConfigData.DefaultView）中的字符串保持一致。
/// </summary>
public static class ViewNames
{
    public const string Dashboard = "Dashboard";
    public const string WorkRecord = "WorkRecord";
    public const string Knowledge = "Knowledge";
    public const string Issue = "Issue";
    public const string Settings = "Settings";
}
