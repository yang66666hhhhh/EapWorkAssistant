using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EapWorkAssistant.ViewModels;

/// <summary>
/// 分页集合 ViewModel 基类：集中实现「全量加载 + 内存筛选/搜索 + 分页 + JSON 备份」的通用框架。
/// 子类只需提供仓储访问、领域筛选/搜索谓词与导入导出实现，公共属性与命令名称保持不变（UI 零改动）。
/// </summary>
public abstract partial class PagedCollectionViewModelBase<T> : ObservableObject, IRefreshable
    where T : class
{
    protected readonly UiTimer _statusTimer;
    protected readonly UiTimer _searchTimer;
    protected bool _suppressDirty;
    protected List<T> _allItems = new();

    public event Action? PanelCloseRequested;

    [ObservableProperty]
    private ObservableCollection<T> _items = new();

    [ObservableProperty]
    private ObservableCollection<T> _pagedItems = new();

    // 分页
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _pageSize = 20;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _totalCount;
    public int[] PageSizeOptions => [10, 20, 50, 100];

    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isFormDirty;

    protected PagedCollectionViewModelBase()
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
            LoadAsync().SafeFire(LoadFailureMessage);
        else
            _searchTimer.Start();
    }

    // ===== 子类需提供的抽象/虚钩子 =====
    protected abstract string LoadFailureMessage { get; }
    protected abstract Task<IEnumerable<T>> GetAllAsync();
    protected virtual Task OnAfterLoadAsync() => Task.CompletedTask;
    protected abstract IEnumerable<T> ApplyExtraFilters(IEnumerable<T> source);
    protected abstract bool MatchesSearch(T item, string kw);
    protected abstract string EmptyExportMessage { get; }
    protected abstract string ExportSuccessMessage { get; }
    protected abstract bool ExportItems(List<T> items);
    protected abstract ExportService.ImportResult<T> GetImportResult();
    protected abstract void SetImportDefaults(T item);
    protected abstract Task InsertAsync(T item);
    protected abstract string EmptyImportMessage { get; }
    protected abstract string ImportSuccessMessage { get; }

    [RelayCommand]
    private void ClosePanel() => PanelCloseRequested?.Invoke();

    [RelayCommand]
    private void ClearSearch() => SearchKeyword = "";

    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    protected async Task LoadAsync()
    {
        // 一次性加载全量，后续搜索/筛选均在内存中进行，避免重复查询数据库
        _allItems = (await GetAllAsync()).ToList();
        ApplyFilter();
        await OnAfterLoadAsync();
    }

    protected void ApplyFilter()
    {
        CurrentPage = 1;
        var q = _allItems.AsEnumerable();
        q = ApplyExtraFilters(q);
        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            var kw = SearchKeyword.Trim();
            q = q.Where(item => MatchesSearch(item, kw));
        }
        Items = new ObservableCollection<T>(q);
        UpdatePager();
    }

    private void UpdatePager()
    {
        TotalCount = Items.Count;
        TotalPages = TotalCount > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;
        if (CurrentPage < 1) CurrentPage = 1;
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;
        var pageItems = Items.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        PagedItems = new ObservableCollection<T>(pageItems);
    }

    partial void OnCurrentPageChanged(int value) => UpdatePager();
    partial void OnPageSizeChanged(int value) => UpdatePager();

    [RelayCommand]
    private void SearchAsync() => ApplyFilter();

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
        if (_allItems.Count == 0) { ToastService.Warning(EmptyExportMessage); return; }
        if (ExportItems(_allItems))
            ToastService.Success(ExportSuccessMessage);
    }

    [RelayCommand]
    private async Task ImportJsonAsync()
    {
        var result = GetImportResult();
        if (result.Canceled) return;
        if (result.Error != null)
        {
            ToastService.Error($"文件解析失败：{result.Error}");
            return;
        }
        var list = result.Items!;
        if (list.Count == 0) { ToastService.Warning(EmptyImportMessage); return; }
        try
        {
            foreach (var item in list)
            {
                SetImportDefaults(item);
                await InsertAsync(item);
            }
            await LoadAsync();
            ToastService.Success(string.Format(ImportSuccessMessage, list.Count));
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
