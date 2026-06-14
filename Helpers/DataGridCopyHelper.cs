using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// DataGrid 全局复制支持（附加属性）。
/// 设置 local:DataGridCopyHelper.EnableCopy="True" 即可启用：
///   - 悬停预览：鼠标停留 300ms 后弹出浮窗，支持选中复制
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

    private static void OnCellMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
        if (cell == _trackedCell) return;

        _trackedCell = cell;
        if (cell != null && IsContentColumn(cell.Column))
        {
            var text = ExtractCellText(cell);
            if (!string.IsNullOrEmpty(text))
                PreviewPopup.Instance.Show(cell, text);
            else
                PreviewPopup.Instance.Hide();
        }
        else
        {
            PreviewPopup.Instance.Hide();
        }
    }

    /// <summary>
    /// 自动判断列是否为"内容"列（需要悬停预览的长文本列）。
    /// 检测规则：Binding 路径为 Content/RootCause/Title，或列头为"内容"/"根本原因"/"标题"。
    /// 所有表格自动生效，无需逐页配置。
    /// </summary>
    private static bool IsContentColumn(DataGridColumn? column)
    {
        if (column == null) return false;

        // 检查文本列的 Binding 路径
        if (column is DataGridTextColumn textCol
            && textCol.Binding is System.Windows.Data.Binding b)
        {
            var path = b.Path?.Path;
            if (path is "Content" or "RootCause" or "Title")
                return true;
        }

        // 检查模板列的 ClipboardContentBinding
        if (column is DataGridTemplateColumn tmplCol
            && tmplCol.ClipboardContentBinding is System.Windows.Data.Binding cb)
        {
            var path = cb.Path?.Path;
            if (path is "Content" or "RootCause" or "Title")
                return true;
        }

        // 兜底：检查列头文字
        var header = column.Header?.ToString();
        return header is "内容" or "根本原因" or "标题";
    }

    private static void OnGridMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _trackedCell = null;
        PreviewPopup.Instance.Hide();
    }

    private static void OnGridMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject) == null)
            PreviewPopup.Instance.HideImmediate();
    }

    private static void OnGridScroll(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 || e.HorizontalChange != 0)
            PreviewPopup.Instance.HideImmediate();
    }

    // ==================== 右键菜单 ====================

    private static void OnRightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        PreviewPopup.Instance.HideImmediate();

        var cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
        if (cell == null) return;

        var item = cell.DataContext;
        var cellText = ExtractCellText(cell);
        var rowText = ExtractRowText(grid, item);

        if (string.IsNullOrEmpty(cellText) && string.IsNullOrEmpty(rowText)) return;

        var menu = new ContextMenu();
        ApplyContextMenuStyle(menu);

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

        cell.ContextMenu = menu;
        menu.PlacementTarget = cell;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    #region Visual Tree Helpers

    internal static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            // LogicalTreeHelper 兼容 Visual 和 ContentElement（如 Run），不会抛异常
            child = LogicalTreeHelper.GetParent(child);
        }
        return null;
    }

    internal static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
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

    internal static string ExtractCellText(DataGridCell cell)
    {
        var textBlock = FindVisualChild<TextBlock>(cell);
        if (textBlock != null)
        {
            var text = textBlock.Text;
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        }

        var textBox = FindVisualChild<TextBox>(cell);
        if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
            return textBox.Text.Trim();

        var allTexts = FindAllVisualChildren<TextBlock>(cell)
            .Select(tb => tb.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        if (allTexts.Count > 0)
            return string.Join(" ", allTexts).Trim();

        return "";
    }

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
        if (column is DataGridTextColumn textCol && textCol.Binding is System.Windows.Data.Binding textBinding)
        {
            var val = GetPropertyValue(item, textBinding.Path.Path);
            return FormatValue(val, textBinding.StringFormat);
        }

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

    internal static void ApplyContextMenuStyle(ContextMenu menu)
    {
        var style = Application.Current.TryFindResource(typeof(ContextMenu)) as Style;
        if (style != null) menu.Style = style;
    }
}

