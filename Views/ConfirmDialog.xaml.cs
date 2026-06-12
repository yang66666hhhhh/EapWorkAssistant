using System.Windows;
using System.Windows.Media;

namespace EapWorkAssistant.Views;

public enum ConfirmDialogType
{
    Warning,
    Danger,
    Info
}

public partial class ConfirmDialog : Window
{
    public string Message { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = "确认";

    public ConfirmDialog(string title, string message, ConfirmDialogType type = ConfirmDialogType.Warning)
    {
        WindowTitle = title;
        Message = message;
        DataContext = this;
        InitializeComponent();
        TitleText.Text = title;

        switch (type)
        {
            case ConfirmDialogType.Danger:
                IconBorder.Background = (Brush)FindResource("DangerLightBrush");
                IconText.Text = "\u26A0";
                ConfirmButton.Style = (Style)FindResource("BtnDanger");
                break;
            case ConfirmDialogType.Warning:
                IconBorder.Background = (Brush)FindResource("WarningLightBrush");
                IconText.Text = "\u26A0";
                ConfirmButton.Style = (Style)FindResource("BtnWarning");
                break;
            case ConfirmDialogType.Info:
                IconBorder.Background = (Brush)FindResource("PrimaryLightBrush");
                IconText.Text = "\u2139";
                ConfirmButton.Style = (Style)FindResource("BtnPrimary");
                break;
        }
    }

    /// <summary>
    /// 快捷方法：显示确认对话框并返回用户选择
    /// </summary>
    public static bool Show(string message, string title = "确认", ConfirmDialogType type = ConfirmDialogType.Warning)
    {
        var dialog = new ConfirmDialog(title, message, type)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true;
    }

    /// <summary>
    /// 快捷方法：显示提示对话框（仅"确定"按钮）
    /// </summary>
    public static void Alert(string message, string title = "提示")
    {
        var dialog = new ConfirmDialog(title, message, ConfirmDialogType.Info)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.CancelButton.Visibility = Visibility.Collapsed;
        dialog.ConfirmButton.Content = "确定";
        dialog.ShowDialog();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
