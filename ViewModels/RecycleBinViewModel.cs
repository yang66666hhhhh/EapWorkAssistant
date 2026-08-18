using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace EapWorkAssistant.ViewModels;

public partial class RecycleBinViewModel : ObservableObject, IRefreshable
{
    private readonly WorkRecordRepository _recordRepo = new();
    private readonly KnowledgeRepository _knowledgeRepo = new();
    private readonly IssueRepository _issueRepo = new();
    private readonly LeaveRecordRepository _leaveRepo = new();

    [ObservableProperty]
    private ObservableCollection<RecycleItem> _items = new();

    [ObservableProperty]
    private ObservableCollection<RecycleItem> _selectedItems = new();

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private bool _hasItems;

    /// <summary>类型筛选：全部 / 工作记录 / 知识库 / 问题跟踪 / 请假记录</summary>
    [ObservableProperty]
    private string _filterType = "全部";

    /// <summary>各类型计数</summary>
    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _workRecordCount;

    [ObservableProperty]
    private int _knowledgeCount;

    [ObservableProperty]
    private int _issueCount;

    [ObservableProperty]
    private int _leaveRecordCount;

    /// <summary>是否处于多选模式</summary>
    [ObservableProperty]
    private bool _isMultiSelectMode;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    private readonly UiTimer _statusTimer;

    public RecycleBinViewModel()
    {
        _statusTimer = new UiTimer { Interval = TimeSpan.FromSeconds(4) };
        _statusTimer.Tick += (_, _) => { StatusMessage = string.Empty; _statusTimer.Stop(); };

        SelectedItems.CollectionChanged += OnSelectedItemsChanged;
    }

