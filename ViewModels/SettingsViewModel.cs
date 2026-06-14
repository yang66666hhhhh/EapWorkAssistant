using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Services;
using EapWorkAssistant.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace EapWorkAssistant.ViewModels;

public partial class SettingsViewModel : ObservableObject, IRefreshable
{
    [ObservableProperty]
    private ObservableCollection<string> _projects = new();

    [ObservableProperty]
    private ObservableCollection<string> _workTypes = new();

    [ObservableProperty]
    private string? _selectedProject;

    [ObservableProperty]
    private string? _selectedWorkType;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ContentTemplate> _contentTemplates = new();

    [ObservableProperty]
    private bool _enableShortcuts = true;

    [ObservableProperty]
    private bool _enableReminder = true;

    [ObservableProperty]
    private int _reminderHour = 17;

    [ObservableProperty]
    private int _reminderMinute = 30;

    // 快捷键配置
    [ObservableProperty] private string _shortcutSearch = "F";
    [ObservableProperty] private string _shortcutNew = "N";
    [ObservableProperty] private string _shortcutSave = "S";
    [ObservableProperty] private string _shortcutView1 = "D1";
    [ObservableProperty] private string _shortcutView2 = "D2";
    [ObservableProperty] private string _shortcutView3 = "D3";
    [ObservableProperty] private string _shortcutView4 = "D4";
    [ObservableProperty] private string _shortcutView5 = "D5";

    // ===== 外观与主题 =====
    [ObservableProperty] private bool _isLightTheme = true;
    [ObservableProperty] private bool _isDarkTheme;
    [ObservableProperty] private string _selectedAccentColor = "Indigo";
    [ObservableProperty] private string _selectedFontSize = "Medium";
    [ObservableProperty] private string _selectedDensity = "Default";

    // 强调色预览列表
    public ObservableCollection<AccentColorItem> AccentColors { get; } = new();

    public List<string> FontSizeOptions { get; } = new() { "Small", "Medium", "Large" };
    public List<string> DensityOptions { get; } = new() { "Compact", "Default", "Comfortable" };

    public string FontSizeLabel => SelectedFontSize switch
    {
        "Small" => "小号",
        "Medium" => "标准",
        "Large" => "大号",
        _ => "标准"
    };

    public string DensityLabel => SelectedDensity switch
    {
        "Compact" => "紧凑",
        "Default" => "标准",
        "Comfortable" => "宽松",
        _ => "标准"
    };

    // ===== Dashboard 布局 =====
    [ObservableProperty] private bool _showDashStats = true;
    [ObservableProperty] private bool _showDashReminder = true;
    [ObservableProperty] private bool _showDashProbation = true;
    [ObservableProperty] private bool _showDashCharts = true;
    [ObservableProperty] private bool _showDashHighlights = true;
    [ObservableProperty] private bool _showDashRecent = true;

    // ===== 启动与行为 =====
    [ObservableProperty] private bool _autoStart;
    [ObservableProperty] private bool _minimizeToTray = true;
    [ObservableProperty] private string _defaultView = "Dashboard";
    [ObservableProperty] private int _autoSaveInterval = 5;

    // ===== 休息日（0=周日, 1=周一, ..., 6=周六）=====
    [ObservableProperty] private bool _isRestDay0; // 周日
    [ObservableProperty] private bool _isRestDay1; // 周一
    [ObservableProperty] private bool _isRestDay2; // 周二
    [ObservableProperty] private bool _isRestDay3; // 周三
    [ObservableProperty] private bool _isRestDay4; // 周四
    [ObservableProperty] private bool _isRestDay5; // 周五
    [ObservableProperty] private bool _isRestDay6; // 周六

    public List<string> ViewOptions { get; } = new() { "Dashboard", "WorkRecord", "Knowledge", "Issue", "Settings" };
    public List<int> AutoSaveOptions { get; } = new() { 1, 3, 5, 10, 15, 30 };

    public string DefaultViewLabel => DefaultView switch
    {
        "Dashboard" => "工作台",
        "WorkRecord" => "工作记录",
        "Knowledge" => "知识库",
        "Issue" => "问题跟踪",
        "Settings" => "设置",
        _ => "工作台"
    };

    // ===== 自定义字段 =====
    [ObservableProperty] private ObservableCollection<CustomFieldItem> _customFields = new();

