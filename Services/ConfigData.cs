using System.Collections.Generic;

namespace EapWorkAssistant.Services;

public class ConfigData
{
    public List<string> Projects { get; set; } = new();
    public List<string> WorkTypes { get; set; } = new();
}
