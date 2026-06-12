using System.IO;
using System.Text.Json;

namespace EapWorkAssistant.Services;

public class ProbationSettings
{
    public string StartDate { get; set; } = string.Empty;
    public int DurationDays { get; set; } = 90;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant",
        "probation_settings.json");

    public static ProbationSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<ProbationSettings>(json) ?? CreateDefault();
            }
        }
        catch { }
        return CreateDefault();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static ProbationSettings CreateDefault()
    {
        return new ProbationSettings
        {
            StartDate = DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd"),
            DurationDays = 90
        };
    }
}
