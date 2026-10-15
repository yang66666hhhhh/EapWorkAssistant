using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Services;
using System.Collections.ObjectModel;

namespace EapWorkAssistant.ViewModels;

/// <summary>
/// 出勤总览 ViewModel：把工作记录/请假/节假日合并为「日状态」列表，
/// 支持按类型（正常上班/周末加班/假日加班/请假/未填/休息日）与时间段查询。
/// 数据全部来自 <see cref="DayStatusService"/>，本类只负责取数、筛选与展示状态。
/// </summary>
public partial class AttendanceViewModel : ObservableObject, IRefreshable
{
    private readonly DayStatusService _service;
    private readonly CompLeaveBalanceService _compLeaveService;

    /// <summary>当前区间全量日状态（筛选在此集合上客户端进行，避免反复查库）。</summary>
    private List<DayStatusItem> _allItems = new();

    public AttendanceViewModel(DayStatusService service, CompLeaveBalanceService compLeaveService)
    {
        _service = service;
        _compLeaveService = compLeaveService;
    }

    [ObservableProperty]
    private ObservableCollection<DayStatusItem> _items = new();

    /// <summary>类型筛选标签（与 View 中的 chips 一一对应）。</summary>
    [ObservableProperty]
    private string _filterTag = "全部";

    /// <summary>时间段预设：本月 / 本年 / 近三个月 / 自定义。</summary>
    [ObservableProperty]
    private string _rangePreset = "本月";

    [ObservableProperty]
    private DateTime? _startDate;

    [ObservableProperty]
    private DateTime? _endDate;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    // ===== 汇总统计（统计卡）=====
    [ObservableProperty] private int _normalDays;
    [ObservableProperty] private double _normalHours;
    /// <summary>正常上班的实际工时合计（与填报口径并存）</summary>
    [ObservableProperty] private double _actualNormalHours;
    [ObservableProperty] private int _weekendOvertimeDays;
    [ObservableProperty] private double _weekendOvertimeHours;
    /// <summary>周末加班的实际工时合计（走这条算调休）</summary>
    [ObservableProperty] private double _actualWeekendOvertimeHours;
    [ObservableProperty] private int _holidayOvertimeDays;
    [ObservableProperty] private double _holidayOvertimeHours;
    /// <summary>法定假日加班的实际工时合计</summary>
    [ObservableProperty] private double _actualHolidayOvertimeHours;
    [ObservableProperty] private int _leaveDays;
    [ObservableProperty] private double _leaveHours;
    [ObservableProperty] private int _unfilledDays;
    [ObservableProperty] private int _restDays;

    // ===== 可调休余额（口径与工作记录页一致：按实际工时的周末加班 - 已用调休）=====
    /// <summary>可用调休余额</summary>
    [ObservableProperty] private double _compLeaveAvailable;
    /// <summary>已使用调休工时</summary>
    [ObservableProperty] private double _compLeaveUsed;
    /// <summary>余额所在年份（按当前区间末年份的全年数据计算）</summary>
    [ObservableProperty] private int _compLeaveYear;
    /// <summary>余额卡副标题：把口径讲清楚（按实际工时算）</summary>
    [ObservableProperty] private string _compLeaveSubText = string.Empty;

    /// <summary>列表结果条数说明（如 "共 31 天 · 命中 8 条"）。</summary>
    [ObservableProperty]
    private string _resultSummary = string.Empty;

    // ===== P1 ⑦ 月度热力图（总览不跟筛选 chips 走，只跟时间段区间走）=====

    /// <summary>热力图格子（6 行 × 7 列，含首尾邻月补齐格）。</summary>
    [ObservableProperty]
    private ObservableCollection<HeatmapCell> _heatmapCells = new();

    /// <summary>当前展示月份（当月 1 日）。</summary>
    [ObservableProperty]
    private DateTime _heatmapMonth = DateTime.Today;

