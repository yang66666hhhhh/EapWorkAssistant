using EapWorkAssistant.Services;

namespace EapWorkAssistant.Models;

public static class ProjectInfo
{
    public static string[] Projects => ConfigService.Instance.Projects.ToArray();
    public static string[] WorkTypes => ConfigService.Instance.WorkTypes.ToArray();
}
