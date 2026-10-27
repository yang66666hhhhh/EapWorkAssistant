using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// DataGrid 全局复制支持（附加属性）。
/// 设置 local:DataGridCopyHelper.EnableCopy="True" 即可启用：
///   - 悬停预览：鼠标停留 300ms 后弹出浮窗，支持选中复制
///   - 右键菜单：复制单元格 / 复制整行
/// <para>
/// ⚠️ 当前**未接线**：<c>ModernGrid</c> 样式里没有该附加属性 Setter（全项目仅 ListBox 版启用）。
/// 悬停浮窗曾于 <c>d06dbd8</c> 从 Styles.xaml 主动回退，表格改用单元格 ToolTip——
/// 原因是浮窗在密集网格里频繁弹出干扰阅读。若要恢复，建议只启用右键菜单
/// （需拆分 <see cref="OnPropertyChanged"/> 的事件订阅），并先评估与既有 ToolTip 的双重提示问题。
/// </para>
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
        {
            grid.PreviewMouseRightButtonDown += OnRightClick;
            grid.PreviewMouseMove += OnCellMouseMove;
            grid.AddHandler(UIElement.MouseLeaveEvent, new MouseEventHandler(OnGridMouseLeave));
            grid.PreviewMouseDown += OnGridMouseDown;
            grid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnGridScroll));
        }
        else
        {
            grid.PreviewMouseRightButtonDown -= OnRightClick;
            grid.PreviewMouseMove -= OnCellMouseMove;
            grid.RemoveHandler(UIElement.MouseLeaveEvent, new MouseEventHandler(OnGridMouseLeave));
            grid.PreviewMouseDown -= OnGridMouseDown;
            grid.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnGridScroll));
        }
    }

    // ==================== 悬停预览 ====================

    private static DataGridCell? _trackedCell;

    private static void OnCellMouseMove(object sender, MouseEventArgs e)
    {
        var cell = CopyVisuals.FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
        if (cell == _trackedCell) return;

        _trackedCell = cell;
        if (cell != null && IsContentColumn(cell.Column))
        {
            var text = CopyVisuals.ExtractCellText(cell);
            if (!string.IsNullOrEmpty(text))
            {
                // 问题列悬停预览时，同时展示对应的解决方案
                if (IsProblemColumn(cell.Column) && cell.DataContext != null)
                {
                    var solution = CopyVisuals.GetPropertyValue(cell.DataContext, "Solution")?.ToString();
                    if (!string.IsNullOrWhiteSpace(solution))
                        text += $"\n\n解决方案：{solution}";
                }
                PreviewPopup.Instance.Show(cell, text);
            }
            else
                PreviewPopup.Instance.Hide();
        }
        else
        {
            PreviewPopup.Instance.Hide();
        }
    }

    /// <summary>
    /// 自动识别需要悬停预览的长文本列。
    /// 统一让内容型列走浮窗预览，避免长文本只能依赖系统 ToolTip。
    /// </summary>
    private static readonly HashSet<string> _contentPaths = new()
    {
        "Content", "Achievement", "Problem",
        "Description", "RootCause", "Solution",
        "Keywords", "Title"
    };

    private static bool IsContentColumn(DataGridColumn? column)
    {
        if (column == null) return false;

        if (column is DataGridTextColumn textCol
            && textCol.Binding is Binding b
            && _contentPaths.Contains(b.Path?.Path ?? ""))
            return true;

        if (column is DataGridTemplateColumn tmplCol
            && tmplCol.ClipboardContentBinding is Binding cb
            && _contentPaths.Contains(cb.Path?.Path ?? ""))
            return true;

        var header = column.Header?.ToString();
        return header == "内容" || header == "工作成果" || header == "问题"
            || header == "描述" || header == "根本原因" || header == "解决方案"
            || header == "关键词" || header == "标题";
    }

    private static bool IsProblemColumn(DataGridColumn? column)
    {
        if (column == null) return false;
        if (column is DataGridTextColumn textCol
            && textCol.Binding is Binding b
            && b.Path?.Path == "Problem")
            return true;
        return column.Header?.ToString() == "问题";
    }

    private static void OnGridMouseLeave(object sender, MouseEventArgs e)
    {
        _trackedCell = null;
        PreviewPopup.Instance.Hide();
    }

    private static void OnGridMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (CopyVisuals.FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject) == null)
            PreviewPopup.Instance.HideImmediate();
    }

    private static void OnGridScroll(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 || e.HorizontalChange != 0)
            PreviewPopup.Instance.HideImmediate();
    }

    // ==================== 右键菜单 ====================

    private static void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        PreviewPopup.Instance.HideImmediate();

        var cell = CopyVisuals.FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
        if (cell == null) return;

        var item = cell.DataContext;
        var cellText = CopyVisuals.ExtractCellText(cell);
        var rowText = ExtractRowText(grid, item);

        if (string.IsNullOrEmpty(cellText) && string.IsNullOrEmpty(rowText)) return;

        var menu = new ContextMenu();
        CopyVisuals.ApplyContextMenuStyle(menu);

        if (!string.IsNullOrEmpty(cellText))
        {
            var copyCell = new MenuItem
            {
                Header = "复制单元格",
                InputGestureText = "Ctrl+C",
                Tag = cellText
            };
            copyCell.Click += CopyVisuals.CopyMenuItemText;
            menu.Items.Add(copyCell);
        }

        if (!string.IsNullOrEmpty(rowText))
        {
            var copyRow = new MenuItem { Header = "复制整行", Tag = rowText };
            copyRow.Click += CopyVisuals.CopyMenuItemText;
            menu.Items.Add(copyRow);
        }

        cell.ContextMenu = menu;
        menu.PlacementTarget = cell;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    // ==================== 整行文本 ====================

    /// <summary>按列顺序拼接整行文本（Tab 分隔，跳过操作列与隐藏列）。</summary>
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
        if (column is DataGridTextColumn textCol && textCol.Binding is Binding textBinding)
        {
            var val = CopyVisuals.GetPropertyValue(item, textBinding.Path.Path);
            return CopyVisuals.FormatValue(val, textBinding.StringFormat);
        }

        if (column is DataGridTemplateColumn templateCol
            && templateCol.ClipboardContentBinding is Binding clipBinding)
        {
            var val = CopyVisuals.GetPropertyValue(item, clipBinding.Path.Path);
            return CopyVisuals.FormatValue(val, clipBinding.StringFormat);
        }

        return "";
    }
}
