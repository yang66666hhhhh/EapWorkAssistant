using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EapWorkAssistant.ViewModels;

public partial class KnowledgeViewModel : PagedCollectionViewModelBase<Knowledge>
{
    private readonly KnowledgeRepository _repo = new();

    [ObservableProperty]
    private Knowledge _currentItem = new();

    [ObservableProperty]
    private Knowledge? _selectedItem;

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

    protected override string LoadFailureMessage => "加载知识失败";

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

    protected override async Task<IEnumerable<Knowledge>> GetAllAsync()
        => await _repo.GetAllAsync();

    /// <summary>DB 级分页：关键词 + 收藏/分类筛选全部下推到 SQL。</summary>
    protected override async Task<(List<Knowledge> PageItems, int Total)> LoadPageAsync(int page, int pageSize, string keyword)
    {
        var skip = (page - 1) * pageSize;
        var result = await _repo.GetFilteredPagedAsync(keyword, ShowFavoritesOnly, FilterCategory, skip, pageSize);
        return (result.Items.ToList(), result.Total);
    }

    protected override async Task OnAfterLoadAsync()
        => await RefreshTagsAndCategoriesAsync();

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

    protected override IEnumerable<Knowledge> ApplyExtraFilters(IEnumerable<Knowledge> source)
    {
        var q = source.AsEnumerable();
        if (ShowFavoritesOnly)
            q = q.Where(k => k.IsFavorite == 1);
        if (!string.IsNullOrEmpty(FilterCategory))
            q = q.Where(k => k.Category == FilterCategory);
        return q;
    }

    protected override bool MatchesSearch(Knowledge item, string kw)
    {
        return (item.Title != null && item.Title.Contains(kw, System.StringComparison.OrdinalIgnoreCase)) ||
               (item.Content != null && item.Content.Contains(kw, System.StringComparison.OrdinalIgnoreCase)) ||
               (item.Tags != null && item.Tags.Contains(kw, System.StringComparison.OrdinalIgnoreCase));
    }

    partial void OnFilterCategoryChanged(string value) => ReloadPageAsync(true).SafeFire(LoadFailureMessage);
    partial void OnShowFavoritesOnlyChanged(bool value) => ReloadPageAsync(true).SafeFire(LoadFailureMessage);

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

        var deleted = item; // 保留引用用于撤销
        await _repo.DeleteAsync(deleted.Id);
        if (SelectedItem?.Id == deleted.Id)
        {
            SelectedItem = null;
            CurrentItem = new Knowledge();
        }
        await LoadAsync();
        ToastService.WithAction("已删除知识库条目", "已删除", ToastType.Success, "撤销",
            () => _ = RestoreAsync(deleted));
    }

    private async Task RestoreAsync(Knowledge deleted)
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

    // ===== JSON 导出 / 导入 =====
    protected override string EmptyExportMessage => "没有可导出的知识";
    protected override string ExportSuccessMessage => "知识库已导出为 JSON";
    protected override bool ExportItems(List<Knowledge> items)
        => ExportService.ExportKnowledgeToJson(items);

    protected override ExportService.ImportResult<Knowledge> GetImportResult()
        => ExportService.ImportKnowledgeFromJson();

    protected override void SetImportDefaults(Knowledge item)
    {
        item.Id = 0;
        if (string.IsNullOrWhiteSpace(item.CreateTime))
            item.CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    protected override async Task InsertAsync(Knowledge item)
        => await _repo.InsertAsync(item);

    protected override string EmptyImportMessage => "文件为空，没有可导入的知识";
    protected override string ImportSuccessMessage => "已导入 {0} 条知识";
}
