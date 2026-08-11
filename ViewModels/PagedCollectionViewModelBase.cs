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
            ReloadPageAsync(true).SafeFire(LoadFailureMessage);
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
        await ReloadPageAsync();
        await OnAfterLoadAsync();
    }

    /// <summary>
    /// 从数据源加载单页数据（DB 级分页）。子类（Knowledge/Issue）重写以走数据库分页；
    /// 默认实现为内存分页（兼容其他潜在子类）。
    /// </summary>
    protected virtual async Task<(List<T> PageItems, int Total)> LoadPageAsync(int page, int pageSize, string keyword)
    {
        var all = (await GetAllAsync()).ToList();
        var q = ApplyExtraFilters(all.AsEnumerable());
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(item => MatchesSearch(item, keyword));
        var filtered = q.ToList();
        var pageItems = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (pageItems, filtered.Count);
    }

    /// <summary>导出用全量数据（非删除态）。默认走 GetAllAsync。</summary>
    protected virtual async Task<List<T>> GetAllForExportAsync()
        => (await GetAllAsync()).ToList();

    /// <summary>重新加载当前页（DB 级）。resetPage=true 表示搜索/筛选变化，回到第 1 页。</summary>
    protected async Task ReloadPageAsync(bool resetPage = false)
    {
        if (resetPage && CurrentPage != 1) CurrentPage = 1;
        var kw = SearchKeyword?.Trim() ?? "";
        var (pageItems, total) = await LoadPageAsync(CurrentPage, PageSize, kw);
        PagedItems = new ObservableCollection<T>(pageItems);
        Items = new ObservableCollection<T>(pageItems);
        TotalCount = total;
        TotalPages = total > 0 ? (int)Math.Ceiling(total / (double)PageSize) : 1;
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;
        if (CurrentPage < 1) CurrentPage = 1;
    }

    partial void OnCurrentPageChanged(int value) => ReloadPageAsync().SafeFire(LoadFailureMessage);
    partial void OnPageSizeChanged(int value) => ReloadPageAsync().SafeFire(LoadFailureMessage);

    [RelayCommand]
    private void SearchAsync() => ReloadPageAsync(true).SafeFire(LoadFailureMessage);

    // ===== 分页命令（仅改变 CurrentPage，由 OnCurrentPageChanged 触发重新加载） =====
    [RelayCommand]
    private void FirstPage() { if (CurrentPage != 1) CurrentPage = 1; }

    [RelayCommand]
    private void PrevPage() { if (CurrentPage > 1) CurrentPage--; }

    [RelayCommand]
    private void NextPage() { if (CurrentPage < TotalPages) CurrentPage++; }

    [RelayCommand]
    private void LastPage() { if (CurrentPage != TotalPages) CurrentPage = TotalPages; }

    // ===== JSON 导出 / 导入 =====
    [RelayCommand]
    private async Task ExportJsonAsync()
    {
        var all = await GetAllForExportAsync();
        if (all.Count == 0) { ToastService.Warning(EmptyExportMessage); return; }
        if (ExportItems(all))
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
