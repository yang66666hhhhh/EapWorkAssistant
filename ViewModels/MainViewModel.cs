using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace EapWorkAssistant.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly WorkRecordRepository _recordRepo = new();
    private readonly KnowledgeRepository _knowledgeRepo = new();
    private readonly IssueRepository _issueRepo = new();
    private readonly DispatcherTimer _searchTimer;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private bool _isSearchOpen;

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

    public DashboardViewModel Dashboard { get; } = new();
    public WorkRecordViewModel WorkRecord { get; } = new();
    public KnowledgeViewModel Knowledge { get; } = new();
    public IssueViewModel Issue { get; } = new();
    public SettingsViewModel Settings { get; } = new();

    public MainViewModel()
    {
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await SearchAsync();
        };

        // 根据配置设置默认启动视图
        var defaultView = ConfigService.Instance.DefaultView;
        CurrentView = defaultView switch
        {
            "WorkRecord" => WorkRecord,
            "Knowledge" => Knowledge,
            "Issue" => Issue,
            "Settings" => Settings,
            _ => Dashboard
        };
        SelectedIndex = defaultView switch
        {
            "WorkRecord" => 1,
            "Knowledge" => 2,
            "Issue" => 3,
            "Settings" => 4,
                _ => 0
        };
        Dashboard.LoadDashboardAsync().SafeFire("加载仪表盘失败");

        // 订阅仪表盘图表点击导航事件
        Dashboard.NavigateToWorkRecord += (date) =>
        {
            WorkRecord.SelectedDate = date;
            WorkRecord.SelectedTabIndex = 0;
            NavigateTo("WorkRecord");
        };
        Dashboard.NavigateToWorkRecordFilter += (project) =>
        {
            WorkRecord.FilterProject = project;
            WorkRecord.SelectedTabIndex = 1;
            NavigateTo("WorkRecord");
        };
        Dashboard.NavigateToPage += (page) =>
        {
            NavigateTo(page);
        };
    }

    [RelayCommand]
    private void NavigateTo(string viewName)
    {
        var previousView = CurrentView;
        CurrentView = viewName switch
        {
            "Dashboard" => Dashboard,
            "WorkRecord" => WorkRecord,
            "Knowledge" => Knowledge,
            "Issue" => Issue,
            "Settings" => Settings,
            _ => Dashboard
        };

        // 离开工作记录时暂停自动保存，进入时恢复
        if (previousView == WorkRecord && CurrentView != WorkRecord)
            WorkRecord.PauseAutoSaveTimer();
        else if (CurrentView == WorkRecord && previousView != WorkRecord)
            WorkRecord.StartAutoSaveTimer();

        // 仅在视图实际切换时才刷新，避免重复加载
        if (CurrentView != previousView && CurrentView is IRefreshable refreshable)
            refreshable.RefreshAsync().SafeFire("刷新失败");
    }

    [RelayCommand]
    private void OpenSearch()
    {
        IsSearchOpen = true;
        SearchKeyword = string.Empty;
        SearchResults.Clear();
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
                    NavigateTo = "WorkRecord",
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
                    NavigateTo = "Knowledge",
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
                    NavigateTo = "Issue",
                    TargetId = i.Id,
                    Keyword = keyword
                });
            }
        }
        catch (Exception ex)
        {
            ToastService.Error($"搜索出错：{ex.Message}");
        }

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
        if (item.NavigateTo == "WorkRecord")
        {
            if (item.TargetDate.HasValue)
                WorkRecord.SelectedDate = item.TargetDate.Value;
            WorkRecord.SearchKeyword = item.Keyword;  // 先设置关键词
            WorkRecord.SelectedTabIndex = 1;           // 再切 Tab，触发加载时已带关键词
        }

        NavigateTo(item.NavigateTo);

        // 知识库/问题跟踪：导航后用搜索关键词过滤，定位到目标条目
        if (item.NavigateTo == "Knowledge")
        {
            Knowledge.SearchKeyword = item.Keyword;
            Knowledge.SearchCommand.ExecuteAsync(null).SafeFire("搜索失败");
        }
        else if (item.NavigateTo == "Issue")
        {
            Issue.SearchKeyword = item.Keyword;
            Issue.SearchCommand.ExecuteAsync(null).SafeFire("搜索失败");
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
        var views = new[] { "Dashboard", "WorkRecord", "Knowledge", "Issue", "Settings" };
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
