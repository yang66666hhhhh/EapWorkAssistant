using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 自定义复制按钮：无边框文字链风格，悬停时显示下划线。
/// 所有颜色通过 DynamicResource 跟随主题/强调色变化。
/// </summary>
public class CopyButton : Button
{
    private readonly TextBlock _txt;
    private readonly Path _icon;
    private readonly Geometry _clipboardGeo;
    private readonly Geometry _checkGeo;
    private readonly StackPanel _content;
    private readonly Line _underline;
    private DispatcherTimer? _resetTimer;
    private bool _copied;

    public CopyButton()
    {
        _clipboardGeo = (Geometry)(Application.Current.TryFindResource("IconClipboard")
            ?? Geometry.Parse("M19,2H15.7C15.3,0.8 14.2,0 13,0H11C9.8,0 8.7,0.8 8.3,2H5A2,2 0 0,0 3,4V20A2,2 0 0,0 5,22H19A2,2 0 0,0 21,20V4A2,2 0 0,0 19,2M11,2H13A1,1 0 0,1 14,3A1,1 0 0,1 13,4H11A1,1 0 0,1 10,3A1,1 0 0,1 11,2Z"));
        _checkGeo = (Geometry)(Application.Current.TryFindResource("IconCheck")
            ?? Geometry.Parse("M9,20.42L2.79,14.21L5.62,11.38L9,14.77L18.88,4.88L21.71,7.71L9,20.42Z"));

        _icon = new Path
        {
            Data = _clipboardGeo,
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        _icon.SetResourceReference(Shape.FillProperty, "TextTertiaryBrush");

        _txt = new TextBlock
        {
            Text = "复制",
            FontSize = 12,
            FontWeight = FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        };
        _txt.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");

        // 下划线（默认隐藏，hover 时显示）
        _underline = new Line
        {
            X1 = 0, X2 = 30,
            StrokeThickness = 0.8,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, -1)
        };
        _underline.SetResourceReference(Line.StrokeProperty, "TextTertiaryBrush");

        var textWithLine = new Grid
        {
            Children = { _txt, _underline }
        };

        _content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _icon, textWithLine }
        };

        Content = _content;
        Cursor = Cursors.Hand;
        FocusVisualStyle = null;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);

        MouseEnter += (_, _) =>
        {
            if (_copied) return;
            _icon.SetResourceReference(Shape.FillProperty, "PrimaryBrush");
            _txt.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryBrush");
            _underline.SetResourceReference(Line.StrokeProperty, "PrimaryBrush");
            _underline.Visibility = Visibility.Visible;
        };
        MouseLeave += (_, _) =>
        {
            if (_copied) return;
            ResetStyle();
        };
    }

    /// <summary>恢复默认文字链风格</summary>
    private void ResetStyle()
    {
        _icon.SetResourceReference(Shape.FillProperty, "TextTertiaryBrush");
        _txt.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");
        _underline.Visibility = Visibility.Collapsed;
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
        _copied = true;
        _underline.Visibility = Visibility.Collapsed;
        _icon.Fill = (Brush)Application.Current.TryFindResource("SuccessBrush") ?? Brushes.Green;
        _icon.Data = _checkGeo;
        _txt.Foreground = (Brush)Application.Current.TryFindResource("SuccessBrush") ?? Brushes.Green;
        _txt.Text = "已复制";

        _resetTimer?.Stop();
        _resetTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        _resetTimer.Tick -= OnResetTimerTick;
        _resetTimer.Tick += OnResetTimerTick;
        _resetTimer.Start();
    }

    private void OnResetTimerTick(object? sender, EventArgs e)
    {
        _resetTimer?.Stop();
        _copied = false;
        _icon.Data = _clipboardGeo;
        _txt.Text = "复制";
        ResetStyle();
    }
}
