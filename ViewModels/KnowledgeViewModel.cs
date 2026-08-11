using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EapWorkAssistant.ViewModels;

public partial class KnowledgeViewModel : ObservableObject, IRefreshable
{
    private readonly KnowledgeRepository _repo = new();
    private readonly UiTimer _statusTimer;
    private readonly UiTimer _searchTimer;
    private bool _suppressDirty;
    private List<Knowledge> _allItems = new();

    public event Action? PanelCloseRequested;

    [ObservableProperty]
    private ObservableCollection<Knowledge> _items = new();

    [ObservableProperty]
    private ObservableCollection<Knowledge> _pagedItems = new();

    // 分页
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _pageSize = 20;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _totalCount;
    public int[] PageSizeOptions => [10, 20, 50, 100];

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
            LoadAsync().SafeFire("加载知识失败");
        else
            _searchTimer.Start();
    }

    partial void OnFilterCategoryChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnShowFavoritesOnlyChanged(bool value)
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
        // 一次性加载全量，后续搜索/筛选均在内存中进行，避免重复查询数据库
        _allItems = (await _repo.GetAllAsync()).ToList();
        ApplyFilter();
        await RefreshTagsAndCategoriesAsync();
    }

    private void ApplyFilter()
    {
        CurrentPage = 1;
        var q = _allItems.AsEnumerable();
        if (ShowFavoritesOnly)
            q = q.Where(k => k.IsFavorite == 1);
        if (!string.IsNullOrEmpty(FilterCategory))
            q = q.Where(k => k.Category == FilterCategory);
        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            var kw = SearchKeyword.Trim();
            q = q.Where(k =>
                (k.Title != null && k.Title.Contains(kw, System.StringComparison.OrdinalIgnoreCase)) ||
                (k.Content != null && k.Content.Contains(kw, System.StringComparison.OrdinalIgnoreCase)) ||
                (k.Tags != null && k.Tags.Contains(kw, System.StringComparison.OrdinalIgnoreCase)));
        }
        Items = new ObservableCollection<Knowledge>(q);
        UpdatePager();
    }

    private void UpdatePager()
    {
        TotalCount = Items.Count;
        TotalPages = TotalCount > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;
        if (CurrentPage < 1) CurrentPage = 1;
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;
        var pageItems = Items.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        PagedItems = new ObservableCollection<Knowledge>(pageItems);
    }

    partial void OnCurrentPageChanged(int value) => UpdatePager();
    partial void OnPageSizeChanged(int value) => UpdatePager();

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

        if (string.IsNullOrWhiteSpace(CurrentItem.Content))
        {
            StatusMessage = "请输入内容";
            _statusTimer.Start();
            return;
        }

        // 去除首尾空白
        CurrentItem.Title = CurrentItem.Title.Trim();
        CurrentItem.Content = CurrentItem.Content.Trim();
        if (CurrentItem.Tags != null)
            CurrentItem.Tags = CurrentItem.Tags.Trim();

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

        if (!DialogService.Instance.ShowConfirm($"确定要删除 \"{item.Title}\" 吗？", "确认删除", ConfirmType.Danger)) return;

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

    // ===== 分页命令 =====
    [RelayCommand]
    private void FirstPage()
    {
        if (CurrentPage != 1) { CurrentPage = 1; UpdatePager(); }
    }

    [RelayCommand]
    private void PrevPage()
    {
        if (CurrentPage > 1) CurrentPage--;
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages) CurrentPage++;
    }

    [RelayCommand]
    private void LastPage()
    {
        if (CurrentPage != TotalPages) { CurrentPage = TotalPages; UpdatePager(); }
    }

    // ===== JSON 导出 / 导入 =====
    [RelayCommand]
    private void ExportJson()
    {
        if (_allItems.Count == 0) { ToastService.Warning("没有可导出的知识"); return; }
        if (ExportService.ExportKnowledgeToJson(_allItems))
            ToastService.Success("知识库已导出为 JSON");
    }

    [RelayCommand]
    private async Task ImportJsonAsync()
    {
        var result = ExportService.ImportKnowledgeFromJson();
        if (result.Canceled) return;
        if (result.Error != null)
        {
            ToastService.Error($"文件解析失败：{result.Error}");
            return;
        }
        var list = result.Items!;
        if (list.Count == 0) { ToastService.Warning("文件为空，没有可导入的知识"); return; }
        try
        {
            foreach (var k in list)
            {
                k.Id = 0;
                if (string.IsNullOrWhiteSpace(k.CreateTime))
                    k.CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                await _repo.InsertAsync(k);
            }
            await LoadAsync();
            ToastService.Success($"已导入 {list.Count} 条知识");
        }
        catch (Exception ex)
        {
            ToastService.Error($"导入失败：{ex.Message}");
        }
    }

    public void MarkDirty()
    {
        if (!_suppressDirty)
            IsFormDirty = true;
    }
}
