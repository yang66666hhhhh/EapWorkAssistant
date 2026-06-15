using EapWorkAssistant.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EapWorkAssistant.Views
{
    public partial class CustomCalendar : UserControl
    {
        // ===== DependencyProperty: SelectedDate =====
        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register(nameof(SelectedDate), typeof(DateTime), typeof(CustomCalendar),
                new PropertyMetadata(DateTime.Today, OnSelectedDateChanged));

        public DateTime SelectedDate
        {
            get => (DateTime)GetValue(SelectedDateProperty);
            set => SetValue(SelectedDateProperty, value);
        }

        // ===== DependencyProperty: HasRecordDates =====
        public static readonly DependencyProperty HasRecordDatesProperty =
            DependencyProperty.Register(nameof(HasRecordDates), typeof(IList<DateTime>), typeof(CustomCalendar),
                new PropertyMetadata(null, OnHasRecordDatesChanged));

        public IList<DateTime> HasRecordDates
        {
            get => (IList<DateTime>)GetValue(HasRecordDatesProperty);
            set => SetValue(HasRecordDatesProperty, value);
        }

        // ===== 事件 =====
        public event EventHandler<DateTime>? SelectedDateChanged;

        // ===== 内部属性 =====
        private DateTime _displayMonth;
        public ObservableCollection<CalendarDayItem> CalendarDays { get; } = new();

        public CustomCalendar()
        {
            InitializeComponent();
            _displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            GenerateCalendarDays();
        }

        private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var cal = (CustomCalendar)d;
            if (cal.SelectedDate != (DateTime)e.OldValue)
            {
                // 如果选中日期在其他月份，跳转到那个月份
                var newDate = cal.SelectedDate;
                if (newDate.Year != cal._displayMonth.Year || newDate.Month != cal._displayMonth.Month)
                {
                    cal._displayMonth = new DateTime(newDate.Year, newDate.Month, 1);
                }
                cal.GenerateCalendarDays();
                cal.SelectedDateChanged?.Invoke(cal, cal.SelectedDate);
            }
        }

        private static void OnHasRecordDatesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var cal = (CustomCalendar)d;
            cal.GenerateCalendarDays();
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _displayMonth = _displayMonth.AddMonths(-1);
            GenerateCalendarDays();
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            _displayMonth = _displayMonth.AddMonths(1);
            GenerateCalendarDays();
        }

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            var wasToday = SelectedDate == DateTime.Today;
            SelectedDate = DateTime.Today;
            _displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            GenerateCalendarDays();
            // 如果已经是今天，OnSelectedDateChanged 不会触发，手动通知以关闭 Popup
            if (wasToday)
                SelectedDateChanged?.Invoke(this, DateTime.Today);
        }

        /// <summary>
        /// 强制同步显示月份到当前 SelectedDate
        /// </summary>
        public void SyncDisplay()
        {
            var date = SelectedDate;
            _displayMonth = new DateTime(date.Year, date.Month, 1);
            GenerateCalendarDays();
        }

        private void Day_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is CalendarDayItem item)
            {
                // 只更新选中日期并触发事件，由父容器负责关闭 Popup
                SelectedDate = item.Date;
            }
        }

        // ===== 日期格子悬浮效果（跟随主题） =====

        private void Day_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border bg && bg.DataContext is CalendarDayItem item && !item.IsSelected)
            {
                bg.SetResourceReference(Border.BackgroundProperty, "SurfaceHoverBrush");
            }
        }

        private void Day_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border bg)
            {
                // ClearValue 移除本地值，让 DataTrigger 重新生效
                bg.ClearValue(Border.BackgroundProperty);
            }
        }

        private void GenerateCalendarDays()
        {
            CalendarDays.Clear();

            var year = _displayMonth.Year;
            var month = _displayMonth.Month;
            var firstDay = new DateTime(year, month, 1);
            var today = DateTime.Today;
            var recordDates = HasRecordDates ?? new List<DateTime>();

            // 更新月份标题
            MonthTitle.Text = $"{year}年{month}月";

            // 计算第一天是星期几（周一=0, 周日=6）
            int startWeekday = ((int)firstDay.DayOfWeek + 6) % 7; // 转换：Sunday(0)→6, Monday(1)→0

            // 前面填充上月日期
            for (int i = startWeekday - 1; i >= 0; i--)
            {
                var date = firstDay.AddDays(-(i + 1));
                CalendarDays.Add(new CalendarDayItem
                {
                    Day = date.Day, Date = date,
                    IsToday = date == today, IsSelected = date == SelectedDate,
                    IsCurrentMonth = false, HasRecords = recordDates.Contains(date.Date),
                    IsWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday
                });
            }

            // 当月所有日期
            int daysInMonth = DateTime.DaysInMonth(year, month);
            for (int d = 1; d <= daysInMonth; d++)
            {
                var date = new DateTime(year, month, d);
                CalendarDays.Add(new CalendarDayItem
                {
                    Day = d, Date = date,
                    IsToday = date == today, IsSelected = date == SelectedDate,
                    IsCurrentMonth = true, HasRecords = recordDates.Contains(date.Date),
                    IsWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday
                });
            }

            // 后面填充下月日期（凑满所需行数：5行=35格 或 6行=42格）
            int totalNeeded = (startWeekday + daysInMonth > 35) ? 42 : 35;
            int remaining = totalNeeded - CalendarDays.Count;
            for (int i = 1; i <= remaining; i++)
            {
                var date = firstDay.AddMonths(1).AddDays(i - 1);
                CalendarDays.Add(new CalendarDayItem
                {
                    Day = date.Day, Date = date,
                    IsToday = date == today, IsSelected = date == SelectedDate,
                    IsCurrentMonth = false, HasRecords = recordDates.Contains(date.Date),
                    IsWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday
                });
            }
        }
    }
}