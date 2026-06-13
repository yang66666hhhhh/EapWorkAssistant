using EapWorkAssistant.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EapWorkAssistant.Views;

public partial class WorkRecordView : UserControl
{
    private bool _isDrawerOpen;
    private enum FilterDateField { Start, End }
    private FilterDateField _activeFilterField;

    public WorkRecordView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => SyncDateDisplay();
        CustomCal.SelectedDateChanged += OnCustomCalendarDateChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is WorkRecordViewModel oldVm)
        {
            oldVm.RecordSaved -= OnRecordSaved;
            oldVm.ReportGenerated -= OnReportGenerated;
            oldVm.PropertyChanged -= OnFilterPropertyChanged;
        }
        if (e.NewValue is WorkRecordViewModel newVm)
        {
            newVm.RecordSaved += OnRecordSaved;
            newVm.ReportGenerated += OnReportGenerated;
            newVm.PropertyChanged += OnFilterPropertyChanged;
        }
        SyncDateDisplay();
    }

    private void OnRecordSaved()
    {
        Dispatcher.Invoke(CloseDrawer);
    }

    private void OnReportGenerated()
    {
        Dispatcher.Invoke(() =>
        {
            ReportTextBox.BringIntoView();
        });
    }

    private void SyncDateDisplay()
    {
        if (DataContext is WorkRecordViewModel vm)
        {
            DateDisplayText.Text = vm.SelectedDate.ToString("yyyy-MM-dd");
            CustomCal.SelectedDate = vm.SelectedDate;
            SyncFilterDateDisplay(vm);
        }
    }

    private void SyncFilterDateDisplay(WorkRecordViewModel vm)
    {
        FilterStartText.Text = vm.FilterStartDate?.ToString("yyyy-MM-dd") ?? "开始日期";
        FilterEndText.Text = vm.FilterEndDate?.ToString("yyyy-MM-dd") ?? "结束日期";
    }

    private void OnFilterPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkRecordViewModel.FilterStartDate)
            or nameof(WorkRecordViewModel.FilterEndDate))
        {
            if (DataContext is WorkRecordViewModel vm)
                SyncFilterDateDisplay(vm);
        }
    }

    private void OnCustomCalendarDateChanged(object? sender, DateTime date)
    {
        if (DataContext is WorkRecordViewModel vm)
        {
            vm.SelectedDate = date;
            DateDisplayText.Text = date.ToString("yyyy-MM-dd");
            CloseCalendar();
        }
    }

    private void CalendarToggle_Click(object sender, RoutedEventArgs e)
    {
        if (CalendarContainer.Visibility == Visibility.Visible)
        {
            CloseCalendar();
        }
        else
        {
            if (DataContext is WorkRecordViewModel vm)
            {
                CustomCal.SelectedDate = vm.SelectedDate;
                CustomCal.SyncDisplay();
            }
            // 计算浮窗位置（对齐到按钮下方）
            UpdateCalendarPosition();
            CalendarBackdrop.Visibility = Visibility.Visible;
            CalendarContainer.Visibility = Visibility.Visible;
        }
    }

    private void CloseCalendar()
    {
        CalendarContainer.Visibility = Visibility.Collapsed;
        CalendarBackdrop.Visibility = Visibility.Collapsed;
    }

    private void CalendarBackdrop_Click(object sender, MouseButtonEventArgs e)
    {
        CloseCalendar();
    }

    private void UpdateCalendarPosition()
    {
        // 获取按钮相对于最外层 Grid 的位置
        var buttonPos = CalendarToggleBtn.TransformToAncestor(this)
            .Transform(new System.Windows.Point(0, 0));

        const double calWidth = 310;
        const double calHeight = 290;
        double viewWidth = this.ActualWidth;
        double viewHeight = this.ActualHeight;

        // 水平：默认居中于按钮，超出边界则修正
        double x = Math.Max(8, buttonPos.X - 100);
        if (x + calWidth > viewWidth - 8) x = viewWidth - calWidth - 8;

        // 垂直：优先在按钮下方，空间不够则放在按钮上方
        double y = buttonPos.Y + CalendarToggleBtn.ActualHeight + 6;
        if (y + calHeight > viewHeight - 8)
        {
            y = buttonPos.Y - calHeight - 6;
        }
        if (y < 8) y = 8;

        CalendarContainer.Margin = new Thickness(x, y, 0, 0);
    }

    // ===== 浮窗抽屉动画 =====

    private void OpenForm_Click(object sender, RoutedEventArgs e)
    {
        if (_isDrawerOpen) return;

        // 新增模式：重置表单
        if (DataContext is WorkRecordViewModel vm)
        {
            vm.NewRecordCommand.Execute(null);
        }

        OpenDrawer();
    }

    private void EditRow_Click(object sender, RoutedEventArgs e)
    {
        // EditRecordCommand 已通过 Command 绑定执行，此处只需打开抽屉
        OpenDrawer();
    }

    private void OpenDrawer()
    {
        if (_isDrawerOpen) return;
        _isDrawerOpen = true;

        // 显示遮罩（淡入）
        Backdrop.Visibility = Visibility.Visible;
        Backdrop.Opacity = 0;
        var fadeIn = new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(120))
        };
        Backdrop.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        // 显示浮窗面板并滑入
        FormPanel.Visibility = Visibility.Visible;
        OpenFormBtn.Visibility = Visibility.Collapsed;

        var translate = new TranslateTransform { X = 540 };
        FormPanel.RenderTransform = translate;

        var slideIn = new DoubleAnimation
        {
            From = 540,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        translate.BeginAnimation(TranslateTransform.XProperty, slideIn);
    }

    private void CloseForm_Click(object sender, RoutedEventArgs e)
    {
        CloseDrawer();
    }

    private void CopyLast_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkRecordViewModel vm)
        {
            vm.CopyLastRecordCommand.Execute(null);
        }
    }

    private void Backdrop_Click(object sender, MouseButtonEventArgs e)
    {
        CloseDrawer();
    }

    private void TabDaily_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkRecordViewModel vm)
            vm.SelectedTabIndex = 0;
    }

    private void TabAll_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkRecordViewModel vm)
            vm.SelectedTabIndex = 1;
    }

    // ===== 筛选日历 =====

    private void FilterStart_Click(object sender, RoutedEventArgs e)
    {
        _activeFilterField = FilterDateField.Start;
        OpenFilterCalendar();
    }

    private void FilterEnd_Click(object sender, RoutedEventArgs e)
    {
        _activeFilterField = FilterDateField.End;
        OpenFilterCalendar();
    }

    private void OpenFilterCalendar()
    {
        if (DataContext is WorkRecordViewModel vm)
        {
            var currentDate = _activeFilterField == FilterDateField.Start
                ? (vm.FilterStartDate ?? DateTime.Now)
                : (vm.FilterEndDate ?? DateTime.Now);
            FilterCal.SelectedDate = currentDate;
            FilterCal.SyncDisplay();
        }

        var button = _activeFilterField == FilterDateField.Start ? FilterStartBtn : FilterEndBtn;
        var buttonPos = button.TransformToAncestor(this).Transform(new System.Windows.Point(0, 0));

        const double calWidth = 310;
        const double calHeight = 290;
        double viewWidth = this.ActualWidth;
        double viewHeight = this.ActualHeight;

        double x = Math.Max(8, buttonPos.X - 60);
        if (x + calWidth > viewWidth - 8) x = viewWidth - calWidth - 8;

        double y = buttonPos.Y + button.ActualHeight + 6;
        if (y + calHeight > viewHeight - 8) y = buttonPos.Y - calHeight - 6;
        if (y < 8) y = 8;

        FilterCalendarContainer.Margin = new Thickness(x, y, 0, 0);
        FilterCalendarBackdrop.Visibility = Visibility.Visible;
        FilterCalendarContainer.Visibility = Visibility.Visible;
    }

    private void FilterCal_SelectedDateChanged(object? sender, DateTime date)
    {
        if (DataContext is WorkRecordViewModel vm)
        {
            if (_activeFilterField == FilterDateField.Start)
                vm.FilterStartDate = date;
            else
                vm.FilterEndDate = date;
        }
        CloseFilterCalendar();
    }

    private void FilterCalendarBackdrop_Click(object sender, MouseButtonEventArgs e)
    {
        CloseFilterCalendar();
    }

    private void CloseFilterCalendar()
    {
        FilterCalendarBackdrop.Visibility = Visibility.Collapsed;
        FilterCalendarContainer.Visibility = Visibility.Collapsed;
    }

    private void CloseDrawer()
    {
        if (!_isDrawerOpen) return;
        _isDrawerOpen = false;

        // 遮罩淡出
        var fadeOut = new DoubleAnimation
        {
            From = 1, To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(120))
        };
        fadeOut.Completed += (_, _) =>
        {
            Backdrop.Visibility = Visibility.Collapsed;
        };
        Backdrop.BeginAnimation(UIElement.OpacityProperty, fadeOut);

        // 浮窗滑出
        var translate = FormPanel.RenderTransform as TranslateTransform
                        ?? new TranslateTransform { X = 0 };
        FormPanel.RenderTransform = translate;

        var slideOut = new DoubleAnimation
        {
            From = 0,
            To = 540,
            Duration = new Duration(TimeSpan.FromMilliseconds(150)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        slideOut.Completed += (_, _) =>
        {
            FormPanel.Visibility = Visibility.Collapsed;
            OpenFormBtn.Visibility = Visibility.Visible;
            // 关闭时重置为新增模式
            if (DataContext is WorkRecordViewModel vm)
            {
                vm.NewRecordCommand.Execute(null);
            }
        };

        translate.BeginAnimation(TranslateTransform.XProperty, slideOut);
    }
}