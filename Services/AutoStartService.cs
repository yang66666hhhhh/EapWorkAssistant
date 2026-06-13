using Microsoft.Win32;
using System.Diagnostics;

namespace EapWorkAssistant.Services;

/// <summary>
/// 开机自启动服务（通过注册表实现）
/// </summary>
public static class AutoStartService
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "EapWorkAssistant";

    /// <summary>
    /// 应用或取消开机自启动
    /// </summary>
    public static void ApplyAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = GetExePath();
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch
        {
            // 静默处理注册表权限问题
        }
    }

    /// <summary>
    /// 查询当前是否已设置开机自启
    /// </summary>
    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    private static string GetExePath()
    {
        try
        {
            return Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return Environment.ProcessPath ?? string.Empty;
        }
    }
}
