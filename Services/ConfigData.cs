using System.Collections.Generic;
using EapWorkAssistant.Helpers;

namespace EapWorkAssistant.Services;

public class ConfigData
{
    public List<string> Projects { get; set; } = new();
    public List<string> WorkTypes { get; set; } = new();
    public List<ContentTemplate> ContentTemplates { get; set; } = new();
    public bool EnableShortcuts { get; set; } = true;
    public bool EnableReminder { get; set; } = true;
    public int ReminderHour { get; set; } = 17;
    public int ReminderMinute { get; set; } = 30;

    // 可配置快捷键（只存按键部分，修饰键固定 Ctrl）
    public string ShortcutSearch { get; set; } = "F";
    public string ShortcutNew { get; set; } = "N";
    public string ShortcutSave { get; set; } = "S";
    public string ShortcutView1 { get; set; } = "D1";
    public string ShortcutView2 { get; set; } = "D2";
    public string ShortcutView3 { get; set; } = "D3";
    public string ShortcutView4 { get; set; } = "D4";
    public string ShortcutView5 { get; set; } = "D5";

    // 各快捷键独立启用状态
    public bool ShortcutSearchEnabled { get; set; } = true;
    public bool ShortcutNewEnabled { get; set; } = true;
    public bool ShortcutSaveEnabled { get; set; } = true;
    public bool ShortcutView1Enabled { get; set; } = true;
    public bool ShortcutView2Enabled { get; set; } = true;
    public bool ShortcutView3Enabled { get; set; } = true;
    public bool ShortcutView4Enabled { get; set; } = true;
    public bool ShortcutView5Enabled { get; set; } = true;

    // ===== 外观与主题 =====
    public string ThemeMode { get; set; } = "Light";        // Light, Dark
    public string AccentColor { get; set; } = "Indigo";     // Indigo, Violet, Blue, Emerald, Rose, Amber
    public string FontSizeLevel { get; set; } = "Medium";   // Small, Medium, Large
    public string UIDensity { get; set; } = "Default";      // Compact, Default, Comfortable

    // ===== Dashboard 布局 =====
    public bool ShowDashStats { get; set; } = true;
    public bool ShowDashReminder { get; set; } = true;
    public bool ShowDashProbation { get; set; } = true;
    public bool ShowDashCharts { get; set; } = true;
    public bool ShowDashHighlights { get; set; } = true;
    public bool ShowDashRecent { get; set; } = true;
    public bool ShowDashInsights { get; set; } = true;

    // ===== 启动与行为 =====
    public bool AutoStart { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public string DefaultView { get; set; } = ViewNames.Dashboard;
    public int AutoSaveInterval { get; set; } = 5;  // 分钟

    // ===== 窗口几何（记忆上次尺寸/位置/状态，下次启动还原）=====
    // Width/Height 为 0 表示"从未记录"，启动时沿用 XAML 默认尺寸；
    // Left/Top 为 null 表示"从未记录"，启动时沿用 WindowStartupLocation（居中）。
    // ⚠️ 位置刻意用 double?（null）而非 double.NaN：System.Text.Json 默认无法序列化 NaN（会抛异常）。
    public double WindowWidth { get; set; } = 0;
    public double WindowHeight { get; set; } = 0;
    public double? WindowLeft { get; set; } = null;
    public double? WindowTop { get; set; } = null;
    public bool WindowMaximized { get; set; } = false;
    // 侧边栏折叠状态（用户手动折叠后记住）
    public bool SidebarCollapsed { get; set; } = false;

    // ===== 休息日（0=周日, 1=周一, ..., 6=周六）=====
    public List<int> RestDays { get; set; } = new() { 0, 6 }; // 默认周六周日

    // ===== 知识分类 =====
    public List<string> KnowledgeCategories { get; set; } = new();
}

public class ContentTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
