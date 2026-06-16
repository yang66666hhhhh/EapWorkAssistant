using EapWorkAssistant.Helpers;
using EapWorkAssistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EapWorkAssistant.Views;

public partial class WorkRecordView : UserControl
{
    private bool _isDrawerOpen;
    private enum CalendarMode { Daily, FilterStart, FilterEnd }
    private CalendarMode _calendarMode;

    public WorkRecordView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => SyncDateDisplay();
        SharedCal.SelectedDateChanged += OnSharedCalendarDateChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is WorkRecordViewModel oldVm)
        {
            oldVm.RecordSaved -= OnRecordSaved;
            oldVm.ReportGenerated -= OnReportGenerated;
            oldVm.SelectedDateChanged -= OnSelectedDateChanged;
            oldVm.PropertyChanged -= OnFilterPropertyChanged;
        }
        if (e.NewValue is WorkRecordViewModel newVm)
        {
            newVm.RecordSaved += OnRecordSaved;
            newVm.ReportGenerated += OnReportGenerated;
            newVm.SelectedDateChanged += OnSelectedDateChanged;
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

    private void OnSelectedDateChanged(DateTime date)
    {
        Dispatcher.Invoke(() =>
        {
            DateDisplayText.Text = date.ToString("yyyy-MM-dd");
            SharedCal.SelectedDate = date;
        });
    }

    private void SyncDateDisplay()
    {
        if (DataContext is WorkRecordViewModel vm)
        {
            DateDisplayText.Text = vm.SelectedDate.ToString("yyyy-MM-dd");
            SharedCal.SelectedDate = vm.SelectedDate;
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

    private void OnSharedCalendarDateChanged(object? sender, DateTime date)
    {
        if (DataContext is WorkRecordViewModel vm)
        {
            switch (_calendarMode)
            {
                case CalendarMode.Daily:
                    // TwoWay 绑定已自动更新 vm.SelectedDate，只需更新显示文本
                    DateDisplayText.Text = date.ToString("yyyy-MM-dd");
                    break;
                case CalendarMode.FilterStart:
                    vm.FilterStartDate = date;
                    break;
                case CalendarMode.FilterEnd:
                    vm.FilterEndDate = date;
                    break;
            }
        }
        CloseCalendar();
    }

    private void CalendarToggle_Click(object sender, RoutedEventArgs e)
    {
        if (CalendarContainer.Visibility == Visibility.Visible)
        {
            CloseCalendar();
        }
        else
        {
            _calendarMode = CalendarMode.Daily;
            if (DataContext is WorkRecordViewModel vm)
            {
                SharedCal.SelectedDate = vm.SelectedDate;
                SharedCal.SyncDisplay();
            }
            ShowCalendar(CalendarToggleBtn);
        }
    }

    private void CloseCalendar()
    {
        CalendarHelper.Close(CalendarBackdrop, CalendarContainer);
    }

    private void CalendarBackdrop_Click(object sender, MouseButtonEventArgs e)
    {
        CloseCalendar();
    }

    private void ShowCalendar(FrameworkElement anchorButton)
    {
        CalendarHelper.Show(CalendarBackdrop, CalendarContainer, anchorButton, this);
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
        DrawerHelper.OpenDrawer(Backdrop, FormPanel, OpenFormBtn, 540);
    }

    private void FormField_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkRecordViewModel vm)
            vm.MarkDirty();
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

    // ===== 筛选日历（复用共享日历浮窗） =====

    private void FilterStart_Click(object sender, RoutedEventArgs e)
    {
        _calendarMode = CalendarMode.FilterStart;
        var date = DataContext is WorkRecordViewModel vm
            ? (vm.FilterStartDate ?? DateTime.Now)
            : DateTime.Now;
        SharedCal.SelectedDate = date;
        SharedCal.SyncDisplay();
        ShowCalendar(FilterStartBtn);
    }

    private void FilterEnd_Click(object sender, RoutedEventArgs e)
    {
        _calendarMode = CalendarMode.FilterEnd;
        var date = DataContext is WorkRecordViewModel vm
            ? (vm.FilterEndDate ?? DateTime.Now)
            : DateTime.Now;
        SharedCal.SelectedDate = date;
        SharedCal.SyncDisplay();
        ShowCalendar(FilterEndBtn);
    }

    private void CloseDrawer()
    {
        if (!_isDrawerOpen) return;

        if (DataContext is WorkRecordViewModel vm && vm.IsFormDirty)
        {
            bool confirmed = ConfirmDialog.Show(
                "当前表单有未保存的修改，确定要放弃吗？",
                "放弃修改？",
                ConfirmDialogType.Warning,
                "放弃", "取消");
            if (!confirmed) return;
        }

        _isDrawerOpen = false;
        DrawerHelper.CloseDrawer(Backdrop, FormPanel, OpenFormBtn, () =>
        {
            if (DataContext is WorkRecordViewModel vm)
                vm.NewRecordCommand.Execute(null);
        }, 540);
    }

    /// <summary>工时输入验证：只允许数字和小数点</summary>
    private void HoursInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var textBox = (TextBox)sender;
        var newText = textBox.Text + e.Text;
        // 允许数字和最多一个小数点
        e.Handled = !double.TryParse(newText, out _) && newText != ".";
    }

    /// <summary>进度输入验证：只允许整数</summary>
    private void ProgressInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }
}