using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using System.Collections.ObjectModel;

namespace EapWorkAssistant.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly WorkRecordRepository _recordRepo = new();
    private readonly KnowledgeRepository _knowledgeRepo = new();
    private readonly IssueRepository _issueRepo = new();
    private readonly UiTimer _searchTimer;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private bool _isSearchOpen;

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _showInitial = true;

    [ObservableProperty]
    private bool _showNoResults;

    [ObservableProperty]
    private bool _showResults;

    [ObservableProperty]
    private ObservableCollection<SearchResultItem> _searchResults = new();

    // 搜索结果分类计数（用于结果区顶部汇总）
    [ObservableProperty] private int _recordResultCount;
    [ObservableProperty] private int _knowledgeResultCount;
    [ObservableProperty] private int _issueResultCount;
    [ObservableProperty] private int _totalResultCount;
    [ObservableProperty] private bool _searchTruncated;

    private string _searchSummary = string.Empty;
    public string SearchSummary
    {
        get => _searchSummary;
        private set => SetProperty(ref _searchSummary, value);
    }

    private void ResetSearchStats()
    {
        RecordResultCount = KnowledgeResultCount = IssueResultCount = TotalResultCount = 0;
        SearchTruncated = false;
        SearchSummary = string.Empty;
    }

    public DashboardViewModel Dashboard { get; } = new();
    public WorkRecordViewModel WorkRecord { get; } = new();
    public KnowledgeViewModel Knowledge { get; } = new();
    public IssueViewModel Issue { get; } = new();
    public SettingsViewModel Settings { get; } = new();
    public RecycleBinViewModel RecycleBin { get; } = new();

    public MainViewModel()
    {
        _searchTimer = new UiTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await SearchAsync();
        };

        // 根据配置设置默认启动视图
        var defaultView = ConfigService.Instance.DefaultView;
        CurrentView = defaultView switch
        {
            ViewNames.WorkRecord => WorkRecord,
            ViewNames.Knowledge => Knowledge,
            ViewNames.Issue => Issue,
            ViewNames.Settings => Settings,
            ViewNames.RecycleBin => RecycleBin,
            _ => Dashboard
        };
        SelectedIndex = defaultView switch
        {
            ViewNames.WorkRecord => 1,
            ViewNames.Knowledge => 2,
            ViewNames.Issue => 3,
            ViewNames.Settings => 4,
            ViewNames.RecycleBin => 5,
                _ => 0
        };
        Dashboard.LoadDashboardAsync().SafeFire("加载仪表盘失败");

        // 订阅仪表盘图表点击导航事件
        Dashboard.NavigateToWorkRecord += (date) =>
        {
            WorkRecord.SelectedDate = date;
            WorkRecord.SelectedTabIndex = 0;
            NavigateTo(ViewNames.WorkRecord);
        };
        Dashboard.NavigateToWorkRecordFilter += (project) =>
        {
            WorkRecord.FilterProject = project;
            WorkRecord.SelectedTabIndex = 1;
            NavigateTo(ViewNames.WorkRecord);
        };
        Dashboard.NavigateToPage += (page) =>
        {
            NavigateTo(page);
        };
    }

    [RelayCommand]
    private void NavigateTo(string viewName)
    {
        object? targetView = viewName switch
        {
            ViewNames.WorkRecord => WorkRecord,
            ViewNames.Knowledge => Knowledge,
            ViewNames.Issue => Issue,
            ViewNames.Settings => Settings,
            ViewNames.RecycleBin => RecycleBin,
            _ => Dashboard
        };

        // 目标与当前视图相同：仅同步导航栏高亮，不拦截、不刷新
        if (ReferenceEquals(targetView, CurrentView))
        {
            SyncSelectedIndex(viewName);
            return;
        }

        // 离开前未保存确认：知识库/问题跟踪无自动保存；工作记录校验不过时。
        // 用户取消离开则还原导航栏选中项，保持当前视图不变。
        if (!CanLeaveCurrentView())
        {
            RevertSelectedIndex();
            return;
        }

        var previousView = CurrentView;
        CurrentView = targetView;
        SyncSelectedIndex(viewName);

        // 离开工作记录时暂停自动保存（会 flush 合法编辑），进入时恢复
        if (previousView == WorkRecord && targetView != WorkRecord)
            WorkRecord.PauseAutoSaveTimer();
        else if (targetView == WorkRecord && previousView != WorkRecord)
            WorkRecord.StartAutoSaveTimer();

        // 仅在视图实际切换时才刷新，避免重复加载
        if (targetView is IRefreshable refreshable)
            refreshable.RefreshAsync().SafeFire("刷新失败");
    }

    private void SyncSelectedIndex(string viewName)
    {
        var newIndex = viewName switch
        {
            ViewNames.WorkRecord => 1,
            ViewNames.Knowledge => 2,
            ViewNames.Issue => 3,
            ViewNames.Settings => 4,
            ViewNames.RecycleBin => 5,
            _ => 0
        };
        if (newIndex != SelectedIndex)
            SelectedIndex = newIndex;
    }

    /// <summary>检查即将离开的当前视图是否有未保存修改，决定是否允许导航。</summary>
    private bool CanLeaveCurrentView()
    {
        return CurrentView switch
        {
            WorkRecordViewModel wr => CanLeaveWorkRecord(wr),
            KnowledgeViewModel kw => !kw.IsFormDirty || !kw.HasUnsavedInput() || DialogService.Instance.ShowConfirm(
                "当前知识库页面有未保存的修改，离开后将丢失。确定离开吗？",
                "未保存的修改", ConfirmType.Warning),
            IssueViewModel iss => !iss.IsFormDirty || !iss.HasUnsavedInput() || DialogService.Instance.ShowConfirm(
                "当前问题跟踪页面有未保存的修改，离开后将丢失。确定离开吗？",
                "未保存的修改", ConfirmType.Warning),
            _ => true
        };
    }

    /// <summary>
    /// 工作记录离开确认。
    /// - 未编辑或空草稿：直接放行，避免「无脑提示」。
    /// - 有实际未保存输入：弹出确认，用户明确选择离开后才放行；可自动保存的数据会先 flush，避免丢失。
    /// </summary>
    private bool CanLeaveWorkRecord(WorkRecordViewModel wr)
    {
        if (!wr.IsFormDirty || !wr.HasUnsavedInput()) return true;

        var confirmed = DialogService.Instance.ShowConfirm(
            "当前工作记录有未保存的内容，离开后将丢失。确定离开吗？",
            "未保存的修改", ConfirmType.Warning);

        if (confirmed && wr.CanQuickSave())
            wr.FlushPendingChangesAsync().SafeFire("离开保存失败");

        return confirmed;
    }

    /// <summary>用户取消离开时，把导航栏选中项还原到当前实际视图，避免高亮错位。</summary>
    private void RevertSelectedIndex()
    {
        var idx = CurrentView switch
        {
            WorkRecordViewModel => 1,
            KnowledgeViewModel => 2,
            IssueViewModel => 3,
            SettingsViewModel => 4,
            RecycleBinViewModel => 5,
            _ => 0
        };
        if (SelectedIndex != idx)
            SelectedIndex = idx;
    }

    [RelayCommand]
    private void OpenSearch()
    {
        IsSearchOpen = true;
        SearchKeyword = string.Empty;
        SearchResults.Clear();
        ResetSearchStats();
        ShowInitial = true;
        ShowNoResults = false;
        ShowResults = false;
        IsSearching = false;
    }

    [RelayCommand]
    private void CloseSearch()
    {
        _searchTimer.Stop();
        IsSearchOpen = false;
        SearchKeyword = string.Empty;
        SearchResults.Clear();
        ResetSearchStats();
        ShowInitial = true;
        ShowNoResults = false;
        ShowResults = false;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            SearchResults.Clear();
            ResetSearchStats();
            ShowInitial = true;
            ShowNoResults = false;
            ShowResults = false;
            IsSearching = false;
            return;
        }

        IsSearching = true;
        ShowInitial = false;
        ShowNoResults = false;
        ShowResults = false;

        var results = new List<SearchResultItem>();
        var keyword = SearchKeyword.Trim();

        try
        {
            // 数据库级搜索（SQL LIKE，支持多关键词空格分隔）
            var records = await _recordRepo.SearchAsync(keyword);
            foreach (var r in records)
            {
                var content = r.Content ?? "";
                results.Add(new SearchResultItem
                {
                    Type = "工作记录",
                    Title = $"{r.ProjectName} - {r.WorkDate}",
                    Content = content.Length > 60 ? content[..60] + "..." : content,
                    Icon = "\U0001F4DD",
                    NavigateTo = ViewNames.WorkRecord,
                    TargetDate = DateTime.TryParse(r.WorkDate, out var d) ? d : null,
                    Keyword = keyword
                });
            }

            var knowledge = await _knowledgeRepo.SearchAsync(keyword);
            foreach (var k in knowledge)
            {
                var content = k.Content ?? "";
                results.Add(new SearchResultItem
                {
                    Type = "知识库",
                    Title = k.Title,
                    Content = content.Length > 60 ? content[..60] + "..." : content,
                    Icon = "\U0001F4DA",
                    NavigateTo = ViewNames.Knowledge,
                    TargetId = k.Id,
                    Keyword = keyword
                });
            }

            var issues = await _issueRepo.SearchAsync(keyword);
            foreach (var i in issues)
            {
                var content = i.Description ?? "";
                results.Add(new SearchResultItem
                {
                    Type = "问题跟踪",
                    Title = $"[{i.ProjectName}] {i.Title}",
                    Content = content.Length > 60 ? content[..60] + "..." : content,
                    Icon = "\U0001F527",
                    NavigateTo = ViewNames.Issue,
                    TargetId = i.Id,
                    Keyword = keyword
                });
            }
        }
        catch (Exception ex)
        {
            ToastService.Error($"搜索出错：{ex.Message}");
        }

        // 分类计数汇总（用于结果区顶部提示，并标识是否被截断）
        RecordResultCount = results.Count(r => r.Type == "工作记录");
        KnowledgeResultCount = results.Count(r => r.Type == "知识库");
        IssueResultCount = results.Count(r => r.Type == "问题跟踪");
        TotalResultCount = results.Count;
        SearchTruncated = TotalResultCount > 20;
        SearchSummary = TotalResultCount > 0
            ? $"工作 {RecordResultCount} · 知识 {KnowledgeResultCount} · 问题 {IssueResultCount}"
            : string.Empty;

        SearchResults = new ObservableCollection<SearchResultItem>(results.Take(20));
        IsSearching = false;

        if (SearchResults.Any())
        {
            ShowResults = true;
            ShowNoResults = false;
        }
        else
        {
            ShowResults = false;
            ShowNoResults = true;
        }
    }

    [RelayCommand]
    private void NavigateToResult(SearchResultItem? item)
    {
        if (item == null) return;
        _searchTimer.Stop();
        IsSearchOpen = false;
        SearchKeyword = string.Empty;

        // 工作记录：切换到全部记录 Tab 并用关键词筛选
        if (item.NavigateTo == ViewNames.WorkRecord)
        {
            if (item.TargetDate.HasValue)
                WorkRecord.SelectedDate = item.TargetDate.Value;
            WorkRecord.SearchKeyword = item.Keyword;  // 先设置关键词
            WorkRecord.SelectedTabIndex = 1;           // 再切 Tab，触发加载时已带关键词
        }

        NavigateTo(item.NavigateTo);

        // 知识库/问题跟踪：导航后设置搜索关键词，OnSearchKeywordChanged 防抖定时器会自动触发搜索
        // 不再手动调用 SearchCommand，避免与防抖定时器竞态导致双重搜索
        if (item.NavigateTo == ViewNames.Knowledge)
        {
            Knowledge.SearchKeyword = item.Keyword;
        }
        else if (item.NavigateTo == ViewNames.Issue)
        {
            Issue.SearchKeyword = item.Keyword;
        }
    }

    partial void OnSearchKeywordChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _searchTimer.Stop();
            SearchResults.Clear();
            ShowInitial = true;
            ShowNoResults = false;
            ShowResults = false;
            IsSearching = false;
        }
        else
        {
            // 防抖：每次按键重启定时器，300ms 后自动搜索
            _searchTimer.Stop();
            _searchTimer.Start();
        }
    }

    partial void OnSelectedIndexChanged(int value)
    {
        var views = new[] { ViewNames.Dashboard, ViewNames.WorkRecord, ViewNames.Knowledge, ViewNames.Issue, ViewNames.Settings, ViewNames.RecycleBin };
        if (value >= 0 && views.Length > value)
            NavigateTo(views[value]);
    }
}

public class SearchResultItem
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string NavigateTo { get; set; } = string.Empty;
    public DateTime? TargetDate { get; set; }
    public int TargetId { get; set; }
    public string Keyword { get; set; } = string.Empty;
}
