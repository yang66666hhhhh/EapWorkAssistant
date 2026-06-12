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
    private bool _isSearchPopupOpen;

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
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            IsSearchPopupOpen = false;
            SearchResults.Clear();
            return;
        }

        var results = new List<SearchResultItem>();
        var keyword = SearchKeyword.ToLower();

        // 搜索工作记录
        var records = await _recordRepo.GetAllAsync();
        foreach (var r in records.Where(r =>
            (r.Content?.ToLower().Contains(keyword) == true) ||
            (r.ProjectName?.ToLower().Contains(keyword) == true) ||
            (r.Problem?.ToLower().Contains(keyword) == true)))
        {
            results.Add(new SearchResultItem
            {
                Type = "工作记录",
                Title = $"{r.ProjectName} - {r.WorkDate}",
                Content = r.Content?.Length > 50 ? r.Content[..50] + "..." : r.Content ?? "",
                Icon = "&#x1F4DD;",
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
            results.Add(new SearchResultItem
            {
                Type = "知识库",
                Title = k.Title,
                Content = k.Content?.Length > 50 ? k.Content[..50] + "..." : k.Content ?? "",
                Icon = "&#x1F4DA;",
                NavigateTo = "Knowledge"
            });
        }

        // 搜索问题跟踪
        var issues = await _issueRepo.GetAllAsync();
        foreach (var i in issues.Where(i =>
            (i.Title?.ToLower().Contains(keyword) == true) ||
            (i.Description?.ToLower().Contains(keyword) == true) ||
            (i.Keywords?.ToLower().Contains(keyword) == true)))
        {
            results.Add(new SearchResultItem
            {
                Type = "问题跟踪",
                Title = $"[{i.ProjectName}] {i.Title}",
                Content = i.Description?.Length > 50 ? i.Description[..50] + "..." : i.Description ?? "",
                Icon = "&#x1F527;",
                NavigateTo = "Issue"
            });
        }

        SearchResults = new ObservableCollection<SearchResultItem>(results.Take(20));
        IsSearchPopupOpen = SearchResults.Any();
    }

    [RelayCommand]
    private void NavigateToResult(SearchResultItem? item)
    {
        if (item == null) return;
        IsSearchPopupOpen = false;
        SearchKeyword = string.Empty;
        NavigateTo(item.NavigateTo);
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
