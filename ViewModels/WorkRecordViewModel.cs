using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using System.Collections.ObjectModel;

namespace EapWorkAssistant.ViewModels;

public partial class WorkRecordViewModel : ObservableObject, IRefreshable
{
    private readonly WorkRecordRepository _repo = new();
    private readonly LeaveRecordRepository _leaveRepo = new();
    private readonly UiTimer _statusTimer;
    private readonly UiTimer _searchTimer;
    private readonly UiTimer _autoSaveTimer;
    private bool _suppressDirty;
    private int _queryGeneration;
    private bool _isAutoSaving;
    private bool _applyingPreset;
    private string _lastCalendarMonth = "";

    /// <summary>保存成功后触发，通知 View 关闭抽屉</summary>
    public event Action? RecordSaved;

    /// <summary>报告生成后触发，通知 View 滚动到报告区域</summary>
    public event Action? ReportGenerated;

    /// <summary>SelectedDate 变化时触发，通知 View 更新日期显示</summary>
    public event Action<DateTime>? SelectedDateChanged;

    /// <summary>请求打开请假对话框（新增模式）</summary>
    public event Action? OpenLeaveDialogRequested;

    /// <summary>请求打开请假对话框（编辑模式），参数为要编辑的记录</summary>
    public event Action<LeaveRecord>? EditLeaveRecordRequested;

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

    // ===== 调休余额 =====
    /// <summary>年度累计加班工时（周末 + 法定假日）</summary>
    [ObservableProperty]
    private double _overtimeHours;

    /// <summary>年度已使用调休工时</summary>
    [ObservableProperty]
    private double _compLeaveUsed;

    /// <summary>可用调休余额（加班工时 - 已调休）</summary>
    [ObservableProperty]
    private double _compLeaveAvailable;

    [ObservableProperty]
    private bool _hasProblem;

    [ObservableProperty]
    private bool _isFormDirty;

    /// <summary>表单抽屉是否打开（由 View 同步），用于判断用户是否正在编辑</summary>
    [ObservableProperty]
    private bool _isDrawerOpen;

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

    // ===== 全部记录排序 =====
    /// <summary>当前排序列名（对应 WorkRecord 属性名）</summary>
    [ObservableProperty]
    private string _sortColumn = "WorkDate";

    /// <summary>当前排序方向：true=升序，false=降序</summary>
    [ObservableProperty]
    private bool _sortAscending = false;

    /// <summary>每列的默认排序方向</summary>
    private static readonly Dictionary<string, bool> DefaultSortAscending = new()
    {
        ["WorkDate"] = false, ["IsHighlight"] = false, ["Hours"] = false,
        ["Progress"] = false, ["CreateTime"] = false,
        ["ProjectName"] = true, ["WorkType"] = true, ["Content"] = true, ["Achievement"] = true
    };

