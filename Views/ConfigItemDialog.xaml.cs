using System.Windows;

namespace EapWorkAssistant.Views;

public partial class ConfigItemDialog : Window
{
    public string ItemValue { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;

    public ConfigItemDialog(string title, string initialValue)
    {
        WindowTitle = title;
        ItemValue = initialValue;
        DataContext = this;
        InitializeComponent();
    }

    /// <summary>
    /// 窗口加载时：将自身尺寸设为 Owner（主窗口）大小，实现全屏遮罩覆盖
    /// </summary>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (Owner is Window owner)
        {
            Width = owner.ActualWidth;
            Height = owner.ActualHeight;
            Left = owner.Left;
            Top = owner.Top;
        }
    }

    private void InputTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        InputTextBox.SelectAll();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ItemValue))
        {
            ConfirmDialog.Alert("请输入内容");
            return;
        }
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
