using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace EapWorkAssistant.ViewModels;

public partial class IssueViewModel : ObservableObject, IRefreshable
{
    private readonly IssueRepository _repo = new();
    private readonly DispatcherTimer _statusTimer;

    [ObservableProperty]
    private ObservableCollection<Issue> _items = new();

    [ObservableProperty]
    private Issue _currentItem = new();

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public string[] Projects => ProjectInfo.Projects;

    public IssueViewModel()
    {
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _statusTimer.Tick += (_, _) => { StatusMessage = string.Empty; _statusTimer.Stop(); };
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

        if (CurrentItem.Id > 0)
            await _repo.UpdateAsync(CurrentItem);
        else
        {
            CurrentItem.CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            await _repo.InsertAsync(CurrentItem);
        }

        CurrentItem = new Issue();
        await LoadAsync();
        StatusMessage = "保存成功";
        _statusTimer.Start();
    }

    [RelayCommand]
    private async Task DeleteAsync(Issue? item)
    {
        if (item == null) return;

        if (!ConfirmDialog.Show($"确定要删除 \"{item.Title}\" 吗？", "确认删除", ConfirmDialogType.Danger)) return;

        await _repo.DeleteAsync(item.Id);
        await LoadAsync();
        StatusMessage = "删除成功";
        _statusTimer.Start();
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
            Keywords = item.Keywords
        };
    }

    [RelayCommand]
    private void New()
    {
        CurrentItem = new Issue();
    }
}
