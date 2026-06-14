namespace EapWorkAssistant.Models;

public class Issue
{
    public int Id { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RootCause { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";         // Open, InProgress, Resolved, Closed
    public string Priority { get; set; } = "Medium";     // Low, Medium, High, Critical
    public string CreateTime { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