    /// <summary>热力图标题，如 "2026年10月"。</summary>
    [ObservableProperty]
    private string _heatmapTitle = string.Empty;

    /// <summary>能否翻到上一月（受区间首月限制）。</summary>
    [ObservableProperty]
    private bool _canPrevMonth;

    /// <summary>能否翻到下一月（受区间末月限制）。</summary>
    [ObservableProperty]
    private bool _canNextMonth;

    /// <summary>区间首月 / 末月（当月 1 日），热力图月份导航的边界。</summary>
    private DateTime _heatmapMinMonth = DateTime.Today;
    private DateTime _heatmapMaxMonth = DateTime.Today;

    // ===== 排序（跟随工作记录/问题库的既有范式：View 的 Sorting 事件 → ToggleSort）=====

    /// <summary>当前排序列（对应列的 SortMemberPath），默认按日期倒序（最近的在最上面）。</summary>
    [ObservableProperty]
    private string _sortColumn = "Date";

    /// <summary>当前排序方向：true=升序，false=降序。</summary>
    [ObservableProperty]
    private bool _sortAscending;

    /// <summary>每列的默认排序方向（首次点击该列时采用）。</summary>
    private static readonly Dictionary<string, bool> DefaultSortAscending = new()
    {
        ["Date"] = false, ["WorkHours"] = false,
        ["WorkKind"] = true, ["LeaveType"] = true, ["ContentSummary"] = true
    };

