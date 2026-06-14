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

public partial class WorkRecordViewModel : ObservableObject, IRefreshable
{
    private readonly WorkRecordRepository _repo = new();
    private readonly DispatcherTimer _statusTimer;
    private readonly DispatcherTimer _searchTimer;
    private readonly DispatcherTimer _autoSaveTimer;
    private bool _suppressDirty;
    private int _queryGeneration;
    private bool _isAutoSaving;

    /// <summary>保存成功后触发，通知 View 关闭抽屉</summary>
    public event Action? RecordSaved;

    /// <summary>报告生成后触发，通知 View 滚动到报告区域</summary>
    public event Action? ReportGenerated;

    /// <summary>SelectedDate 变化时触发，通知 View 更新日期显示</summary>
    public event Action<DateTime>? SelectedDateChanged;

    [ObservableProperty]
    private ObservableCollection<WorkRecord> _records = new();

    [ObservableProperty]
    private WorkRecord _currentRecord = new();

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Now;

    [ObservableProperty]
    private string _reportText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private double _todayHours;

    [ObservableProperty]
    private int _recordCount;

    [ObservableProperty]
    private int _highlightCount;

    [ObservableProperty]
    private bool _hasProblem;

    [ObservableProperty]
    private bool _isFormDirty;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _formTitle = "新增记录";

    [ObservableProperty]
    private string _saveButtonText = "保存记录";

    // ===== 全部记录 Tab =====
    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private ObservableCollection<WorkRecord> _allRecords = new();

    [ObservableProperty]
    private DateTime? _filterStartDate;

    [ObservableProperty]
    private DateTime? _filterEndDate;

    [ObservableProperty]
    private string _filterProject = "";

    [ObservableProperty]
    private string _filterWorkType = "";

    [ObservableProperty]
    private double _allTotalHours;

    [ObservableProperty]
    private int _allTotalCount;

    [ObservableProperty]
    private int _allHighlightCount;

    [ObservableProperty]
    private string _searchKeyword = "";

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _filteredTotalCount;

    [ObservableProperty]
    private string _pageText = "";

    [ObservableProperty]
    private int _pageSize = 20;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private ObservableCollection<int> _visiblePageNumbers = new();

    public int[] PageSizeOptions => [10, 20, 50, 100];

    public string[] Projects => ProjectInfo.Projects;
    public string[] WorkTypes => ProjectInfo.WorkTypes;
    public string[] FilterProjects => ["", .. ProjectInfo.Projects];
    public string[] FilterWorkTypes => ["", .. ProjectInfo.WorkTypes];
    public List<ContentTemplate> ContentTemplates => ConfigService.Instance.ContentTemplates;

    public WorkRecordViewModel()
    {
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _statusTimer.Tick += (_, _) => { StatusMessage = string.Empty; _statusTimer.Stop(); };

        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            CurrentPage = 1;
            await LoadAllRecordsAsync();
        };

        // 自动保存计时器
        _autoSaveTimer = new DispatcherTimer();
        _autoSaveTimer.Tick += async (_, _) =>
        {
            if (_isAutoSaving) return; // 防止重入
            if (IsFormDirty && !string.IsNullOrWhiteSpace(CurrentRecord.ProjectName)
                && !string.IsNullOrWhiteSpace(CurrentRecord.Content))
            {
                _isAutoSaving = true;
                try
                {
                    await SaveRecordAsync();
                    StatusMessage = "已自动保存";
                    _statusTimer.Start();
                }
                catch (Exception ex)
                {
                    ToastService.Error($"自动保存失败：{ex.Message}");
                }
                finally
                {
                    _isAutoSaving = false;
                }
            }
        };
        StartAutoSaveTimer();

