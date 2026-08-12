using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace EapWorkAssistant.ViewModels;

public partial class SettingsViewModel : ObservableObject, IRefreshable
{
    private readonly UiTimer _statusTimer;
    [ObservableProperty]
    private ObservableCollection<string> _projects = new();

    [ObservableProperty]
    private ObservableCollection<string> _workTypes = new();

    [ObservableProperty]
    private ObservableCollection<string> _knowledgeCategories = new();

    [ObservableProperty]
    private string? _selectedProject;

    [ObservableProperty]
    private string? _selectedWorkType;

    [ObservableProperty]
    private string? _selectedKnowledgeCategory;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    partial void OnStatusMessageChanged(string value)
    {
        _statusTimer.Stop();
        if (!string.IsNullOrEmpty(value))
            _statusTimer.Start();
    }

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

    // 快捷键独立启用状态
    [ObservableProperty] private bool _shortcutSearchEnabled = true;
    [ObservableProperty] private bool _shortcutNewEnabled = true;
    [ObservableProperty] private bool _shortcutSaveEnabled = true;
    [ObservableProperty] private bool _shortcutView1Enabled = true;
    [ObservableProperty] private bool _shortcutView2Enabled = true;
    [ObservableProperty] private bool _shortcutView3Enabled = true;
    [ObservableProperty] private bool _shortcutView4Enabled = true;
    [ObservableProperty] private bool _shortcutView5Enabled = true;

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
    [ObservableProperty] private string _defaultView = ViewNames.Dashboard;
    [ObservableProperty] private int _autoSaveInterval = 5;

    // ===== 休息日（0=周日, 1=周一, ..., 6=周六）=====
    [ObservableProperty] private bool _isRestDay0; // 周日
    [ObservableProperty] private bool _isRestDay1; // 周一
    [ObservableProperty] private bool _isRestDay2; // 周二
    [ObservableProperty] private bool _isRestDay3; // 周三
    [ObservableProperty] private bool _isRestDay4; // 周四
    [ObservableProperty] private bool _isRestDay5; // 周五
    [ObservableProperty] private bool _isRestDay6; // 周六

    public List<string> ViewOptions { get; } = new() { ViewNames.Dashboard, ViewNames.WorkRecord, ViewNames.Knowledge, ViewNames.Issue, ViewNames.Settings };
    public List<int> AutoSaveOptions { get; } = new() { 1, 3, 5, 10, 15, 30 };

    public string DefaultViewLabel => DefaultView switch
    {
        ViewNames.Dashboard => "工作台",
        ViewNames.WorkRecord => "工作记录",
        ViewNames.Knowledge => "知识库",
        ViewNames.Issue => "问题跟踪",
        ViewNames.Settings => "设置",
        _ => "工作台"
    };

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
        _statusTimer = new UiTimer { Interval = TimeSpan.FromSeconds(3) };
        _statusTimer.Tick += (_, _) => { StatusMessage = string.Empty; _statusTimer.Stop(); };

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