    /// <summary>切换排序：同列反转方向，异列套用默认方向（与工作记录页一致）。</summary>
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
        ApplyFilter();
    }

    /// <summary>可选的筛选标签（chips 数据源）。</summary>
    public string[] FilterTags { get; } = ["全部", "正常上班", "周末加班", "假日加班", "请假", "未填", "休息日"];

    /// <summary>可选的时间段预设。</summary>
    public string[] RangePresets { get; } = ["本月", "本年", "近三个月", "自定义"];

    /// <summary>
    /// 请求跳转到工作记录页并预选日期（由 MainViewModel 订阅后执行实际导航）。
    /// 双击某天时触发：未填的日子即「去补录」，已填的日子即「去看当天记录」。
    /// </summary>
    public event Action<DateTime>? NavigateToWorkRecord;

    /// <summary>双击某天 → 请求跳转工作记录（无记录的休息日也允许跳，方便手工补录）。</summary>
    public void OpenDay(DayStatusItem? item)
    {
        if (item is not null)
            NavigateToWorkRecord?.Invoke(item.Date);
    }

    /// <summary>进入页面 / 外部刷新时调用（IRefreshable）。</summary>
    public async Task RefreshAsync()
    {
        await LoadAsync();
    }

    /// <summary>按当前预设加载数据；自定义模式使用 StartDate/EndDate。</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return; // 防重：连点预设不重复加载
        IsLoading = true;
        try
        {
            var today = DateTime.Today;
            DateTime start;
            DateTime end;

            if (RangePreset == "自定义" && StartDate is { } s && EndDate is { } e)
            {
                start = s.Date;
                end = e.Date;
            }
            else
            {
                (start, end) = RangePreset switch
                {
                    "本年" => (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31)),
                    "近三个月" => (today.AddMonths(-3), today),
                    _ => (new DateTime(today.Year, today.Month, 1),
                          new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)))
                };
                // 预设模式下同步日期框，保持三者（预设/起/止）一致
                StartDate = start;
                EndDate = end;
            }

            _allItems = await _service.GetRangeAsync(start, end);
            BuildHeatmap();
            ApplySummary();
            ApplyFilter();
            await LoadCompLeaveAsync(end);
        }
        catch (Exception ex)
        {
            ToastService.Error($"加载出勤数据失败：{ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>切换时间段预设（由 View 的 chips 调用）。</summary>
    [RelayCommand]
    private Task SetRangeAsync(string preset)
    {
        RangePreset = preset;
        return LoadAsync();
    }

    /// <summary>应用自定义日期区间（DatePicker 选择后调用）。</summary>
    [RelayCommand]
    private Task ApplyCustomRangeAsync()
    {
        RangePreset = "自定义";
        return LoadAsync();
    }

    /// <summary>切换类型筛选（客户端过滤，不查库）。</summary>
    [RelayCommand]
    private void SetFilter(string tag)
    {
        FilterTag = tag;
        ApplyFilter();
    }

    /// <summary>统计卡点击下钻：正常上班/周末加班/假日加班/请假/未填 → 对应筛选。</summary>
    [RelayCommand]
    private void FilterByStat(string statKey)
    {
        FilterTag = statKey switch
        {
            "Normal" => "正常上班",
            "Weekend" => "周末加班",
            "Holiday" => "假日加班",
            "Leave" => "请假",
            "Unfilled" => "未填",
            _ => "全部"
        };
        ApplyFilter();
    }

    partial void OnSearchKeywordChanged(string value) => ApplyFilter();

    /// <summary>
    /// C2：导出当前筛选结果（Items）为 CSV 明细。
    /// 导出的是「所见即所得」——跟着类型 chips / 时间段 / 关键词走，方便按需给领导或存档。
    /// </summary>
    [RelayCommand]
    private void ExportCsv()
    {
        if (Items.Count == 0)
        {
            ToastService.Info("没有可导出的记录");
            return;
        }

        // 文件名带区间，便于多份导出区分（自定义区间用实际起止，预设区间同样已回填 StartDate/EndDate）
        var start = StartDate?.ToString("yyyyMMdd") ?? "all";
        var end = EndDate?.ToString("yyyyMMdd") ?? "now";
        if (ExportService.ExportDayStatusToCsv(Items, $"出勤明细_{start}_{end}"))
            ToastService.Success($"已导出 {Items.Count} 条明细");
    }

    /// <summary>刷新汇总统计卡。</summary>
    private void ApplySummary()
    {
        var s = DayStatusService.Summarize(_allItems);
        NormalDays = s.NormalWorkdays;
        NormalHours = s.NormalHours;
        ActualNormalHours = s.ActualNormalHours;
        WeekendOvertimeDays = s.WeekendOvertimeDays;
        WeekendOvertimeHours = s.WeekendOvertimeHours;
        ActualWeekendOvertimeHours = s.ActualWeekendOvertimeHours;
        HolidayOvertimeDays = s.HolidayOvertimeDays;
        HolidayOvertimeHours = s.HolidayOvertimeHours;
        ActualHolidayOvertimeHours = s.ActualHolidayOvertimeHours;
        LeaveDays = s.LeaveDays;
        LeaveHours = s.LeaveHours;
        UnfilledDays = s.UnfilledDays;
        RestDays = s.RestDays;
    }

    // ===== P1 ⑦ 月度热力图构建 =====

    /// <summary>
    /// 按当前区间重建热力图：月份边界取区间覆盖的首末月。
    /// 默认展示「今天所在月」（落在区间内时），否则退回区间末月——
    /// 「本年」区间延伸到 12 月，直接取末月会默认展示无数据的未来月份。
    /// </summary>
    private void BuildHeatmap()
    {
        if (_allItems.Count > 0)
        {
            var min = _allItems.Min(i => i.Date);
            var max = _allItems.Max(i => i.Date);
            _heatmapMinMonth = new DateTime(min.Year, min.Month, 1);
            _heatmapMaxMonth = new DateTime(max.Year, max.Month, 1);
        }
        else
        {
            var t = DateTime.Today;
            _heatmapMinMonth = new DateTime(t.Year, t.Month, 1);
            _heatmapMaxMonth = _heatmapMinMonth;
        }

        var todayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        HeatmapMonth = todayMonth >= _heatmapMinMonth && todayMonth <= _heatmapMaxMonth
            ? todayMonth
            : _heatmapMaxMonth;
        RefreshHeatmapCells();
    }

    /// <summary>按 <see cref="HeatmapMonth"/> 重建 6 行 × 7 列格子（周日开头，首尾用邻月灰格补齐）。</summary>
    private void RefreshHeatmapCells()
    {
        var first = new DateTime(HeatmapMonth.Year, HeatmapMonth.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(first.Year, first.Month);

        // 区间日状态按日期索引（GetRangeAsync 补全区间内每天；重复日期 TryAdd 防御，不抛异常）
        var map = new Dictionary<DateTime, DayStatusItem>();
        foreach (var it in _allItems)
            map.TryAdd(it.Date.Date, it);

        var cells = new List<HeatmapCell>(42);

        // 首部补齐：当月 1 日之前的上月末尾灰格（周日 = 0 开头）
        int leading = (int)first.DayOfWeek;
        for (int i = 0; i < leading; i++)
        {
            var d = first.AddDays(i - leading);
            cells.Add(new HeatmapCell { Day = d.Day, Date = d, IsCurrentMonth = false });
        }

        for (int day = 1; day <= daysInMonth; day++)
        {
            var d = new DateTime(first.Year, first.Month, day);
            map.TryGetValue(d, out var item);
            cells.Add(new HeatmapCell
            {
                Day = day,
                Date = d,
                IsCurrentMonth = true,
                IsToday = d == DateTime.Today,
                DotColor = item is null ? "" : ToDotColor(item),
                Tooltip = item?.TooltipText ?? $"{d:yyyy-MM-dd} {d:ddd} 无记录",
                Item = item
            });
        }

        // 尾部补齐到整周（42 格上限，任何月份都不会超）
        var next = first.AddDays(daysInMonth);
        while (cells.Count % 7 != 0 && cells.Count < 42)
        {
            cells.Add(new HeatmapCell { Day = next.Day, Date = next, IsCurrentMonth = false });
            next = next.AddDays(1);
        }

        HeatmapCells = new ObservableCollection<HeatmapCell>(cells);
        HeatmapTitle = $"{first:yyyy年M月}";
        CanPrevMonth = HeatmapMonth > _heatmapMinMonth;
        CanNextMonth = HeatmapMonth < _heatmapMaxMonth;
    }

    /// <summary>
    /// 日状态 → 热力图/日历圆点色标识。与 <c>CustomCalendar.CreateDayItem</c> 保持同一套优先级
    /// （ amber 假日加班 → green 工作日已填 → 请假类型 → blue 周末加班 → red 应上班未填 →
    /// purple 法定假日 → gray 普通周末；补班/假日判定先于周末）。
    /// internal 供单元测试直接覆盖（ExportService.BuildDayStatusCsv 同款约定）。
    /// </summary>
    internal static string ToDotColor(DayStatusItem i) => i.WorkKind switch
    {
        WorkKind.HolidayOvertime => "amber",
        WorkKind.Workday => "green",
        WorkKind.WeekendOvertime => i.HasLeave ? i.LeaveType! : "blue",
        _ => i.HasLeave ? i.LeaveType!
            : i.IsMakeupWorkday ? "red"
            : i.IsHolidayDate ? "purple"
            : i.IsWeekend ? "gray"
            : "red"
    };

    /// <summary>热力图翻上一月（边界内才生效）。</summary>
    [RelayCommand]
    private void PrevHeatmapMonth()
    {
        if (!CanPrevMonth) return;
        HeatmapMonth = HeatmapMonth.AddMonths(-1);
        RefreshHeatmapCells();
    }

    /// <summary>热力图翻下一月（边界内才生效）。</summary>
    [RelayCommand]
    private void NextHeatmapMonth()
    {
        if (!CanNextMonth) return;
        HeatmapMonth = HeatmapMonth.AddMonths(1);
        RefreshHeatmapCells();
    }

    /// <summary>点击热力图格子 → 请求跳转工作记录（复用列表双击的同一事件；区间外灰格无 Item 不响应）。</summary>
    public void OpenHeatmapDay(HeatmapCell? cell)
    {
        if (cell?.Item is { } item)
            NavigateToWorkRecord?.Invoke(item.Date);
    }

    /// <summary>
    /// 加载可调休余额。调休是**年度**概念，而本页区间可能是本月 / 近三个月，
    /// 故统一按「区间末日期所在年份」的全年数据计算，与工作记录页的调休余额保持同一口径：
    /// 余额 = 周末加班【填报工时】 - 已用调休（调休是公司给的权利，只能按公司认可的填报口径算；
    /// 实际工时通常更多，仅作参考、不参与余额计算）。
    /// 单独 try/catch：读余额失败不应连累出勤明细的展示。
    /// </summary>
    private async Task LoadCompLeaveAsync(DateTime rangeEnd)
    {
        var year = rangeEnd.Year;
        CompLeaveYear = year;
        try
        {
            var r = await _compLeaveService.CalculateAsync(year);
            CompLeaveAvailable = r.CompLeaveAvailable;
            CompLeaveUsed = r.CompLeaveUsed;
            CompLeaveSubText = $"{year} 年 · 周末加班填报 {r.WeekendOvertimeReportHours:F1}h · 已休 {r.CompLeaveUsed:F1}h";
        }
        catch (Exception ex)
        {
            CompLeaveAvailable = 0;
            CompLeaveUsed = 0;
            CompLeaveSubText = $"{year} 年 · 读取失败";
            ToastService.Error($"加载调休余额失败：{ex.Message}");
        }
    }

    /// <summary>按筛选标签 + 关键词过滤 _allItems 到 Items。</summary>
    private void ApplyFilter()
    {
        IEnumerable<DayStatusItem> query = _allItems;

        query = FilterTag switch
        {
            "正常上班" => query.Where(i => i.WorkKind == WorkKind.Workday),
            "周末加班" => query.Where(i => i.WorkKind == WorkKind.WeekendOvertime),
            "假日加班" => query.Where(i => i.WorkKind == WorkKind.HolidayOvertime),
            "请假" => query.Where(i => i.HasLeave),
            "未填" => query.Where(i => i.IsUnfilled),
            "休息日" => query.Where(i => i.IsRestDay),
            _ => query
        };

        var keyword = SearchKeyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(i =>
                (i.ContentSummary?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.LeaveType?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                i.Date.ToString("yyyy-MM-dd").Contains(keyword, StringComparison.OrdinalIgnoreCase));

        // 排序（内存排序：区间数据量小，无需回库；列键与列的 SortMemberPath 一一对应）
        query = SortColumn switch
        {
            "WorkHours" => SortAscending ? query.OrderBy(i => i.WorkHours) : query.OrderByDescending(i => i.WorkHours),
            "WorkKind" => SortAscending ? query.OrderBy(i => i.WorkKind) : query.OrderByDescending(i => i.WorkKind),
            "LeaveType" => SortAscending
                ? query.OrderBy(i => i.LeaveType ?? string.Empty, StringComparer.Ordinal)
                : query.OrderByDescending(i => i.LeaveType ?? string.Empty, StringComparer.Ordinal),
            "ContentSummary" => SortAscending
                ? query.OrderBy(i => i.ContentSummary ?? string.Empty, StringComparer.Ordinal)
                : query.OrderByDescending(i => i.ContentSummary ?? string.Empty, StringComparer.Ordinal),
            _ => SortAscending ? query.OrderBy(i => i.Date) : query.OrderByDescending(i => i.Date)
        };

        var result = query.ToList();
        Items = new ObservableCollection<DayStatusItem>(result);
        IsEmpty = result.Count == 0;
        ResultSummary = _allItems.Count == result.Count
            ? $"共 {_allItems.Count} 天"
            : $"区间 {_allItems.Count} 天 · 命中 {result.Count} 条";
    }
}
