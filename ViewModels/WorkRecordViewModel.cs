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
    private bool _applyingPreset;

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

    [ObservableProperty]
    private WorkRecord? _selectedDailyRecord;

    [ObservableProperty]
    private WorkRecord? _selectedAllRecord;

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
    private string _activeDatePreset = "";

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
                    StatusMessage = "正在保存...";
                    await AutoSaveRecordAsync();
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

    /// <summary>暂停自动保存（离开工作记录页面时调用），同时刷出未保存的编辑数据</summary>
    public void PauseAutoSaveTimer()
    {
        _autoSaveTimer.Stop();
        if (IsFormDirty)
            FlushPendingChangesAsync().SafeFire("自动保存失败");
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

    // ===== 企业级校验常量 =====
    private const double MinRecordHours = 0.5;
    private const double DailyHoursHardCap = 15;
    private const double DailyHoursWarnCap = 12;
    private const double DailyHoursSoftCap = 10;
    private const int MinContentLength = 5;

    /// <summary>
    /// 硬性校验：不满足则绝对不能保存。
    /// 用于手动保存、关闭面板、导航离开等场景。
    /// </summary>
    private (bool IsValid, string? Error) ValidateStrict()
    {
        if (string.IsNullOrWhiteSpace(CurrentRecord.ProjectName))
            return (false, "请选择任务");
        if (string.IsNullOrWhiteSpace(CurrentRecord.WorkType))
            return (false, "请选择类型");
        if (string.IsNullOrWhiteSpace(CurrentRecord.Content))
            return (false, "请输入工作内容");
        if (CurrentRecord.Content.Trim().Length < MinContentLength)
            return (false, $"工作内容至少需要 {MinContentLength} 个字符，请补充更多细节");
        if (CurrentRecord.Hours <= 0)
            return (false, "工时必须大于 0");
        if (CurrentRecord.Hours < MinRecordHours)
            return (false, $"单条工时不应少于 {MinRecordHours} 小时，过短的请合并到其他记录");
        if (CurrentRecord.Progress < 0 || CurrentRecord.Progress > 100)
            return (false, "进度应在 0-100% 之间");

        var existingHours = Records
            .Where(r => r.Id != CurrentRecord.Id)
            .Sum(r => r.Hours);
        var projectedTotal = existingHours + CurrentRecord.Hours;
        if (projectedTotal > DailyHoursHardCap)
            return (false, $"当日累计工时将达 {projectedTotal:F1}h，超过每日上限 {DailyHoursHardCap} 小时");

        return (true, null);
    }

    /// <summary>
    /// 软性校验：仅发出警告，不阻止保存。仅在手动保存时调用。
    /// </summary>
    private async Task<bool> ValidateSoftAsync()
    {
        var existingHours = Records
            .Where(r => r.Id != CurrentRecord.Id)
            .Sum(r => r.Hours);
        var projectedTotal = existingHours + CurrentRecord.Hours;

        if (projectedTotal > DailyHoursWarnCap)
        {
            bool proceed = ConfirmDialog.Show(
                $"当日累计工时将达 {projectedTotal:F1} 小时，已超过 {DailyHoursWarnCap} 小时。\n\n确定要继续保存吗？",
                "工时偏长提醒",
                ConfirmDialogType.Warning,
                "继续保存", "取消");
            if (!proceed) return false;
        }
        else if (projectedTotal > DailyHoursSoftCap)
        {
            ToastService.Info($"当日累计工时将达 {projectedTotal:F1} 小时，请注意合理安排休息");
        }

        if (ConfigService.Instance.IsRestDay(SelectedDate))
        {
            ToastService.Info($"{SelectedDate:yyyy-MM-dd} 是休息日，已记录加班工时");
        }

        return true;
    }

    /// <summary>
    /// 将当前记录持久化到数据库（仅 DB 操作，不重置表单、不校验）。
    /// 新建记录会设置 CreateTime 并调用 InsertAsync（内部回写 Id）。
    /// </summary>
    private async Task<bool> PersistCurrentRecordAsync()
    {
        if (CurrentRecord.Id == 0)
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
            return true;
        }
        catch (Exception ex)
        {
            ToastService.Error($"保存失败：{ex.Message}");
            return false;
        }
    }

    [RelayCommand]
    private async Task SaveRecordAsync()
    {
        // ── 硬性校验 ──
        var (isValid, error) = ValidateStrict();
        if (!isValid)
        {
            StatusMessage = error!;
            _statusTimer.Start();
            return;
        }

        // ── 软性校验（可弹出确认对话框） ──
        if (!await ValidateSoftAsync()) return;

        StatusMessage = "正在保存...";

        if (!await PersistCurrentRecordAsync())
        {
            StatusMessage = string.Empty;
            return;
        }

        // 重置表单状态
        _suppressDirty = true;
        CurrentRecord = new WorkRecord { WorkDate = SelectedDate.ToString("yyyy-MM-dd") };
        HasProblem = false;
        IsEditing = false;
        IsFormDirty = false;
        FormTitle = "新增记录";
        SaveButtonText = "保存记录";
        SelectedDailyRecord = null;
        SelectedAllRecord = null;
        _suppressDirty = false;
        if (SelectedTabIndex == 1)
            await LoadAllRecordsAsync();
        else
            await LoadRecordsAsync();
        StatusMessage = string.Empty;
        ToastService.Success("工作记录已保存");
        RecordSaved?.Invoke();
    }

    /// <summary>
    /// 自动保存专用：跳过严格校验，直接持久化，不重置表单。
    /// 确保用户正在编辑的数据不会因校验失败而丢失。
    /// </summary>
    private async Task AutoSaveRecordAsync()
    {
        // 新建记录使用当前选中日期
        if (CurrentRecord.Id == 0)
            CurrentRecord.WorkDate = SelectedDate.ToString("yyyy-MM-dd");

        if (CurrentRecord.Id > 0)
        {
            await _repo.UpdateAsync(CurrentRecord);
        }
        else
        {
            // 自动插入：WorkType 为空时填占位值以满足 DB NOT NULL 约束
            if (string.IsNullOrWhiteSpace(CurrentRecord.WorkType))
                CurrentRecord.WorkType = "其他";
            if (string.IsNullOrWhiteSpace(CurrentRecord.CreateTime))
                CurrentRecord.CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // InsertAsync 内部会回写 record.Id，后续自动保存将走 Update 分支
            await _repo.InsertAsync(CurrentRecord);

            // 将新记录加入列表以便统计和显示
            Records.Add(CurrentRecord);
            UpdateStats();
        }
    }

    /// <summary>
    /// 判断当前表单数据是否可直接保存（无需用户交互确认）。
    /// 已入库的记录（Id > 0）总是可以保存；新记录需满足最低必填要求。
    /// </summary>
    public bool CanQuickSave()
    {
        if (!IsFormDirty) return false;
        if (CurrentRecord.Id > 0) return true;
        return !string.IsNullOrWhiteSpace(CurrentRecord.ProjectName)
            && !string.IsNullOrWhiteSpace(CurrentRecord.Content)
            && CurrentRecord.Content.Trim().Length >= MinContentLength;
    }

    /// <summary>
    /// 刷出未保存的编辑数据。用于导航离开或关闭面板前调用。
    /// 仅做硬性校验 + 持久化，不弹出确认对话框，不重置表单。
    /// </summary>
    public async Task FlushPendingChangesAsync()
    {
        if (!IsFormDirty) return;

        var (isValid, error) = ValidateStrict();
        if (!isValid)
        {
            ToastService.Info($"数据未保存：{error}");
            return;
        }

        await PersistCurrentRecordAsync();
        IsFormDirty = false;
    }

    [RelayCommand]
    private async Task DeleteRecordAsync(WorkRecord? record)
    {
        if (record == null) return;

        if (!ConfirmDialog.Show($"确定要删除这条记录吗？\n{record.Content}", "确认删除", ConfirmDialogType.Danger)) return;

        await _repo.DeleteAsync(record.Id);
        SelectedDailyRecord = null;
        await LoadRecordsAsync();
        ToastService.Success("记录已删除");
    }

    [RelayCommand]
    private async Task DeleteCurrentRecordAsync()
    {
        if (CurrentRecord.Id <= 0) return;
        if (!ConfirmDialog.Show($"确定要删除这条记录吗？\n{CurrentRecord.Content}", "确认删除", ConfirmDialogType.Danger)) return;

        await _repo.DeleteAsync(CurrentRecord.Id);
        _suppressDirty = true;
        CurrentRecord = new WorkRecord { WorkDate = SelectedDate.ToString("yyyy-MM-dd") };
        HasProblem = false;
        IsEditing = false;
        IsFormDirty = false;
        FormTitle = "新增记录";
        SaveButtonText = "保存记录";
        _suppressDirty = false;
        await LoadRecordsAsync();
        if (SelectedTabIndex == 1)
            await LoadAllRecordsAsync();
        ToastService.Success("记录已删除");
        RecordSaved?.Invoke(); // 关闭面板
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
        SelectedDailyRecord = record;
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
        SelectedDailyRecord = null;
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
            if (ExportService.SaveToFile(ReportText, "工作日报"))
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
        if (ExportService.ExportToCsv(Records, $"工作记录_{SelectedDate:yyyyMMdd}"))
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

        // ── CSV 数据清洗与校验 ──
        var valid = new List<WorkRecord>();
        var skipped = new List<string>();
        foreach (var r in records)
        {
            // 日期格式校验
            if (!DateTime.TryParseExact(r.WorkDate, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _))
            {
                skipped.Add($"日期格式错误「{r.WorkDate}」，已跳过");
                continue;
            }
            // 必填字段
            if (string.IsNullOrWhiteSpace(r.ProjectName) || string.IsNullOrWhiteSpace(r.Content))
            {
                skipped.Add($"{r.WorkDate} 记录缺少任务或内容，已跳过");
                continue;
            }
            // 工时范围
            if (r.Hours <= 0 || r.Hours > 24)
            {
                skipped.Add($"{r.WorkDate}「{r.ProjectName}」工时 {r.Hours}h 不合理，已跳过");
                continue;
            }
            // 进度范围
            if (r.Progress < 0 || r.Progress > 100)
                r.Progress = Math.Clamp(r.Progress, 0, 100);

            valid.Add(r);
        }

        if (valid.Count == 0)
        {
            ToastService.Error("导入失败：所有记录均未通过校验");
            return;
        }

        var msg = $"共解析 {records.Count} 条，{valid.Count} 条有效";
        if (skipped.Count > 0)
            msg += $"\n\n已跳过 {skipped.Count} 条异常记录：\n{string.Join("\n", skipped.Take(5))}"
                 + (skipped.Count > 5 ? $"\n...及其他 {skipped.Count - 5} 条" : "");

        if (!ConfirmDialog.Show($"{msg}\n\n确定要导入吗？", "确认导入", ConfirmDialogType.Warning))
            return;

        try
        {
            var count = await _repo.BatchInsertAsync(valid);
            await LoadRecordsAsync();
            ToastService.Success($"已导入 {count} 条工作记录" +
                (skipped.Count > 0 ? $"，跳过 {skipped.Count} 条" : ""));
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
            if (ExportService.SaveAsMarkdown("工作日报", ReportText, "工作日报"))
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
        _applyingPreset = true;
        try
        {
            ActiveDatePreset = preset ?? "";
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
        finally { _applyingPreset = false; }
    }

    [RelayCommand]
    private async Task DeleteAllRecordAsync(WorkRecord? record)
    {
        if (record == null) return;
        if (!ConfirmDialog.Show($"确定要删除这条记录吗？\n{record.Content}", "确认删除", ConfirmDialogType.Danger)) return;
        await _repo.DeleteAsync(record.Id);
        SelectedAllRecord = null;
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
        // 不切换 Tab 和日期，直接在全部记录页打开编辑抽屉
        EditRecord(record);
        SelectedAllRecord = record;
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
        if (ExportService.ExportToCsv(new ObservableCollection<WorkRecord>(records), $"工作记录_{startStr}_{endStr}"))
        {
            StatusMessage = "已导出CSV文件";
            _statusTimer.Start();
        }
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == 1)
        {
            // 首次切到全部记录时，默认显示本月
            if (FilterStartDate == null && FilterEndDate == null)
            {
                _applyingPreset = true;
                try
                {
                    ActiveDatePreset = "thisMonth";
                    var today = DateTime.Now;
                    FilterStartDate = new DateTime(today.Year, today.Month, 1);
                    FilterEndDate = today;
                }
                finally { _applyingPreset = false; }
            }
            CurrentPage = 1;
            LoadAllRecordsAsync().SafeFire("加载记录失败");
        }
    }

    partial void OnFilterProjectChanged(string value) { ActiveDatePreset = ""; CurrentPage = 1; LoadAllRecordsAsync().SafeFire("筛选失败"); }
    partial void OnFilterWorkTypeChanged(string value) { ActiveDatePreset = ""; CurrentPage = 1; LoadAllRecordsAsync().SafeFire("筛选失败"); }
    partial void OnFilterStartDateChanged(DateTime? value) { if (!_applyingPreset) ActiveDatePreset = ""; CurrentPage = 1; LoadAllRecordsAsync().SafeFire("筛选失败"); }
    partial void OnFilterEndDateChanged(DateTime? value) { if (!_applyingPreset) ActiveDatePreset = ""; CurrentPage = 1; LoadAllRecordsAsync().SafeFire("筛选失败"); }

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
