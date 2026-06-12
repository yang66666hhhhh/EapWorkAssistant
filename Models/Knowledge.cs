namespace EapWorkAssistant.Models;

public class Knowledge
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string CreateTime { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
