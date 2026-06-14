using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using EapWorkAssistant.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace EapWorkAssistant.ViewModels;

public partial class IssueViewModel : ObservableObject, IRefreshable
{
    private readonly IssueRepository _repo = new();
    private readonly DispatcherTimer _statusTimer;
    private readonly DispatcherTimer _searchTimer;
    private bool _suppressDirty;

    public event Action? PanelCloseRequested;

    [ObservableProperty]
    private ObservableCollection<Issue> _items = new();

    [ObservableProperty]
    private Issue _currentItem = new();

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

    public IssueViewModel()
    {
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _statusTimer.Tick += (_, _) => { StatusMessage = string.Empty; _statusTimer.Stop(); };

        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await SearchAsync();
        };
    }

    partial void OnSearchKeywordChanged(string value)
    {
        _searchTimer.Stop();
        if (string.IsNullOrWhiteSpace(value))
            _ = LoadAsync();
        else
            _searchTimer.Start();
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
        var items = await _repo.GetAllAsync();
        Items = new ObservableCollection<Issue>(items);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        var items = string.IsNullOrWhiteSpace(SearchKeyword)
            ? await _repo.GetAllAsync()
            : await _repo.SearchAsync(SearchKeyword);
        Items = new ObservableCollection<Issue>(items);
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
            ToastService.Error($"保存失败：{ex.Message}");
            return;
        }

        _suppressDirty = true;
        CurrentItem = new Issue();
        IsFormDirty = false;
        _suppressDirty = false;
        await LoadAsync();
        ToastService.Success("问题已保存");
    }

    [RelayCommand]
    private async Task DeleteAsync(Issue? item)
    {
        if (item == null) return;

        if (!ConfirmDialog.Show($"确定要删除 \"{item.Title}\" 吗？", "确认删除", ConfirmDialogType.Danger)) return;

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
