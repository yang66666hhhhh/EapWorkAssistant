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
