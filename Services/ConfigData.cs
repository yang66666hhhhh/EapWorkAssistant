using System.Collections.Generic;

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

    // ===== 启动与行为 =====
    public bool AutoStart { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public string DefaultView { get; set; } = "Dashboard";
    public int AutoSaveInterval { get; set; } = 5;  // 分钟

    // ===== 休息日（0=周日, 1=周一, ..., 6=周六）=====
    public List<int> RestDays { get; set; } = new() { 0, 6 }; // 默认周六周日

    // ===== 知识分类 =====
    public List<string> KnowledgeCategories { get; set; } = new();

    // ===== 工作记录自定义字段 =====
    public List<CustomField> CustomFields { get; set; } = new();
}

public class ContentTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class CustomField
{
    public string Name { get; set; } = string.Empty;
    public string FieldType { get; set; } = "Text";       // Text, Number, Dropdown
    public string DefaultValue { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();     // Dropdown 选项
}
