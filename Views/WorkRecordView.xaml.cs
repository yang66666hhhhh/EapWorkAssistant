using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using System.ComponentModel;
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
        SharedCal.DisplayMonthChanged += OnCalendarDisplayMonthChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is WorkRecordViewModel oldVm)
        {
            oldVm.RecordSaved -= OnRecordSaved;
            oldVm.ReportGenerated -= OnReportGenerated;
            oldVm.SelectedDateChanged -= OnSelectedDateChanged;
            oldVm.PropertyChanged -= OnFilterPropertyChanged;
            oldVm.OpenLeaveDialogRequested -= OnOpenLeaveDialog;
            oldVm.EditLeaveRecordRequested -= OnEditLeaveRecord;
        }
        if (e.NewValue is WorkRecordViewModel newVm)
        {
            newVm.RecordSaved += OnRecordSaved;
            newVm.ReportGenerated += OnReportGenerated;
            newVm.SelectedDateChanged += OnSelectedDateChanged;
            newVm.PropertyChanged += OnFilterPropertyChanged;
            newVm.OpenLeaveDialogRequested += OnOpenLeaveDialog;
            newVm.EditLeaveRecordRequested += OnEditLeaveRecord;
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
            ReportViewer.BringIntoView();
        });
    }

    private void OnSelectedDateChanged(DateTime date)
    {
        // 日历浮窗可见时跳过：用户正在选日期，TwoWay 绑定已同步，无需回写
        if (CalendarContainer.Visibility == Visibility.Visible) return;
        DateDisplayText.Text = date.ToString("yyyy-MM-dd");
        // 临时解绑事件，防止设置 SelectedDate 触发事件链 → CloseCalendar → 闪退
        SharedCal.SelectedDateChanged -= OnSharedCalendarDateChanged;
        SharedCal.SelectedDate = date;
        SharedCal.SelectedDateChanged += OnSharedCalendarDateChanged;
    }

    private void SyncDateDisplay()
    {
        if (DataContext is WorkRecordViewModel vm)
        {
            DateDisplayText.Text = vm.SelectedDate.ToString("yyyy-MM-dd");
            // 临时解绑事件，防止程序设置 SelectedDate 触发事件链 → CloseCalendar
            SharedCal.SelectedDateChanged -= OnSharedCalendarDateChanged;
            SharedCal.SelectedDate = vm.SelectedDate;
            SharedCal.SelectedDateChanged += OnSharedCalendarDateChanged;
            vm.CalendarDisplayMonth = vm.SelectedDate;
            SyncFilterDateDisplay(vm);
            SyncCalendarData();
        }
    }

    private void SyncFilterDateDisplay(WorkRecordViewModel vm)
    {
        FilterStartText.Text = vm.FilterStartDate?.ToString("yyyy-MM-dd") ?? "开始日期";
        FilterEndText.Text = vm.FilterEndDate?.ToString("yyyy-MM-dd") ?? "结束日期";
    }

    private void OnFilterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkRecordViewModel.FilterStartDate)
            or nameof(WorkRecordViewModel.FilterEndDate))
        {
            if (DataContext is WorkRecordViewModel vm)
                SyncFilterDateDisplay(vm);
        }

        // 日历状态数据变化时同步到日历控件
        if (e.PropertyName is nameof(WorkRecordViewModel.RecordDates)
            or nameof(WorkRecordViewModel.HolidayDates)
            or nameof(WorkRecordViewModel.LeaveDateMap)
            or nameof(WorkRecordViewModel.MakeupDates))
        {
            SyncCalendarData();
        }
    }

    /// <summary>将 ViewModel 的日历状态数据同步到 SharedCal 控件的 DependencyProperty</summary>
    private void SyncCalendarData()
    {
        if (DataContext is not WorkRecordViewModel vm) return;
        SharedCal.HasRecordDates = vm.RecordDates;
        SharedCal.HolidayDates = vm.HolidayDates;
        SharedCal.LeaveDateMap = vm.LeaveDateMap;
        SharedCal.MakeupDates = vm.MakeupDates;
    }

    /// <summary>日历月份导航变化时，通知 ViewModel 重新加载该月数据</summary>
    private void OnCalendarDisplayMonthChanged(object? sender, DateTime displayMonth)
    {
        if (DataContext is WorkRecordViewModel vm)
        {
            vm.CalendarDisplayMonth = displayMonth;
            vm.LoadCalendarStatusAsync().SafeFire("加载日历状态失败");
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
        // 仅日历可见时（用户点选日期）才关闭；不可见时（初始化/外部事件）关闭是空操作但需避免
        if (CalendarContainer.Visibility != Visibility.Visible) return;
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
                // 临时解绑事件，防止程序设置 SelectedDate 触发事件链 → CloseCalendar → 闪退
                SharedCal.SelectedDateChanged -= OnSharedCalendarDateChanged;
                SharedCal.SelectedDate = vm.SelectedDate;
                SharedCal.SyncDisplay();
                SharedCal.SelectedDateChanged += OnSharedCalendarDateChanged;
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

    // ===== 表格交互打磨：双击编辑 =====

    private void RecordsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawerOpen) return;
        if (DataContext is not WorkRecordViewModel vm) return;
        if (sender is DataGrid dg
            && dg.ContainerFromElement(e.OriginalSource as DependencyObject) is DataGridRow
            && dg.SelectedItem is WorkRecord record)
        {
            vm.EditRecordCommand.Execute(record);
            OpenDrawer();
        }
    }

    private void OpenDrawer()
    {
        if (_isDrawerOpen) return;
        _isDrawerOpen = true;
        if (DataContext is WorkRecordViewModel vm)
            vm.IsDrawerOpen = true;
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
        // 临时解绑事件，防止程序设置 SelectedDate 触发事件链 → CloseCalendar → 闪退
        SharedCal.SelectedDateChanged -= OnSharedCalendarDateChanged;
        SharedCal.SelectedDate = date;
        SharedCal.SyncDisplay();
        SharedCal.SelectedDateChanged += OnSharedCalendarDateChanged;
        ShowCalendar(FilterStartBtn);
    }

    private void FilterEnd_Click(object sender, RoutedEventArgs e)
    {
        _calendarMode = CalendarMode.FilterEnd;
        var date = DataContext is WorkRecordViewModel vm
            ? (vm.FilterEndDate ?? DateTime.Now)
            : DateTime.Now;
        // 临时解绑事件，防止程序设置 SelectedDate 触发事件链 → CloseCalendar → 闪退
        SharedCal.SelectedDateChanged -= OnSharedCalendarDateChanged;
        SharedCal.SelectedDate = date;
        SharedCal.SyncDisplay();
        SharedCal.SelectedDateChanged += OnSharedCalendarDateChanged;
        ShowCalendar(FilterEndBtn);
    }

    private async void CloseDrawer()
    {
        if (!_isDrawerOpen) return;

        if (DataContext is WorkRecordViewModel vm && vm.HasUnsavedInput())
        {
            if (vm.CanQuickSave())
            {
                // 数据满足保存条件 → 自动保存后关闭，无需用户确认
                try
                {
                    await vm.FlushPendingChangesAsync();
                }
                catch (Exception ex)
                {
                    ToastService.Error($"保存失败：{ex.Message}");
                    return; // 保存异常时不关闭，让用户继续编辑
                }
            }
            else
            {
                // 数据不完整，无法自动保存 → 警告用户
                bool confirmed = ConfirmDialog.Show(
                    "当前表单数据不完整，无法自动保存。\n确定要放弃这些修改吗？",
                    "放弃修改？",
                    ConfirmDialogType.Warning,
                    "放弃", "继续编辑");
                if (!confirmed) return;
            }
        }

        _isDrawerOpen = false;
        if (DataContext is WorkRecordViewModel vmClose)
            vmClose.IsDrawerOpen = false;
        DrawerHelper.CloseDrawer(Backdrop, FormPanel, OpenFormBtn, () =>
        {
            if (DataContext is WorkRecordViewModel vm2)
                vm2.NewRecordCommand.Execute(null);
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

    // ===== 请假对话框 =====

    private void OnOpenLeaveDialog()
    {
        if (DataContext is WorkRecordViewModel vm)
            OpenLeaveDialogForDate(vm);
    }

    private void OnEditLeaveRecord(LeaveRecord record)
    {
        if (DataContext is WorkRecordViewModel vm)
            OpenLeaveDialogForDate(vm, record);
    }

    private void OpenLeaveDialogForDate(WorkRecordViewModel vm, LeaveRecord? editRecord = null)
    {
        var dialog = new LeaveDialog(vm.SelectedDate.Year, vm.SelectedDate.Month)
        {
            Owner = Window.GetWindow(this)
        };

        // 如果是编辑模式，让对话框加载后自动进入编辑状态
        if (editRecord != null)
            dialog.PendingEditRecord = editRecord;

        dialog.ShowDialog();

        if (dialog.HasChanges)
        {
            vm.LoadDailyLeaveRecordsAsync().SafeFire("加载请假记录失败");
            vm.LoadCalendarStatusAsync().SafeFire("加载日历状态失败");
            vm.LoadCompLeaveBalanceAsync().SafeFire("加载调休余额失败");
        }
    }

    // ===== 全部记录表格排序 =====

    /// <summary>DataGrid 列头点击排序：拦截 WPF 默认排序，委托给 ViewModel 执行服务端排序</summary>
    private void AllRecordsGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (DataContext is WorkRecordViewModel vm && !string.IsNullOrEmpty(e.Column.SortMemberPath))
            vm.ToggleSort(e.Column.SortMemberPath);
    }
}