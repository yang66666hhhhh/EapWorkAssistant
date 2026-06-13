using EapWorkAssistant.Data;
using EapWorkAssistant.Services;
using System.Windows;

namespace EapWorkAssistant;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DatabaseInitializer.Initialize();
        DatabaseBackupService.BackupIfNeeded();
        ReminderService.Start();

        // 初始化主题服务（从配置加载并应用主题）
        ThemeService.Instance.Initialize();

        // 设置开机自启动状态
        AutoStartService.ApplyAutoStart(ConfigService.Instance.AutoStart);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ReminderService.Stop();
        base.OnExit(e);
    }
}