    private void OnSelectedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasSelection = SelectedItems.Count > 0;
    }

    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var allItems = new List<RecycleItem>();
        try
        {
            var recordList = await _recordRepo.GetDeletedAsync();
            var knowledgeList = await _knowledgeRepo.GetDeletedAsync();
            var issueList = await _issueRepo.GetDeletedAsync();

            foreach (var r in recordList)
            {
                var c = r.Content ?? "";
                allItems.Add(new RecycleItem
                {
                    EntityType = "WorkRecord",
                    Id = r.Id,
                    TypeLabel = "工作记录",
                    Title = $"{r.ProjectName} - {r.WorkDate}",
                    Detail = c.Length > 80 ? c[..80] + "..." : c,
                    DeletedAt = r.DeletedAt
                });
            }

            foreach (var k in knowledgeList)
            {
                var c = k.Content ?? "";
                allItems.Add(new RecycleItem
                {
                    EntityType = "Knowledge",
                    Id = k.Id,
                    TypeLabel = "知识库",
                    Title = k.Title,
                    Detail = c.Length > 80 ? c[..80] + "..." : c,
                    DeletedAt = k.DeletedAt
                });
            }

            foreach (var i in issueList)
            {
                var c = i.Description ?? "";
                allItems.Add(new RecycleItem
                {
                    EntityType = "Issue",
                    Id = i.Id,
                    TypeLabel = "问题跟踪",
                    Title = $"[{i.ProjectName}] {i.Title}",
                    Detail = c.Length > 80 ? c[..80] + "..." : c,
                    DeletedAt = i.DeletedAt
                });
            }

            var leaveList = await _leaveRepo.GetDeletedAsync();
            foreach (var l in leaveList)
            {
                var c = l.Note ?? "";
                allItems.Add(new RecycleItem
                {
                    EntityType = "LeaveRecord",
                    Id = l.Id,
                    TypeLabel = "请假记录",
                    Title = $"{l.Date} - {l.LeaveType}",
                    Detail = $"{l.Hours}h" + (c.Length > 0 ? (c.Length > 70 ? "，" + c[..70] + "..." : "，" + c) : ""),
                    DeletedAt = l.DeletedAt
                });
            }

            // 更新统计
            TotalCount = allItems.Count;
            WorkRecordCount = recordList.Count();
            KnowledgeCount = knowledgeList.Count();
            IssueCount = issueList.Count();
            LeaveRecordCount = leaveList.Count();

            // 应用筛选
            ApplyFilter(allItems);
        }
        catch (Exception ex)
        {
            ToastService.Error($"加载回收站失败：{ex.Message}");
        }
    }

    /// <summary>根据当前 FilterType 筛选并更新 Items</summary>
    private void ApplyFilter(List<RecycleItem> allItems)
    {
        IEnumerable<RecycleItem> filtered = FilterType switch
        {
            "工作记录" => allItems.Where(x => x.EntityType == "WorkRecord"),
            "知识库" => allItems.Where(x => x.EntityType == "Knowledge"),
            "问题跟踪" => allItems.Where(x => x.EntityType == "Issue"),
            "请假记录" => allItems.Where(x => x.EntityType == "LeaveRecord"),
            _ => allItems
        };

        Items = new ObservableCollection<RecycleItem>(filtered);
        IsEmpty = Items.Count == 0;
        HasItems = Items.Count > 0;
    }

    [RelayCommand]
    private void SetFilter(string type)
    {
        if (FilterType == type) return;
        FilterType = type;
        // 重新从缓存数据中筛选（避免再次查询数据库）
        // 这里简化处理：直接重新 Load，因为数据量不大
        _ = LoadAsync();
    }

    [RelayCommand]
    private void ToggleMultiSelect()
    {
        IsMultiSelectMode = !IsMultiSelectMode;
        if (!IsMultiSelectMode)
        {
            SelectedItems.Clear();
        }
    }

    [RelayCommand]
    private async Task RestoreAsync(RecycleItem? item)
    {
        if (item == null) return;
        try
        {
            await RestoreByType(item.EntityType, item.Id);
            Items.Remove(item);
            UpdateCounts(-1, item.EntityType);
            ToastService.Success($"「{item.Title}」已恢复到原模块");
        }
        catch (Exception ex)
        {
            ToastService.Error($"恢复失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PermanentDeleteAsync(RecycleItem? item)
    {
        if (item == null) return;
        if (!DialogService.Instance.ShowConfirm(
            $"确定要彻底删除「{item.Title}」吗？\n此操作不可撤销。", "彻底删除", ConfirmType.Danger)) return;
        try
        {
            await HardDeleteByType(item.EntityType, item.Id);
            Items.Remove(item);
            UpdateCounts(-1, item.EntityType);
            ToastService.Success("已彻底删除");
        }
        catch (Exception ex)
        {
            ToastService.Error($"删除失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task BatchRestoreAsync()
    {
        if (SelectedItems.Count == 0) return;
        if (!DialogService.Instance.ShowConfirm(
            $"确定要恢复选中的 {SelectedItems.Count} 条内容吗？", "批量恢复", ConfirmType.Info)) return;

        try
        {
            foreach (var item in SelectedItems.ToList())
            {
                await RestoreByType(item.EntityType, item.Id);
                Items.Remove(item);
            }
            var count = SelectedItems.Count;
            UpdateCounts(-count, mixed: true);
            SelectedItems.Clear();
            ToastService.Success($"已恢复 {count} 条内容到原模块");
        }
        catch (Exception ex)
        {
            ToastService.Error($"批量恢复失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task BatchPermanentDeleteAsync()
    {
        if (SelectedItems.Count == 0) return;
        if (!DialogService.Instance.ShowConfirm(
            $"确定要彻底删除选中的 {SelectedItems.Count} 条内容吗？\n此操作不可撤销。", "批量彻底删除", ConfirmType.Danger)) return;

        try
        {
            foreach (var item in SelectedItems.ToList())
            {
                await HardDeleteByType(item.EntityType, item.Id);
                Items.Remove(item);
            }
            var count = SelectedItems.Count;
            UpdateCounts(-count, mixed: true);
            SelectedItems.Clear();
            ToastService.Success($"已彻底删除 {count} 条内容");
        }
        catch (Exception ex)
        {
            ToastService.Error($"批量删除失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task EmptyAsync()
    {
        var countToClear = FilterType == "全部"
            ? TotalCount
            : Items.Count;
        if (countToClear == 0) return;
        if (!DialogService.Instance.ShowConfirm(
            $"确定要清空回收站吗？共 {countToClear} 条内容将被永久删除，不可撤销。",
            "清空回收站", ConfirmType.Danger)) return;
        try
        {
            // 对当前筛选结果逐条物理删除
            foreach (var it in Items.ToList())
                await HardDeleteByType(it.EntityType, it.Id);

            TotalCount = 0;
            WorkRecordCount = 0;
            KnowledgeCount = 0;
            IssueCount = 0;
            LeaveRecordCount = 0;
            Items.Clear();
            IsEmpty = true;
            HasItems = false;
            ToastService.Success("回收站已清空");
        }
        catch (Exception ex)
        {
            ToastService.Error($"清空失败：{ex.Message}");
        }
    }

    /// <summary>更新各类型计数（增减量）</summary>
    private void UpdateCounts(int delta, string? entityType = null, bool mixed = false)
    {
        TotalCount += delta;
        if (TotalCount < 0) TotalCount = 0;

        if (mixed)
        {
            // 批量操作后无法精确知道各类型分布，重新 Load 更准确
            // 但为避免闪烁，这里仅重置总数标记，下次进入时自动刷新
            _ = LoadAsync();
            return;
        }

        switch (entityType)
        {
            case "WorkRecord": WorkRecordCount += delta; break;
            case "Knowledge": KnowledgeCount += delta; break;
            case "Issue": IssueCount += delta; break;
            case "LeaveRecord": LeaveRecordCount += delta; break;
        }
        IsEmpty = TotalCount == 0;
        HasItems = TotalCount > 0;
    }

    private async Task RestoreByType(string entityType, int id)
    {
        switch (entityType)
        {
            case "WorkRecord": await _recordRepo.RestoreAsync(id); break;
            case "Knowledge": await _knowledgeRepo.RestoreAsync(id); break;
            case "Issue": await _issueRepo.RestoreAsync(id); break;
            case "LeaveRecord": await _leaveRepo.RestoreAsync(id); break;
        }
    }

    private async Task HardDeleteByType(string entityType, int id)
    {
        switch (entityType)
        {
            case "WorkRecord": await _recordRepo.HardDeleteAsync(id); break;
            case "Knowledge": await _knowledgeRepo.HardDeleteAsync(id); break;
            case "Issue": await _issueRepo.HardDeleteAsync(id); break;
            case "LeaveRecord": await _leaveRepo.HardDeleteAsync(id); break;
        }
    }
}
