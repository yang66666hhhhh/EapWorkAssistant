using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using EapWorkAssistant.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace EapWorkAssistant.ViewModels;

public partial class KnowledgeViewModel : ObservableObject, IRefreshable
{
    private readonly KnowledgeRepository _repo = new();
    private readonly DispatcherTimer _statusTimer;
    private readonly DispatcherTimer _searchTimer;
    private bool _suppressDirty;

    public event Action? PanelCloseRequested;

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

    [ObservableProperty]
    private bool _isFormDirty;

    // 分类和标签建议
    [ObservableProperty]
    private ObservableCollection<string> _allCategories = new();

    [ObservableProperty]
    private ObservableCollection<string> _allTags = new();

    [ObservableProperty]
    private string _filterCategory = "";

    [ObservableProperty]
    private bool _showFavoritesOnly;

    public string[] FilterCategories => ["", .. AllCategories];

    public KnowledgeViewModel()
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
            LoadAsync().SafeFire("加载知识失败");
        else
            _searchTimer.Start();
    }

    partial void OnFilterCategoryChanged(string value)
    {
        LoadAsync().SafeFire("加载知识失败");
    }

    partial void OnShowFavoritesOnlyChanged(bool value)
    {
        LoadAsync().SafeFire("加载知识失败");
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

    partial void OnSelectedItemChanged(Knowledge? value)
    {
        if (value != null)
        {
            CurrentItem = new Knowledge
            {
                Id = value.Id,
                Title = value.Title,
                Content = value.Content,
                Tags = value.Tags,
                Category = value.Category,
                IsFavorite = value.IsFavorite
            };
            IsFormDirty = false;
        }
    }

    partial void OnCurrentItemChanged(Knowledge value)
    {
        IsFormDirty = false;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IEnumerable<Knowledge> items;
        if (ShowFavoritesOnly)
            items = await _repo.GetFavoritesAsync();
        else
            items = await _repo.GetAllAsync();

        // 分类筛选
        if (!string.IsNullOrEmpty(FilterCategory))
            items = items.Where(k => k.Category == FilterCategory);

        Items = new ObservableCollection<Knowledge>(items);
        await RefreshTagsAndCategoriesAsync();
    }

    private async Task RefreshTagsAndCategoriesAsync()
    {
        try
        {
            var tags = await _repo.GetAllTagsAsync();
            AllTags = new ObservableCollection<string>(tags);

            // 仅使用设置页面维护的分类列表
            AllCategories = new ObservableCollection<string>(
                ConfigService.Instance.KnowledgeCategories.OrderBy(c => c));
            OnPropertyChanged(nameof(FilterCategories));
        }
        catch { }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        var items = string.IsNullOrWhiteSpace(SearchKeyword)
            ? await _repo.GetAllAsync()
            : await _repo.SearchAsync(SearchKeyword);

        if (!string.IsNullOrEmpty(FilterCategory))
            items = items.Where(k => k.Category == FilterCategory);

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

        if (string.IsNullOrWhiteSpace(CurrentItem.Content))
        {
            StatusMessage = "请输入内容";
            _statusTimer.Start();
            return;
        }

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
        CurrentItem = new Knowledge();
        SelectedItem = null;
        IsFormDirty = false;
        _suppressDirty = false;
        await LoadAsync();
        StatusMessage = string.Empty;
        ToastService.Success("知识已保存");
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(Knowledge? item)
    {
        if (item == null) return;
        item.IsFavorite = item.IsFavorite == 1 ? 0 : 1;
        await _repo.UpdateAsync(item);
        await LoadAsync();
        ToastService.Success(item.IsFavorite == 1 ? "已收藏" : "已取消收藏");
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
        ToastService.Success("已删除");
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
            Tags = item.Tags,
            Category = item.Category,
            IsFavorite = item.IsFavorite
        };
    }

    [RelayCommand]
    private void New()
    {
        _suppressDirty = true;
        CurrentItem = new Knowledge();
        SelectedItem = null;
        IsFormDirty = false;
        _suppressDirty = false;
    }

    public void MarkDirty()
    {
        if (!_suppressDirty)
            IsFormDirty = true;
    }
}
