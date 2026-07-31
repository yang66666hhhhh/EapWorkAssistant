namespace EapWorkAssistant.Models;

public class LeaveRecord
{
    public int Id { get; set; }
    /// <summary>请假日期，格式 yyyy-MM-dd</summary>
    public string Date { get; set; } = "";
    public string LeaveType { get; set; } = "事假";
    public string Note { get; set; } = "";
    public double Hours { get; set; } = 8;
}
