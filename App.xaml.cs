using EapWorkAssistant.Data;
using EapWorkAssistant.Services;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace EapWorkAssistant;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 全局异常兜底：防止未捕获异常导致应用直接崩溃
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

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

    /// <summary>
    /// UI 线程未捕获异常
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogAndNotify(e.Exception);
        e.Handled = true; // 阻止应用直接崩溃
    }

    /// <summary>
    /// 非 UI 线程未捕获异常（通常无法恢复）
    /// </summary>
    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogAndNotify(ex);
        }
    }

    /// <summary>
    /// Task 中未 await 的异常
    /// </summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogAndNotify(e.Exception);
        e.SetObserved();
    }

    /// <summary>
    /// 记录异常日志并通知用户
    /// </summary>
    private static void LogAndNotify(Exception ex)
    {
        try
        {
            // 写入日志文件
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EapWorkAssistant", "logs");
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, $"error_{DateTime.Now:yyyyMMdd}.log");
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(logFile, logEntry);
        }
        catch { /* 日志写入失败不应阻塞 */ }

        // Toast 通知用户
        try
        {
            ToastService.Error($"发生了一个错误：{ex.Message}");
        }
        catch { /* Toast 失败不应阻塞 */ }
    }
}
