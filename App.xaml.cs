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
    }
}
