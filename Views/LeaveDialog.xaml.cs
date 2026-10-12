using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace EapWorkAssistant.Views;

public partial class LeaveDialog : Window
{
    private readonly LeaveRecordRepository _repo = new();
    private int _year;
    private int _month;
    private LeaveRecord? _editingRecord;
    private DateTime? _selectedDate;
    private DateTime? _rangeStart;
    private DateTime? _rangeEnd;
    private bool _calendarOpen;
    private bool _isSaving;
    private bool _isLoading;
    private bool _isGenerating;
    private DatePickTarget _pickTarget = DatePickTarget.Single;

    /// <summary>日历浮窗当前为哪个日期目标选取（单日 / 连续录入起点 / 连续录入终点）</summary>
    private enum DatePickTarget { Single, RangeStart, RangeEnd }

    /// <summary>对话框关闭后检查是否有变更</summary>
    public bool HasChanges { get; private set; }

    /// <summary>外部设置的待编辑记录，对话框加载后自动进入编辑模式</summary>
    public LeaveRecord? PendingEditRecord { get; set; }

    /// <summary>假期类型列表</summary>
    public string[] LeaveTypes { get; } = ["年假", "事假", "病假", "调休", "出差", "婚假"];

    /// <summary>默认请假类型（新增/清空表单时预选，用户最常用调休）</summary>
    private const string DefaultLeaveType = "调休";

    public ObservableCollection<LeaveRecord> LeaveRecords { get; } = new();

    public LeaveDialog(int year, int month)
    {
        _year = year;
        _month = month;
        InitializeComponent();
        LeaveList.ItemsSource = LeaveRecords;
        LeaveTypeCombo.SelectedItem = DefaultLeaveType;
        UpdateMonthLabel();
        SharedCal.SelectedDateChanged += OnCalendarDateChanged;
        Loaded += async (_, _) =>
        {
            // 待编辑记录可能不属于初始年月（外部按选中日期开窗），先按其日期定位，避免匹配不上静默失败
            if (PendingEditRecord != null && DateTime.TryParse(PendingEditRecord.Date, out var pendingDate)
                && (pendingDate.Year != _year || pendingDate.Month != _month))
            {
                _year = pendingDate.Year;
                _month = pendingDate.Month;
                UpdateMonthLabel();
            }

            await LoadRecordsAsync();
            // 如果有待编辑记录，自动进入编辑模式
            if (PendingEditRecord != null)
            {
                // 从已加载的列表中找到匹配的记录
                var match = LeaveRecords.FirstOrDefault(r => r.Id == PendingEditRecord.Id);
                if (match != null)
                    EnterEditMode(match);
            }
        };
    }

