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
    }
}