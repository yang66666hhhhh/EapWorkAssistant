using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EapWorkAssistant.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncProbationDate();
        Loaded += (_, _) => SyncProbationDate();
        CustomCal.SelectedDateChanged += OnCalendarDateChanged;

        // 监听个人资料变更，身份切换时立即刷新仪表盘
        ProfileService.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProfileService.IsProbation)
                && DataContext is DashboardViewModel vm)
            {
                _ = vm.LoadDashboardAsync();
            }
        };
    }

    private void SyncProbationDate()
    {
        if (DataContext is DashboardViewModel vm)
        {
            ProbDateText.Text = vm.ProbationStartDate;
            CustomCal.SelectedDate = vm.CalendarDate;
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

            // 计算浮窗位置（响应式：空间不够时向上弹出）
            var buttonPos = ProbDateBtn.TransformToAncestor(this)
                .Transform(new System.Windows.Point(0, 0));

            const double calWidth = 310;
            const double calHeight = 290;
            double viewWidth = this.ActualWidth;
            double viewHeight = this.ActualHeight;

            // 水平：默认右对齐到按钮，超出左边界则修正
            double x = buttonPos.X + ProbDateBtn.ActualWidth - calWidth;
            if (x < 8) x = 8;
            if (x + calWidth > viewWidth - 8) x = viewWidth - calWidth - 8;

            // 垂直：优先在按钮下方，空间不够则放在按钮上方
            double y = buttonPos.Y + ProbDateBtn.ActualHeight + 6;
            if (y + calHeight > viewHeight - 8)
            {
                y = buttonPos.Y - calHeight - 6;
            }
            if (y < 8) y = 8;

            CalendarContainer.Margin = new Thickness(x, y, 0, 0);
            CalendarBackdrop.Visibility = Visibility.Visible;
            CalendarContainer.Visibility = Visibility.Visible;
        }
    }

    private void CalendarBackdrop_Click(object sender, MouseButtonEventArgs e)
    {
        CloseCalendar();
    }

    private void CloseCalendar()
    {
        CalendarContainer.Visibility = Visibility.Collapsed;
        CalendarBackdrop.Visibility = Visibility.Collapsed;
    }
}
