namespace EapWorkAssistant.Models
{
    public class CalendarDayItem
    {
        public int Day { get; set; }
        public DateTime Date { get; set; }
        public bool IsToday { get; set; }
        public bool IsSelected { get; set; }
        public bool IsCurrentMonth { get; set; }
        public bool HasRecords { get; set; }
        public bool IsWeekend { get; set; }

        /// <summary>
        /// 圆点颜色标识：green/red/blue/purple/gray/年假/事假/病假/调休/出差/婚假。
        /// 空字符串表示不显示圆点。
        /// </summary>
        public string DotColor { get; set; } = "";

        /// <summary>假期缩写标签（非空时显示在日期数字下方）</summary>
        public string LeaveLabel { get; set; } = "";
    }
}