using EapWorkAssistant.Repositories;
using EapWorkAssistant.Views;
using System.Windows;
using System.Windows.Threading;

namespace EapWorkAssistant.Services;

public static class ReminderService
{
    private static readonly WorkRecordRepository _repo = new();
    private static DispatcherTimer? _timer;
    private static bool _remindedToday;
    private static DateTime _lastCheckDate;

    public static void Start()
    {
        _lastCheckDate = DateTime.Today;
        _remindedToday = false;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(10) // 每10分钟检查一次
        };
        _timer.Tick += async (_, _) => await CheckAndRemindAsync();
        _timer.Start();
    }

    public static void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    private static async Task CheckAndRemindAsync()
    {
        // 检查是否启用了提醒
        if (!ConfigService.Instance.EnableReminder) return;

        // 如果日期变了，重置提醒状态
        if (DateTime.Today != _lastCheckDate)
        {
            _lastCheckDate = DateTime.Today;
            _remindedToday = false;
        }

        // 如果已经提醒过，跳过
        if (_remindedToday) return;

        // 使用用户配置的提醒时间
        var now = DateTime.Now;
        var reminderHour = ConfigService.Instance.ReminderHour;
        var reminderMinute = ConfigService.Instance.ReminderMinute;
        if (now.Hour < reminderHour || (now.Hour == reminderHour && now.Minute < reminderMinute))
            return;

        // 检查今天是否有记录
        try
        {
            var today = now.ToString("yyyy-MM-dd");
            var records = await _repo.GetByDateAsync(today);

            if (!records.Any())
            {
                _remindedToday = true;
                ShowReminder();
            }
        }
        catch
        {
            // 忽略错误
        }
    }

    private static void ShowReminder()
    {
        _remindedToday = true;
        ToastService.Info("今天还没有记录工作，点击「工作记录」开始记录吧！", "工作记录提醒");
    }
}
