using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace EapWorkAssistant.ViewModels;

public partial class DashboardViewModel : ObservableObject, IRefreshable
{
    private readonly WorkRecordRepository _recordRepo = new();
    private readonly KnowledgeRepository _knowledgeRepo = new();
    private readonly IssueRepository _issueRepo = new();

    // 基础统计
    [ObservableProperty] private double _todayHours;
    [ObservableProperty] private double _weekHours;
    [ObservableProperty] private double _monthHours;
    [ObservableProperty] private int _totalRecords;
    [ObservableProperty] private int _totalIssues;
    [ObservableProperty] private int _totalKnowledge;
    [ObservableProperty] private string _probationReport = string.Empty;
    [ObservableProperty] private string _currentDate = DateTime.Now.ToString("yyyy-MM-dd dddd");
    [ObservableProperty] private ObservableCollection<RecentRecordItem> _recentRecords = new();

    // 试用期进度
    [ObservableProperty] private int _probationDaysPassed;
    [ObservableProperty] private int _probationDaysTotal = 90;
    [ObservableProperty] private double _probationProgressPercent;
    [ObservableProperty] private int _recordedDaysCount;
    [ObservableProperty] private double _coverageRatePercent;
    [ObservableProperty] private string _probationStartDate = string.Empty;
    [ObservableProperty] private DateTime _calendarDate = DateTime.Now;
    [ObservableProperty] private string _probationInfo = string.Empty;

    // 工时趋势图表
    [ObservableProperty] private ISeries[] _chartSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _chartXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _chartYAxes = Array.Empty<Axis>();

    // 项目分布饼图
    [ObservableProperty] private ISeries[] _projectPieSeries = Array.Empty<ISeries>();

    // 亮点列表
    [ObservableProperty] private ObservableCollection<HighlightItem> _highlights = new();

    // 今日提醒
    [ObservableProperty] private bool _hasTodayRecords;
    [ObservableProperty] private string _todayReminderText = string.Empty;
    [ObservableProperty] private int _probationRemainingDays;
    [ObservableProperty] private string _coverageWarningText = string.Empty;
    [ObservableProperty] private bool _showCoverageWarning;

    public async Task RefreshAsync() => await LoadDashboardAsync();

    [RelayCommand]
    public async Task LoadDashboardAsync()
    {
        CurrentDate = DateTime.Now.ToString("yyyy-MM-dd dddd");

        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var weekStart = Helpers.DateTimeHelper.GetWeekStart(DateTime.Now).ToString("yyyy-MM-dd");
        var weekEnd = Helpers.DateTimeHelper.GetWeekEnd(DateTime.Now).ToString("yyyy-MM-dd");
        var monthStart = Helpers.DateTimeHelper.GetMonthStart(DateTime.Now).ToString("yyyy-MM-dd");
        var monthEnd = Helpers.DateTimeHelper.GetMonthEnd(DateTime.Now).ToString("yyyy-MM-dd");

        TodayHours = await _recordRepo.GetTotalHoursAsync(today, today);
        WeekHours = await _recordRepo.GetTotalHoursAsync(weekStart, weekEnd);
        MonthHours = await _recordRepo.GetTotalHoursAsync(monthStart, monthEnd);

        // 今日记录提醒
        HasTodayRecords = TodayHours > 0;
        TodayReminderText = HasTodayRecords
            ? $"今日已记录 {TodayHours:F1} 小时，继续保持！"
            : "今天还没有记录工作，点击「工作记录」开始记录吧！";

        var allRecords = (await _recordRepo.GetAllAsync()).ToList();
        TotalRecords = allRecords.Count;

        var allIssues = await _issueRepo.GetAllAsync();
        TotalIssues = allIssues.Count();

        var allKnowledge = await _knowledgeRepo.GetAllAsync();
        TotalKnowledge = allKnowledge.Count();

        // 最近5条工作记录
        var recent = allRecords.Take(5).Select(r => new RecentRecordItem
        {
            ProjectName = r.ProjectName,
            WorkType = r.WorkType,
            Content = r.Content,
            Hours = r.Hours,
            Progress = r.Progress,
            WorkDate = r.WorkDate
        }).ToList();
        RecentRecords = new ObservableCollection<RecentRecordItem>(recent);

        // 试用期进度
        await LoadProbationProgressAsync();

        // 工时趋势图表
        await LoadChartAsync();

        // 项目分布饼图
        await LoadProjectPieChartAsync();

        // 亮点列表
        await LoadHighlightsAsync();
    }

