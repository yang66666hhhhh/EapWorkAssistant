using EapWorkAssistant.Helpers;
using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EapWorkAssistant.Views;

public partial class DashboardView : UserControl
{
    private PropertyChangedEventHandler? _profileHandler;

    /// <summary>日历浮窗目标：区分试用期日期 / AI 报告开始 / AI 报告结束</summary>
    private enum CalendarTarget { ProbationStart, AiReportStart, AiReportEnd }
    private CalendarTarget _calendarTarget = CalendarTarget.ProbationStart;

    public DashboardView()
    {
        InitializeComponent();

        // 图表入场动画时长并入 Duration 令牌（P1 ⑥）：
        // Duration 令牌是 WPF Duration 类型，ViewModel 不可引用（红线 6），故在视图层读取后赋给图表控件。
        if (TryFindResource("DurationNormal") is Duration chartAnim)
        {
            TrendChart.AnimationsSpeed = chartAnim.TimeSpan;
            ProjectPie.AnimationsSpeed = chartAnim.TimeSpan;
        }
        // 缓动与 DesignTokens 的 EaseStandard（CubicEase EaseOut：快出慢收、无回弹）保持一致
        TrendChart.EasingFunction = t => (float)(1 - Math.Pow(1 - t, 3));
        ProjectPie.EasingFunction = t => (float)(1 - Math.Pow(1 - t, 3));

        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => SyncDates();
        CustomCal.SelectedDateChanged += OnCalendarDateChanged;
        MainScroller.ScrollChanged += (_, _) =>
            BackToTopBtn.Visibility = MainScroller.VerticalOffset > 40 ? Visibility.Visible : Visibility.Collapsed;

        // 监听个人资料变更，身份切换时立即刷新仪表盘
        _profileHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(ProfileService.IsProbation)
                && DataContext is DashboardViewModel vm)
            {
                vm.LoadDashboardAsync().SafeFire("加载仪表盘失败");
            }
        };
        ProfileService.Instance.PropertyChanged += _profileHandler;

        // 视图卸载时解绑单例事件，避免 DashboardView 实例被单例 ProfileService 持有而无法回收
        Unloaded += (_, _) =>
        {
            if (_profileHandler != null)
                ProfileService.Instance.PropertyChanged -= _profileHandler;
        };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SyncDates();
    }

    private void SyncDates()
    {
        if (DataContext is DashboardViewModel vm)
        {
            ProbDateText.Text = vm.ProbationStartDate;
            AiStartDateText.Text = vm.AiReportStartDate;
            AiEndDateText.Text = vm.AiReportEndDate;

            // 临时取消事件订阅，防止初始同步触发回调
            CustomCal.SelectedDateChanged -= OnCalendarDateChanged;
            CustomCal.SelectedDate = vm.CalendarDate;
            CustomCal.SelectedDateChanged += OnCalendarDateChanged;
        }
    }

    private void OnCalendarDateChanged(object? sender, DateTime date)
    {
        if (DataContext is DashboardViewModel vm)
        {
            switch (_calendarTarget)
            {
                case CalendarTarget.ProbationStart:
                    vm.SaveProbationStartDateCommand.Execute(date);
                    ProbDateText.Text = date.ToString("yyyy-MM-dd");
                    break;

                case CalendarTarget.AiReportStart:
                    vm.AiReportStartDate = date.ToString("yyyy-MM-dd");
                    AiStartDateText.Text = date.ToString("yyyy-MM-dd");
                    break;

                case CalendarTarget.AiReportEnd:
                    vm.AiReportEndDate = date.ToString("yyyy-MM-dd");
                    AiEndDateText.Text = date.ToString("yyyy-MM-dd");
                    break;
            }

            CustomCal.SyncDisplay();
            CloseCalendar();
        }
    }

    // ===== 试用期开始日期 =====
    private void ProbDateBtn_Click(object sender, MouseButtonEventArgs e)
    {
        if (CalendarContainer.Visibility == Visibility.Visible)
        {
            CloseCalendar();
        }
        else
        {
            _calendarTarget = CalendarTarget.ProbationStart;
            if (DataContext is DashboardViewModel vm)
            {
                CustomCal.SelectedDateChanged -= OnCalendarDateChanged;
                CustomCal.SelectedDate = vm.CalendarDate;
                CustomCal.SyncDisplay();
                CustomCal.SelectedDateChanged += OnCalendarDateChanged;
            }
            CalendarHelper.Show(CalendarBackdrop, CalendarContainer, ProbDateBtn, this);
        }
    }

    // ===== AI 报告：开始日期 =====
    private void AiStartDateBtn_Click(object sender, MouseButtonEventArgs e)
    {
        if (CalendarContainer.Visibility == Visibility.Visible)
        {
            CloseCalendar();
        }
        else
        {
            _calendarTarget = CalendarTarget.AiReportStart;
            if (DataContext is DashboardViewModel vm && DateTime.TryParse(vm.AiReportStartDate, out var dt))
            {
                CustomCal.SelectedDateChanged -= OnCalendarDateChanged;
                CustomCal.SelectedDate = dt;
                CustomCal.SyncDisplay();
                CustomCal.SelectedDateChanged += OnCalendarDateChanged;
            }
            CalendarHelper.Show(CalendarBackdrop, CalendarContainer, AiStartDateBtn, this);
        }
    }

    // ===== AI 报告：结束日期 =====
    private void AiEndDateBtn_Click(object sender, MouseButtonEventArgs e)
    {
        if (CalendarContainer.Visibility == Visibility.Visible)
        {
            CloseCalendar();
        }
        else
        {
            _calendarTarget = CalendarTarget.AiReportEnd;
            if (DataContext is DashboardViewModel vm && DateTime.TryParse(vm.AiReportEndDate, out var dt))
            {
                CustomCal.SelectedDateChanged -= OnCalendarDateChanged;
                CustomCal.SelectedDate = dt;
                CustomCal.SyncDisplay();
                CustomCal.SelectedDateChanged += OnCalendarDateChanged;
            }
            CalendarHelper.Show(CalendarBackdrop, CalendarContainer, AiEndDateBtn, this);
        }
    }

    private void CalendarBackdrop_Click(object sender, MouseButtonEventArgs e)
    {
        CloseCalendar();
    }

    private void CloseCalendar()
    {
        CalendarHelper.Close(CalendarBackdrop, CalendarContainer);
    }

    // ===== 统计卡片点击导航 =====
    private void StatCard_Today_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
            vm.RaiseNavigateToWorkRecord(DateTime.Now);
    }

    private void StatCard_Week_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
            vm.RaiseNavigateToPage(ViewNames.WorkRecord);
    }

    private void StatCard_Month_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
            vm.RaiseNavigateToPage(ViewNames.WorkRecord);
    }

    private void StatCard_Records_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
            vm.RaiseNavigateToPage(ViewNames.WorkRecord);
    }

    private void StatCard_Issues_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
            vm.RaiseNavigateToPage(ViewNames.Issue);
    }

    private void StatCard_Knowledge_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
            vm.RaiseNavigateToPage(ViewNames.Knowledge);
    }

    private void Refresh_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.LoadDashboardAsync().SafeFire("刷新仪表盘失败");
            ToastService.Success("数据已刷新");
        }
    }

    private void BackToTop_Click(object sender, MouseButtonEventArgs e)
    {
        MainScroller.ScrollToTop();
    }
}
