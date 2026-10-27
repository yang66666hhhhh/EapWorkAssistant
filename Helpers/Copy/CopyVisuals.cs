using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 复制功能共享的视觉树遍历与文本提取。
/// 由 <see cref="DataGridCopyHelper"/> 与 <see cref="ListBoxCopyHelper"/> 共用，
/// 使两个附加属性互不依赖（P3 #8 从 785 行单文件拆出）。
/// </summary>
internal static class CopyVisuals
{
    /// <summary>
    /// 向上查找指定类型的祖先。
    /// VisualTreeHelper 可正确穿越 DataTemplate 边界；
    /// ContentElement（如 Run）不是 Visual，需先走 LogicalTree 到 TextBlock。
    /// </summary>
    public static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = child is Visual
                ? VisualTreeHelper.GetParent(child)
                : LogicalTreeHelper.GetParent(child);
        }
        return null;
    }

    /// <summary>向下深度优先查找第一个指定类型的子元素。</summary>
    public static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) return null;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>向下深度优先枚举全部指定类型的子元素。</summary>
    public static IEnumerable<T> FindAllVisualChildren<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) yield break;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) yield return found;
            foreach (var nested in FindAllVisualChildren<T>(child))
                yield return nested;
        }
    }

    /// <summary>
    /// 提取单元格文本：优先单个 TextBlock（含内联高亮），其次编辑态 TextBox，
    /// 最后兜底合并全部 TextBlock（模板列多段文本）。
    /// </summary>
    public static string ExtractCellText(DataGridCell cell)
    {
        var textBlock = FindVisualChild<TextBlock>(cell);
        if (textBlock != null)
        {
            var text = ExtractTextBlockText(textBlock);
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        }

        var textBox = FindVisualChild<TextBox>(cell);
        if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
            return textBox.Text.Trim();

        var allTexts = FindAllVisualChildren<TextBlock>(cell)
            .Select(ExtractTextBlockText)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        if (allTexts.Count > 0)
            return string.Join(" ", allTexts).Trim();

        return "";
    }

    /// <summary>
    /// 读取 TextBlock 文本；搜索高亮会把内容拆成 Inlines，此时 Text 为空，需走 TextRange。
    /// </summary>
    public static string ExtractTextBlockText(TextBlock textBlock)
    {
        if (!string.IsNullOrWhiteSpace(textBlock.Text))
            return textBlock.Text.Trim();

        if (textBlock.Inlines.Count > 0)
        {
            var inlineText = new TextRange(textBlock.ContentStart, textBlock.ContentEnd).Text;
            if (!string.IsNullOrWhiteSpace(inlineText))
                return inlineText.Trim();
        }

        return "";
    }

    /// <summary>给右键菜单套用全局 ContextMenu 样式（若已定义）。</summary>
    public static void ApplyContextMenuStyle(ContextMenu menu)
    {
        var style = Application.Current.TryFindResource(typeof(ContextMenu)) as Style;
        if (style != null) menu.Style = style;
    }

    /// <summary>把菜单项 Tag 中的文本写入剪贴板（失败静默：剪贴板可能被其他进程占用）。</summary>
    public static void CopyMenuItemText(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string text } && !string.IsNullOrEmpty(text))
        {
            try { Clipboard.SetText(text); } catch { }
        }
    }

    /// <summary>反射读取属性值，异常时返回 null（绑定路径可能不存在）。</summary>
    public static object? GetPropertyValue(object item, string? path)
    {
        if (string.IsNullOrEmpty(path)) return item;
        try
        {
            var prop = item.GetType().GetProperty(path);
            return prop?.GetValue(item);
        }
        catch { return null; }
    }

    /// <summary>按列 StringFormat 格式化值，格式串非法时回落到 ToString。</summary>
    public static string FormatValue(object? value, string? stringFormat)
    {
        if (value == null) return "";
        if (string.IsNullOrEmpty(stringFormat)) return value.ToString() ?? "";
        try { return string.Format(stringFormat, value); }
        catch { return value.ToString() ?? ""; }
    }
}