    private async Task LoadProbationProgressAsync()
    {
        var settings = ProbationSettings.Load();
        ProbationStartDate = settings.StartDate;
        ProbationDaysTotal = settings.DurationDays;

        if (!string.IsNullOrWhiteSpace(settings.StartDate) && DateTime.TryParse(settings.StartDate, out var start))
        {
            CalendarDate = start;
            var daysPassed = Math.Max(0, (DateTime.Now.Date - start.Date).Days);
            ProbationDaysPassed = Math.Min(daysPassed, settings.DurationDays);
            ProbationProgressPercent = settings.DurationDays > 0
                ? Math.Min(100, (double)ProbationDaysPassed / settings.DurationDays * 100)
                : 0;

            var end = start.AddDays(settings.DurationDays);
            var remaining = Math.Max(0, (end.Date - DateTime.Now.Date).Days);
            ProbationRemainingDays = remaining;
            ProbationInfo = $"{start:yyyy-MM-dd} ~ {end:yyyy-MM-dd}（剩余 {remaining} 天）";

            // 记录覆盖率：有记录的工作日占比
            var startDateStr = start.ToString("yyyy-MM-dd");
            var endDateStr = DateTime.Now.ToString("yyyy-MM-dd");
            var recordedDays = await _recordRepo.GetRecordedDaysCountAsync(startDateStr, endDateStr);
            RecordedDaysCount = recordedDays;

            // 计算工作日数（排除周末）
            var workingDays = 0;
            for (var d = start.Date; d <= DateTime.Now.Date && d <= end.Date; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                    workingDays++;
            }
            CoverageRatePercent = workingDays > 0
                ? Math.Min(100, (double)recordedDays / workingDays * 100)
                : 0;

            // 覆盖率提醒
            if (CoverageRatePercent < 60)
            {
                ShowCoverageWarning = true;
                CoverageWarningText = $"记录覆盖率仅 {CoverageRatePercent:F0}%，建议每天记录工作内容，转正述职时数据更充实。";
            }
            else if (CoverageRatePercent < 80)
            {
                ShowCoverageWarning = true;
                CoverageWarningText = $"记录覆盖率 {CoverageRatePercent:F0}%，接近完美！坚持每天记录，数据更完整。";
            }
            else
            {
                ShowCoverageWarning = false;
                CoverageWarningText = string.Empty;
            }
        }
        else
        {
            ProbationInfo = "请先设置试用期开始日期";
        }
    }

