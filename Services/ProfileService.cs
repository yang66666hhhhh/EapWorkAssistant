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
    private static readonly string AvatarFile = Path.Combine(ConfigDir, "avatar.png");

    public static ProfileService Instance { get; } = new ProfileService();

    // ⚠️ 默认值刻意留空：此前硬编码「EAP工程师 / 试用期工程师 / PCB行业 / 设备自动化」，
    // 导致全新用户（甚至删库重装）永远显示成一个"假身份"，看起来像历史残留数据。
    // 现在未填写时统一走 Display* 展示「未设置」，由用户点击侧边栏卡片自行补全。
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _role = string.Empty;
    [ObservableProperty] private string _department = string.Empty;
    [ObservableProperty] private string _industry = string.Empty;
    [ObservableProperty] private string _focus = string.Empty;
    [ObservableProperty] private bool _isProbation;

    /// <summary>头像图片的完整路径（无头像时为 null）</summary>
    [ObservableProperty] private string? _avatarPath;

    // ===== 展示层（XAML 只绑这几个，避免各自判空）=====

    /// <summary>是否已填写过个人资料（以姓名是否为空为准）。</summary>
    public bool IsProfileSet => !string.IsNullOrWhiteSpace(Name);

    /// <summary>侧边栏展示名：未填写显示「未设置」。</summary>
    public string DisplayName => IsProfileSet ? Name : "未设置";

    /// <summary>侧边栏展示岗位：未填写时提示可点击补全。</summary>
    public string DisplayRole => string.IsNullOrWhiteSpace(Role) ? "点击填写个人资料" : Role;

    /// <summary>侧边栏展示行业。</summary>
    public string DisplayIndustry => string.IsNullOrWhiteSpace(Industry) ? "未设置" : Industry;

    /// <summary>侧边栏展示部门。</summary>
    public string DisplayDepartment => string.IsNullOrWhiteSpace(Department) ? "未设置" : Department;

    /// <summary>是否需要在下次启动时提示补全资料（资料为空且从未提示过）。</summary>
    public bool NeedsPrompt => !IsProfileSet && !ConfigService.Instance.HasPromptedProfile;

    /// <summary>标记「已提示过」——无论用户填没填，都只提示一次，不反复打扰。</summary>
    public void MarkPrompted() => ConfigService.Instance.HasPromptedProfile = true;

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
                    IsProbation = loaded.IsProbation;

                    // 加载头像
                    if (File.Exists(AvatarFile))
                        AvatarPath = AvatarFile;
                    else
                        AvatarPath = null;

                    return;
                }
            }
        }
        catch (Exception ex)
        {
            ToastService.Error($"个人信息加载失败：{ex.Message}");
        }
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
                Focus = Focus,
                IsProbation = IsProbation
            };
            File.WriteAllText(ProfilePath, JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch (Exception ex)
        {
            ToastService.Error($"个人信息保存失败：{ex.Message}");
        }
    }

    /// <summary>保存头像图片到本地存储</summary>
    public void SaveAvatar(string sourceFilePath)
    {
        try
        {
            if (!Directory.Exists(ConfigDir))
                Directory.CreateDirectory(ConfigDir);

            File.Copy(sourceFilePath, AvatarFile, overwrite: true);
            AvatarPath = AvatarFile;
            OnPropertyChanged(nameof(AvatarPath));
            OnPropertyChanged(nameof(HasAvatar));
        }
        catch (Exception ex)
        {
            ToastService.Error($"头像保存失败：{ex.Message}");
        }
    }

    /// <summary>清除头像</summary>
    public void ClearAvatar()
    {
        try
        {
            if (File.Exists(AvatarFile))
                File.Delete(AvatarFile);
        }
        catch { }
        AvatarPath = null;
        OnPropertyChanged(nameof(AvatarPath));
        OnPropertyChanged(nameof(HasAvatar));
    }

    /// <summary>是否有自定义头像</summary>
    public bool HasAvatar => !string.IsNullOrEmpty(AvatarPath) && File.Exists(AvatarPath);

    /// <summary>头像显示文字：取名字的第一个字符；未填写时显示「?」，不伪装成某个具体的人。</summary>
    public string AvatarInitial => string.IsNullOrWhiteSpace(Name) ? "?" : Name[..1];

    /// <summary>保存后刷新所有绑定属性</summary>
    public void NotifyAllChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Role));
        OnPropertyChanged(nameof(Department));
        OnPropertyChanged(nameof(Industry));
        OnPropertyChanged(nameof(Focus));
        OnPropertyChanged(nameof(IsProbation));
        OnPropertyChanged(nameof(AvatarInitial));
        OnPropertyChanged(nameof(AvatarPath));
        OnPropertyChanged(nameof(HasAvatar));
        OnPropertyChanged(nameof(IsProfileSet));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(DisplayRole));
        OnPropertyChanged(nameof(DisplayIndustry));
        OnPropertyChanged(nameof(DisplayDepartment));
    }

    private class ProfileData
    {
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public string Focus { get; set; } = string.Empty;
        public bool IsProbation { get; set; }
    }
}
