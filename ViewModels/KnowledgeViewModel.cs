using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace EapWorkAssistant.ViewModels;

public partial class KnowledgeViewModel : ObservableObject, IRefreshable
{
    private readonly KnowledgeRepository _repo = new();
    private readonly DispatcherTimer _statusTimer;

    [ObservableProperty]
    private ObservableCollection<Knowledge> _items = new();

    [ObservableProperty]
    private Knowledge _currentItem = new();

    [ObservableProperty]
    private Knowledge? _selectedItem;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public KnowledgeViewModel()
    {
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _statusTimer.Tick += (_, _) => { StatusMessage = string.Empty; _statusTimer.Stop(); };
    }

    public async Task RefreshAsync() => await LoadAsync();

    partial void OnSelectedItemChanged(Knowledge? value)
    {
        if (value != null)
        {
            CurrentItem = new Knowledge
            {
                Id = value.Id,
                Title = value.Title,
                Content = value.Content,
                Tags = value.Tags
            };
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var items = await _repo.GetAllAsync();
        Items = new ObservableCollection<Knowledge>(items);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        var items = string.IsNullOrWhiteSpace(SearchKeyword)
            ? await _repo.GetAllAsync()
            : await _repo.SearchAsync(SearchKeyword);
        Items = new ObservableCollection<Knowledge>(items);
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

        CurrentItem = new Knowledge();
        SelectedItem = null;
        await LoadAsync();
        StatusMessage = "保存成功";
        _statusTimer.Start();
    }

    [RelayCommand]
    private async Task DeleteAsync(Knowledge? item)
    {
        if (item == null) return;

        if (!ConfirmDialog.Show($"确定要删除 \"{item.Title}\" 吗？", "确认删除", ConfirmDialogType.Danger)) return;

        await _repo.DeleteAsync(item.Id);
        if (SelectedItem?.Id == item.Id)
        {
            SelectedItem = null;
            CurrentItem = new Knowledge();
        }
        await LoadAsync();
        StatusMessage = "删除成功";
        _statusTimer.Start();
    }

    [RelayCommand]
    private void Edit(Knowledge? item)
    {
        if (item == null) return;
        CurrentItem = new Knowledge
        {
            Id = item.Id,
            Title = item.Title,
            Content = item.Content,
            Tags = item.Tags
        };
    }

    [RelayCommand]
    private void New()
    {
        CurrentItem = new Knowledge();
        SelectedItem = null;
    }
}
