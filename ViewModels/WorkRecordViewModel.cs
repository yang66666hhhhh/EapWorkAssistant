using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    /// <summary>保存成功后触发，通知 View 关闭抽屉</summary>
    public event Action? RecordSaved;

    /// <summary>报告生成后触发，通知 View 滚动到报告区域</summary>
    public event Action? ReportGenerated;

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

    public string[] Projects => ProjectInfo.Projects;
    public string[] WorkTypes => ProjectInfo.WorkTypes;
    public string[] FilterProjects => ["", .. ProjectInfo.Projects];
    public string[] FilterWorkTypes => ["", .. ProjectInfo.WorkTypes];
    public List<ContentTemplate> ContentTemplates => ConfigService.Instance.ContentTemplates;

    public WorkRecordViewModel()
    {
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _statusTimer.Tick += (_, _) => { StatusMessage = string.Empty; _statusTimer.Stop(); };
        PropertyChanged += WorkRecordViewModel_PropertyChanged;
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

    public async Task RefreshAsync() => await LoadRecordsAsync();

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
        if (CurrentRecord.Id > 0)
            await _repo.UpdateAsync(CurrentRecord);
        else
        {
            CurrentRecord.CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            await _repo.InsertAsync(CurrentRecord);
        }

        CurrentRecord = new WorkRecord { WorkDate = SelectedDate.ToString("yyyy-MM-dd") };
        HasProblem = false;
        IsEditing = false;
        FormTitle = "新增记录";
        SaveButtonText = "保存记录";
        await LoadRecordsAsync();
        StatusMessage = $"✓ 保存成功 · 今日共 {RecordCount} 条记录，{TodayHours:F1} 小时";
        _statusTimer.Start();
        RecordSaved?.Invoke();
    }

    [RelayCommand]
    private async Task DeleteRecordAsync(WorkRecord? record)
    {
        if (record == null) return;

        if (!ConfirmDialog.Show($"确定要删除这条记录吗？\n{record.Content}", "确认删除", ConfirmDialogType.Danger)) return;

        await _repo.DeleteAsync(record.Id);
        await LoadRecordsAsync();
        StatusMessage = "✓ 删除成功";
        _statusTimer.Start();
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
        CurrentRecord = new WorkRecord { WorkDate = SelectedDate.ToString("yyyy-MM-dd") };
        HasProblem = false;
        IsEditing = false;
        FormTitle = "新增记录";
        SaveButtonText = "保存记录";
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
            StatusMessage = "已复制到剪贴板";
            _statusTimer.Start();
        }
    }

    [RelayCommand]
    private void SaveReport()
    {
        if (!string.IsNullOrWhiteSpace(ReportText))
        {
            ExportService.SaveToFile(ReportText, "工作日报");
            StatusMessage = "已保存";
            _statusTimer.Start();
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
        StatusMessage = "已导出CSV文件";
        _statusTimer.Start();
    }

    [RelayCommand]
    private async Task LoadAllRecordsAsync()
    {
        var start = FilterStartDate ?? DateTime.Now.AddMonths(-1);
        var end = FilterEndDate ?? DateTime.Now;
        var startStr = start.ToString("yyyy-MM-dd");
        var endStr = end.ToString("yyyy-MM-dd");

        var records = (await _repo.GetByDateRangeAsync(startStr, endStr)).ToList();

        // 项目筛选
        if (!string.IsNullOrEmpty(FilterProject))
            records = records.Where(r => r.ProjectName == FilterProject).ToList();

        // 类型筛选
        if (!string.IsNullOrEmpty(FilterWorkType))
            records = records.Where(r => r.WorkType == FilterWorkType).ToList();

        // 按日期倒序排列
        AllRecords = new ObservableCollection<WorkRecord>(
            records.OrderByDescending(r => r.WorkDate).ThenByDescending(r => r.Id));

        AllTotalHours = AllRecords.Sum(r => r.Hours);
        AllTotalCount = AllRecords.Count;
        AllHighlightCount = AllRecords.Count(r => r.IsHighlight == 1);
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
        _ = LoadAllRecordsAsync();
    }

    [RelayCommand]
    private async Task DeleteAllRecordAsync(WorkRecord? record)
    {
        if (record == null) return;
        if (!ConfirmDialog.Show($"确定要删除这条记录吗？\n{record.Content}", "确认删除", ConfirmDialogType.Danger)) return;
        await _repo.DeleteAsync(record.Id);
        await LoadAllRecordsAsync();
        StatusMessage = "删除成功";
        _statusTimer.Start();
    }

    [RelayCommand]
    private void EditAllRecord(WorkRecord? record)
    {
        if (record == null) return;
        // 切换到当日记录 Tab 并定位到该日期
        SelectedDate = DateTime.Parse(record.WorkDate);
        SelectedTabIndex = 0;
        EditRecord(record);
        RecordSaved?.Invoke(); // 通知 View 打开抽屉
    }

    [RelayCommand]
    private void ExportAllCsv()
    {
        if (!AllRecords.Any())
        {
            StatusMessage = "没有可导出的记录";
            _statusTimer.Start();
            return;
        }
        var start = FilterStartDate?.ToString("yyyyMMdd") ?? "all";
        var end = FilterEndDate?.ToString("yyyyMMdd") ?? "now";
        ExportService.ExportToCsv(AllRecords, $"工作记录_{start}_{end}");
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
            _ = LoadAllRecordsAsync();
        }
    }

    partial void OnFilterProjectChanged(string value) => _ = LoadAllRecordsAsync();
    partial void OnFilterWorkTypeChanged(string value) => _ = LoadAllRecordsAsync();

    partial void OnSelectedDateChanged(DateTime value)
    {
        _ = LoadRecordsAsync();
    }
}
