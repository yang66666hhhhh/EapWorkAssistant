using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace EapWorkAssistant.Models;

public enum ToastType
{
    Success,
    Error,
    Info,
    Warning
}

public partial class ToastMessage : ObservableObject
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private ToastType _type = ToastType.Info;
    [ObservableProperty] private bool _isDismissing;

    // 可选动作（如「撤销」），为空则不显示按钮
    [ObservableProperty] private string _actionText = string.Empty;
    [ObservableProperty] private bool _hasAction;
    public ICommand? ActionCommand { get; set; }

    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}