    public List<string> HotkeyOptions { get; } = new()
    {
        "A","B","C","D","E","F","G","H","J","K","L","M","N","P","Q","R","S","T","U","W",
        "D1","D2","D3","D4","D5","D6","D7","D8","D9","D0",
        "F1","F2","F3","F4","F5","F6","F7","F8","F9","F10","F11","F12"
    };

    public List<string> KeyOptions { get; } = new()
    {
        "A","B","C","D","E","F","G","H","J","K","L","M","N","P","Q","R","S","T","U","W"
    };

    public SettingsViewModel()
    {
        // 初始化强调色列表
        foreach (var name in ThemeService.GetAccentColorNames)
        {
            AccentColors.Add(new AccentColorItem
            {
                Name = name,
                PreviewColor = ThemeService.GetAccentPreviewColor(name)
            });
        }

        // 监听主题变化
        ThemeService.Instance.PropertyChanged += OnThemeServiceChanged;

        _ = RefreshAsync();
    }

    public Task RefreshAsync()
    {
        Projects = new ObservableCollection<string>(ConfigService.Instance.Projects);
        WorkTypes = new ObservableCollection<string>(ConfigService.Instance.WorkTypes);
        ContentTemplates = new ObservableCollection<ContentTemplate>(ConfigService.Instance.ContentTemplates);
        EnableShortcuts = ConfigService.Instance.EnableShortcuts;
        EnableReminder = ConfigService.Instance.EnableReminder;
        ReminderHour = ConfigService.Instance.ReminderHour;
        ReminderMinute = ConfigService.Instance.ReminderMinute;
        ShortcutSearch = ConfigService.Instance.ShortcutSearch;
        ShortcutNew = ConfigService.Instance.ShortcutNew;
        ShortcutSave = ConfigService.Instance.ShortcutSave;
        ShortcutView1 = ConfigService.Instance.ShortcutView1;
        ShortcutView2 = ConfigService.Instance.ShortcutView2;
        ShortcutView3 = ConfigService.Instance.ShortcutView3;
        ShortcutView4 = ConfigService.Instance.ShortcutView4;
        ShortcutView5 = ConfigService.Instance.ShortcutView5;

        // 外观与主题
        IsLightTheme = ThemeService.Instance.ThemeMode == "Light";
        IsDarkTheme = ThemeService.Instance.ThemeMode == "Dark";
        SelectedAccentColor = ThemeService.Instance.AccentColor;
        SelectedFontSize = ThemeService.Instance.FontSizeLevel;
        SelectedDensity = ThemeService.Instance.UIDensity;
        OnPropertyChanged(nameof(FontSizeLabel));
        OnPropertyChanged(nameof(DensityLabel));

        // Dashboard 布局
        ShowDashStats = ConfigService.Instance.ShowDashStats;
        ShowDashReminder = ConfigService.Instance.ShowDashReminder;
        ShowDashProbation = ConfigService.Instance.ShowDashProbation;
        ShowDashCharts = ConfigService.Instance.ShowDashCharts;
        ShowDashHighlights = ConfigService.Instance.ShowDashHighlights;
        ShowDashRecent = ConfigService.Instance.ShowDashRecent;

        // 启动与行为
        AutoStart = ConfigService.Instance.AutoStart;
        MinimizeToTray = ConfigService.Instance.MinimizeToTray;
        DefaultView = ConfigService.Instance.DefaultView;
        AutoSaveInterval = ConfigService.Instance.AutoSaveInterval;
        OnPropertyChanged(nameof(DefaultViewLabel));

        // 休息日
        var restDays = ConfigService.Instance.RestDays;
        IsRestDay0 = restDays.Contains(0);
        IsRestDay1 = restDays.Contains(1);
        IsRestDay2 = restDays.Contains(2);
        IsRestDay3 = restDays.Contains(3);
        IsRestDay4 = restDays.Contains(4);
        IsRestDay5 = restDays.Contains(5);
        IsRestDay6 = restDays.Contains(6);

        // 自定义字段
        CustomFields = new ObservableCollection<CustomFieldItem>(
            ConfigService.Instance.CustomFields.Select(f => new CustomFieldItem
            {
                Name = f.Name,
                FieldType = f.FieldType,
                DefaultValue = f.DefaultValue
            }));

        return Task.CompletedTask;
    }