/// <summary>
/// 自定义复制按钮：内置点击反馈动画。
/// 点击后背景变绿、文字切换为"已复制"，1 秒后自动恢复。
/// </summary>
public class CopyButton : Button
{
    private readonly Border _bg;
    private readonly TextBlock _txt;
    private DispatcherTimer? _resetTimer;
    private readonly Color _normalColor = Color.FromRgb(0xEE, 0xF2, 0xFF); // #EEF2FF primary-light

    public CopyButton()
    {
        _txt = new TextBlock
        {
            Text = "📋 复制",
            FontSize = 12,
            FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush(Color.FromRgb(0x43, 0x38, 0xCA)), // primary
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _bg = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(_normalColor),
            Padding = new Thickness(10, 4, 10, 4),
            Child = _txt
        };

        // 直接设置 Content 为 Border，不使用 ControlTemplate
        Content = _bg;
        Cursor = Cursors.Hand;

        // 悬停效果
        _bg.MouseEnter += (_, _) =>
        {
            _bg.Background = new SolidColorBrush(Color.FromRgb(0xDB, 0xEA, 0xFE)); // lighter blue
        };
        _bg.MouseLeave += (_, _) =>
        {
            _bg.Background = new SolidColorBrush(_normalColor);
        };
    }

    /// <summary>设置按钮显示的文字（同时更新 Tag 用于复制）</summary>
    public void SetContentText(string text)
    {
        _txt.Text = text;
        Tag = text;
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (Tag is string text && !string.IsNullOrEmpty(text))
        {
            try { Clipboard.SetText(text); } catch { }
        }
        AnimateCopied();
    }

    private void AnimateCopied()
    {
        // 背景色动画：→ 成功绿
        var green = Color.FromRgb(0x05, 0x96, 0x69);
        _bg.Background = new SolidColorBrush(green);
        _txt.Foreground = new SolidColorBrush(Colors.White);

        string original = _txt.Text;
        _txt.Text = "✓ 已复制";

        _resetTimer?.Stop();
        _resetTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _resetTimer.Tick += (_, _) =>
        {
            _resetTimer.Stop();
            _bg.Background = new SolidColorBrush(_normalColor);
            _txt.Foreground = new SolidColorBrush(Color.FromRgb(0x43, 0x38, 0xCA));
            _txt.Text = original;
        };
        _resetTimer.Start();
    }
}

/// <summary>
/// 通用悬停预览弹出层（单例）。
/// 替代系统 ToolTip，解决鼠标移到浮窗上即消失的问题。
/// 可用于 DataGridCell、ListBoxItem 等任意 FrameworkElement。
/// </summary>
internal sealed class PreviewPopup
{
    public static readonly PreviewPopup Instance = new();

    private readonly Popup _popup;
    private readonly TextBox _textBox;
    private readonly CopyButton _copyButton;

    private DispatcherTimer? _showTimer;
    private DispatcherTimer? _hideTimer;
    private FrameworkElement? _currentTarget;

