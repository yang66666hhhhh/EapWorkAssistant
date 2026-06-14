using EapWorkAssistant.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace EapWorkAssistant.Services;

public class ToastService
{
    public static ToastService Instance { get; } = new();

    public ObservableCollection<ToastMessage> Items { get; } = new();

    public ICommand DismissCommand { get; }

    private const int MaxToasts = 5;
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(3.5);
    private readonly Dictionary<Guid, DispatcherTimer> _timers = new();

    private ToastService()
    {
        DismissCommand = new RelayCommand(param =>
        {
            if (param is ToastMessage toast)
                Dismiss(toast);
        });
    }

    public void Show(string message, string title, ToastType type, TimeSpan? duration = null)
    {
        EnsureUiThread(() =>
        {
            var toast = new ToastMessage
            {
                Title = title,
                Message = message,
                Type = type
            };

            while (Items.Count >= MaxToasts)
                RemoveItem(Items[0]);

            Items.Add(toast);

            var delay = duration ?? DefaultDuration;
            var timer = new DispatcherTimer { Interval = delay };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                _timers.Remove(toast.Id);
                toast.IsDismissing = true;
                var removeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                removeTimer.Tick += (_, _) =>
                {
                    removeTimer.Stop();
                    RemoveItem(toast);
                };
                removeTimer.Start();
            };
            _timers[toast.Id] = timer;
            timer.Start();
        });
    }

    public void Dismiss(ToastMessage toast)
    {
        EnsureUiThread(() =>
        {
            if (_timers.TryGetValue(toast.Id, out var timer))
            {
                timer.Stop();
                _timers.Remove(toast.Id);
            }

            toast.IsDismissing = true;
            var removeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            removeTimer.Tick += (_, _) =>
            {
                removeTimer.Stop();
                RemoveItem(toast);
            };
            removeTimer.Start();
        });
    }

    public void DismissAll()
    {
        EnsureUiThread(() =>
        {
            foreach (var timer in _timers.Values)
                timer.Stop();
            _timers.Clear();
            Items.Clear();
        });
    }

    public static void Success(string message, string title = "成功")
        => Instance.Show(message, title, ToastType.Success);

    public static void Error(string message, string title = "错误")
        => Instance.Show(message, title, ToastType.Error, TimeSpan.FromSeconds(4.5));

    public static void Info(string message, string title = "提示")
        => Instance.Show(message, title, ToastType.Info);

    private void RemoveItem(ToastMessage toast)
    {
        if (Items.Contains(toast))
            Items.Remove(toast);
    }

    private static void EnsureUiThread(Action action)
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == true)
            action();
        else
            Application.Current?.Dispatcher?.BeginInvoke(action);
    }

    private class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        public RelayCommand(Action<object?> execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);
    }
}