        RefreshAsync().SafeFire("加载设置失败");
    }

    public Task RefreshAsync()
    {
        Projects = new ObservableCollection<string>(ConfigService.Instance.Projects);
        WorkTypes = new ObservableCollection<string>(ConfigService.Instance.WorkTypes);
        KnowledgeCategories = new ObservableCollection<string>(ConfigService.Instance.KnowledgeCategories);
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
        ShortcutSearchEnabled = ConfigService.Instance.ShortcutSearchEnabled;
        ShortcutNewEnabled = ConfigService.Instance.ShortcutNewEnabled;
        ShortcutSaveEnabled = ConfigService.Instance.ShortcutSaveEnabled;
        ShortcutView1Enabled = ConfigService.Instance.ShortcutView1Enabled;
        ShortcutView2Enabled = ConfigService.Instance.ShortcutView2Enabled;
        ShortcutView3Enabled = ConfigService.Instance.ShortcutView3Enabled;
        ShortcutView4Enabled = ConfigService.Instance.ShortcutView4Enabled;
        ShortcutView5Enabled = ConfigService.Instance.ShortcutView5Enabled;

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

        // AI 服务配置
        var aiSettings = AiSettings.Load();
        _isSavingAiSettings = true;
        AiEndpoint = aiSettings.Endpoint;
        AiApiKey = aiSettings.ApiKey;
        AiModel = aiSettings.Model;
        AiTimeoutSeconds = aiSettings.TimeoutSeconds;
        _isSavingAiSettings = false;

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
        ReregisterShortcuts();
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

    partial void OnShortcutSearchChanged(string value) { ConfigService.Instance.ShortcutSearch = value; ReregisterShortcuts(); StatusMessage = $"搜索快捷键 → Ctrl+{value}"; }
    partial void OnShortcutNewChanged(string value) { ConfigService.Instance.ShortcutNew = value; ReregisterShortcuts(); StatusMessage = $"新增快捷键 → Ctrl+{value}"; }
    partial void OnShortcutSaveChanged(string value) { ConfigService.Instance.ShortcutSave = value; ReregisterShortcuts(); StatusMessage = $"保存快捷键 → Ctrl+{value}"; }
    partial void OnShortcutView1Changed(string value) { ConfigService.Instance.ShortcutView1 = value; ReregisterShortcuts(); StatusMessage = $"工作台快捷键 → Ctrl+{value}"; }
    partial void OnShortcutView2Changed(string value) { ConfigService.Instance.ShortcutView2 = value; ReregisterShortcuts(); StatusMessage = $"工作记录快捷键 → Ctrl+{value}"; }
    partial void OnShortcutView3Changed(string value) { ConfigService.Instance.ShortcutView3 = value; ReregisterShortcuts(); StatusMessage = $"知识库快捷键 → Ctrl+{value}"; }
    partial void OnShortcutView4Changed(string value) { ConfigService.Instance.ShortcutView4 = value; ReregisterShortcuts(); StatusMessage = $"问题跟踪快捷键 → Ctrl+{value}"; }
    partial void OnShortcutView5Changed(string value) { ConfigService.Instance.ShortcutView5 = value; ReregisterShortcuts(); StatusMessage = $"设置快捷键 → Ctrl+{value}"; }

    // 快捷键启用/禁用时重新注册
    partial void OnShortcutSearchEnabledChanged(bool value) { ConfigService.Instance.ShortcutSearchEnabled = value; ReregisterShortcuts(); StatusMessage = value ? "搜索快捷键已启用" : "搜索快捷键已禁用"; }
    partial void OnShortcutNewEnabledChanged(bool value) { ConfigService.Instance.ShortcutNewEnabled = value; ReregisterShortcuts(); StatusMessage = value ? "新增快捷键已启用" : "新增快捷键已禁用"; }
    partial void OnShortcutSaveEnabledChanged(bool value) { ConfigService.Instance.ShortcutSaveEnabled = value; ReregisterShortcuts(); StatusMessage = value ? "保存快捷键已启用" : "保存快捷键已禁用"; }
    partial void OnShortcutView1EnabledChanged(bool value) { ConfigService.Instance.ShortcutView1Enabled = value; ReregisterShortcuts(); StatusMessage = value ? "工作台快捷键已启用" : "工作台快捷键已禁用"; }
    partial void OnShortcutView2EnabledChanged(bool value) { ConfigService.Instance.ShortcutView2Enabled = value; ReregisterShortcuts(); StatusMessage = value ? "工作记录快捷键已启用" : "工作记录快捷键已禁用"; }
    partial void OnShortcutView3EnabledChanged(bool value) { ConfigService.Instance.ShortcutView3Enabled = value; ReregisterShortcuts(); StatusMessage = value ? "知识库快捷键已启用" : "知识库快捷键已禁用"; }
    partial void OnShortcutView4EnabledChanged(bool value) { ConfigService.Instance.ShortcutView4Enabled = value; ReregisterShortcuts(); StatusMessage = value ? "问题跟踪快捷键已启用" : "问题跟踪快捷键已禁用"; }
    partial void OnShortcutView5EnabledChanged(bool value) { ConfigService.Instance.ShortcutView5Enabled = value; ReregisterShortcuts(); StatusMessage = value ? "设置快捷键已启用" : "设置快捷键已禁用"; }

    public event System.Action? ShortcutsChanged;

    private void ReregisterShortcuts()
    {
        ShortcutsChanged?.Invoke();
    }

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
        var value = DialogService.Instance.ShowInputDialog("添加任务", "");
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = value.Trim();
            if (ConfigService.Instance.Projects.Contains(value))
            {
                StatusMessage = $"任务「{value}」已存在，无需重复添加";
                return;
            }
            ConfigService.Instance.AddProject(value);
            RefreshAsync();
            StatusMessage = "任务已添加";
        }
    }

    [RelayCommand]
    private async Task EditProject(string? project)
    {
        if (string.IsNullOrWhiteSpace(project)) return;
        var value = DialogService.Instance.ShowInputDialog("编辑任务", project);
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = value.Trim();
            if (value != project && ConfigService.Instance.Projects.Contains(value))
            {
                StatusMessage = $"任务「{value}」已存在，无法重命名";
                return;
            }
            ConfigService.Instance.UpdateProject(project, value);
            var count = await new WorkRecordRepository().UpdateProjectNameAsync(project, value);
            await RefreshAsync();
            StatusMessage = count > 0 ? $"任务已更新，已同步 {count} 条记录" : "任务已更新";
        }
    }

    [RelayCommand]
    private async Task DeleteProject(string? project)
    {
        if (string.IsNullOrWhiteSpace(project)) return;
        var count = await new WorkRecordRepository().GetCountByProjectAsync(project);
        var message = count > 0
            ? $"确定要删除任务「{project}」吗？\n该任务被 {count} 条工作记录引用。删除后这些记录仍保留原任务名「{project}」，但将不再出现在任务筛选下拉中。"
            : $"确定要删除任务「{project}」吗？";
        if (!DialogService.Instance.ShowConfirm(message, "确认删除", ConfirmType.Danger)) return;
        ConfigService.Instance.RemoveProject(project);
        await RefreshAsync();
        StatusMessage = "任务已删除";
    }

    [RelayCommand]
    private void AddWorkType()
    {
        var value = DialogService.Instance.ShowInputDialog("添加工作类型", "");
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = value.Trim();
            if (ConfigService.Instance.WorkTypes.Contains(value))
            {
                StatusMessage = $"类型「{value}」已存在，无需重复添加";
                return;
            }
            ConfigService.Instance.AddWorkType(value);
            RefreshAsync();
            StatusMessage = "类型已添加";
        }
    }

    [RelayCommand]
    private async Task EditWorkType(string? workType)
    {
        if (string.IsNullOrWhiteSpace(workType)) return;
        var value = DialogService.Instance.ShowInputDialog("编辑工作类型", workType);
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = value.Trim();
            if (value != workType && ConfigService.Instance.WorkTypes.Contains(value))
            {
                StatusMessage = $"类型「{value}」已存在，无法重命名";
                return;
            }
            ConfigService.Instance.UpdateWorkType(workType, value);
            var count = await new WorkRecordRepository().UpdateWorkTypeAsync(workType, value);
            await RefreshAsync();
            StatusMessage = count > 0 ? $"类型已更新，已同步 {count} 条记录" : "类型已更新";
        }
    }

    [RelayCommand]
    private async Task DeleteWorkType(string? workType)
    {
        if (string.IsNullOrWhiteSpace(workType)) return;
        var count = await new WorkRecordRepository().GetCountByWorkTypeAsync(workType);
        var message = count > 0
            ? $"确定要删除类型「{workType}」吗？\n该类型被 {count} 条工作记录引用。删除后这些记录仍保留原类型「{workType}」，但将不再出现在类型筛选下拉中。"
            : $"确定要删除类型「{workType}」吗？";
        if (!DialogService.Instance.ShowConfirm(message, "确认删除", ConfirmType.Danger)) return;
        ConfigService.Instance.RemoveWorkType(workType);
        await RefreshAsync();
        StatusMessage = "类型已删除";
    }

    // ===== 知识分类管理 commands =====
    [RelayCommand]
    private void AddKnowledgeCategory()
    {
        var value = DialogService.Instance.ShowInputDialog("添加知识分类", "");
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = value.Trim();
            if (ConfigService.Instance.KnowledgeCategories.Contains(value))
            {
                StatusMessage = $"分类「{value}」已存在，无需重复添加";
                return;
            }
            ConfigService.Instance.AddKnowledgeCategory(value);
            RefreshAsync();
            StatusMessage = "分类已添加";
        }
    }

    [RelayCommand]
    private async Task EditKnowledgeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return;
        var value = DialogService.Instance.ShowInputDialog("编辑知识分类", category);
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = value.Trim();
            if (value != category && ConfigService.Instance.KnowledgeCategories.Contains(value))
            {
                StatusMessage = $"分类「{value}」已存在，无法重命名";
                return;
            }
            ConfigService.Instance.UpdateKnowledgeCategory(category, value);
            var count = await new KnowledgeRepository().UpdateCategoryAsync(category, value);
            await RefreshAsync();
            StatusMessage = count > 0 ? $"分类已更新，已同步 {count} 条知识" : "分类已更新";
        }
    }

    [RelayCommand]
    private async Task DeleteKnowledgeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return;
        var count = await new KnowledgeRepository().GetCountByCategoryAsync(category);
        var message = count > 0
            ? $"确定要删除分类「{category}」吗？\n该分类被 {count} 条知识条目引用。删除后这些条目仍保留原分类「{category}」，但将不再出现在分类筛选下拉中。"
            : $"确定要删除分类「{category}」吗？";
        if (!DialogService.Instance.ShowConfirm(message, "确认删除", ConfirmType.Danger)) return;
        ConfigService.Instance.RemoveKnowledgeCategory(category);
        await RefreshAsync();
        StatusMessage = "分类已删除";
    }

    [RelayCommand]
    private void AddTemplate()
    {
        var value = DialogService.Instance.ShowInputDialog("添加模板名称", "");
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = value.Trim();
            var content = DialogService.Instance.ShowInputDialog("添加模板内容", "");
            if (!string.IsNullOrWhiteSpace(content))
            {
                ConfigService.Instance.AddContentTemplate(new ContentTemplate
                {
                    Name = value,
                    Content = content.Trim()
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
        var value = DialogService.Instance.ShowInputDialog("编辑模板名称", template.Name);
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = value.Trim();
            var content = DialogService.Instance.ShowInputDialog("编辑模板内容", template.Content);
            if (!string.IsNullOrWhiteSpace(content))
            {
                ConfigService.Instance.UpdateContentTemplate(template.Name, new ContentTemplate
                {
                    Name = value,
                    Content = content.Trim()
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
        if (!DialogService.Instance.ShowConfirm($"确定要删除模板「{template.Name}」吗？", "确认删除", ConfirmType.Danger)) return;
        ConfigService.Instance.RemoveContentTemplate(template.Name);
        RefreshAsync();
        StatusMessage = "模板已删除";
    }

    // ===== AI 服务配置 =====
    [ObservableProperty] private string _aiEndpoint = "https://api.deepseek.com/v1";
    [ObservableProperty] private string _aiApiKey = string.Empty;
    [ObservableProperty] private string _aiModel = "deepseek-chat";
    [ObservableProperty] private int _aiTimeoutSeconds = 120;
    [ObservableProperty] private bool _isTestingAi;

    public List<int> AiTimeoutOptions { get; } = new() { 60, 120, 180, 300 };

    private bool _isSavingAiSettings;

    private void SaveAiSettings()
    {
        if (_isSavingAiSettings) return;
        _isSavingAiSettings = true;
        try
        {
            var settings = new AiSettings
            {
                Endpoint = AiEndpoint,
                ApiKey = AiApiKey,
                Model = AiModel,
                TimeoutSeconds = AiTimeoutSeconds
            };
            settings.Save();
            StatusMessage = "AI 配置已保存";
        }
        finally
        {
            _isSavingAiSettings = false;
        }
    }

    partial void OnAiEndpointChanged(string value) => SaveAiSettings();
    partial void OnAiApiKeyChanged(string value) => SaveAiSettings();
    partial void OnAiModelChanged(string value) => SaveAiSettings();
    partial void OnAiTimeoutSecondsChanged(int value) => SaveAiSettings();

    [RelayCommand]
    private async Task TestAiConnectionAsync()
    {
        IsTestingAi = true;
        try
        {
            SaveAiSettings();
            var aiService = AiService.Instance;
            var result = await aiService.SendChatAsync(
                "你是一位测试助手。",
                "请回复'连接成功'四个字。");
            var preview = result.Length > 50 ? result[..50] + "..." : result;
            ToastService.Success($"AI 服务连接成功！回复：{preview}");
            StatusMessage = "AI 服务连接测试通过";
        }
        catch (Exception ex)
        {
            ToastService.Error($"AI 服务连接失败：{ex.Message}");
            StatusMessage = "AI 服务连接测试失败";
        }
        finally
        {
            IsTestingAi = false;
        }
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
public partial class AccentColorItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string PreviewColor { get; set; } = "#4F46E5";
    [ObservableProperty] private bool _isSelected;
}
