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

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => SyncProbationDate();
        CustomCal.SelectedDateChanged += OnCalendarDateChanged;

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
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SyncProbationDate();
    }

    private void SyncProbationDate()
    {
        if (DataContext is DashboardViewModel vm)
        {
            ProbDateText.Text = vm.ProbationStartDate;
            // 临时取消事件订阅，防止初始同步触发 SaveProbationStartDate 校验
            CustomCal.SelectedDateChanged -= OnCalendarDateChanged;
            CustomCal.SelectedDate = vm.CalendarDate;
            CustomCal.SelectedDateChanged += OnCalendarDateChanged;
        }
    }

    private void OnCalendarDateChanged(object? sender, DateTime date)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.SaveProbationStartDateCommand.Execute(date);
            ProbDateText.Text = date.ToString("yyyy-MM-dd");
            CustomCal.SyncDisplay();
            CloseCalendar();
        }
    }

    private void ProbDateBtn_Click(object sender, MouseButtonEventArgs e)
    {
        if (CalendarContainer.Visibility == Visibility.Visible)
        {
            CloseCalendar();
        }
        else
        {
            if (DataContext is DashboardViewModel vm)
            {
                CustomCal.SelectedDate = vm.CalendarDate;
                CustomCal.SyncDisplay();
            }
            CalendarHelper.Show(CalendarBackdrop, CalendarContainer, ProbDateBtn, this);
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
}
