using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// DataGrid 全局右键复制支持（附加属性）。
/// 在 DataGrid 上设置 local:DataGridCopyHelper.EnableCopy="True" 即可启用：
///   - 右键菜单：复制单元格 / 复制整行
/// </summary>
public static class DataGridCopyHelper
{
    public static readonly DependencyProperty EnableCopyProperty =
        DependencyProperty.RegisterAttached("EnableCopy", typeof(bool), typeof(DataGridCopyHelper),
            new PropertyMetadata(false, OnPropertyChanged));

    public static void SetEnableCopy(DependencyObject obj, bool value) => obj.SetValue(EnableCopyProperty, value);
    public static bool GetEnableCopy(DependencyObject obj) => (bool)obj.GetValue(EnableCopyProperty);

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        if ((bool)e.NewValue)
            grid.PreviewMouseRightButtonDown += OnRightClick;
        else
            grid.PreviewMouseRightButtonDown -= OnRightClick;
    }

    private static void OnRightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid) return;

        var cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
        if (cell == null) return;

        var item = cell.DataContext;
        var cellText = ExtractCellText(cell);
        var rowText = ExtractRowText(grid, item);

        if (string.IsNullOrEmpty(cellText) && string.IsNullOrEmpty(rowText)) return;

        var menu = new ContextMenu();

        if (!string.IsNullOrEmpty(cellText))
        {
            var copyCell = new MenuItem
            {
                Header = "复制单元格",
                InputGestureText = "Ctrl+C",
                Tag = cellText
            };
            copyCell.Click += CopyToClipboard;
            menu.Items.Add(copyCell);
        }

        if (!string.IsNullOrEmpty(rowText))
        {
            var copyRow = new MenuItem { Header = "复制整行", Tag = rowText };
            copyRow.Click += CopyToClipboard;
            menu.Items.Add(copyRow);
        }

        // 替换单元格上已有的 ContextMenu 并打开
        cell.ContextMenu = menu;
        menu.PlacementTarget = cell;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    #region Visual Tree Helpers

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
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

    private static IEnumerable<T> FindAllVisualChildren<T>(DependencyObject? parent) where T : DependencyObject
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

    #endregion

    #region Text Extraction

    /// <summary>从单元格视觉树中提取显示文本</summary>
    private static string ExtractCellText(DataGridCell cell)
    {
        // 1. 优先读取第一个 TextBlock.Text（含 Highlight 高亮 Inlines 拼接文本）
        var textBlock = FindVisualChild<TextBlock>(cell);
        if (textBlock != null)
        {
            var text = textBlock.Text;
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        }

        // 2. TextBox（编辑模式）
        var textBox = FindVisualChild<TextBox>(cell);
        if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
            return textBox.Text.Trim();

        // 3. 拼接所有 TextBlock（进度条等复合模板）
        var allTexts = FindAllVisualChildren<TextBlock>(cell)
            .Select(tb => tb.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        if (allTexts.Count > 0)
            return string.Join(" ", allTexts).Trim();

        return "";
    }

    /// <summary>从 DataGrid 所有可见列中提取整行文本（tab 分隔）</summary>
    private static string ExtractRowText(DataGrid grid, object? item)
    {
        if (item == null) return "";

        var parts = new List<string>();
        foreach (var column in grid.Columns)
        {
            if (!column.Visibility.HasFlag(Visibility.Visible)) continue;
            if (column is DataGridTemplateColumn tc && tc.Header?.ToString() == "操作")
                continue;

            var text = GetColumnCellText(column, item);
            if (!string.IsNullOrWhiteSpace(text))
                parts.Add(text);
        }
        return string.Join("\t", parts);
    }

    private static string GetColumnCellText(DataGridColumn column, object item)
    {
        // DataGridTextColumn: 从 Binding path 反射
        if (column is DataGridTextColumn textCol && textCol.Binding is System.Windows.Data.Binding textBinding)
        {
            var val = GetPropertyValue(item, textBinding.Path.Path);
            return FormatValue(val, textBinding.StringFormat);
        }

        // DataGridTemplateColumn: 优先用 ClipboardContentBinding
        if (column is DataGridTemplateColumn templateCol)
        {
            if (templateCol.ClipboardContentBinding is System.Windows.Data.Binding clipBinding)
            {
                var val = GetPropertyValue(item, clipBinding.Path.Path);
                return FormatValue(val, clipBinding.StringFormat);
            }
        }

        return "";
    }

    private static object? GetPropertyValue(object item, string? path)
    {
        if (string.IsNullOrEmpty(path)) return item;
        try
        {
            var prop = item.GetType().GetProperty(path);
            return prop?.GetValue(item);
        }
        catch { return null; }
    }

    private static string FormatValue(object? value, string? stringFormat)
    {
        if (value == null) return "";
        if (string.IsNullOrEmpty(stringFormat)) return value.ToString() ?? "";
        try { return string.Format(stringFormat, value); }
        catch { return value.ToString() ?? ""; }
    }

    #endregion

    private static void CopyToClipboard(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string text } && !string.IsNullOrEmpty(text))
        {
            try { Clipboard.SetText(text); } catch { }
        }
    }
}

/// <summary>
/// 自定义按钮：点击时将 Tag 中的文本复制到剪贴板。
/// 用于 ToolTip 模板中的"复制"按钮（避免 XAML 事件绑定问题）。
/// </summary>
public class CopyButton : Button
{
    protected override void OnClick()
    {
        base.OnClick();
        if (Tag is string text && !string.IsNullOrEmpty(text))
        {
            try { Clipboard.SetText(text); } catch { }
        }
    }
}
