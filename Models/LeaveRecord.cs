namespace EapWorkAssistant.Models;

public class LeaveRecord
{
    public int Id { get; set; }
    /// <summary>请假日期，格式 yyyy-MM-dd</summary>
    public string Date { get; set; } = "";
    public string LeaveType { get; set; } = "事假";
    public string Note { get; set; } = "";
    public double Hours { get; set; } = 8;
    /// <summary>软删除标记（0=正常，1=已删除进回收站）</summary>
    public int IsDeleted { get; set; }
    /// <summary>删除时间戳（回收站排序用），UTC 时间</summary>
    public string? DeletedAt { get; set; }
}
