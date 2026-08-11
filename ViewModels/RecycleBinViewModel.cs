using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using System.Collections.ObjectModel;

namespace EapWorkAssistant.ViewModels;

public partial class RecycleBinViewModel : ObservableObject, IRefreshable
{
    private readonly WorkRecordRepository _recordRepo = new();
    private readonly KnowledgeRepository _knowledgeRepo = new();
    private readonly IssueRepository _issueRepo = new();

    [ObservableProperty]
    private ObservableCollection<RecycleItem> _items = new();

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private bool _hasItems;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    private readonly UiTimer _statusTimer;

    public RecycleBinViewModel()
    {
        _statusTimer = new UiTimer { Interval = TimeSpan.FromSeconds(4) };
        _statusTimer.Tick += (_, _) => { StatusMessage = string.Empty; _statusTimer.Stop(); };
    }

    public async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var items = new List<RecycleItem>();
        try
        {
            foreach (var r in await _recordRepo.GetDeletedAsync())
            {
                var c = r.Content ?? "";
                items.Add(new RecycleItem
                {
                    EntityType = "WorkRecord",
                    Id = r.Id,
                    TypeLabel = "工作记录",
                    Title = $"{r.ProjectName} - {r.WorkDate}",
                    Detail = c.Length > 80 ? c[..80] + "..." : c
                });
            }

            foreach (var k in await _knowledgeRepo.GetDeletedAsync())
            {
                var c = k.Content ?? "";
                items.Add(new RecycleItem
                {
                    EntityType = "Knowledge",
                    Id = k.Id,
                    TypeLabel = "知识库",
                    Title = k.Title,
                    Detail = c.Length > 80 ? c[..80] + "..." : c
                });
            }

            foreach (var i in await _issueRepo.GetDeletedAsync())
            {
                var c = i.Description ?? "";
                items.Add(new RecycleItem
                {
                    EntityType = "Issue",
                    Id = i.Id,
                    TypeLabel = "问题跟踪",
                    Title = $"[{i.ProjectName}] {i.Title}",
                    Detail = c.Length > 80 ? c[..80] + "..." : c
                });
            }

            Items = new ObservableCollection<RecycleItem>(items);
            IsEmpty = items.Count == 0;
            HasItems = items.Count > 0;
        }
        catch (Exception ex)
        {
            ToastService.Error($"加载回收站失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RestoreAsync(RecycleItem? item)
    {
        if (item == null) return;
        try
        {
            await RestoreByType(item.EntityType, item.Id);
            await LoadAsync();
            ToastService.Success("已恢复到原模块");
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
        if (!DialogService.Instance.ShowConfirm($"确定要彻底删除「{item.Title}」吗？此操作不可撤销。", "彻底删除", ConfirmType.Danger)) return;
        try
        {
            await HardDeleteByType(item.EntityType, item.Id);
            await LoadAsync();
            ToastService.Success("已彻底删除");
        }
        catch (Exception ex)
        {
            ToastService.Error($"删除失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task EmptyAsync()
    {
        if (IsEmpty) return;
        if (!DialogService.Instance.ShowConfirm("确定要清空回收站吗？所有已删除内容将被永久删除，不可撤销。", "清空回收站", ConfirmType.Danger)) return;
        try
        {
            foreach (var it in Items)
                await HardDeleteByType(it.EntityType, it.Id);
            await LoadAsync();
            ToastService.Success("回收站已清空");
        }
        catch (Exception ex)
        {
            ToastService.Error($"清空失败：{ex.Message}");
        }
    }

    private async Task RestoreByType(string entityType, int id)
    {
        switch (entityType)
        {
            case "WorkRecord": await _recordRepo.RestoreAsync(id); break;
            case "Knowledge": await _knowledgeRepo.RestoreAsync(id); break;
            case "Issue": await _issueRepo.RestoreAsync(id); break;
        }
    }

    private async Task HardDeleteByType(string entityType, int id)
    {
        switch (entityType)
        {
            case "WorkRecord": await _recordRepo.HardDeleteAsync(id); break;
            case "Knowledge": await _knowledgeRepo.HardDeleteAsync(id); break;
            case "Issue": await _issueRepo.HardDeleteAsync(id); break;
        }
    }
}
