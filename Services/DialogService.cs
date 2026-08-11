using EapWorkAssistant.Views;
using System.Windows;

namespace EapWorkAssistant.Services;

/// <summary>确认对话框类型（与 Views.ConfirmDialogType 对应，供 ViewModel 层无 WPF 依赖地使用）。</summary>
public enum ConfirmType
{
    Info,
    Warning,
    Danger
}

/// <summary>
/// 对话框抽象。ViewModel 只依赖此接口，不引用任何 WPF/View 类型，
/// 具体实现（WPF 窗口）收敛在 DialogService 中。
/// </summary>
public interface IDialogService
{
    /// <summary>弹出单行输入对话框，返回用户输入（取消或为空时返回 null）。</summary>
    string? ShowInputDialog(string title, string defaultValue);

    /// <summary>弹出确认对话框，返回用户是否确认。confirmText/cancelText 为空时使用默认按钮文字。</summary>
    bool ShowConfirm(string message, string title, ConfirmType type = ConfirmType.Info,
        string? confirmText = null, string? cancelText = null);
}

/// <summary>
/// IDialogService 的 WPF 实现。所有对 ConfigItemDialog / ConfirmDialog / Application.Current.MainWindow
/// 的引用都收敛在此处，ViewModel 层因此可保持 WPF 无关。
/// </summary>
public sealed class DialogService : IDialogService
{
    public static DialogService Instance { get; } = new();

    public string? ShowInputDialog(string title, string defaultValue)
    {
        var dialog = new ConfigItemDialog(title, defaultValue)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.ItemValue : null;
    }

    public bool ShowConfirm(string message, string title, ConfirmType type = ConfirmType.Info,
        string? confirmText = null, string? cancelText = null)
    {
        var mapped = type switch
        {
            ConfirmType.Danger => ConfirmDialogType.Danger,
            ConfirmType.Warning => ConfirmDialogType.Warning,
            _ => ConfirmDialogType.Info
        };
        if (confirmText == null && cancelText == null)
            return ConfirmDialog.Show(message, title, mapped);
        return ConfirmDialog.Show(message, title, mapped, confirmText ?? "确认", cancelText ?? "取消");
    }
}
