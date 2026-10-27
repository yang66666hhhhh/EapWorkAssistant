using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// ListBox 全局复制支持（附加属性）。
/// 在 ListBox 上设置 local:ListBoxCopyHelper.EnableCopy="True" 即可启用：
///   - 悬停预览：鼠标停留后弹出浮窗，支持选中复制
///   - 右键菜单：复制点击位置文本 / 复制整条记录
/// </summary>
public static class ListBoxCopyHelper
{
    public static readonly DependencyProperty EnableCopyProperty =
        DependencyProperty.RegisterAttached("EnableCopy", typeof(bool), typeof(ListBoxCopyHelper),
            new PropertyMetadata(false, OnPropertyChanged));

    public static void SetEnableCopy(DependencyObject obj, bool value) => obj.SetValue(EnableCopyProperty, value);
    public static bool GetEnableCopy(DependencyObject obj) => (bool)obj.GetValue(EnableCopyProperty);

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox) return;
        if ((bool)e.NewValue)
        {
            listBox.PreviewMouseRightButtonDown += OnRightClick;
            listBox.PreviewMouseMove += OnItemMouseMove;
            listBox.AddHandler(UIElement.MouseLeaveEvent, new MouseEventHandler(OnListBoxMouseLeave));
        }
        else
        {
            listBox.PreviewMouseRightButtonDown -= OnRightClick;
            listBox.PreviewMouseMove -= OnItemMouseMove;
            listBox.RemoveHandler(UIElement.MouseLeaveEvent, new MouseEventHandler(OnListBoxMouseLeave));
        }
    }

    // ==================== 悬停预览 ====================

    private static ListBoxItem? _trackedItem;

    private static void OnItemMouseMove(object sender, MouseEventArgs e)
    {
        var item = CopyVisuals.FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item == _trackedItem) return;

        _trackedItem = item;
        if (item?.DataContext != null)
        {
            var text = ExtractFullText(item.DataContext);
            if (!string.IsNullOrEmpty(text))
                PreviewPopup.Instance.Show(item, text);
            else
                PreviewPopup.Instance.Hide();
        }
        else
        {
            PreviewPopup.Instance.Hide();
        }
    }

    private static void OnListBoxMouseLeave(object sender, MouseEventArgs e)
    {
        _trackedItem = null;
        PreviewPopup.Instance.Hide();
    }

    // ==================== 右键菜单 ====================

    private static void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox) return;

        var listBoxItem = CopyVisuals.FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (listBoxItem == null) return;

        var item = listBoxItem.DataContext;
        if (item == null) return;

        var clickedText = ExtractClickedText(e.OriginalSource as DependencyObject);
        var fullText = ExtractFullText(item);

        if (string.IsNullOrEmpty(clickedText) && string.IsNullOrEmpty(fullText)) return;

        var menu = new ContextMenu();
        CopyVisuals.ApplyContextMenuStyle(menu);

        if (!string.IsNullOrEmpty(clickedText))
        {
            var copyText = new MenuItem
            {
                Header = "复制",
                InputGestureText = "Ctrl+C",
                Tag = clickedText
            };
            copyText.Click += CopyVisuals.CopyMenuItemText;
            menu.Items.Add(copyText);
        }

        if (!string.IsNullOrEmpty(fullText) && fullText != clickedText)
        {
            var copyAll = new MenuItem { Header = "复制整条", Tag = fullText };
            copyAll.Click += CopyVisuals.CopyMenuItemText;
            menu.Items.Add(copyAll);
        }

        if (menu.Items.Count == 0) return;

        listBoxItem.ContextMenu = menu;
        menu.PlacementTarget = listBoxItem;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    // ==================== 文本提取 ====================

    /// <summary>取鼠标位置上最近的非空 TextBlock 文本。</summary>
    private static string ExtractClickedText(DependencyObject? source)
    {
        var textBlock = CopyVisuals.FindVisualParent<TextBlock>(source);
        while (textBlock != null)
        {
            var text = CopyVisuals.ExtractTextBlockText(textBlock);
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
            textBlock = CopyVisuals.FindVisualParent<TextBlock>(
                VisualTreeHelper.GetParent(textBlock));
        }
        return "";
    }

    /// <summary>按知识库卡片字段顺序拼接整条文本（Tab 分隔）。</summary>
    private static string ExtractFullText(object item)
    {
        var parts = new List<string>();
        var properties = new[] { "Title", "Content", "Tags", "Category", "CreateTime" };

        foreach (var name in properties)
        {
            var val = CopyVisuals.GetPropertyValue(item, name)?.ToString();
            if (!string.IsNullOrWhiteSpace(val))
                parts.Add(val);
        }

        return parts.Count > 0 ? string.Join("\t", parts) : item.ToString() ?? "";
    }
}