        PropertyChanged += WorkRecordViewModel_PropertyChanged;
    }

    /// <summary>根据配置启动/重启自动保存计时器</summary>
    public void StartAutoSaveTimer()
    {
        var interval = ConfigService.Instance.AutoSaveInterval;
        _autoSaveTimer.Interval = TimeSpan.FromMinutes(Math.Max(1, interval));
        _autoSaveTimer.Start();
    }

    private void WorkRecordViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HasProblem) && !HasProblem)
        {
            // 取消勾选时清空问题和解决方案
            if (CurrentRecord != null)
            {
                CurrentRecord.Problem = string.Empty;
                CurrentRecord.Solution = string.Empty;
            }
        }
    }

    public async Task RefreshAsync()
    {
        StartAutoSaveTimer(); // 重新读取配置并重启自动保存
        await LoadRecordsAsync();
    }

    [RelayCommand]
    private async Task LoadRecordsAsync()
    {
        var dateStr = SelectedDate.ToString("yyyy-MM-dd");
        var records = await _repo.GetByDateAsync(dateStr);
        Records = new ObservableCollection<WorkRecord>(records);
        UpdateStats();
    }

    private void UpdateStats()
    {
        TodayHours = Records.Sum(r => r.Hours);
        RecordCount = Records.Count;
        HighlightCount = Records.Count(r => r.IsHighlight == 1);
    }

    [RelayCommand]
    private async Task SaveRecordAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentRecord.ProjectName))
        {
            StatusMessage = "请选择任务";
            _statusTimer.Start();
            return;
        }
        if (string.IsNullOrWhiteSpace(CurrentRecord.WorkType))
        {
            StatusMessage = "请选择类型";
            _statusTimer.Start();
            return;
        }
        if (string.IsNullOrWhiteSpace(CurrentRecord.Content))
        {
            StatusMessage = "请输入工作内容";
            _statusTimer.Start();
            return;
        }

        if (CurrentRecord.Hours < 0 || CurrentRecord.Hours > 24)
        {
            StatusMessage = "工时应在 0-24 小时之间";
            _statusTimer.Start();
            return;
        }

        if (CurrentRecord.Progress < 0 || CurrentRecord.Progress > 100)
        {
            StatusMessage = "进度应在 0-100% 之间";
            _statusTimer.Start();
            return;
        }

        CurrentRecord.WorkDate = SelectedDate.ToString("yyyy-MM-dd");
        try
        {
            if (CurrentRecord.Id > 0)
                await _repo.UpdateAsync(CurrentRecord);
            else
            {
                CurrentRecord.CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                await _repo.InsertAsync(CurrentRecord);
            }
        }
        catch (Exception ex)
        {
            ToastService.Error($"保存失败：{ex.Message}");
            return;
        }

        _suppressDirty = true;
        CurrentRecord = new WorkRecord { WorkDate = SelectedDate.ToString("yyyy-MM-dd") };
        HasProblem = false;
        IsEditing = false;
        IsFormDirty = false;
        FormTitle = "新增记录";
        SaveButtonText = "保存记录";
        _suppressDirty = false;
        await LoadRecordsAsync();
        ToastService.Success($"工作记录已保存 · 今日共 {RecordCount} 条");
        RecordSaved?.Invoke();
    }

    [RelayCommand]
    private async Task DeleteRecordAsync(WorkRecord? record)
    {
        if (record == null) return;

        if (!ConfirmDialog.Show($"确定要删除这条记录吗？\n{record.Content}", "确认删除", ConfirmDialogType.Danger)) return;

        await _repo.DeleteAsync(record.Id);
        await LoadRecordsAsync();
        ToastService.Success("记录已删除");
    }

    [RelayCommand]
    private void EditRecord(WorkRecord? record)
    {
        if (record == null) return;
        CurrentRecord = new WorkRecord
        {
            Id = record.Id,
            WorkDate = record.WorkDate,
            ProjectName = record.ProjectName,
            WorkType = record.WorkType,
            Content = record.Content,
            Achievement = record.Achievement,
            Problem = record.Problem,
            Solution = record.Solution,
            Hours = record.Hours,
            Progress = record.Progress,
            IsHighlight = record.IsHighlight,
            HighlightNote = record.HighlightNote
        };
        HasProblem = !string.IsNullOrWhiteSpace(record.Problem);
        IsEditing = true;
        FormTitle = "编辑记录";
        SaveButtonText = "更新记录";
    }

    [RelayCommand]
    private void NewRecord()
    {
        _suppressDirty = true;
        CurrentRecord = new WorkRecord { WorkDate = SelectedDate.ToString("yyyy-MM-dd") };
        HasProblem = false;
        IsEditing = false;
        IsFormDirty = false;
        FormTitle = "新增记录";
        SaveButtonText = "保存记录";
        _suppressDirty = false;
    }

    public void MarkDirty()
    {
        if (!_suppressDirty)
            IsFormDirty = true;
    }

    [RelayCommand]
    private void ClosePanel()
    {
        RecordSaved?.Invoke();
    }

    [RelayCommand]
    private void ApplyTemplate(ContentTemplate? template)
    {
        if (template == null) return;
        CurrentRecord.Content = template.Content;
        StatusMessage = $"已应用模板：{template.Name}";
        _statusTimer.Start();
    }

    [RelayCommand]
    private async Task CopyLastRecordAsync()
    {
        WorkRecord? last = Records.FirstOrDefault();
        if (last == null)
        {
            // 当天没有记录时，向前查找最近一条记录
            last = await _repo.GetLatestBeforeOrOnAsync(SelectedDate.ToString("yyyy-MM-dd"));
        }
        if (last == null)
        {
            StatusMessage = "当前没有可复制的记录";
            _statusTimer.Start();
            return;
        }

        CurrentRecord = new WorkRecord
        {
            WorkDate = SelectedDate.ToString("yyyy-MM-dd"),
            ProjectName = last.ProjectName,
            WorkType = last.WorkType
        };
        HasProblem = false;
        StatusMessage = "已复制上条记录的任务和类型";
        _statusTimer.Start();
    }

    [RelayCommand]
    private async Task GenerateDailyReportAsync()
    {
        var service = new ReportService();
        ReportText = await service.GenerateDailyReportAsync(SelectedDate.ToString("yyyy-MM-dd"));
        ReportGenerated?.Invoke();
    }

    [RelayCommand]
    private async Task GenerateWeeklyReportAsync()
    {
        var service = new ReportService();
        ReportText = await service.GenerateWeeklyReportAsync(SelectedDate);
        ReportGenerated?.Invoke();
    }

    [RelayCommand]
    private async Task GenerateMonthlyReportAsync()
    {
        var service = new ReportService();
        var yearMonth = SelectedDate.ToString("yyyy-MM");
        ReportText = await service.GenerateMonthlyReportAsync(yearMonth);
        ReportGenerated?.Invoke();
    }

    [RelayCommand]
    private void CopyReport()
    {
        if (!string.IsNullOrWhiteSpace(ReportText))
        {
            ExportService.CopyToClipboard(ReportText);
            ToastService.Success("报告已复制到剪贴板");
        }
    }

    [RelayCommand]
    private void SaveReport()
    {
        if (!string.IsNullOrWhiteSpace(ReportText))
        {
            ExportService.SaveToFile(ReportText, "工作日报");
            ToastService.Success("报告已保存");
        }
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (!Records.Any())
        {
            StatusMessage = "没有可导出的记录";
            _statusTimer.Start();
            return;
        }
        ExportService.ExportToCsv(Records, $"工作记录_{SelectedDate:yyyyMMdd}");
        ToastService.Success("CSV 文件已导出");
    }

    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        var records = ExportService.ImportFromCsv();
        if (records == null || records.Count == 0)
        {
            StatusMessage = "导入失败：文件为空或格式不正确";
            _statusTimer.Start();
            return;
        }

        if (!ConfirmDialog.Show($"确定要导入 {records.Count} 条工作记录吗？\n导入后不可撤销。", "确认导入", ConfirmDialogType.Warning))
            return;

        try
        {
            var count = await _repo.BatchInsertAsync(records);
            await LoadRecordsAsync();
            ToastService.Success($"已导入 {count} 条工作记录");
        }
        catch
        {
            ToastService.Error("导入失败，请检查 CSV 文件格式");
        }
    }

    [RelayCommand]
    private void SaveReportAsMarkdown()
    {
        if (!string.IsNullOrWhiteSpace(ReportText))
        {
            ExportService.SaveAsMarkdown("工作日报", ReportText, "工作日报");
            ToastService.Success("Markdown 文件已保存");
        }
    }

    [RelayCommand]
    private async Task LoadAllRecordsAsync()
    {
        var gen = ++_queryGeneration;
        var start = FilterStartDate?.ToString("yyyy-MM-dd");
        var end = FilterEndDate?.ToString("yyyy-MM-dd");
        var offset = (CurrentPage - 1) * PageSize;

        var (records, totalCount, totalHours, highlightCount) = await _repo.GetFilteredPagedAsync(
            SearchKeyword, FilterProject, FilterWorkType, start, end, offset, PageSize);

        // 如果已有更新的查询启动，丢弃本次结果
        if (gen != _queryGeneration) return;

        AllRecords = new ObservableCollection<WorkRecord>(records);
        AllTotalHours = totalHours;
        AllTotalCount = totalCount;
        AllHighlightCount = highlightCount;
        FilteredTotalCount = totalCount;
        UpdatePagination();
    }

    private void UpdatePagination()
    {
        TotalPages = CalculateTotalPages();
        if (CurrentPage > TotalPages && TotalPages > 0)
            CurrentPage = TotalPages;
        PageText = FilteredTotalCount > 0
            ? $"第 {CurrentPage} / {TotalPages} 页"
            : "无记录";
        UpdateVisiblePageNumbers();
    }

    private int CalculateTotalPages()
        => FilteredTotalCount > 0 ? (FilteredTotalCount + PageSize - 1) / PageSize : 1;

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

    [RelayCommand]
    private async Task FirstPageAsync()
    {
        if (CurrentPage > 1) { CurrentPage = 1; await LoadAllRecordsAsync(); }
    }

    [RelayCommand]
    private async Task PrevPageAsync()
    {
        if (CurrentPage > 1) { CurrentPage--; await LoadAllRecordsAsync(); }
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages) { CurrentPage++; await LoadAllRecordsAsync(); }
    }

    [RelayCommand]
    private async Task LastPageAsync()
    {
        if (CurrentPage < TotalPages) { CurrentPage = TotalPages; await LoadAllRecordsAsync(); }
    }

    [RelayCommand]
    private async Task GoToPageAsync(object? param)
    {
        if (param is int page && page > 0 && page <= TotalPages)
        {
            CurrentPage = page;
            await LoadAllRecordsAsync();
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchKeyword = "";
    }

    [RelayCommand]
    private void SetDatePreset(string? preset)
    {
        var today = DateTime.Now;
        switch (preset)
        {
            case "thisWeek":
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                FilterStartDate = today.AddDays(-diff);
                FilterEndDate = today.AddDays(6 - diff);
                break;
            case "thisMonth":
                FilterStartDate = new DateTime(today.Year, today.Month, 1);
                FilterEndDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                break;
            case "last3Months":
                FilterStartDate = today.AddMonths(-3);
                FilterEndDate = today;
                break;
            case "all":
                FilterStartDate = null;
                FilterEndDate = null;
                break;
        }
        CurrentPage = 1;
        LoadAllRecordsAsync().SafeFire("加载记录失败");
    }

    [RelayCommand]
    private async Task DeleteAllRecordAsync(WorkRecord? record)
    {
        if (record == null) return;
        if (!ConfirmDialog.Show($"确定要删除这条记录吗？\n{record.Content}", "确认删除", ConfirmDialogType.Danger)) return;
        await _repo.DeleteAsync(record.Id);
        // 删除后如果当前页变空，回退到上一页
        if (AllRecords.Count <= 1 && CurrentPage > 1)
            CurrentPage--;
        await LoadAllRecordsAsync();
        StatusMessage = "删除成功";
        _statusTimer.Start();
    }

    [RelayCommand]
    private void EditAllRecord(WorkRecord? record)
    {
        if (record == null) return;
        // 切换到当日记录 Tab 并定位到该日期
        SelectedDate = DateTime.TryParse(record.WorkDate, out var d) ? d : DateTime.Now;
        SelectedTabIndex = 0;
        EditRecord(record);
        RecordSaved?.Invoke(); // 通知 View 打开抽屉
    }

    [RelayCommand]
    private async Task ExportAllCsv()
    {
        if (FilteredTotalCount == 0)
        {
            StatusMessage = "没有可导出的记录";
            _statusTimer.Start();
            return;
        }
        // 导出全部匹配记录（不受分页限制）
        var start = FilterStartDate?.ToString("yyyy-MM-dd");
        var end = FilterEndDate?.ToString("yyyy-MM-dd");
        var (records, _, _, _) = await _repo.GetFilteredPagedAsync(
            SearchKeyword, FilterProject, FilterWorkType, start, end, 0, int.MaxValue);
        var startStr = FilterStartDate?.ToString("yyyyMMdd") ?? "all";
        var endStr = FilterEndDate?.ToString("yyyyMMdd") ?? "now";
        ExportService.ExportToCsv(new ObservableCollection<WorkRecord>(records), $"工作记录_{startStr}_{endStr}");
        StatusMessage = "已导出CSV文件";
        _statusTimer.Start();
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == 1)
        {
            // 首次切到全部记录时，默认显示本月
            if (FilterStartDate == null && FilterEndDate == null)
            {
                var today = DateTime.Now;
                FilterStartDate = new DateTime(today.Year, today.Month, 1);
                FilterEndDate = today;
            }
            CurrentPage = 1;
            LoadAllRecordsAsync().SafeFire("加载记录失败");
        }
    }

    partial void OnFilterProjectChanged(string value) { CurrentPage = 1; LoadAllRecordsAsync().SafeFire("筛选失败"); }
    partial void OnFilterWorkTypeChanged(string value) { CurrentPage = 1; LoadAllRecordsAsync().SafeFire("筛选失败"); }
    partial void OnFilterStartDateChanged(DateTime? value) { CurrentPage = 1; LoadAllRecordsAsync().SafeFire("筛选失败"); }
    partial void OnFilterEndDateChanged(DateTime? value) { CurrentPage = 1; LoadAllRecordsAsync().SafeFire("筛选失败"); }

    partial void OnSearchKeywordChanged(string value)
    {
        _searchTimer.Stop();
        CurrentPage = 1;
        if (string.IsNullOrWhiteSpace(value))
        {
            LoadAllRecordsAsync().SafeFire("加载记录失败");
        }
        else
        {
            _searchTimer.Start();
        }
    }

    partial void OnCurrentPageChanged(int value) => UpdatePagination();

    partial void OnPageSizeChanged(int value)
    {
        CurrentPage = 1;
        LoadAllRecordsAsync().SafeFire("加载记录失败");
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        SelectedDateChanged?.Invoke(value);
        LoadRecordsAsync().SafeFire("加载记录失败");
    }
}
