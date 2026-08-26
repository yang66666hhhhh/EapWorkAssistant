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

    public ConfirmDialog(string title, string message, ConfirmDialogType type = ConfirmDialogType.Warning,
        string confirmText = "确认", string cancelText = "取消")
    {
        WindowTitle = title;
        Message = message;
        DataContext = this;
        InitializeComponent();
        TitleText.Text = title;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;

        ConfirmButton.Style = (Style)FindResource("BtnPrimary");

        switch (type)
        {
            case ConfirmDialogType.Danger:
                IconBorder.Background = (Brush)FindResource("DangerLightBrush");
                IconPath.Data = (Geometry)FindResource("IconWarning");
                IconPath.Fill = (Brush)FindResource("DangerBrush");
                break;
            case ConfirmDialogType.Warning:
                IconBorder.Background = (Brush)FindResource("WarningLightBrush");
                IconPath.Data = (Geometry)FindResource("IconWarning");
                IconPath.Fill = (Brush)FindResource("WarningBrush");
                break;
            case ConfirmDialogType.Info:
                IconBorder.Background = (Brush)FindResource("PrimaryLightBrush");
                IconPath.Data = (Geometry)FindResource("IconInfo");
                IconPath.Fill = (Brush)FindResource("PrimaryBrush");
                break;
        }

        // 非危险操作支持回车确认；危险操作（删除确认）需用户主动点击，避免误删。
        // 取消按钮始终支持 Esc 关闭（见 xaml 的 IsCancel）。
        if (type != ConfirmDialogType.Danger)
            ConfirmButton.IsDefault = true;

        // 打开时按风险自动聚焦：危险操作聚焦"取消"（安全默认），其余聚焦"确认"，
        // 使键盘焦点可见（配合 DialogFocusVisual 焦点环），Esc/Enter 行为一目了然。
        Loaded += (_, _) =>
        {
            var focusTarget = type == ConfirmDialogType.Danger ? CancelButton : ConfirmButton;
            focusTarget.Focus();
        };
    }

    /// <summary>
    /// 快捷方法：显示确认对话框并返回用户选择
    /// </summary>
    public static bool Show(string message, string title = "确认", ConfirmDialogType type = ConfirmDialogType.Warning,
        string confirmText = "确认", string cancelText = "取消")
    {
        var dialog = CreateAndPositionDialog(title, message, type, confirmText, cancelText);
        return dialog.ShowDialog() == true;
    }

    /// <summary>
    /// 快捷方法：显示提示对话框（仅"确定"按钮）
    /// </summary>
    public static void Alert(string message, string title = "提示")
    {
        var dialog = CreateAndPositionDialog(title, message, ConfirmDialogType.Info, "确定", "取消");
        dialog.CancelButton.Visibility = Visibility.Collapsed;
        dialog.ConfirmButton.Content = "确定";
        dialog.ShowDialog();
    }

    /// <summary>
    /// 创建弹窗并按 Owner 全屏定位与缩放，使遮罩覆盖整个主窗口；
    /// 必须在 ShowDialog() 之前完成，避免「先小框居中再放大跳屏」的闪烁。
    /// </summary>
    private static ConfirmDialog CreateAndPositionDialog(string title, string message, ConfirmDialogType type,
        string confirmText, string cancelText)
    {
        var owner = Application.Current.MainWindow;
        var dialog = new ConfirmDialog(title, message, type, confirmText, cancelText)
        {
            Owner = owner,
            Left = owner.Left,
            Top = owner.Top,
            Width = owner.ActualWidth,
            Height = owner.ActualHeight
        };
        return dialog;
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
