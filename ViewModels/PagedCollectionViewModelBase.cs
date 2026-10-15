using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    internal bool _suppressDirty;  // internal: View code-behind 需要访问以安全重置表单（屏蔽绑定事件触发的脏标记）

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
    [ObservableProperty] private ObservableCollection<int> _visiblePageNumbers = new();
    public int[] PageSizeOptions => [10, 20, 50, 100];

    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isFormDirty;

    /// <summary>搜索了关键词但无匹配结果（用于展示"未找到与 XXX 相关"的区分空态）。</summary>
    [ObservableProperty] private bool _isSearchEmpty;

    // ===== 多选批量操作 =====
    /// <summary>多选模式下选中的条目（由 View 的 DataGrid.SelectedItems 同步过来）</summary>
    [ObservableProperty] private ObservableCollection<T> _selectedItems = new();

    /// <summary>是否处于多选模式（驱动 DataGrid SelectionMode 与批量操作栏显隐）</summary>
    [ObservableProperty] private bool _isMultiSelectMode;

    /// <summary>当前是否有选中项（驱动批量按钮可用性）</summary>
    [ObservableProperty] private bool _hasSelection;

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

        // 选择集变化 → 同步「是否有选中项」，驱动批量按钮可用性
        SelectedItems.CollectionChanged += (_, _) => HasSelection = SelectedItems.Count > 0;
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

    /// <summary>软删除单条（移入回收站），供批量删除复用。子类接各自的仓储。</summary>
    protected abstract Task DeleteItemAsync(T item);

    /// <summary>恢复单条软删除记录（撤销批量删除），供子类接各自仓储。</summary>
    protected abstract Task RestoreItemAsync(T item);

    [RelayCommand]
    private void ClosePanel() => RequestPanelClose();

    /// <summary>
    /// 请求关闭抽屉面板（供子类在保存成功等场景触发，与 View 的 PanelCloseRequested 订阅连通）。
    /// 事件只能在声明类内部触发，故暴露此受保护方法给子类调用。
    /// </summary>
    protected void RequestPanelClose() => PanelCloseRequested?.Invoke();

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
        UpdateVisiblePageNumbers();

        // 搜索态且无结果 → 区分空态；无关键词时归为通用"暂无数据"空态。
        IsSearchEmpty = !string.IsNullOrWhiteSpace(kw) && total == 0;

        // 列表内容已刷新（翻页/搜索/筛选/删除），旧的选中项不再指向当前可见数据 → 清空选择
        if (SelectedItems.Count > 0) SelectedItems.Clear();
    }

    private void UpdateVisiblePageNumbers()
    {
        var pages = new ObservableCollection<int>();
        var total = TotalPages;
        var current = CurrentPage;

        if (total <= 7)
        {
            for (int i = 1; i <= total; i++) pages.Add(i);
        }
        else
        {
            pages.Add(1);
            int start = Math.Max(2, current - 2);
            int end = Math.Min(total - 1, current + 2);

            if (start > 2) pages.Add(0); // 0 = 省略号
            for (int i = start; i <= end; i++) pages.Add(i);
            if (end < total - 1) pages.Add(0);
            pages.Add(total);
        }

        VisiblePageNumbers = pages;
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

    [RelayCommand]
    private void GoToPage(object? param)
    {
        if (param is int page && page > 0 && page <= TotalPages && page != CurrentPage)
            CurrentPage = page;
    }

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

    // ===== 多选批量操作 =====

    /// <summary>切换多选模式；退出多选时清空已有选择。</summary>
    [RelayCommand]
    private void ToggleMultiSelect()
    {
        IsMultiSelectMode = !IsMultiSelectMode;
        if (!IsMultiSelectMode)
            SelectedItems.Clear();
    }

    /// <summary>
    /// 批量软删除当前选中条目（移入回收站，可通过 Toast「撤销」或回收站恢复）。
    /// 确认对话框 + Toast 属 UI 编排；核心删除逻辑抽到 <see cref="DeleteItemsAsync"/>（可单测）。
    /// </summary>
    [RelayCommand]
    private async Task BatchDeleteAsync()
    {
        var count = SelectedItems.Count;
        if (count == 0) return;
        if (!DialogService.Instance.ShowConfirm(
                $"确定要删除选中的 {count} 条内容吗？\n删除后可在「回收站」中恢复。",
                "批量删除", ConfirmType.Danger))
            return;

        try
        {
            var deleted = SelectedItems.ToList();
            await DeleteItemsAsync(deleted);
            ToastService.WithAction($"已删除 {count} 条", "已删除", ToastType.Success, "撤销",
                () => _ = RestoreItemsAsync(deleted));
        }
        catch (Exception ex)
        {
            ToastService.Error($"批量删除失败：{ex.Message}");
        }
    }

    /// <summary>批量软删除并刷新列表（不含确认对话框，供命令与单元测试调用）。</summary>
    internal async Task DeleteItemsAsync(IReadOnlyList<T> items)
    {
        if (items.Count == 0) return;
        foreach (var item in items)
            await DeleteItemAsync(item);
        SelectedItems.Clear();
        await LoadAsync();
    }

    /// <summary>批量恢复（撤销批量删除）。</summary>
    internal async Task RestoreItemsAsync(IReadOnlyList<T> items)
    {
        if (items.Count == 0) return;
        foreach (var item in items)
            await RestoreItemAsync(item);
        await LoadAsync();
    }

    public void MarkDirty()
    {
        if (!_suppressDirty)
            IsFormDirty = true;
    }
}
