using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 通用悬停预览弹出层（单例）。
/// 替代系统 ToolTip，解决鼠标移到浮窗上即消失的问题。
/// 可用于 DataGridCell、ListBoxItem 等任意 FrameworkElement。
/// 所有颜色通过 DynamicResource 跟随主题变化。
/// </summary>
internal sealed class PreviewPopup
{
    public static readonly PreviewPopup Instance = new();

    private readonly Popup _popup;
    private readonly TextBox _textBox;
    private readonly CopyButton _copyButton;

    private DispatcherTimer? _showTimer;
    private FrameworkElement? _currentTarget;
    private Border _outerShell = null!;

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
            MaxWidth = 400,
            AcceptsReturn = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        _textBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");

        _copyButton = new CopyButton
        {
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // 左侧主色装饰条（跟随强调色）
        var accentBar = new Border
        {
            Width = 3,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 2, 10, 2),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        accentBar.SetResourceReference(Border.BackgroundProperty, "PrimaryBrush");

        var textArea = new StackPanel { Children = { _textBox, _copyButton } };

        var contentPanel = new DockPanel { Children = { accentBar, textArea } };

        // 主内容卡片（跟随主题卡片色）
        var content = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 14, 16, 14),
            BorderThickness = new Thickness(1),
            Child = contentPanel
        };
        content.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        content.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        // 多层 Border 模拟阴影（黑色半透明，深浅主题通用）
        // 注意：顶部 margin 极小以避免浮窗与单元格之间出现不可悬停的间隙
        var shadow1 = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Color.FromArgb(8, 0, 0, 0)),
            Margin = new Thickness(6, 0, 6, 10),
            Child = content
        };
        var shadow2 = new Border
        {
            CornerRadius = new CornerRadius(15),
            Background = new SolidColorBrush(Color.FromArgb(5, 0, 0, 0)),
            Margin = new Thickness(4, 0, 4, 4),
            Child = shadow1
        };
        _outerShell = new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromArgb(3, 0, 0, 0)),
            Margin = new Thickness(2, 0, 2, 2),
            Child = shadow2
        };

        _popup = new Popup
        {
            Child = _outerShell,
            StaysOpen = true,
            AllowsTransparency = true,
            Placement = PlacementMode.Bottom,
            VerticalOffset = 2,
            Focusable = false
        };

        // 鼠标离开弹出层 → 立即淡出
        content.MouseLeave += (_, _) => FadeOutAndClose();
    }

    public void Show(FrameworkElement target, string text)
    {
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
        _outerShell.Opacity = 1; // 确保可见（可能上次淡出中途被打断）

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

    public void Hide() => FadeOutAndClose();

    public void HideImmediate()
    {
        _showTimer?.Stop();
        _outerShell.Opacity = 1;
        _popup.IsOpen = false;
        _currentTarget = null;
    }

    private void FadeOutAndClose()
    {
        _showTimer?.Stop();
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            _popup.IsOpen = false;
            _outerShell.Opacity = 1; // 重置，下次打开时无残留
            _currentTarget = null;
        };
        _outerShell.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
