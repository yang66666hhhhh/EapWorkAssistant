using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;

namespace EapWorkAssistant.ViewModels;

public partial class IssueViewModel : PagedCollectionViewModelBase<Issue>
{
    private readonly IssueRepository _repo = new();

    [ObservableProperty]
    private Issue _currentItem = new();

    [ObservableProperty]
    private Issue? _selectedItem;

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

    protected override string LoadFailureMessage => "加载问题失败";

    protected override async Task<IEnumerable<Issue>> GetAllAsync()
        => await _repo.GetAllAsync();

    /// <summary>DB 级分页：关键词 + 状态/优先级筛选全部下推到 SQL。</summary>
    protected override async Task<(List<Issue> PageItems, int Total)> LoadPageAsync(int page, int pageSize, string keyword)
    {
        var skip = (page - 1) * pageSize;
        var result = await _repo.GetFilteredPagedAsync(keyword, FilterStatus, FilterPriority, skip, pageSize);
        return (result.Items.ToList(), result.Total);
    }

    protected override IEnumerable<Issue> ApplyExtraFilters(IEnumerable<Issue> source)
    {
        var q = source;
        if (!string.IsNullOrEmpty(FilterStatus))
            q = q.Where(i => i.Status == FilterStatus);
        if (!string.IsNullOrEmpty(FilterPriority))
            q = q.Where(i => i.Priority == FilterPriority);
        return q;
    }

    protected override bool MatchesSearch(Issue item, string kw)
    {
        return (item.Title != null && item.Title.Contains(kw, System.StringComparison.OrdinalIgnoreCase)) ||
               (item.Description != null && item.Description.Contains(kw, System.StringComparison.OrdinalIgnoreCase)) ||
               (item.Keywords != null && item.Keywords.Contains(kw, System.StringComparison.OrdinalIgnoreCase)) ||
               (item.RootCause != null && item.RootCause.Contains(kw, System.StringComparison.OrdinalIgnoreCase)) ||
               (item.Solution != null && item.Solution.Contains(kw, System.StringComparison.OrdinalIgnoreCase));
    }

    partial void OnFilterStatusChanged(string value) => ReloadPageAsync(true).SafeFire(LoadFailureMessage);
    partial void OnFilterPriorityChanged(string value) => ReloadPageAsync(true).SafeFire(LoadFailureMessage);

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

        var deleted = item; // 保留引用用于撤销
        await _repo.DeleteAsync(deleted.Id);
        await LoadAsync();
        ToastService.WithAction("已删除问题跟踪条目", "已删除", ToastType.Success, "撤销",
            () => _ = RestoreAsync(deleted));
    }

    private async Task RestoreAsync(Issue deleted)
    {
        try
        {
            await _repo.RestoreAsync(deleted.Id);
            await LoadAsync();
            ToastService.Success("已恢复");
        }
        catch (Exception ex)
        {
            ToastService.Error($"恢复失败：{ex.Message}");
        }
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

    // ===== JSON 导出 / 导入 =====
    protected override string EmptyExportMessage => "没有可导出的问题";
    protected override string ExportSuccessMessage => "问题库已导出为 JSON";
    protected override bool ExportItems(List<Issue> items)
        => ExportService.ExportIssuesToJson(items);

    protected override ExportService.ImportResult<Issue> GetImportResult()
        => ExportService.ImportIssuesFromJson();

    protected override void SetImportDefaults(Issue item)
    {
        item.Id = 0;
        if (string.IsNullOrWhiteSpace(item.CreateTime))
            item.CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    protected override async Task InsertAsync(Issue item)
        => await _repo.InsertAsync(item);

    protected override string EmptyImportMessage => "文件为空，没有可导入的问题";
    protected override string ImportSuccessMessage => "已导入 {0} 条问题";
}
