using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Text.Json;

namespace EapWorkAssistant.Services;

/// <summary>
/// 个人信息持久化服务（单例），数据保存在 %LOCALAPPDATA%\EapWorkAssistant\profile.json
/// </summary>
public partial class ProfileService : ObservableObject
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant");

    private static readonly string ProfilePath = Path.Combine(ConfigDir, "profile.json");

    public static ProfileService Instance { get; } = new ProfileService();

    [ObservableProperty] private string _name = "EAP工程师";
    [ObservableProperty] private string _role = "试用期工程师";
    [ObservableProperty] private string _department = "设备自动化";
    [ObservableProperty] private string _industry = "PCB行业";
    [ObservableProperty] private string _focus = "设备自动化";

    static ProfileService()
    {
        Instance.Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(ProfilePath))
            {
                var json = File.ReadAllText(ProfilePath);
                var loaded = JsonSerializer.Deserialize<ProfileData>(json);
                if (loaded != null)
                {
                    Name = loaded.Name;
                    Role = loaded.Role;
                    Department = loaded.Department;
                    Industry = loaded.Industry;
                    Focus = loaded.Focus;
                    return;
                }
            }
        }
        catch { }
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(ConfigDir))
                Directory.CreateDirectory(ConfigDir);

            var data = new ProfileData
            {
                Name = Name,
                Role = Role,
                Department = Department,
                Industry = Industry,
                Focus = Focus
            };
            File.WriteAllText(ProfilePath, JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch { }
    }

    /// <summary>头像显示文字：取名字的第一个字符</summary>
    public string AvatarInitial => string.IsNullOrWhiteSpace(Name) ? "U" : Name[..1];

    /// <summary>保存后刷新所有绑定属性</summary>
    public void NotifyAllChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Role));
        OnPropertyChanged(nameof(Department));
        OnPropertyChanged(nameof(Industry));
        OnPropertyChanged(nameof(Focus));
        OnPropertyChanged(nameof(AvatarInitial));
    }

    private class ProfileData
    {
        public string Name { get; set; } = "EAP工程师";
        public string Role { get; set; } = "试用期工程师";
        public string Department { get; set; } = "设备自动化";
        public string Industry { get; set; } = "PCB行业";
        public string Focus { get; set; } = "设备自动化";
    }
}
