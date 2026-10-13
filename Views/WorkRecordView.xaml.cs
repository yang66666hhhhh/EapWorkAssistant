using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using EapWorkAssistant.Controls;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EapWorkAssistant.Views;

public partial class WorkRecordView : UserControl
{
    private bool _closingGuard;
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
            oldVm.OpenNewFormRequested -= OpenNewForm;
        }
        if (e.NewValue is WorkRecordViewModel newVm)
        {
            newVm.RecordSaved += OnRecordSaved;
            newVm.ReportGenerated += OnReportGenerated;
            newVm.SelectedDateChanged += OnSelectedDateChanged;
            newVm.PropertyChanged += OnFilterPropertyChanged;
            newVm.OpenLeaveDialogRequested += OnOpenLeaveDialog;
            newVm.EditLeaveRecordRequested += OnEditLeaveRecord;
            newVm.OpenNewFormRequested += OpenNewForm;
        }
        SyncDateDisplay();
    }

    private void OnRecordSaved()
    {
        Dispatcher.Invoke(() => FormDrawer.RequestClose());
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

    private void OpenForm_Click(object sender, RoutedEventArgs e) => OpenNewForm();

    /// <summary>
    /// 打开「新增记录」抽屉并聚焦首个输入框。
    /// 供「新增记录」按钮（Click）与 Ctrl+N 快捷键（VM 事件）共用。
    /// </summary>
    public void OpenNewForm()
    {
        // 抽屉已打开：不重置已有输入（可能是编辑态或有未保存内容），仅把焦点移回输入框
        if (FormDrawer.IsOpen)
        {
            FocusFirstInput();
            return;
        }

        // 新增模式：重置表单
        if (DataContext is WorkRecordViewModel vm)
        {
            vm.NewRecordCommand.Execute(null);
            vm.IsDrawerOpen = true;
        }

        FormDrawer.IsOpen = true;
        OfferDraftRestore();  // 模态询问会阻塞，返回后再聚焦，避免焦点被对话框抢走
        FocusFirstInput();
    }

    /// <summary>把键盘焦点移到抽屉内首个文本输入框（工作内容）。等抽屉滑入布局完成后再聚焦。</summary>
    private void FocusFirstInput()
    {
        // 抽屉内容（Drawer.Body）处在控件自身的命名域内，x:Name 取不到 → 走视觉树查找
        Dispatcher.BeginInvoke(() =>
        {
            var box = FindFirstFocusableTextBox(FormDrawer);
            if (box == null) return;
            box.Focus();
            Keyboard.Focus(box);
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>在视觉树中深度优先查找第一个可聚焦的 TextBox（跳过只读/禁用项）。</summary>
    private static TextBox? FindFirstFocusableTextBox(DependencyObject root)
    {
        if (root is TextBox tb && tb.IsEnabled && tb.Focusable && !tb.IsReadOnly)
            return tb;

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var found = FindFirstFocusableTextBox(VisualTreeHelper.GetChild(root, i));
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>打开新建抽屉后：若存在上次未完成的草稿，询问是否继续编辑。</summary>
    private void OfferDraftRestore()
    {
        if (DataContext is not WorkRecordViewModel vm) return;

        var draft = DraftService.Instance.Get<WorkRecord>();
        if (draft == null) return;

        var savedAt = DraftService.Instance.GetSavedAt<WorkRecord>();
        var summary = !string.IsNullOrWhiteSpace(draft.ProjectName)
            ? $"{draft.ProjectName} · {draft.Content}"
            : draft.Content;
        if (string.IsNullOrWhiteSpace(summary)) summary = "（未填写内容）";

        bool restore = ConfirmDialog.Show(
            $"发现上次未完成的工作记录草稿（保存于 {savedAt}），是否继续编辑？\n\n{summary}",
            "恢复草稿？",
            ConfirmDialogType.Info,
            "恢复", "放弃");
        // 读取即消费：无论恢复与否都清掉，避免反复打扰。
        DraftService.Instance.Clear<WorkRecord>();
        if (!restore) return;

        vm.RestoreDraft(draft);
    }

    private void EditRow_Click(object sender, RoutedEventArgs e)
    {
        // EditRecordCommand 已通过 Command 绑定执行，此处只需打开抽屉
        if (FormDrawer.IsOpen) return;
        if (DataContext is WorkRecordViewModel vm)
            vm.IsDrawerOpen = true;
        FormDrawer.IsOpen = true;
    }

    // ===== 表格交互打磨：双击编辑 =====

    private void RecordsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FormDrawer.IsOpen) return;
        if (DataContext is not WorkRecordViewModel vm) return;
        if (sender is DataGrid dg
            && dg.ContainerFromElement(e.OriginalSource as DependencyObject) is DataGridRow
            && dg.SelectedItem is WorkRecord record)
        {
            vm.EditRecordCommand.Execute(record);
            vm.IsDrawerOpen = true;
            FormDrawer.IsOpen = true;
        }
    }

    private void FormField_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkRecordViewModel vm)
            vm.MarkDirty();
    }

    private void CopyLast_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkRecordViewModel vm)
        {
            vm.CopyLastRecordCommand.Execute(null);
        }
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

    // ===== 侧滑抽屉：直接驱动 Drawer.IsOpen，遮罩/滑入动画与关闭逻辑均内化于 Drawer 控件 =====

    private async void FormDrawer_Closing(object sender, DrawerClosingEventArgs e)
    {
        // RequestClose 二次进入（自动保存后）时跳过脏检查，放行关闭
        if (_closingGuard)
        {
            _closingGuard = false;
            return;
        }

        if (DataContext is not WorkRecordViewModel vm) return;
        if (!vm.HasUnsavedInput()) return;

        if (vm.CanQuickSave())
        {
            // 数据满足保存条件 → 取消本次关闭并自动保存，保存后再次请求关闭
            e.Cancel = true;
            _closingGuard = true;
            try
            {
                await vm.FlushPendingChangesAsync();
            }
            catch (Exception ex)
            {
                ToastService.Error($"保存失败：{ex.Message}");
                _closingGuard = false; // 保存异常时不关闭，让用户继续编辑
                return;
            }
            FormDrawer.RequestClose();
        }
        else
        {
            // 数据不完整，无法自动保存 → 警告用户
            bool confirmed = ConfirmDialog.Show(
                "当前表单数据不完整，无法自动保存。\n确定要放弃这些修改吗？",
                "放弃修改？",
                ConfirmDialogType.Warning,
                "放弃", "继续编辑");
            if (!confirmed) e.Cancel = true;
        }
    }

    private void FormDrawer_Closed(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkRecordViewModel vm)
        {
            SaveDraftOnClose(vm);
            vm.IsDrawerOpen = false;
            vm.IsFormDirty = false;
            vm.NewRecordCommand.Execute(null);
        }
    }

    /// <summary>关闭抽屉时静默保留「新增」草稿，供下次打开新建时恢复。</summary>
    private static void SaveDraftOnClose(WorkRecordViewModel vm)
    {
        if (vm.CurrentRecord.Id > 0) return;  // 编辑态数据本就在库中，不占草稿位
        if (!vm.HasUnsavedInput()) return;    // 空表单 / 已自动保存成功 → 不写文件
        DraftService.Instance.Save(vm.CurrentRecord);
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