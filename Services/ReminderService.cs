using EapWorkAssistant.Repositories;
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
        // 如果日期变了，重置提醒状态
        if (DateTime.Today != _lastCheckDate)
        {
            _lastCheckDate = DateTime.Today;
            _remindedToday = false;
        }

        // 如果已经提醒过，跳过
        if (_remindedToday) return;

        // 只在下班时间后提醒（17:30之后）
        var now = DateTime.Now;
        if (now.Hour < 17 || (now.Hour == 17 && now.Minute < 30))
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
        var result = MessageBox.Show(
            "今天还没有记录工作内容哦！\n\n及时记录有助于转正述职报告的生成。\n\n是否现在去记录？",
            "工作记录提醒",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            // 导航到工作记录页面
            if (Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel vm)
            {
                vm.NavigateToCommand.Execute("WorkRecord");
            }
        }
    }
}
