using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;

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

    // ===== AI 工作总结报告 =====
    public List<string> AiTimeRangeOptions { get; } = new() { "今天", "本周", "本月", "自定义" };
    [ObservableProperty] private string _aiReportTimeRange = "本月";
    [ObservableProperty] private string _aiReportStartDate = DateTimeHelper.GetMonthStart(DateTime.Now).ToString("yyyy-MM-dd");
    [ObservableProperty] private string _aiReportEndDate = DateTime.Now.ToString("yyyy-MM-dd");
    [ObservableProperty] private bool _isGeneratingAiReport;
    [ObservableProperty] private bool _canGenerateAiReport = true;
    [ObservableProperty] private string _aiReportStatus = string.Empty;
    [ObservableProperty] private ObservableCollection<RecentRecordItem> _recentRecords = new();
    [ObservableProperty] private RecentRecordItem? _selectedRecentRecord;

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

    // 图表数据状态
    public bool HasChartData => ChartSeries != null && ChartSeries.Length > 0;
    public bool HasPieData => ProjectPieSeries != null && ProjectPieSeries.Length > 0;

    // 工时趋势图表区间：周 / 月 / 季
    [ObservableProperty]
    private string _chartRange = "周";

    [ObservableProperty]
    private string _chartTrendTitle = "本周工时趋势";

    [ObservableProperty]
    private string _chartPieTitle = "本月项目分布";

    public string[] ChartRanges => ["周", "月", "季"];

    [RelayCommand]
    private async Task SetChartRangeAsync(string range)
    {
        if (ChartRange == range) return;
        ChartRange = range;
        await LoadChartAsync();
        await LoadProjectPieChartAsync();
    }

    /// <summary>
    /// 根据当前选择的区间返回统计起止日期。
    /// </summary>
    private (DateTime Start, DateTime End) GetChartRangeDates()
    {
        var now = DateTime.Now;
        return ChartRange switch
        {
            "月" => (Helpers.DateTimeHelper.GetMonthStart(now), Helpers.DateTimeHelper.GetMonthEnd(now)),
            "季" => Helpers.DateTimeHelper.GetQuarterRange(now),
            _ => (Helpers.DateTimeHelper.GetWeekStart(now), Helpers.DateTimeHelper.GetWeekEnd(now))
        };
    }

    // 亮点列表
    [ObservableProperty] private ObservableCollection<HighlightItem> _highlights = new();

    // 今日提醒
    [ObservableProperty] private bool _hasTodayRecords;
    [ObservableProperty] private string _todayReminderText = string.Empty;
    [ObservableProperty] private int _probationRemainingDays;
    [ObservableProperty] private string _coverageWarningText = string.Empty;
    [ObservableProperty] private bool _showCoverageWarning;

    // 身份类型
    public bool IsProbation => ProfileService.Instance.IsProbation;

    // Dashboard 布局可见性（从配置读取）
    public bool ShowDashStats => ConfigService.Instance.ShowDashStats;
    public bool ShowDashReminder => ConfigService.Instance.ShowDashReminder;
    public bool ShowDashProbation => ConfigService.Instance.ShowDashProbation && IsProbation;
    public bool ShowDashCharts => ConfigService.Instance.ShowDashCharts;
    public bool ShowDashHighlights => ConfigService.Instance.ShowDashHighlights;
    public bool ShowDashRecent => ConfigService.Instance.ShowDashRecent;

    public async Task RefreshAsync() => await LoadDashboardAsync();

    [RelayCommand]
    public async Task LoadDashboardAsync()
    {
        CurrentDate = DateTime.Now.ToString("yyyy-MM-dd dddd");
        OnPropertyChanged(nameof(IsProbation));
        OnPropertyChanged(nameof(ShowDashStats));
        OnPropertyChanged(nameof(ShowDashReminder));
        OnPropertyChanged(nameof(ShowDashProbation));
        OnPropertyChanged(nameof(ShowDashCharts));
        OnPropertyChanged(nameof(ShowDashHighlights));
        OnPropertyChanged(nameof(ShowDashRecent));

        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var weekStart = Helpers.DateTimeHelper.GetWeekStart(DateTime.Now).ToString("yyyy-MM-dd");
        var weekEnd = Helpers.DateTimeHelper.GetWeekEnd(DateTime.Now).ToString("yyyy-MM-dd");
        var monthStart = Helpers.DateTimeHelper.GetMonthStart(DateTime.Now).ToString("yyyy-MM-dd");
        var monthEnd = Helpers.DateTimeHelper.GetMonthEnd(DateTime.Now).ToString("yyyy-MM-dd");

        TodayHours = await _recordRepo.GetTotalHoursAsync(today, today);
        WeekHours = await _recordRepo.GetTotalHoursAsync(weekStart, weekEnd);
        MonthHours = await _recordRepo.GetTotalHoursAsync(monthStart, monthEnd);

        // 今日记录提醒（休息日显示不同提示）
        HasTodayRecords = TodayHours > 0;
        var isRestDay = ConfigService.Instance.IsTodayRestDay;
        if (isRestDay)
        {
            var dayName = new[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" }[(int)DateTime.Now.DayOfWeek];
            TodayReminderText = $"今天是{dayName}（休息日），好好休息，下个工作日继续记录吧！";
        }
        else
        {
            TodayReminderText = HasTodayRecords
                ? $"今日已记录 {TodayHours:F1} 小时，继续保持！"
                : "今天还没有记录工作，点击「工作记录」开始记录吧！";
        }

        // 计数走轻量 COUNT(*) 查询，避免为几个数字全表拉取
        TotalRecords = await _recordRepo.GetTotalCountAsync();
        TotalIssues = await _issueRepo.GetTotalCountAsync();
        TotalKnowledge = await _knowledgeRepo.GetTotalCountAsync();

        // 最近5条工作记录（只取所需的 5 条，而非全表载入）
        var recent = (await _recordRepo.GetRecentAsync(5)).Select(r => new RecentRecordItem
        {
            ProjectName = r.ProjectName,
            WorkType = r.WorkType,
            Content = r.Content,
            Achievement = r.Achievement,
            Hours = r.Hours,
            Progress = r.Progress,
            Problem = r.Problem,
            WorkDate = r.WorkDate
        }).ToList();
        RecentRecords = new ObservableCollection<RecentRecordItem>(recent);

        // 试用期进度（仅试用期加载，正式员工清零）
        if (ProfileService.Instance.IsProbation)
        {
            await LoadProbationProgressAsync();
        }
        else
        {
            ProbationStartDate = string.Empty;
            ProbationDaysPassed = 0;
            ProbationDaysTotal = 0;
            ProbationProgressPercent = 0;
            ProbationRemainingDays = 0;
            ProbationInfo = string.Empty;
            CoverageRatePercent = 0;
            RecordedDaysCount = 0;
            ShowCoverageWarning = false;
            CoverageWarningText = string.Empty;
            ProbationReport = string.Empty;
        }

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

            // 计算工作日数（排除配置的休息日）
            var restDays = ConfigService.Instance.RestDays;
            var workingDays = 0;
            for (var d = start.Date; d <= DateTime.Now.Date && d <= end.Date; d = d.AddDays(1))
            {
                if (!restDays.Contains((int)d.DayOfWeek))
                    workingDays++;
            }
            CoverageRatePercent = workingDays > 0
                ? Math.Min(100, (double)recordedDays / workingDays * 100)
                : 0;

            // 覆盖率提醒（仅试用期，休息日不显示）
            if (ProfileService.Instance.IsProbation && !ConfigService.Instance.IsTodayRestDay)
            {
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
        var (start, end) = GetChartRangeDates();
        var dailyStats = await _recordRepo.GetDailyStatsAsync(
            start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));

        var statsList = dailyStats.ToList();

        // 补全区间内每一天（包括没有记录的日期）
        var allDays = new Dictionary<string, double>();
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
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

        ChartTrendTitle = ChartRange switch
        {
            "月" => "本月工时趋势",
            "季" => "本季工时趋势",
            _ => "本周工时趋势"
        };

        OnPropertyChanged(nameof(HasChartData));
    }

    private async Task LoadProjectPieChartAsync()
    {
        var (start, end) = GetChartRangeDates();
        var projectStats = await _recordRepo.GetProjectStatsAsync(
            start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));

        var statsList = projectStats.ToList();
        if (!statsList.Any())
        {
            ProjectPieSeries = Array.Empty<ISeries>();
            OnPropertyChanged(nameof(HasPieData));
        }

        ChartPieTitle = ChartRange switch
        {
            "月" => "本月项目分布",
            "季" => "本季项目分布",
            _ => "本周项目分布"
        };

        if (!statsList.Any()) return;

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
        OnPropertyChanged(nameof(HasPieData));
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

        // 防护：如果起始日期 >= 今天（误操作导致），自动回退到最早的工作记录日期
        if (DateTime.TryParse(startDate, out var startDt) && startDt.Date >= DateTime.Now.Date)
        {
            var allRecords = (await _recordRepo.GetAllAsync()).ToList();
            if (allRecords.Any())
            {
                var validDates = allRecords
                    .Where(r => !string.IsNullOrWhiteSpace(r.WorkDate) && DateTime.TryParse(r.WorkDate, out _))
                    .Select(r => DateTime.Parse(r.WorkDate))
                    .ToList();
                if (validDates.Any())
                {
                    startDate = validDates.Min().ToString("yyyy-MM-dd");
                }
            }
        }

        var service = ReportService.Instance;
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

    // ===== AI 报告：时间范围切换自动计算日期 =====
    partial void OnAiReportTimeRangeChanged(string value)
    {
        var today = DateTime.Now;
        switch (value)
        {
            case "今天":
                AiReportStartDate = today.ToString("yyyy-MM-dd");
                AiReportEndDate = today.ToString("yyyy-MM-dd");
                break;
            case "本周":
                AiReportStartDate = Helpers.DateTimeHelper.GetWeekStart(today).ToString("yyyy-MM-dd");
                AiReportEndDate = Helpers.DateTimeHelper.GetWeekEnd(today).ToString("yyyy-MM-dd");
                break;
            case "本月":
                AiReportStartDate = Helpers.DateTimeHelper.GetMonthStart(today).ToString("yyyy-MM-dd");
                AiReportEndDate = today.ToString("yyyy-MM-dd");
                break;
            // "自定义" 不自动计算，用户手动选择
        }
    }

    // ===== AI 报告：生成命令 =====
    [RelayCommand]
    private async Task GenerateAiReportAsync()
    {
        // 1. 校验 AI 配置
        var aiSettings = AiSettings.Load();
        if (!aiSettings.IsConfigured)
        {
            ToastService.Info("请先在设置中配置 AI 服务（API 地址和密钥）");
            return;
        }

        // 2. 进入生成状态
        IsGeneratingAiReport = true;
        CanGenerateAiReport = false;
        AiReportStatus = "正在收集工作数据...";

        try
        {
            // 3. 收集工作数据
            var workData = await GatherWorkDataAsync(AiReportStartDate, AiReportEndDate);
            if (string.IsNullOrWhiteSpace(workData))
            {
                ToastService.Error("所选日期范围内暂无工作记录，请先添加工作记录。");
                return;
            }

            // 4. 调用 AI 生成报告
            AiReportStatus = "正在调用 AI 生成报告...";
            var aiService = AiService.Instance;
            var systemPrompt = BuildSystemPrompt();
            var userMessage = BuildUserMessage(AiReportStartDate, AiReportEndDate, workData);
            var aiContent = await aiService.SendChatAsync(systemPrompt, userMessage);

            // 5. 生成 DOCX 文件
            AiReportStatus = "正在生成 Word 文档...";
            var docxService = DocxReportService.Instance;
            var dateRange = $"{AiReportStartDate} ~ {AiReportEndDate}";
            var tempPath = docxService.GenerateDocx("AI 工作总结报告", dateRange, aiContent);

            // 6. 弹出保存对话框
            AiReportStatus = "请保存文档...";
            var saved = DocxReportService.SaveDocxFile(tempPath, "AI工作总结");

            if (saved)
            {
                ToastService.Success("AI 工作总结报告已生成并保存");
                AiReportStatus = "报告已生成";
            }
            else
            {
                AiReportStatus = "已取消保存";
            }

            // 清理临时文件
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
        catch (InvalidOperationException ex)
        {
            ToastService.Error(ex.Message);
            AiReportStatus = "生成失败";
        }
        catch (TaskCanceledException ex)
        {
            ToastService.Error(ex.Message);
            AiReportStatus = "请求超时";
        }
        catch (HttpRequestException ex)
        {
            ToastService.Error(ex.Message);
            AiReportStatus = "请求失败";
        }
        catch (Exception ex)
        {
            ToastService.Error($"生成报告失败：{ex.Message}");
            AiReportStatus = "生成失败";
        }
        finally
        {
            IsGeneratingAiReport = false;
            CanGenerateAiReport = true;
        }
    }

    /// <summary>
    /// 收集指定日期范围内的工作数据，构建给 AI 的原始文本
    /// </summary>
    private async Task<string> GatherWorkDataAsync(string startDate, string endDate)
    {
        var records = (await _recordRepo.GetByDateRangeAsync(startDate, endDate)).ToList();
        if (!records.Any()) return string.Empty;

        var sb = new System.Text.StringBuilder();

        // 工作记录明细
        sb.AppendLine("=== 工作记录明细 ===");
        foreach (var r in records)
        {
            sb.AppendLine($"日期: {r.WorkDate} | 项目: {r.ProjectName} | 类型: {r.WorkType} | 工时: {r.Hours}h");
            sb.AppendLine($"  内容: {r.Content}");
            if (!string.IsNullOrWhiteSpace(r.Achievement))
                sb.AppendLine($"  成果: {r.Achievement}");
            if (!string.IsNullOrWhiteSpace(r.Problem))
                sb.AppendLine($"  问题: {r.Problem}");
            if (!string.IsNullOrWhiteSpace(r.Solution))
                sb.AppendLine($"  解决方案: {r.Solution}");
            if (r.IsHighlight == 1)
                sb.AppendLine($"  ★ 亮点: {(!string.IsNullOrWhiteSpace(r.HighlightNote) ? r.HighlightNote : r.Content)}");
            sb.AppendLine();
        }

        // 统计概览
        var totalHours = records.Sum(r => r.Hours);
        var workDays = records.Select(r => r.WorkDate).Distinct().Count();
        var highlightCount = records.Count(r => r.IsHighlight == 1);

        sb.AppendLine("=== 统计概览 ===");
        sb.AppendLine($"总工时: {totalHours:F1} 小时");
        sb.AppendLine($"工作天数: {workDays} 天");
        sb.AppendLine($"记录条数: {records.Count} 条");
        sb.AppendLine($"工作亮点: {highlightCount} 个");

        // 项目分布
        var projectStats = (await _recordRepo.GetProjectStatsAsync(startDate, endDate)).ToList();
        if (projectStats.Any())
        {
            sb.AppendLine("\n=== 项目投入分布 ===");
            foreach (var ps in projectStats)
                sb.AppendLine($"  {ps.ProjectName}: {ps.TotalHours}h ({ps.cnt}条记录)");
        }

        // 类型分布
        var typeStats = (await _recordRepo.GetTypeStatsAsync(startDate, endDate)).ToList();
        if (typeStats.Any())
        {
            sb.AppendLine("\n=== 工作类型分布 ===");
            foreach (var ts in typeStats)
                sb.AppendLine($"  {ts.WorkType}: {ts.TotalHours}h ({ts.cnt}条记录)");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 构建 AI 系统提示词（角色 + 格式要求 + 用户信息）
    /// </summary>
    private string BuildSystemPrompt()
    {
        var profile = ProfileService.Instance;
        return $"""
            你是一位专业的工作总结撰写助手，擅长将零散的工作记录整理为结构清晰、语言精炼的专业工作总结报告。

            ## 用户信息
            - 姓名：{profile.Name}
            - 角色：{profile.Role}
            - 部门：{profile.Department}
            - 行业：{profile.Industry}
            - 工作方向：{profile.Focus}

            ## 输出要求
            请使用 Markdown 格式输出，包含以下五个章节（每个章节以 ## 开头）：

            ## 一、工作概述
            简要概括本阶段的主要工作内容和整体工作状态，2-3 段文字。

            ## 二、重点工作成果
            提炼出 3-5 项重点工作成果，每项包含成果描述和具体贡献。使用有序列表。

            ## 三、项目投入分析
            基于项目和工作类型的工时分布数据，分析工作重心和投入比例。可以引用具体数据。

            ## 四、问题与解决方案
            归纳遇到的问题及对应的解决方案，展现问题解决能力。如果数据中没有明确问题，可以从工作内容中合理推断潜在挑战。

            ## 五、下一步工作计划
            基于当前工作进展和行业背景，提出 3-5 条具体可行的后续工作建议。

            ## 约束
            - 严格基于提供的数据撰写，不编造不存在的工作内容
            - 语言专业精炼，适合提交给领导审阅
            - 合理归纳同类工作，避免逐条罗列
            - 突出工作亮点和价值贡献
            """;
    }

    /// <summary>
    /// 构建给 AI 的用户消息（包含日期范围和原始工作数据）
    /// </summary>
    private static string BuildUserMessage(string startDate, string endDate, string workData)
    {
        return $"请根据以下 {startDate} ~ {endDate} 期间的工作数据，生成一份专业的工作总结报告：\n\n{workData}";
    }

    [RelayCommand]
    private void SaveProbationStartDate(DateTime startDate)
    {
        // 校验：转正开始日期不能是今天或未来日期
        if (startDate.Date >= DateTime.Now.Date)
        {
            ToastService.Info(
                $"请选择入职日期（早于 {DateTime.Now:yyyy-MM-dd}），不能设为今天或未来的日期。",
                "日期校验");
            return;
        }

        var settings = ProbationSettings.Load();
        settings.StartDate = startDate.ToString("yyyy-MM-dd");
        settings.Save();
        LoadDashboardAsync().SafeFire("加载仪表盘数据失败");
    }

    /// <summary>柱状图点击：跳转到对应日期的工作记录</summary>
    [RelayCommand]
    private void ChartPointClick(IEnumerable<ChartPoint>? points)
    {
        var point = points?.FirstOrDefault();
        if (point == null) return;
        var index = point.Index;
        var weekStart = Helpers.DateTimeHelper.GetWeekStart(DateTime.Now);
        var targetDate = weekStart.AddDays(index);
        // 通过事件通知 MainViewModel 导航
        NavigateToWorkRecord?.Invoke(targetDate);
    }

    /// <summary>饼图点击：跳转到对应项目的全部记录</summary>
    [RelayCommand]
    private void PieChartClick(IEnumerable<ChartPoint>? points)
    {
        var point = points?.FirstOrDefault();
        if (point == null) return;
        // 通过索引从饼图系列中获取项目名称
        var index = point.Index;
        if (index >= 0 && index < ProjectPieSeries.Length)
        {
            var series = ProjectPieSeries[index];
            var projectName = series.Name;
            if (!string.IsNullOrEmpty(projectName))
                NavigateToWorkRecordFilter?.Invoke(projectName);
        }
    }

    /// <summary>请求导航到工作记录页面（由 MainViewModel 订阅）</summary>
    public event Action<DateTime>? NavigateToWorkRecord;

    /// <summary>请求导航到工作记录并按项目筛选</summary>
    public event Action<string>? NavigateToWorkRecordFilter;

    /// <summary>请求导航到指定页面（由 MainViewModel 订阅）</summary>
    public event Action<string>? NavigateToPage;

    /// <summary>触发导航到工作记录（供外部调用）</summary>
    public void RaiseNavigateToWorkRecord(DateTime date) => NavigateToWorkRecord?.Invoke(date);

    /// <summary>触发导航到指定页面（供外部调用）</summary>
    public void RaiseNavigateToPage(string page) => NavigateToPage?.Invoke(page);
}

public class RecentRecordItem
{
    public string WorkDate { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string WorkType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Achievement { get; set; } = string.Empty;
    public double Hours { get; set; }
    public int Progress { get; set; }
    public string Problem { get; set; } = string.Empty;
}

public class HighlightItem
{
    public string WorkDate { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}
