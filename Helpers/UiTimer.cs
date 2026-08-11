using System;
using System.Windows.Threading;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 对 DispatcherTimer 的轻量包装，使 ViewModel 不必直接引用 System.Windows.Threading，
/// 同时保留 UI 线程上的计时语义（Tick 仍在 UI 线程触发，行为与 DispatcherTimer 一致）。
/// 仅通过 Start/Stop/Interval/Tick/IsEnabled 暴露原计时器能力。
/// </summary>
public sealed class UiTimer
{
    private readonly DispatcherTimer _timer = new();

    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public bool IsEnabled => _timer.IsEnabled;

    public event EventHandler Tick
    {
        add => _timer.Tick += value;
        remove => _timer.Tick -= value;
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();
}
