using System.Windows;
using System.Windows.Controls;

namespace EapWorkAssistant.Controls;

/// <summary>
/// 统一搜索框：图标 + 输入框 + 水印占位符 + 一键清除。
/// 复用既有 SearchInput 样式、IconSearch/IconClose 资源与令牌，三页搜索框统一外观。
/// </summary>
public partial class SearchBox : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(SearchBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder), typeof(string), typeof(SearchBox),
            new PropertyMetadata("搜索…"));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public SearchBox()
    {
        InitializeComponent();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        Text = string.Empty;
        // 清除后让输入框重新获得焦点，方便连续输入
        InputBox?.Focus();
        InputBox?.SelectAll();
    }
}
