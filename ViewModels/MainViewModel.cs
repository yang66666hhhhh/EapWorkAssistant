using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using System.Collections.ObjectModel;

namespace EapWorkAssistant.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly WorkRecordRepository _recordRepo;
    private readonly KnowledgeRepository _knowledgeRepo;
    private readonly IssueRepository _issueRepo;
    private readonly UiTimer _searchTimer;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private bool _isSearchOpen;

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _showInitial = true;

    [ObservableProperty]
    private bool _showNoResults;

    [ObservableProperty]
    private bool _showResults;

    [ObservableProperty]
    private ObservableCollection<SearchResultItem> _searchResults = new();

    /// <summary>命令面板当前高亮项索引（-1 = 无高亮）。由 View 的 ↑↓ 驱动，Enter 打开。</summary>
    [ObservableProperty]
    private int _selectedSearchIndex = -1;

    // 搜索结果分类计数（用于结果区顶部汇总）
    [ObservableProperty] private int _recordResultCount;
    [ObservableProperty] private int _knowledgeResultCount;
    [ObservableProperty] private int _issueResultCount;
    [ObservableProperty] private int _totalResultCount;
    [ObservableProperty] private bool _searchTruncated;

    private string _searchSummary = string.Empty;
    public string SearchSummary
    {
        get => _searchSummary;
        private set => SetProperty(ref _searchSummary, value);
    }

    private void ResetSearchStats()
    {
        RecordResultCount = KnowledgeResultCount = IssueResultCount = TotalResultCount = 0;
        SearchTruncated = false;
        SearchSummary = string.Empty;
    }

    public DashboardViewModel Dashboard { get; }
    public WorkRecordViewModel WorkRecord { get; }
    public AttendanceViewModel Attendance { get; }
    public KnowledgeViewModel Knowledge { get; }
    public IssueViewModel Issue { get; }
    public SettingsViewModel Settings { get; }
    public RecycleBinViewModel RecycleBin { get; }

    /// <summary>
    /// 无参构造：MainWindow.xaml 中的 <c>&lt;vm:MainViewModel/&gt;</c> 由 XAML 实例化，
    /// 必须保留。依赖与子 ViewModel 均从组合根（ServiceContainer）解析。
    /// </summary>
    public MainViewModel()
        : this(ServiceContainer.Get<WorkRecordRepository>(),
               ServiceContainer.Get<KnowledgeRepository>(),
               ServiceContainer.Get<IssueRepository>())
    { }

    /// <summary>显式注入构造：供单元测试传入 Fake 仓储。</summary>
    public MainViewModel(WorkRecordRepository recordRepo, KnowledgeRepository knowledgeRepo, IssueRepository issueRepo)
    {
        _recordRepo = recordRepo;
        _knowledgeRepo = knowledgeRepo;
        _issueRepo = issueRepo;

        // 子 ViewModel 由容器统一供给（单例），保证全应用只有一个实例，
        // 避免导航状态出现多份副本。
        Dashboard = ServiceContainer.Get<DashboardViewModel>();
        WorkRecord = ServiceContainer.Get<WorkRecordViewModel>();
        Attendance = ServiceContainer.Get<AttendanceViewModel>();
        Knowledge = ServiceContainer.Get<KnowledgeViewModel>();
        Issue = ServiceContainer.Get<IssueViewModel>();
        Settings = ServiceContainer.Get<SettingsViewModel>();
        RecycleBin = ServiceContainer.Get<RecycleBinViewModel>();

        _searchTimer = new UiTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await SearchAsync();
        };

        // 根据配置设置默认启动视图
        var defaultView = ConfigService.Instance.DefaultView;
        CurrentView = defaultView switch
        {
            ViewNames.WorkRecord => WorkRecord,
            ViewNames.Attendance => Attendance,
            ViewNames.Knowledge => Knowledge,
            ViewNames.Issue => Issue,
            ViewNames.Settings => Settings,
            ViewNames.RecycleBin => RecycleBin,
            _ => Dashboard
        };
        SelectedIndex = defaultView switch
        {
            ViewNames.WorkRecord => 1,
            ViewNames.Attendance => 6,
            ViewNames.Knowledge => 2,
            ViewNames.Issue => 3,
            ViewNames.Settings => 4,
            ViewNames.RecycleBin => 5,
                _ => 0
        };
        Dashboard.LoadDashboardAsync().SafeFire("加载仪表盘失败");

        // 订阅仪表盘图表点击导航事件
        Dashboard.NavigateToWorkRecord += (date) =>
        {
            WorkRecord.SelectedDate = date;
            WorkRecord.SelectedTabIndex = 0;
            NavigateTo(ViewNames.WorkRecord);
        };
        Dashboard.NavigateToWorkRecordFilter += (project) =>
        {
            WorkRecord.FilterProject = project;
            WorkRecord.SelectedTabIndex = 1;
            NavigateTo(ViewNames.WorkRecord);
        };
        Dashboard.NavigateToPage += (page) =>
        {
            NavigateTo(page);
        };

        // 出勤总览双击某天 → 跳到工作记录并预选该日期（未填行即「去补录」）
        Attendance.NavigateToWorkRecord += (date) =>
        {
            WorkRecord.SelectedDate = date;
            WorkRecord.SelectedTabIndex = 0;
            NavigateTo(ViewNames.WorkRecord);
        };
    }

    [RelayCommand]
    private void NavigateTo(string viewName)
    {
        object? targetView = viewName switch
        {
            ViewNames.WorkRecord => WorkRecord,
            ViewNames.Attendance => Attendance,
            ViewNames.Knowledge => Knowledge,
            ViewNames.Issue => Issue,
            ViewNames.Settings => Settings,
            ViewNames.RecycleBin => RecycleBin,
            _ => Dashboard
        };

        // 目标与当前视图相同：仅同步导航栏高亮，不拦截、不刷新
        if (ReferenceEquals(targetView, CurrentView))
        {
            SyncSelectedIndex(viewName);
            return;
        }

        // 离开前未保存确认：知识库/问题跟踪无自动保存；工作记录校验不过时。
        // 用户取消离开则还原导航栏选中项，保持当前视图不变。
        if (!CanLeaveCurrentView())
        {
            RevertSelectedIndex();
            return;
        }

        var previousView = CurrentView;
        CurrentView = targetView;
        SyncSelectedIndex(viewName);

        // 离开工作记录时暂停自动保存（会 flush 合法编辑），进入时恢复
        if (previousView == WorkRecord && targetView != WorkRecord)
            WorkRecord.PauseAutoSaveTimer();
        else if (targetView == WorkRecord && previousView != WorkRecord)
            WorkRecord.StartAutoSaveTimer();

        // 仅在视图实际切换时才刷新，避免重复加载
        if (targetView is IRefreshable refreshable)
            refreshable.RefreshAsync().SafeFire("刷新失败");
    }

    private void SyncSelectedIndex(string viewName)
    {
        var newIndex = viewName switch
        {
            ViewNames.WorkRecord => 1,
            ViewNames.Attendance => 6,
            ViewNames.Knowledge => 2,
            ViewNames.Issue => 3,
            ViewNames.Settings => 4,
            ViewNames.RecycleBin => 5,
            _ => 0
        };
        if (newIndex != SelectedIndex)
            SelectedIndex = newIndex;
    }

    /// <summary>检查即将离开的当前视图是否有未保存修改，决定是否允许导航。</summary>
    private bool CanLeaveCurrentView()
    {
        return CurrentView switch
        {
            WorkRecordViewModel wr => CanLeaveWorkRecord(wr),
            KnowledgeViewModel kw => !kw.IsFormDirty || !kw.HasUnsavedInput() || DialogService.Instance.ShowConfirm(
                "当前知识库页面有未保存的修改，离开后将丢失。确定离开吗？",
                "未保存的修改", ConfirmType.Warning),
            IssueViewModel iss => !iss.IsFormDirty || !iss.HasUnsavedInput() || DialogService.Instance.ShowConfirm(
                "当前问题跟踪页面有未保存的修改，离开后将丢失。确定离开吗？",
                "未保存的修改", ConfirmType.Warning),
            _ => true
        };
    }

    /// <summary>
    /// 工作记录离开确认。
    /// - 未编辑或空草稿：直接放行，避免「无脑提示」。
    /// - 有实际未保存输入：弹出确认，用户明确选择离开后才放行；可自动保存的数据会先 flush，避免丢失。
    /// </summary>
    private bool CanLeaveWorkRecord(WorkRecordViewModel wr)
    {
        if (!wr.IsFormDirty || !wr.HasUnsavedInput()) return true;

        var confirmed = DialogService.Instance.ShowConfirm(
            "当前工作记录有未保存的内容，离开后将丢失。确定离开吗？",
            "未保存的修改", ConfirmType.Warning);

        if (confirmed && wr.CanQuickSave())
            wr.FlushPendingChangesAsync().SafeFire("离开保存失败");

        return confirmed;
    }

    /// <summary>用户取消离开时，把导航栏选中项还原到当前实际视图，避免高亮错位。</summary>
    private void RevertSelectedIndex()
    {
        var idx = CurrentView switch
        {
            WorkRecordViewModel => 1,
            AttendanceViewModel => 6,
            KnowledgeViewModel => 2,
            IssueViewModel => 3,
            SettingsViewModel => 4,
            RecycleBinViewModel => 5,
            _ => 0
        };
        if (SelectedIndex != idx)
            SelectedIndex = idx;
    }

    [RelayCommand]
    private void OpenSearch()
    {
        IsSearchOpen = true;
        SearchKeyword = string.Empty;
        SearchResults.Clear();
        SelectedSearchIndex = -1;
        ResetSearchStats();
        ShowInitial = true;
        ShowNoResults = false;
        ShowResults = false;
        IsSearching = false;
    }

    [RelayCommand]
    private void CloseSearch()
    {
        _searchTimer.Stop();
        IsSearchOpen = false;
        SearchKeyword = string.Empty;
        SearchResults.Clear();
        SelectedSearchIndex = -1;
        ResetSearchStats();
        ShowInitial = true;
        ShowNoResults = false;
        ShowResults = false;
    }

    /// <summary>
    /// 移动命令面板高亮项（delta = +1 下移 / -1 上移，环绕）。
    /// 无高亮时：下移落到首项，上移落到末项。位移规则见 <see cref="SearchNavigationHelper"/>。
    /// </summary>
    [RelayCommand]
    private void MoveSearchSelection(int delta)
    {
        if (!ShowResults || SearchResults.Count == 0 || delta == 0) return;
        SelectedSearchIndex = SearchNavigationHelper.Move(SelectedSearchIndex, delta, SearchResults.Count);
    }

    /// <summary>
    /// 打开当前高亮项；无高亮时退回"立即搜索"（兼容用户敲完关键词直接回车的习惯）。
    /// </summary>
    [RelayCommand]
    private void ActivateSelectedResult()
    {
        if (ShowResults && SelectedSearchIndex >= 0 && SelectedSearchIndex < SearchResults.Count)
        {
            NavigateToResult(SearchResults[SelectedSearchIndex]);
            return;
        }

        SearchCommand.ExecuteAsync(null).SafeFire("搜索失败");
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            SearchResults.Clear();
            SelectedSearchIndex = -1;
            ResetSearchStats();
            ShowInitial = true;
            ShowNoResults = false;
            ShowResults = false;
            IsSearching = false;
            return;
        }

        IsSearching = true;
        ShowInitial = false;
        ShowNoResults = false;
        ShowResults = false;

        var results = new List<SearchResultItem>();
        var keyword = SearchKeyword.Trim();

        try
        {
            // 数据库级搜索（SQL LIKE，支持多关键词空格分隔）
            var records = await _recordRepo.SearchAsync(keyword);
            foreach (var r in records)
            {
                var content = r.Content ?? "";
                results.Add(new SearchResultItem
                {
                    Type = "工作记录",
                    Title = $"{r.ProjectName} - {r.WorkDate}",
                    Content = content.Length > 60 ? content[..60] + "..." : content,
                    Icon = "\U0001F4DD",
                    NavigateTo = ViewNames.WorkRecord,
                    TargetDate = DateTime.TryParse(r.WorkDate, out var d) ? d : null,
                    Keyword = keyword
                });
            }

            var knowledge = await _knowledgeRepo.SearchAsync(keyword);
            foreach (var k in knowledge)
            {
                var content = k.Content ?? "";
                results.Add(new SearchResultItem
                {
                    Type = "知识库",
                    Title = k.Title,
                    Content = content.Length > 60 ? content[..60] + "..." : content,
                    Icon = "\U0001F4DA",
                    NavigateTo = ViewNames.Knowledge,
                    TargetId = k.Id,
                    Keyword = keyword
                });
            }

            var issues = await _issueRepo.SearchAsync(keyword);
            foreach (var i in issues)
            {
                var content = i.Description ?? "";
                results.Add(new SearchResultItem
                {
                    Type = "问题跟踪",
                    Title = $"[{i.ProjectName}] {i.Title}",
                    Content = content.Length > 60 ? content[..60] + "..." : content,
                    Icon = "\U0001F527",
                    NavigateTo = ViewNames.Issue,
                    TargetId = i.Id,
                    Keyword = keyword
                });
            }
        }
        catch (Exception ex)
        {
            ToastService.Error($"搜索出错：{ex.Message}");
        }

        // 分类计数汇总（用于结果区顶部提示，并标识是否被截断）
        // 计数改用稳定的导航键 NavigateTo（ViewNames.* 常量），不依赖显示文案 Type——
        // 后者一旦在 UI 改文案，按文案计数就会静默算错。
        RecordResultCount = results.Count(r => r.NavigateTo == ViewNames.WorkRecord);
        KnowledgeResultCount = results.Count(r => r.NavigateTo == ViewNames.Knowledge);
        IssueResultCount = results.Count(r => r.NavigateTo == ViewNames.Issue);
        TotalResultCount = results.Count;
        SearchTruncated = TotalResultCount > 20;
        SearchSummary = TotalResultCount > 0
            ? $"工作 {RecordResultCount} · 知识 {KnowledgeResultCount} · 问题 {IssueResultCount}"
            : string.Empty;

        SearchResults = new ObservableCollection<SearchResultItem>(results.Take(20));
        IsSearching = false;

        // 结果就绪后默认高亮首项，Enter 即可直接打开（标准命令面板交互）
        SelectedSearchIndex = SearchResults.Count > 0 ? 0 : -1;

        if (SearchResults.Any())
        {
            ShowResults = true;
            ShowNoResults = false;
        }
        else
        {
            ShowResults = false;
            ShowNoResults = true;
        }
    }

    [RelayCommand]
    private void NavigateToResult(SearchResultItem? item)
    {
        if (item == null) return;
        _searchTimer.Stop();
        IsSearchOpen = false;
        SearchKeyword = string.Empty;

        // 工作记录：切换到全部记录 Tab 并用关键词筛选
        if (item.NavigateTo == ViewNames.WorkRecord)
        {
            if (item.TargetDate.HasValue)
                WorkRecord.SelectedDate = item.TargetDate.Value;
            WorkRecord.SearchKeyword = item.Keyword;  // 先设置关键词
            WorkRecord.SelectedTabIndex = 1;           // 再切 Tab，触发加载时已带关键词
        }

        NavigateTo(item.NavigateTo);

        // 知识库/问题跟踪：导航后设置搜索关键词，OnSearchKeywordChanged 防抖定时器会自动触发搜索
        // 不再手动调用 SearchCommand，避免与防抖定时器竞态导致双重搜索
        if (item.NavigateTo == ViewNames.Knowledge)
        {
            Knowledge.SearchKeyword = item.Keyword;
        }
        else if (item.NavigateTo == ViewNames.Issue)
        {
            Issue.SearchKeyword = item.Keyword;
        }
    }

    partial void OnSearchKeywordChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _searchTimer.Stop();
            SearchResults.Clear();
            SelectedSearchIndex = -1;
            ShowInitial = true;
            ShowNoResults = false;
            ShowResults = false;
            IsSearching = false;
        }
        else
        {
            // 关键词变化时清掉旧高亮，待新结果就绪后重新落到首项
            SelectedSearchIndex = -1;
            // 防抖：每次按键重启定时器，300ms 后自动搜索
            _searchTimer.Stop();
            _searchTimer.Start();
        }
    }

    partial void OnSelectedIndexChanged(int value)
    {
        // 索引 → 视图名映射，必须与 SyncSelectedIndex 及侧边栏 ConverterParameter 保持一致；
        // Attendance 固定为 6（追加制，插在中间会打乱既有索引）
        var views = new[] { ViewNames.Dashboard, ViewNames.WorkRecord, ViewNames.Knowledge, ViewNames.Issue, ViewNames.Settings, ViewNames.RecycleBin, ViewNames.Attendance };
        if (value >= 0 && value < views.Length)
            NavigateTo(views[value]);
    }
}

public class SearchResultItem
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string NavigateTo { get; set; } = string.Empty;
    public DateTime? TargetDate { get; set; }
    public int TargetId { get; set; }
    public string Keyword { get; set; } = string.Empty;
}
