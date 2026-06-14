using CommunityToolkit.Mvvm.ComponentModel;

namespace EapWorkAssistant.Models;

public enum ToastType
{
    Success,
    Error,
    Info
}

public partial class ToastMessage : ObservableObject
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private ToastType _type = ToastType.Info;
    [ObservableProperty] private bool _isDismissing;

    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}