    private void OnThemeServiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ThemeService.ThemeMode))
        {
            IsLightTheme = ThemeService.Instance.ThemeMode == "Light";
            IsDarkTheme = ThemeService.Instance.ThemeMode == "Dark";
        }
        else if (e.PropertyName == nameof(ThemeService.AccentColor))
        {
            SelectedAccentColor = ThemeService.Instance.AccentColor;
        }
    }

    // ===== 快捷键 handlers =====
    partial void OnEnableShortcutsChanged(bool value)
    {
        ConfigService.Instance.EnableShortcuts = value;
        StatusMessage = value ? "快捷键已启用" : "快捷键已禁用";
    }

    partial void OnEnableReminderChanged(bool value)
    {
        ConfigService.Instance.EnableReminder = value;
        StatusMessage = value ? "定时提醒已启用" : "定时提醒已禁用";
    }

    partial void OnReminderHourChanged(int value)
    {
        if (value >= 0 && value <= 23)
        {
            ConfigService.Instance.ReminderHour = value;
            StatusMessage = $"提醒时间已更新为 {value:D2}:{ReminderMinute:D2}";
        }
    }

    partial void OnReminderMinuteChanged(int value)
    {
        if (value >= 0 && value <= 59)
        {
            ConfigService.Instance.ReminderMinute = value;
            StatusMessage = $"提醒时间已更新为 {ReminderHour:D2}:{value:D2}";
        }
    }

    partial void OnShortcutSearchChanged(string value) { ConfigService.Instance.ShortcutSearch = value; StatusMessage = $"搜索快捷键 → Ctrl+{value}"; }
    partial void OnShortcutNewChanged(string value) { ConfigService.Instance.ShortcutNew = value; StatusMessage = $"新增快捷键 → Ctrl+{value}"; }
    partial void OnShortcutSaveChanged(string value) { ConfigService.Instance.ShortcutSave = value; StatusMessage = $"保存快捷键 → Ctrl+{value}"; }
    partial void OnShortcutView1Changed(string value) { ConfigService.Instance.ShortcutView1 = value; StatusMessage = $"工作台快捷键 → Ctrl+{value}"; }
    partial void OnShortcutView2Changed(string value) { ConfigService.Instance.ShortcutView2 = value; StatusMessage = $"工作记录快捷键 → Ctrl+{value}"; }
    partial void OnShortcutView3Changed(string value) { ConfigService.Instance.ShortcutView3 = value; StatusMessage = $"知识库快捷键 → Ctrl+{value}"; }
    partial void OnShortcutView4Changed(string value) { ConfigService.Instance.ShortcutView4 = value; StatusMessage = $"问题跟踪快捷键 → Ctrl+{value}"; }
    partial void OnShortcutView5Changed(string value) { ConfigService.Instance.ShortcutView5 = value; StatusMessage = $"设置快捷键 → Ctrl+{value}"; }

    // ===== 外观与主题 handlers =====
    partial void OnIsLightThemeChanged(bool value)
    {
        if (value)
        {
            IsDarkTheme = false;
            ThemeService.Instance.SetThemeMode("Light");
            StatusMessage = "已切换至浅色模式";
        }
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (value)
        {
            IsLightTheme = false;
            ThemeService.Instance.SetThemeMode("Dark");
            StatusMessage = "已切换至深色模式";
        }
    }

    partial void OnSelectedAccentColorChanged(string value)
    {
        foreach (var item in AccentColors)
            item.IsSelected = item.Name == value;
        OnPropertyChanged(nameof(AccentColors));
        ThemeService.Instance.SetAccentColor(value);
        StatusMessage = $"强调色已切换为 {value}";
    }

    partial void OnSelectedFontSizeChanged(string value)
    {
        ThemeService.Instance.SetFontSizeLevel(value);
        OnPropertyChanged(nameof(FontSizeLabel));
        StatusMessage = $"字体大小已切换为 {FontSizeLabel}";
    }

    partial void OnSelectedDensityChanged(string value)
    {
        ThemeService.Instance.SetUIDensity(value);
        OnPropertyChanged(nameof(DensityLabel));
        StatusMessage = $"界面密度已切换为 {DensityLabel}";
    }

    // ===== Dashboard 布局 handlers =====
    partial void OnShowDashStatsChanged(bool value) { ConfigService.Instance.ShowDashStats = value; StatusMessage = value ? "统计卡片已显示" : "统计卡片已隐藏"; }
    partial void OnShowDashReminderChanged(bool value) { ConfigService.Instance.ShowDashReminder = value; StatusMessage = value ? "今日提醒已显示" : "今日提醒已隐藏"; }
    partial void OnShowDashProbationChanged(bool value) { ConfigService.Instance.ShowDashProbation = value; StatusMessage = value ? "试用期进度已显示" : "试用期进度已隐藏"; }
    partial void OnShowDashChartsChanged(bool value) { ConfigService.Instance.ShowDashCharts = value; StatusMessage = value ? "图表区域已显示" : "图表区域已隐藏"; }
    partial void OnShowDashHighlightsChanged(bool value) { ConfigService.Instance.ShowDashHighlights = value; StatusMessage = value ? "工作亮点已显示" : "工作亮点已隐藏"; }
    partial void OnShowDashRecentChanged(bool value) { ConfigService.Instance.ShowDashRecent = value; StatusMessage = value ? "最近记录已显示" : "最近记录已隐藏"; }

    // ===== 启动与行为 handlers =====
    partial void OnAutoStartChanged(bool value)
    {
        ConfigService.Instance.AutoStart = value;
        AutoStartService.ApplyAutoStart(value);
        StatusMessage = value ? "开机自启动已启用" : "开机自启动已禁用";
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        ConfigService.Instance.MinimizeToTray = value;
        StatusMessage = value ? "最小化到托盘已启用" : "最小化到托盘已禁用";
    }

    partial void OnDefaultViewChanged(string value)
    {
        ConfigService.Instance.DefaultView = value;
        OnPropertyChanged(nameof(DefaultViewLabel));
        StatusMessage = $"默认视图已设为 {DefaultViewLabel}";
    }

    partial void OnAutoSaveIntervalChanged(int value)
    {
        ConfigService.Instance.AutoSaveInterval = value;
        StatusMessage = $"自动保存间隔已设为 {value} 分钟";
    }

    // ===== 休息日 handlers =====
    private void UpdateRestDays()
    {
        var days = new List<int>();
        if (IsRestDay0) days.Add(0);
        if (IsRestDay1) days.Add(1);
        if (IsRestDay2) days.Add(2);
        if (IsRestDay3) days.Add(3);
        if (IsRestDay4) days.Add(4);
        if (IsRestDay5) days.Add(5);
        if (IsRestDay6) days.Add(6);
        ConfigService.Instance.RestDays = days;

        var names = new[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
        var selected = days.Select(d => names[d]).ToList();
        StatusMessage = days.Count > 0
            ? $"休息日已设为：{string.Join("、", selected)}"
            : "未设置休息日，所有日期均为工作日";
    }

    partial void OnIsRestDay0Changed(bool value) => UpdateRestDays();
    partial void OnIsRestDay1Changed(bool value) => UpdateRestDays();
    partial void OnIsRestDay2Changed(bool value) => UpdateRestDays();
    partial void OnIsRestDay3Changed(bool value) => UpdateRestDays();
    partial void OnIsRestDay4Changed(bool value) => UpdateRestDays();
    partial void OnIsRestDay5Changed(bool value) => UpdateRestDays();
    partial void OnIsRestDay6Changed(bool value) => UpdateRestDays();

    // ===== 任务管理 commands =====
    [RelayCommand]
    private void AddProject()
    {
        var dialog = new Views.ConfigItemDialog("添加任务", "");
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemValue))
        {
            ConfigService.Instance.AddProject(dialog.ItemValue.Trim());
            RefreshAsync();
            StatusMessage = "任务已添加";
        }
    }

    [RelayCommand]
    private void EditProject(string? project)
    {
        if (string.IsNullOrWhiteSpace(project)) return;
        var dialog = new Views.ConfigItemDialog("编辑任务", project);
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemValue))
        {
            ConfigService.Instance.UpdateProject(project, dialog.ItemValue.Trim());
            RefreshAsync();
            StatusMessage = "任务已更新";
        }
    }

    [RelayCommand]
    private void DeleteProject(string? project)
    {
        if (string.IsNullOrWhiteSpace(project)) return;
        if (!ConfirmDialog.Show($"确定要删除任务「{project}」吗？", "确认删除", ConfirmDialogType.Danger)) return;
        ConfigService.Instance.RemoveProject(project);
        RefreshAsync();
        StatusMessage = "任务已删除";
    }

    [RelayCommand]
    private void AddWorkType()
    {
        var dialog = new Views.ConfigItemDialog("添加工作类型", "");
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemValue))
        {
            ConfigService.Instance.AddWorkType(dialog.ItemValue.Trim());
            RefreshAsync();
            StatusMessage = "类型已添加";
        }
    }

    [RelayCommand]
    private void EditWorkType(string? workType)
    {
        if (string.IsNullOrWhiteSpace(workType)) return;
        var dialog = new Views.ConfigItemDialog("编辑工作类型", workType);
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemValue))
        {
            ConfigService.Instance.UpdateWorkType(workType, dialog.ItemValue.Trim());
            RefreshAsync();
            StatusMessage = "类型已更新";
        }
    }

    [RelayCommand]
    private void DeleteWorkType(string? workType)
    {
        if (string.IsNullOrWhiteSpace(workType)) return;
        if (!ConfirmDialog.Show($"确定要删除类型「{workType}」吗？", "确认删除", ConfirmDialogType.Danger)) return;
        ConfigService.Instance.RemoveWorkType(workType);
        RefreshAsync();
        StatusMessage = "类型已删除";
    }

    [RelayCommand]
    private void AddTemplate()
    {
        var dialog = new Views.ConfigItemDialog("添加模板名称", "");
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemValue))
        {
            var contentDialog = new Views.ConfigItemDialog("添加模板内容", "");
            contentDialog.Owner = Application.Current.MainWindow;
            if (contentDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(contentDialog.ItemValue))
            {
                ConfigService.Instance.AddContentTemplate(new ContentTemplate
                {
                    Name = dialog.ItemValue.Trim(),
                    Content = contentDialog.ItemValue.Trim()
                });
                RefreshAsync();
                StatusMessage = "模板已添加";
            }
        }
    }

    [RelayCommand]
    private void EditTemplate(ContentTemplate? template)
    {
        if (template == null) return;
        var dialog = new Views.ConfigItemDialog("编辑模板名称", template.Name);
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemValue))
        {
            var contentDialog = new Views.ConfigItemDialog("编辑模板内容", template.Content);
            contentDialog.Owner = Application.Current.MainWindow;
            if (contentDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(contentDialog.ItemValue))
            {
                ConfigService.Instance.UpdateContentTemplate(template.Name, new ContentTemplate
                {
                    Name = dialog.ItemValue.Trim(),
                    Content = contentDialog.ItemValue.Trim()
                });
                RefreshAsync();
                StatusMessage = "模板已更新";
            }
        }
    }

    [RelayCommand]
    private void DeleteTemplate(ContentTemplate? template)
    {
        if (template == null) return;
        if (!ConfirmDialog.Show($"确定要删除模板「{template.Name}」吗？", "确认删除", ConfirmDialogType.Danger)) return;
        ConfigService.Instance.RemoveContentTemplate(template.Name);
        RefreshAsync();
        StatusMessage = "模板已删除";
    }

    // ===== 自定义字段 commands =====
    [RelayCommand]
    private void AddCustomField()
    {
        var dialog = new Views.ConfigItemDialog("添加自定义字段名称", "");
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemValue))
        {
            var field = new CustomField { Name = dialog.ItemValue.Trim(), FieldType = "Text" };
            ConfigService.Instance.AddCustomField(field);
            RefreshAsync();
            StatusMessage = $"字段「{field.Name}」已添加";
        }
    }

    [RelayCommand]
    private void DeleteCustomField(CustomFieldItem? field)
    {
        if (field == null) return;
        if (!ConfirmDialog.Show($"确定要删除字段「{field.Name}」吗？", "确认删除", ConfirmDialogType.Danger)) return;
        ConfigService.Instance.RemoveCustomField(field.Name);
        RefreshAsync();
        StatusMessage = $"字段「{field.Name}」已删除";
    }

    [RelayCommand]
    private void ResetDashboardLayout()
    {
        ShowDashStats = true;
        ShowDashReminder = true;
        ShowDashProbation = true;
        ShowDashCharts = true;
        ShowDashHighlights = true;
        ShowDashRecent = true;
        StatusMessage = "Dashboard 布局已重置为默认";
    }
}

/// <summary>
/// 强调色选项（用于 UI 展示色块）
/// </summary>
public class AccentColorItem
{
    public string Name { get; set; } = string.Empty;
    public string PreviewColor { get; set; } = "#4F46E5";
    public bool IsSelected { get; set; }
}

/// <summary>
/// 自定义字段 UI 模型
/// </summary>
public class CustomFieldItem
{
    public string Name { get; set; } = string.Empty;
    public string FieldType { get; set; } = "Text";
    public string DefaultValue { get; set; } = string.Empty;
}
