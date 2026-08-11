using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EapWorkAssistant.ViewModels;

public partial class IssueViewModel : ObservableObject, IRefreshable
{
    private readonly IssueRepository _repo = new();
    private readonly UiTimer _statusTimer;
    private readonly UiTimer _searchTimer;
    private bool _suppressDirty;
    private List<Issue> _allItems = new();

    public event Action? PanelCloseRequested;

    [ObservableProperty]
    private ObservableCollection<Issue> _items = new();

    [ObservableProperty]
    private Issue _currentItem = new();

    [ObservableProperty]
    private Issue? _selectedItem;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isFormDirty;

    public string[] Projects => ProjectInfo.Projects;
    public string[] Statuses => ["Open", "InProgress", "Resolved", "Closed"];
    public string[] StatusLabels => ["待处理", "进行中", "已解决", "已关闭"];
    public string[] Priorities => ["Low", "Medium", "High", "Critical"];
    public string[] PriorityLabels => ["低", "中", "高", "紧急"];

    // 筛选
    [ObservableProperty]
    private string _filterStatus = "";

    [ObservableProperty]
    private string _filterPriority = "";

    public string[] FilterStatuses => ["", .. Statuses];
    public string[] FilterPriorities => ["", .. Priorities];

    public IssueViewModel()
    {
        _statusTimer = new UiTimer { Interval = TimeSpan.FromSeconds(5) };
        _statusTimer.Tick += (_, _) => { StatusMessage = string.Empty; _statusTimer.Stop(); };

        _searchTimer = new UiTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            SearchAsync();
        };
    }

    partial void OnSearchKeywordChanged(string value)
    {
        _searchTimer.Stop();
        if (string.IsNullOrWhiteSpace(value))
            LoadAsync().SafeFire("加载问题失败");
        else
            _searchTimer.Start();
    }

    partial void OnFilterStatusChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnFilterPriorityChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    private void ClosePanel()
    {
        PanelCloseRequested?.Invoke();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchKeyword = "";
    }

    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        // 一次性加载全量，后续搜索/筛选均在内存中进行，避免重复查询数据库
        _allItems = (await _repo.GetAllAsync()).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = _allItems.AsEnumerable();
        if (!string.IsNullOrEmpty(FilterStatus))
            q = q.Where(i => i.Status == FilterStatus);
        if (!string.IsNullOrEmpty(FilterPriority))
            q = q.Where(i => i.Priority == FilterPriority);
        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            var kw = SearchKeyword.Trim();
            q = q.Where(i =>
                (i.Title != null && i.Title.Contains(kw, System.StringComparison.OrdinalIgnoreCase)) ||
                (i.Description != null && i.Description.Contains(kw, System.StringComparison.OrdinalIgnoreCase)) ||
                (i.Keywords != null && i.Keywords.Contains(kw, System.StringComparison.OrdinalIgnoreCase)) ||
                (i.RootCause != null && i.RootCause.Contains(kw, System.StringComparison.OrdinalIgnoreCase)) ||
                (i.Solution != null && i.Solution.Contains(kw, System.StringComparison.OrdinalIgnoreCase)));
        }
        Items = new ObservableCollection<Issue>(q);
    }

    [RelayCommand]
    private void SearchAsync()
    {
        ApplyFilter();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentItem.Title))
        {
            StatusMessage = "请输入标题";
            _statusTimer.Start();
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentItem.ProjectName))
        {
            StatusMessage = "请选择任务";
            _statusTimer.Start();
            return;
        }

        // 状态枚举校验
        if (!Statuses.Contains(CurrentItem.Status))
        {
            StatusMessage = "请选择有效的状态";
            _statusTimer.Start();
            return;
        }

        // 优先级枚举校验
        if (!Priorities.Contains(CurrentItem.Priority))
        {
            StatusMessage = "请选择有效的优先级";
            _statusTimer.Start();
            return;
        }

        // 去除首尾空白
        CurrentItem.Title = CurrentItem.Title.Trim();
        if (CurrentItem.Description != null)
            CurrentItem.Description = CurrentItem.Description.Trim();

        StatusMessage = "正在保存...";

        try
        {
            if (CurrentItem.Id > 0)
                await _repo.UpdateAsync(CurrentItem);
            else
            {
                CurrentItem.CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                await _repo.InsertAsync(CurrentItem);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = string.Empty;
            ToastService.Error($"保存失败：{ex.Message}");
            return;
        }

        _suppressDirty = true;
        CurrentItem = new Issue();
        IsFormDirty = false;
        _suppressDirty = false;
        await LoadAsync();
        StatusMessage = string.Empty;
        ToastService.Success("问题已保存");
    }

    [RelayCommand]
    private async Task DeleteAsync(Issue? item)
    {
        if (item == null) return;

        if (!DialogService.Instance.ShowConfirm($"确定要删除 \"{item.Title}\" 吗？", "确认删除", ConfirmType.Danger)) return;

        await _repo.DeleteAsync(item.Id);
        await LoadAsync();
        ToastService.Success("已删除");
    }

    [RelayCommand]
    private void Edit(Issue? item)
    {
        if (item == null) return;
        CurrentItem = new Issue
        {
            Id = item.Id,
            ProjectName = item.ProjectName,
            Title = item.Title,
            Description = item.Description,
            RootCause = item.RootCause,
            Solution = item.Solution,
            Keywords = item.Keywords,
            Status = item.Status,
            Priority = item.Priority
        };
        IsFormDirty = false;
    }

    [RelayCommand]
    private void New()
    {
        _suppressDirty = true;
        CurrentItem = new Issue();
        IsFormDirty = false;
        _suppressDirty = false;
    }

    public void MarkDirty()
    {
        if (!_suppressDirty)
            IsFormDirty = true;
    }
}