    private PreviewPopup()
    {
        _textBox = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            IsTabStop = false,
            FontSize = 13,
            MaxWidth = 380,
            AcceptsReturn = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27))
        };

        _copyButton = new CopyButton
        {
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var content = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10),
            Background = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB)),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Children = { _textBox, _copyButton } }
        };

        // 阴影层
        var shadowWrap = new Border
        {
            Child = content,
            Effect = new DropShadowEffect
            {
                BlurRadius = 24,
                ShadowDepth = 4,
                Opacity = 0.10,
                Color = Colors.Black
            }
        };

        _popup = new Popup
        {
            Child = shadowWrap,
            StaysOpen = true,
            AllowsTransparency = true,
            Placement = PlacementMode.Bottom,
            VerticalOffset = 2,
            Focusable = false
        };

        // 鼠标在弹出层上 → 取消隐藏
        content.MouseEnter += (_, _) => _hideTimer?.Stop();
        content.MouseLeave += (_, _) => StartHideTimer();
        _textBox.MouseEnter += (_, _) => _hideTimer?.Stop();
    }

    public void Show(FrameworkElement target, string text)
    {
        _hideTimer?.Stop();
        _showTimer?.Stop();

        if (string.IsNullOrWhiteSpace(text))
        {
            HideImmediate();
            return;
        }

        _currentTarget = target;
        _textBox.Text = text;
        _copyButton.Tag = text;
        _popup.PlacementTarget = target;

        _showTimer = new DispatcherTimer(DispatcherPriority.Input, Application.Current.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _showTimer.Tick += (_, _) =>
        {
            _showTimer.Stop();
            if (_currentTarget != null && _currentTarget.IsMouseOver)
                _popup.IsOpen = true;
        };
        _showTimer.Start();
    }

    public void Hide() => StartHideTimer();

    public void HideImmediate()
    {
        _showTimer?.Stop();
        _hideTimer?.Stop();
        _popup.IsOpen = false;
        _currentTarget = null;
    }

    private void StartHideTimer()
    {
        _hideTimer?.Stop();
        _hideTimer = new DispatcherTimer(DispatcherPriority.Input, Application.Current.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (!_popup.IsMouseOver && (_currentTarget == null || !_currentTarget.IsMouseOver))
            {
                _popup.IsOpen = false;
                _currentTarget = null;
            }
        };
        _hideTimer.Start();
    }
}

/// <summary>
/// ListBox 全局右键复制支持（附加属性）。
/// 在 ListBox 上设置 local:ListBoxCopyHelper.EnableCopy="True" 即可启用：
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
            listBox.PreviewMouseRightButtonDown += OnRightClick;
        else
            listBox.PreviewMouseRightButtonDown -= OnRightClick;
    }

    private static void OnRightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox) return;

        var listBoxItem = DataGridCopyHelper.FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (listBoxItem == null) return;

        var item = listBoxItem.DataContext;
        if (item == null) return;

        var clickedText = ExtractClickedText(e.OriginalSource as DependencyObject, listBoxItem);
        var fullText = ExtractFullText(item);

        if (string.IsNullOrEmpty(clickedText) && string.IsNullOrEmpty(fullText)) return;

        var menu = new ContextMenu();
        DataGridCopyHelper.ApplyContextMenuStyle(menu);

        if (!string.IsNullOrEmpty(clickedText))
        {
            var copyText = new MenuItem
            {
                Header = "复制",
                InputGestureText = "Ctrl+C",
                Tag = clickedText
            };
            copyText.Click += CopyToClipboard;
            menu.Items.Add(copyText);
        }

        if (!string.IsNullOrEmpty(fullText) && fullText != clickedText)
        {
            var copyAll = new MenuItem { Header = "复制整条", Tag = fullText };
            copyAll.Click += CopyToClipboard;
            menu.Items.Add(copyAll);
        }

        if (menu.Items.Count == 0) return;

        listBoxItem.ContextMenu = menu;
        menu.PlacementTarget = listBoxItem;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private static string ExtractClickedText(DependencyObject? source, ListBoxItem container)
    {
        var textBlock = DataGridCopyHelper.FindVisualParent<TextBlock>(source);
        while (textBlock != null)
        {
            var text = textBlock.Text;
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
            textBlock = DataGridCopyHelper.FindVisualParent<TextBlock>(
                VisualTreeHelper.GetParent(textBlock));
        }
        return "";
    }

    private static string ExtractFullText(object item)
    {
        var parts = new List<string>();
        var properties = new[] { "Title", "Content", "Tags", "Category", "CreateTime" };

        foreach (var name in properties)
        {
            try
            {
                var prop = item.GetType().GetProperty(name);
                var val = prop?.GetValue(item)?.ToString();
                if (!string.IsNullOrWhiteSpace(val))
                    parts.Add(val);
            }
            catch { }
        }

        return parts.Count > 0 ? string.Join("\t", parts) : item.ToString() ?? "";
    }

    private static void CopyToClipboard(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string text } && !string.IsNullOrEmpty(text))
        {
            try { Clipboard.SetText(text); } catch { }
        }
    }
}