    private async Task LoadChartAsync()
    {
        var weekStart = Helpers.DateTimeHelper.GetWeekStart(DateTime.Now);
        var weekEnd = Helpers.DateTimeHelper.GetWeekEnd(DateTime.Now);
        var dailyStats = await _recordRepo.GetDailyStatsAsync(
            weekStart.ToString("yyyy-MM-dd"), weekEnd.ToString("yyyy-MM-dd"));

        var statsList = dailyStats.ToList();

        // 补全本周7天（包括没有记录的日期）
        var allDays = new Dictionary<string, double>();
        for (var d = weekStart; d <= weekEnd; d = d.AddDays(1))
        {
            allDays[d.ToString("MM/dd")] = 0;
        }
        foreach (var stat in statsList)
        {
            if (DateTime.TryParse((string)stat.WorkDate, out var date))
            {
                var key = date.ToString("MM/dd");
                if (allDays.ContainsKey(key))
                    allDays[key] = (double)stat.TotalHours;
            }
        }

        ChartSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = allDays.Values.ToArray(),
                Fill = new SolidColorPaint(new SKColor(79, 70, 229)),
                MaxBarWidth = 28,
                Rx = 4,
                Ry = 4
            }
        };

        ChartXAxes = new Axis[]
        {
            new Axis
            {
                Labels = allDays.Keys.ToArray(),
                LabelsRotation = 0
            }
        };

        ChartYAxes = new Axis[]
        {
            new Axis
            {
                Name = "工时(h)",
                MinLimit = 0
            }
        };
    }

    private async Task LoadProjectPieChartAsync()
    {
        var monthStart = Helpers.DateTimeHelper.GetMonthStart(DateTime.Now);
        var monthEnd = Helpers.DateTimeHelper.GetMonthEnd(DateTime.Now);
        var projectStats = await _recordRepo.GetProjectStatsAsync(
            monthStart.ToString("yyyy-MM-dd"), monthEnd.ToString("yyyy-MM-dd"));

        var statsList = projectStats.ToList();
        if (!statsList.Any())
        {
            ProjectPieSeries = Array.Empty<ISeries>();
            return;
        }

        var colors = new SKColor[]
        {
            new(79, 70, 229),   // Indigo
            new(16, 185, 129),  // Emerald
            new(245, 158, 11),  // Amber
            new(239, 68, 68),   // Red
            new(59, 130, 246),  // Blue
            new(168, 85, 247),  // Purple
            new(236, 72, 153),  // Pink
            new(20, 184, 166),  // Teal
        };

        var series = new List<ISeries>();
        for (int i = 0; i < statsList.Count; i++)
        {
            var stat = statsList[i];
            var color = colors[i % colors.Length];
            series.Add(new PieSeries<double>
            {
                Values = new[] { (double)stat.TotalHours },
                Name = (string)stat.ProjectName,
                Fill = new SolidColorPaint(color),
                Stroke = null,
            });
        }

        ProjectPieSeries = series.ToArray();
    }

    private async Task LoadHighlightsAsync()
    {
        var settings = ProbationSettings.Load();
        var startDate = !string.IsNullOrWhiteSpace(settings.StartDate)
            ? settings.StartDate
            : DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd");
        var endDate = DateTime.Now.ToString("yyyy-MM-dd");

        var highlightRecords = await _recordRepo.GetHighlightsAsync(startDate, endDate);
        Highlights = new ObservableCollection<HighlightItem>(
            highlightRecords.Take(10).Select(r => new HighlightItem
            {
                WorkDate = r.WorkDate,
                ProjectName = r.ProjectName,
                Note = !string.IsNullOrWhiteSpace(r.HighlightNote) ? r.HighlightNote : r.Content
            }));
    }

    [RelayCommand]
    private async Task GenerateProbationReportAsync()
    {
        var settings = ProbationSettings.Load();
        var startDate = !string.IsNullOrWhiteSpace(settings.StartDate)
            ? settings.StartDate
            : DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd");
        var endDate = DateTime.Now.ToString("yyyy-MM-dd");

        var service = new ReportService();
        ProbationReport = await service.GenerateProbationReportAsync(startDate, endDate);
    }

    [RelayCommand]
    private void CopyReport()
    {
        if (!string.IsNullOrWhiteSpace(ProbationReport))
            ExportService.CopyToClipboard(ProbationReport);
    }

    [RelayCommand]
    private void SaveReport()
    {
        if (!string.IsNullOrWhiteSpace(ProbationReport))
            ExportService.SaveToFile(ProbationReport, "转正述职");
    }

    [RelayCommand]
    private void SaveProbationStartDate(DateTime startDate)
    {
        var settings = ProbationSettings.Load();
        settings.StartDate = startDate.ToString("yyyy-MM-dd");
        settings.Save();
        _ = LoadDashboardAsync();
    }
}

public class RecentRecordItem
{
    public string WorkDate { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string WorkType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Hours { get; set; }
    public int Progress { get; set; }
}

public class HighlightItem
{
    public string WorkDate { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}