    /// <summary>切换排序：同列反转方向，异列设为默认方向并回到第 1 页</summary>
    public void ToggleSort(string column)
    {
        if (column == SortColumn)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = column;
            SortAscending = DefaultSortAscending.GetValueOrDefault(column, true);
        }
        CurrentPage = 1;
        LoadAllRecordsAsync().SafeFire("排序失败");
    }

    // ===== 日历状态圆点数据 =====
    /// <summary>日历当前显示的月份（由 View 同步），用于加载对应月份的日历状态</summary>
    public DateTime CalendarDisplayMonth { get; set; } = DateTime.Today;

    [ObservableProperty]
    private List<DateTime> _recordDates = new();

    [ObservableProperty]
    private List<DateTime> _holidayDates = new();

    [ObservableProperty]
    private Dictionary<DateTime, string> _leaveDateMap = new();

    [ObservableProperty]
    private List<DateTime> _makeupDates = new();

    // ===== 请假管理（整合到工作记录页面） =====

    /// <summary>当日请假记录</summary>
    [ObservableProperty]
    private ObservableCollection<LeaveRecord> _dailyLeaveRecords = new();

    /// <summary>请假类型列表</summary>
    public string[] LeaveTypes { get; } = ["年假", "事假", "病假", "调休", "出差", "婚假"];

    public int[] PageSizeOptions => [10, 20, 50, 100];

    public string[] Projects => ProjectInfo.Projects;
    public string[] WorkTypes => ProjectInfo.WorkTypes;
    public string[] FilterProjects => ["", .. ProjectInfo.Projects];
    public string[] FilterWorkTypes => ["", .. ProjectInfo.WorkTypes];
    public List<ContentTemplate> ContentTemplates => ConfigService.Instance.ContentTemplates;

    public WorkRecordViewModel()
    {
        _statusTimer = new UiTimer { Interval = TimeSpan.FromSeconds(5) };
        _statusTimer.Tick += (_, _) => { StatusMessage = string.Empty; _statusTimer.Stop(); };

        _searchTimer = new UiTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            CurrentPage = 1;
            await LoadAllRecordsAsync();
        };

        // 自动保存计时器
        _autoSaveTimer = new UiTimer();
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
        if (IsFormDirty && IsDrawerOpen)
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
        await LoadCalendarStatusAsync();
        await LoadCompLeaveBalanceAsync();
    }

    /// <summary>
    /// 加载当前选中日期所在月份的日历状态数据（记录日期、假日、请假、补班）
    /// </summary>
    public async Task LoadCalendarStatusAsync()
    {
        var year = CalendarDisplayMonth.Year;
        var month = CalendarDisplayMonth.Month;
        var yearMonth = $"{year:D4}-{month:D2}";

        try
        {
            // 1. 加载有工作记录的日期
            var dates = await _repo.GetDistinctDatesByMonthAsync(yearMonth);
            RecordDates = dates
                .Select(d => DateTime.Parse(d))
                .ToList();

            // 2. 加载法定假日和补班日
            await HolidayService.Instance.LoadYearAsync(year);
            var holidays = HolidayService.Instance.GetHolidaysForMonth(year, month);
            HolidayDates = holidays.Select(h => h.Date).ToList();
            var makeups = HolidayService.Instance.GetMakeupDaysForMonth(year, month);
            MakeupDates = makeups.Select(m => m.Date).ToList();

            // 3. 加载请假记录
            var leaves = await _leaveRepo.GetByMonthAsync(year, month);
            LeaveDateMap = leaves.ToDictionary(
                l => DateTime.Parse(l.Date),
                l => l.LeaveType);
        }
        catch (Exception ex)
        {
            // 日历状态加载失败不影响核心功能，静默降级
            ToastService.Error($"加载日历状态失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 计算年度调休余额：加班工时（周末 + 法定假日）- 已使用调休工时
    /// </summary>
    public async Task LoadCompLeaveBalanceAsync()
    {
        var year = SelectedDate.Year;
        var yearStart = $"{year:D4}-01-01";
        var yearEnd = $"{year:D4}-12-31";

        try
        {
            // 确保假日数据已加载
            await HolidayService.Instance.LoadYearAsync(year);

            // 1. 获取全年工作记录
            var allRecords = await _repo.GetByDateRangeAsync(yearStart, yearEnd);

            // 2. 筛选加班记录：周六/周日 + 法定假日（排除补班日，补班日算正常工作日不计入调休）
            double overtimeHours = 0;
            foreach (var r in allRecords)
            {
                if (!DateTime.TryParse(r.WorkDate, out var date)) continue;
                var dow = date.DayOfWeek;
                bool isWeekend = dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday;
                bool isHoliday = HolidayService.Instance.IsHoliday(date);
                bool isMakeup = HolidayService.Instance.IsMakeupWorkday(date);

                // 周末或法定假日工作 → 计入可调休加班；补班日虽在周末但属正常工作日，不计入
                if ((isWeekend || isHoliday) && !isMakeup)
                    overtimeHours += r.Hours;
            }

            // 3. 获取全年调休请假记录
            var leaves = await _leaveRepo.GetByYearAsync(year);
            double compUsed = leaves
                .Where(l => l.LeaveType == "调休")
                .Sum(l => l.Hours);

            // 4. 计算余额
            OvertimeHours = overtimeHours;
            CompLeaveUsed = compUsed;
            CompLeaveAvailable = Math.Max(0, overtimeHours - compUsed);
        }
        catch (Exception ex)
        {
            ToastService.Error($"加载调休余额失败：{ex.Message}");
        }
    }

    // ===== 请假管理方法 =====

    /// <summary>加载选中日期的请假记录</summary>
    public async Task LoadDailyLeaveRecordsAsync()
    {
        try
        {
            var dateStr = SelectedDate.ToString("yyyy-MM-dd");
            var allMonth = await _leaveRepo.GetByMonthAsync(SelectedDate.Year, SelectedDate.Month);
            var daily = allMonth.Where(l => l.Date == dateStr).ToList();
            DailyLeaveRecords = new ObservableCollection<LeaveRecord>(daily);
        }
        catch (Exception ex)
        {
            ToastService.Error($"加载请假记录失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void ShowLeaveForm()
    {
        OpenLeaveDialogRequested?.Invoke();
    }

    [RelayCommand]
    private void EditLeaveRecord(LeaveRecord? record)
    {
        if (record == null) return;
        EditLeaveRecordRequested?.Invoke(record);
    }

    [RelayCommand]
    private async Task DeleteLeaveRecordAsync(LeaveRecord? record)
    {
        if (record == null) return;
        if (!DialogService.Instance.ShowConfirm(
            $"确定要删除 {record.Date} 的「{record.LeaveType}」请假记录吗？",
            "确认删除", ConfirmType.Danger))
            return;

        try
        {
            await _leaveRepo.DeleteAsync(record.Id);
            DailyLeaveRecords.Remove(record);
            ToastService.Success("请假记录已删除");
            await LoadCalendarStatusAsync();
            await LoadCompLeaveBalanceAsync();
        }
        catch (Exception ex)
        {
            ToastService.Error($"删除请假记录失败：{ex.Message}");
        }
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
            bool proceed = DialogService.Instance.ShowConfirm(
                $"当日累计工时将达 {projectedTotal:F1} 小时，已超过 {DailyHoursWarnCap} 小时。\n\n确定要继续保存吗？",
                "工时偏长提醒",
                ConfirmType.Warning,
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
        await LoadCalendarStatusAsync();
        await LoadCompLeaveBalanceAsync();
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

        if (!DialogService.Instance.ShowConfirm($"确定要删除这条记录吗？\n{record.Content}", "确认删除", ConfirmType.Danger)) return;

        await _repo.DeleteAsync(record.Id);
        SelectedDailyRecord = null;
        await LoadRecordsAsync();
        await LoadCalendarStatusAsync();
        await LoadCompLeaveBalanceAsync();
        ToastService.Success("记录已删除");
    }

    [RelayCommand]
    private async Task DeleteCurrentRecordAsync()
    {
        if (CurrentRecord.Id <= 0) return;
        if (!DialogService.Instance.ShowConfirm($"确定要删除这条记录吗？\n{CurrentRecord.Content}", "确认删除", ConfirmType.Danger)) return;

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
        await LoadCalendarStatusAsync();
        await LoadCompLeaveBalanceAsync();
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
        var service = ReportService.Instance;
        ReportText = await service.GenerateDailyReportAsync(SelectedDate.ToString("yyyy-MM-dd"));
        ReportGenerated?.Invoke();
    }

    [RelayCommand]
    private async Task GenerateWeeklyReportAsync()
    {
        var service = ReportService.Instance;
        ReportText = await service.GenerateWeeklyReportAsync(SelectedDate);
        ReportGenerated?.Invoke();
    }

    [RelayCommand]
    private async Task GenerateMonthlyReportAsync()
    {
        var service = ReportService.Instance;
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

        if (!DialogService.Instance.ShowConfirm($"{msg}\n\n确定要导入吗？", "确认导入", ConfirmType.Warning))
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
            SearchKeyword, FilterProject, FilterWorkType, start, end, offset, PageSize,
            SortColumn, SortAscending);

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
        if (!DialogService.Instance.ShowConfirm($"确定要删除这条记录吗？\n{record.Content}", "确认删除", ConfirmType.Danger)) return;
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
            SearchKeyword, FilterProject, FilterWorkType, start, end, 0, int.MaxValue,
            SortColumn, SortAscending);
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
        LoadDailyLeaveRecordsAsync().SafeFire("加载请假记录失败");

        // 月份变化时重新加载日历状态数据
        var ym = value.ToString("yyyy-MM");
        if (ym != _lastCalendarMonth)
        {
            _lastCalendarMonth = ym;
            CalendarDisplayMonth = value;
            LoadCalendarStatusAsync().SafeFire("加载日历状态失败");
        }
    }
}
