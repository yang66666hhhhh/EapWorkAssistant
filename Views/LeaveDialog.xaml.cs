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
    private bool _calendarOpen;

    /// <summary>对话框关闭后检查是否有变更</summary>
    public bool HasChanges { get; private set; }

    /// <summary>外部设置的待编辑记录，对话框加载后自动进入编辑模式</summary>
    public LeaveRecord? PendingEditRecord { get; set; }

    /// <summary>假期类型列表</summary>
    public string[] LeaveTypes { get; } = ["年假", "事假", "病假", "调休", "出差", "婚假"];

    public ObservableCollection<LeaveRecord> LeaveRecords { get; } = new();

    public LeaveDialog(int year, int month)
    {
        _year = year;
        _month = month;
        InitializeComponent();
        LeaveList.ItemsSource = LeaveRecords;
        LeaveTypeCombo.SelectedIndex = 0;
        UpdateMonthLabel();
        SharedCal.SelectedDateChanged += OnCalendarDateChanged;
        Loaded += async (_, _) =>
        {
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
        }
        catch (Exception ex)
        {
            ToastService.Error($"加载请假记录失败：{ex.Message}");
        }
    }

    private void PrevMonth_Click(object sender, RoutedEventArgs e)
    {
        _month--;
        if (_month < 1) { _month = 12; _year--; }
        UpdateMonthLabel();
        LoadRecordsAsync().SafeFire("加载请假记录失败");
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _month++;
        if (_month > 12) { _month = 1; _year++; }
        UpdateMonthLabel();
        LoadRecordsAsync().SafeFire("加载请假记录失败");
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
        _editingRecord = null;
        ClearForm();
        CancelEditBtn.Visibility = Visibility.Collapsed;
    }

    private async void SaveLeave_Click(object sender, RoutedEventArgs e)
    {
        // 校验日期（从日历选择器获取）
        if (_selectedDate == null)
        {
            ConfirmDialog.Alert("请选择日期");
            return;
        }

        // 校验工时
        if (!double.TryParse(HoursInput.Text, out var hours) || hours <= 0 || hours > 24)
        {
            ConfirmDialog.Alert("请输入有效的工时（0.5 ~ 24 小时）");
            return;
        }

        var leaveType = LeaveTypeCombo.SelectedItem?.ToString() ?? "事假";
        var note = NoteInput.Text.Trim();

        try
        {
            if (_editingRecord != null)
            {
                // 编辑模式
                _editingRecord.Date = _selectedDate.Value.ToString("yyyy-MM-dd");
                _editingRecord.LeaveType = leaveType;
                _editingRecord.Hours = hours;
                _editingRecord.Note = note;
                await _repo.UpdateAsync(_editingRecord);
                ToastService.Success("请假记录已更新");
            }
            else
            {
                // 新增模式
                var record = new LeaveRecord
                {
                    Date = _selectedDate.Value.ToString("yyyy-MM-dd"),
                    LeaveType = leaveType,
                    Hours = hours,
                    Note = note
                };
                await _repo.InsertAsync(record);
                LeaveRecords.Add(record);
                ToastService.Success("请假记录已添加");
            }

            HasChanges = true;
            _editingRecord = null;
            ClearForm();
            CancelEditBtn.Visibility = Visibility.Collapsed;
            await LoadRecordsAsync();
        }
        catch (Exception ex)
        {
            ToastService.Error($"保存失败：{ex.Message}");
        }
    }

    private void ClearForm()
    {
        _selectedDate = null;
        UpdateDateButtonText();
        LeaveTypeCombo.SelectedIndex = 0;
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
        if (_calendarOpen) { CloseCalendar(); return; }

        // 临时解绑事件，防止程序设置 SelectedDate 触发事件链
        SharedCal.SelectedDateChanged -= OnCalendarDateChanged;
        SharedCal.SelectedDate = _selectedDate ?? new DateTime(_year, _month, 1);
        SharedCal.SyncDisplay();
        SharedCal.SelectedDateChanged += OnCalendarDateChanged;

        _calendarOpen = true;

        // 定位日历浮窗（相对按钮位置）
        var buttonPos = DateBtn.TransformToAncestor(this).Transform(new Point(0, 0));
        const double calWidth = 310;
        const double calHeight = 290;

        double x = buttonPos.X;
        if (x + calWidth > ActualWidth - 28) x = ActualWidth - calWidth - 28;
        if (x < 20) x = 20;

        double y = buttonPos.Y + DateBtn.ActualHeight + 6;
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
        _selectedDate = date;
        UpdateDateButtonText();
        CloseCalendar();
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

    /// <summary>工时输入验证：只允许数字和小数点</summary>
    private void HoursInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var textBox = (System.Windows.Controls.TextBox)sender;
        var newText = textBox.Text + e.Text;
        e.Handled = !double.TryParse(newText, out _) && newText != ".";
    }
}
