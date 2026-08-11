using System.Windows;
using System.Windows.Controls;
using EapWorkAssistant.Helpers;

namespace EapWorkAssistant.Views;

/// <summary>
/// Markdown 报告预览控件：将 Markdown 文本渲染为带格式的只读 FlowDocument。
/// 替代原有的纯文本框（避免用户直接看到 ##、** 等原始标记）。
/// </summary>
public partial class MarkdownViewer : UserControl
{
    public static readonly DependencyProperty MarkdownTextProperty =
        DependencyProperty.Register(
            nameof(MarkdownText),
            typeof(string),
            typeof(MarkdownViewer),
            new PropertyMetadata(null, OnMarkdownChanged));

    public string? MarkdownText
    {
        get => (string?)GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    public MarkdownViewer()
    {
        InitializeComponent();
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            viewer.Viewer.Document = MarkdownToFlowDocument.Build(e.NewValue as string);
        }
    }
}
