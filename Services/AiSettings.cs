using System.IO;
using System.Text.Json;
using EapWorkAssistant.Helpers;

namespace EapWorkAssistant.Services;

/// <summary>
/// AI 服务配置持久化（API 地址、密钥、模型、超时等）
/// 遵循 ProbationSettings 模式：静态 Load / 实例 Save
/// </summary>
public class AiSettings
{
    public string Endpoint { get; set; } = "https://api.deepseek.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "deepseek-chat";
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxTokens { get; set; } = 4096;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant",
        "ai_settings.json");

    /// <summary>
    /// 从本地 JSON 文件加载 AI 配置，失败时返回默认值
    /// </summary>
    public static AiSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AiSettings>(json) ?? new AiSettings();
            }
        }
        catch (Exception ex) { Logger.Error("加载 AI 配置失败，将使用默认值", ex); }
        return new AiSettings();
    }

    /// <summary>
    /// 将 AI 配置保存到本地 JSON 文件
    /// </summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            ToastService.Error($"保存 AI 配置失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 是否已配置 API 地址和密钥（可发起请求）
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(ApiKey);
}
