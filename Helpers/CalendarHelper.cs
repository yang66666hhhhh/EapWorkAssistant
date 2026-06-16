using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 日历浮窗定位工具（与 DrawerHelper 配合使用）
/// </summary>
public static class CalendarHelper
{
    private const double DefaultWidth = 310;
    private const double DefaultHeight = 290;
    private const int FadeMs = 150;

    /// <summary>
    /// 在锚点按钮附近显示日历浮窗，自动处理边界碰撞
    /// </summary>
    public static void Show(
        Border backdrop,
        Border container,
        FrameworkElement anchorButton,
        UserControl host,
        double horizontalOffset = -100)
    {
        var buttonPos = anchorButton.TransformToAncestor(host)
            .Transform(new Point(0, 0));

        double viewWidth = host.ActualWidth;
        double viewHeight = host.ActualHeight;

        // 水平：默认偏移居中于按钮，超出边界则修正
        double x = Math.Max(8, buttonPos.X + horizontalOffset);
        if (x + DefaultWidth > viewWidth - 8) x = viewWidth - DefaultWidth - 8;

        // 垂直：优先在按钮下方，空间不够则放在按钮上方
        double y = buttonPos.Y + anchorButton.ActualHeight + 6;
        if (y + DefaultHeight > viewHeight - 8)
        {
            y = buttonPos.Y - DefaultHeight - 6;
        }
        if (y < 8) y = 8;

        container.Margin = new Thickness(x, y, 0, 0);

        // 淡入动画
        backdrop.Visibility = Visibility.Visible;
        backdrop.Opacity = 0;
        backdrop.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(FadeMs))
        });

        container.Visibility = Visibility.Visible;
        container.Opacity = 0;
        container.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(FadeMs))
        });
    }

    /// <summary>
    /// 关闭日历浮窗（带淡出动画）
    /// </summary>
    public static void Close(Border backdrop, Border container)
    {
        var fadeOut = new DoubleAnimation
        {
            From = 1, To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(FadeMs)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            container.Visibility = Visibility.Collapsed;
            container.Opacity = 1;
            backdrop.Visibility = Visibility.Collapsed;
            backdrop.Opacity = 1;
        };
        container.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        backdrop.BeginAnimation(UIElement.OpacityProperty, fadeOut.Clone());
    }
}
