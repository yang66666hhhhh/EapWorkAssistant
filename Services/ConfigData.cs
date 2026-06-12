using System.Collections.Generic;

namespace EapWorkAssistant.Services;

public class ConfigData
{
    public List<string> Projects { get; set; } = new();
    public List<string> WorkTypes { get; set; } = new();
    public List<ContentTemplate> ContentTemplates { get; set; } = new();
    public bool EnableShortcuts { get; set; } = true;
    public bool EnableReminder { get; set; } = true;
    public int ReminderHour { get; set; } = 17;
    public int ReminderMinute { get; set; } = 30;
}

public class ContentTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
