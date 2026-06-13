using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using System.Collections.ObjectModel;

namespace EapWorkAssistant.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly WorkRecordRepository _recordRepo = new();
    private readonly KnowledgeRepository _knowledgeRepo = new();
    private readonly IssueRepository _issueRepo = new();

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
        CurrentView = Dashboard;
        _ = Dashboard.LoadDashboardAsync();
    }

    [RelayCommand]
    private void NavigateTo(string viewName)
    {
        CurrentView = viewName switch
        {
            "Dashboard" => Dashboard,
            "WorkRecord" => WorkRecord,
            "Knowledge" => Knowledge,
            "Issue" => Issue,
            "Settings" => Settings,
            _ => Dashboard
        };

        if (CurrentView is IRefreshable refreshable)
            _ = refreshable.RefreshAsync();
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
        IsSearchOpen = false;
        SearchKeyword = string.Empty;
        SearchResults.Clear();
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
        var keyword = SearchKeyword.Trim().ToLower();

        try
        {
            // 搜索工作记录
            var records = await _recordRepo.GetAllAsync();
            foreach (var r in records.Where(r =>
                (r.Content?.ToLower().Contains(keyword) == true) ||
                (r.ProjectName?.ToLower().Contains(keyword) == true) ||
                (r.Problem?.ToLower().Contains(keyword) == true) ||
                (r.Solution?.ToLower().Contains(keyword) == true)))
            {
                var content = r.Content ?? "";
                results.Add(new SearchResultItem
                {
                    Type = "工作记录",
                    Title = $"{r.ProjectName} - {r.WorkDate}",
                    Content = content.Length > 60 ? content[..60] + "..." : content,
                    Icon = "\U0001F4DD",
                    NavigateTo = "WorkRecord"
                });
            }

            // 搜索知识库
            var knowledge = await _knowledgeRepo.GetAllAsync();
            foreach (var k in knowledge.Where(k =>
                (k.Title?.ToLower().Contains(keyword) == true) ||
                (k.Content?.ToLower().Contains(keyword) == true) ||
                (k.Tags?.ToLower().Contains(keyword) == true)))
            {
                var content = k.Content ?? "";
                results.Add(new SearchResultItem
                {
                    Type = "知识库",
                    Title = k.Title,
                    Content = content.Length > 60 ? content[..60] + "..." : content,
                    Icon = "\U0001F4DA",
                    NavigateTo = "Knowledge"
                });
            }

            // 搜索问题跟踪
            var issues = await _issueRepo.GetAllAsync();
            foreach (var i in issues.Where(i =>
                (i.Title?.ToLower().Contains(keyword) == true) ||
                (i.Description?.ToLower().Contains(keyword) == true) ||
                (i.Keywords?.ToLower().Contains(keyword) == true) ||
                (i.RootCause?.ToLower().Contains(keyword) == true) ||
                (i.Solution?.ToLower().Contains(keyword) == true)))
            {
                var content = i.Description ?? "";
                results.Add(new SearchResultItem
                {
                    Type = "问题跟踪",
                    Title = $"[{i.ProjectName}] {i.Title}",
                    Content = content.Length > 60 ? content[..60] + "..." : content,
                    Icon = "\U0001F527",
                    NavigateTo = "Issue"
                });
            }
        }
        catch
        {
            // 搜索出错时显示无结果
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
        IsSearchOpen = false;
        SearchKeyword = string.Empty;
        NavigateTo(item.NavigateTo);
    }

    partial void OnSearchKeywordChanged(string value)
    {
        // 当搜索词清空时，重置状态
        if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
            ShowInitial = true;
            ShowNoResults = false;
            ShowResults = false;
            IsSearching = false;
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
}