    /// <summary>
    /// 窗口加载时：将自身尺寸设为 Owner（主窗口）大小，实现全屏遮罩覆盖
    /// </summary>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (Owner is Window owner)
        {
            Width = owner.ActualWidth;
            Height = owner.ActualHeight;
            Left = owner.Left;
            Top = owner.Top;
        }
    }

    private void UpdateMonthLabel()
    {
        MonthLabel.Text = $"{_year}年{_month}月";
    }

    private async Task LoadRecordsAsync()
    {
        try
        {
            var records = await _repo.GetByMonthAsync(_year, _month);
            LeaveRecords.Clear();
            foreach (var r in records)
                LeaveRecords.Add(r);
            UpdateSummary();
        }
        catch (Exception ex)
        {
            ToastService.Error($"加载请假记录失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 刷新月度汇总条：天数 + 总工时 + 按类型分布（双栏布局左栏的信息补充）
    /// </summary>
    private void UpdateSummary()
    {
        if (LeaveRecords.Count == 0)
        {
            SummaryText.Text = "本月暂无请假记录";
            return;
        }

        var days = LeaveRecords.Select(r => r.Date).Distinct().Count();
        var totalHours = LeaveRecords.Sum(r => r.Hours);
        var byType = LeaveRecords
            .GroupBy(r => r.LeaveType)
            .OrderByDescending(g => g.Sum(r => r.Hours))
            .Select(g => $"{g.Key} {g.Sum(r => r.Hours):0.#}h");
        SummaryText.Text = $"本月 {days} 天 · {totalHours:0.#}h（{string.Join(" · ", byType)}）";
    }

    private async void PrevMonth_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _isLoading = true;
        SetMonthNavEnabled(false);
        try
        {
            _month--;
            if (_month < 1) { _month = 12; _year--; }
            // 切月后表单/编辑态必须重置，否则残留的编辑目标属于其他月份，保存会改错记录
            ResetFormAndEditState();
            UpdateMonthLabel();
            await LoadRecordsAsync();
        }
        finally
        {
            _isLoading = false;
            SetMonthNavEnabled(true);
        }
    }

    private async void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _isLoading = true;
        SetMonthNavEnabled(false);
        try
        {
            _month++;
            if (_month > 12) { _month = 1; _year++; }
            ResetFormAndEditState();
            UpdateMonthLabel();
            await LoadRecordsAsync();
        }
        finally
        {
            _isLoading = false;
            SetMonthNavEnabled(true);
        }
    }

    /// <summary>月份导航按钮启用状态（加载中禁用，防连点乱序）</summary>
    private void SetMonthNavEnabled(bool enabled)
    {
        PrevMonthBtn.IsEnabled = enabled;
        NextMonthBtn.IsEnabled = enabled;
    }

    /// <summary>清空表单并退出编辑态（切月 / 取消 / 删除编辑中记录 / 保存成功后调用）</summary>
    private void ResetFormAndEditState()
    {
        _editingRecord = null;
        ClearForm();
        CancelEditBtn.Visibility = Visibility.Collapsed;
        // 连续录入的区间选择一并清掉，避免切月后残留一个指向别月的区间
        _rangeStart = null;
        _rangeEnd = null;
        RangeStartText.Text = "开始日期";
        RangeEndText.Text = "结束日期";
        UpdateRangePreview();
    }

    private void EditLeave_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is LeaveRecord record)
            EnterEditMode(record);
    }

    private void EnterEditMode(LeaveRecord record)
    {
        _editingRecord = record;
        if (DateTime.TryParse(record.Date, out var d))
        {
            _selectedDate = d;
            UpdateDateButtonText();
        }
        LeaveTypeCombo.SelectedItem = record.LeaveType;
        HoursInput.Text = record.Hours.ToString();
        NoteInput.Text = record.Note;
        CancelEditBtn.Visibility = Visibility.Visible;
    }

    private async void DeleteLeave_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is LeaveRecord record)
        {
            if (!ConfirmDialog.Show(
                $"确定要删除 {record.Date} 的「{record.LeaveType}」记录吗？",
                "确认删除", ConfirmDialogType.Danger))
                return;

            try
            {
                await _repo.DeleteAsync(record.Id);
                LeaveRecords.Remove(record);
                // 若删掉的正是正在编辑的记录，必须退出编辑态，否则残留的 _editingRecord 会把已删记录再更新一遍
                if (_editingRecord != null && _editingRecord.Id == record.Id)
                    ResetFormAndEditState();
                UpdateSummary();
                HasChanges = true;
                ToastService.Success("请假记录已删除");
            }
            catch (Exception ex)
            {
                ToastService.Error($"删除失败：{ex.Message}");
            }
        }
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        ResetFormAndEditState();
    }

    private async void SaveLeave_Click(object sender, RoutedEventArgs e)
    {
        // 防重：异步保存期间禁用按钮，避免连点并发插入多条
        if (_isSaving) return;

        // 校验日期（从日历选择器获取）
        if (_selectedDate == null)
        {
            ConfirmDialog.Alert("请选择日期");
            return;
        }

        // 校验工时（下限 0.5，与提示语保持一致）
        if (!double.TryParse(HoursInput.Text, out var hours) || hours < 0.5 || hours > 24)
        {
            ConfirmDialog.Alert("请输入有效的工时（0.5 ~ 24 小时）");
            return;
        }

        var leaveType = LeaveTypeCombo.SelectedItem?.ToString() ?? DefaultLeaveType;
        var note = NoteInput.Text.Trim();
        var savedDate = _selectedDate.Value;

        _isSaving = true;
        SaveBtn.IsEnabled = false;
        try
        {
            if (_editingRecord != null)
            {
                // 编辑模式
                _editingRecord.Date = savedDate.ToString("yyyy-MM-dd");
                _editingRecord.LeaveType = leaveType;
                _editingRecord.Hours = hours;
                _editingRecord.Note = note;
                await _repo.UpdateAsync(_editingRecord);
                ToastService.Success($"已更新 {savedDate:yyyy-MM-dd} 的请假记录");
            }
            else
            {
                // 新增模式
                var record = new LeaveRecord
                {
                    Date = savedDate.ToString("yyyy-MM-dd"),
                    LeaveType = leaveType,
                    Hours = hours,
                    Note = note
                };
                await _repo.InsertAsync(record);
                ToastService.Success($"已添加 {savedDate:yyyy-MM-dd} 的请假记录");
            }

            HasChanges = true;
            ResetFormAndEditState();

            // 保存的记录可能不属于当前展示月份（用户选了其他月的日期）：
            // 若原地刷新列表，记录会"凭空消失"，必须跳到该记录所属月份
            if (savedDate.Year != _year || savedDate.Month != _month)
            {
                _year = savedDate.Year;
                _month = savedDate.Month;
                UpdateMonthLabel();
            }
            await LoadRecordsAsync();
        }
        catch (Exception ex)
        {
            ToastService.Error($"保存失败：{ex.Message}");
        }
        finally
        {
            _isSaving = false;
            SaveBtn.IsEnabled = true;
        }
    }

    private void ClearForm()
    {
        _selectedDate = null;
        UpdateDateButtonText();
        LeaveTypeCombo.SelectedItem = DefaultLeaveType;
        HoursInput.Text = "8";
        NoteInput.Text = "";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ===== 日历浮窗 =====

    private void DateBtn_Click(object sender, RoutedEventArgs e)
    {
        _pickTarget = DatePickTarget.Single;
        OpenCalendarFor(DateBtn, _selectedDate);
    }

    private void RangeStartBtn_Click(object sender, RoutedEventArgs e)
    {
        _pickTarget = DatePickTarget.RangeStart;
        OpenCalendarFor(RangeStartBtn, _rangeStart);
    }

    private void RangeEndBtn_Click(object sender, RoutedEventArgs e)
    {
        _pickTarget = DatePickTarget.RangeEnd;
        OpenCalendarFor(RangeEndBtn, _rangeEnd ?? _rangeStart);
    }

    /// <summary>
    /// 打开日历浮窗并定位到锚定按钮下方（单日 / 区间起点 / 区间终点三个目标共用）
    /// </summary>
    private void OpenCalendarFor(FrameworkElement anchorButton, DateTime? initialDate)
    {
        if (_calendarOpen) { CloseCalendar(); return; }

        // 临时解绑事件，防止程序设置 SelectedDate 触发事件链
        SharedCal.SelectedDateChanged -= OnCalendarDateChanged;
        SharedCal.SelectedDate = initialDate ?? new DateTime(_year, _month, 1);
        SharedCal.SyncDisplay();
        SharedCal.SelectedDateChanged += OnCalendarDateChanged;

        _calendarOpen = true;

        // 定位日历浮窗（相对按钮位置）
        var buttonPos = anchorButton.TransformToAncestor(this).Transform(new Point(0, 0));
        const double calWidth = 310;
        const double calHeight = 290;

        double x = buttonPos.X;
        if (x + calWidth > ActualWidth - 28) x = ActualWidth - calWidth - 28;
        if (x < 20) x = 20;

        double y = buttonPos.Y + anchorButton.ActualHeight + 6;
        if (y + calHeight > ActualHeight - 28)
            y = buttonPos.Y - calHeight - 6;
        if (y < 20) y = 20;

        CalendarContainer.Margin = new Thickness(x, y, 0, 0);

        // 淡入动画
        CalendarBackdrop.Visibility = Visibility.Visible;
        CalendarBackdrop.Opacity = 0;
        CalendarBackdrop.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(150)) });

        CalendarContainer.Visibility = Visibility.Visible;
        CalendarContainer.Opacity = 0;
        CalendarContainer.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(150)) });
    }

    private void OnCalendarDateChanged(object? sender, DateTime date)
    {
        switch (_pickTarget)
        {
            case DatePickTarget.RangeStart:
                _rangeStart = date;
                RangeStartText.Text = date.ToString("yyyy-MM-dd");
                // 起点晚于终点时作废终点，避免生成空区间
                if (_rangeEnd is { } end && end < date)
                {
                    _rangeEnd = null;
                    RangeEndText.Text = "结束日期";
                }
                UpdateRangePreview();
                break;
            case DatePickTarget.RangeEnd:
                _rangeEnd = date;
                RangeEndText.Text = date.ToString("yyyy-MM-dd");
                UpdateRangePreview();
                break;
            default:
                _selectedDate = date;
                UpdateDateButtonText();
                break;
        }
        CloseCalendar();
    }

    /// <summary>刷新连续录入的区间预览：天数、含周末数、共用工时/类型说明</summary>
    private void UpdateRangePreview()
    {
        if (_rangeStart == null || _rangeEnd == null)
        {
            RangePreviewText.Text = "选择起止日期后可批量生成记录（类型 / 工时 / 备注沿用左侧表单）";
            return;
        }

        var days = (_rangeEnd.Value.Date - _rangeStart.Value.Date).Days + 1;
        var weekendDays = Enumerable.Range(0, days)
            .Count(i => _rangeStart.Value.Date.AddDays(i).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        var weekendNote = weekendDays > 0 ? $" · 含周末 {weekendDays} 天" : "";
        RangePreviewText.Text = $"将生成 {days} 条记录（{_rangeStart.Value:MM-dd} ~ {_rangeEnd.Value:MM-dd}）{weekendNote}";
    }

    private void CalendarBackdrop_Click(object sender, MouseButtonEventArgs e)
    {
        CloseCalendar();
    }

    private void CloseCalendar()
    {
        if (!_calendarOpen) return;
        _calendarOpen = false;

        var fadeOut = new DoubleAnimation
        {
            From = 1, To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(150)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            CalendarContainer.Visibility = Visibility.Collapsed;
            CalendarContainer.Opacity = 1;
            CalendarBackdrop.Visibility = Visibility.Collapsed;
            CalendarBackdrop.Opacity = 1;
        };
        CalendarContainer.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        CalendarBackdrop.BeginAnimation(UIElement.OpacityProperty, fadeOut.Clone());
    }

    private void UpdateDateButtonText()
    {
        DateBtnText.Text = _selectedDate?.ToString("yyyy-MM-dd") ?? "选择日期";
    }

    // ===== 多选批量删除 =====

    private void LeaveList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var count = LeaveList.SelectedItems.Count;
        SelectionBar.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var hours = LeaveList.SelectedItems.OfType<LeaveRecord>().Sum(r => r.Hours);
        SelectionSummary.Text = $"已选 {count} 项 · {hours:0.#}h";
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        LeaveList.SelectedItems.Clear();
    }

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = LeaveList.SelectedItems.OfType<LeaveRecord>().ToList();
        if (selected.Count == 0) return;

        if (!ConfirmDialog.Show(
            $"确定要删除选中的 {selected.Count} 条请假记录吗？（可在回收站恢复）",
            "确认批量删除", ConfirmDialogType.Danger))
            return;

        try
        {
            await _repo.DeleteManyAsync(selected.Select(r => r.Id));
            // 删掉的记录里可能包含正在编辑的目标，统一退出编辑态
            if (_editingRecord != null && selected.Any(r => r.Id == _editingRecord.Id))
                ResetFormAndEditState();
            LeaveList.SelectedItems.Clear();
            HasChanges = true;
            await LoadRecordsAsync();
            ToastService.Success($"已删除 {selected.Count} 条请假记录");
        }
        catch (Exception ex)
        {
            ToastService.Error($"批量删除失败：{ex.Message}");
        }
    }

    // ===== 连续录入 =====

    private async void GenerateRange_Click(object sender, RoutedEventArgs e)
    {
        if (_isGenerating) return;

        if (_rangeStart == null || _rangeEnd == null)
        {
            ConfirmDialog.Alert("请先选择起止日期");
            return;
        }

        if (!double.TryParse(HoursInput.Text, out var hours) || hours < 0.5 || hours > 24)
        {
            ConfirmDialog.Alert("请输入有效的工时（0.5 ~ 24 小时）");
            return;
        }

        var leaveType = LeaveTypeCombo.SelectedItem?.ToString() ?? DefaultLeaveType;
        var note = NoteInput.Text.Trim();
        var days = (_rangeEnd.Value.Date - _rangeStart.Value.Date).Days + 1;

        if (!ConfirmDialog.Show(
            $"将生成 {days} 条「{leaveType}」记录（{_rangeStart.Value:MM-dd} ~ {_rangeEnd.Value:MM-dd}，每条 {hours:0.#}h），继续？",
            "确认批量生成"))
            return;

        _isGenerating = true;
        GenerateRangeBtn.IsEnabled = false;
        try
        {
            for (var d = _rangeStart.Value.Date; d <= _rangeEnd.Value.Date; d = d.AddDays(1))
            {
                await _repo.InsertAsync(new LeaveRecord
                {
                    Date = d.ToString("yyyy-MM-dd"),
                    LeaveType = leaveType,
                    Hours = hours,
                    Note = note
                });
            }

            HasChanges = true;
            // 生成区间可能跨月：跳到起始日期所属月，保证记录可见
            if (_rangeStart.Value.Year != _year || _rangeStart.Value.Month != _month)
            {
                _year = _rangeStart.Value.Year;
                _month = _rangeStart.Value.Month;
                UpdateMonthLabel();
            }
            await LoadRecordsAsync();
            ToastService.Success($"已生成 {days} 条请假记录");
        }
        catch (Exception ex)
        {
            ToastService.Error($"生成失败：{ex.Message}");
        }
        finally
        {
            _isGenerating = false;
            GenerateRangeBtn.IsEnabled = true;
        }
    }

    /// <summary>工时输入验证：只允许数字和小数点</summary>
    private void HoursInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var textBox = (System.Windows.Controls.TextBox)sender;
        var newText = textBox.Text + e.Text;
        e.Handled = !double.TryParse(newText, out _) && newText != ".";
    }
}
