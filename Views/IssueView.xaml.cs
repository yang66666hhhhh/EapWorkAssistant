using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EapWorkAssistant.Views;

public partial class IssueView : UserControl
{
    private bool _isDrawerOpen;

    public IssueView()
    {
        InitializeComponent();
    }

    // ===== 浮窗抽屉动画 =====

    private void OpenForm_Click(object sender, RoutedEventArgs e)
    {
        if (_isDrawerOpen) return;
        _isDrawerOpen = true;

        // 显示遮罩（淡入）
        Backdrop.Visibility = Visibility.Visible;
        Backdrop.Opacity = 0;
        var fadeIn = new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(120))
        };
        Backdrop.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        // 显示浮窗面板并滑入
        FormPanel.Visibility = Visibility.Visible;
        OpenFormBtn.Visibility = Visibility.Collapsed;

        var translate = new TranslateTransform { X = 500 };
        FormPanel.RenderTransform = translate;

        var slideIn = new DoubleAnimation
        {
            From = 500,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        translate.BeginAnimation(TranslateTransform.XProperty, slideIn);
    }

    private void CloseForm_Click(object sender, RoutedEventArgs e)
    {
        CloseDrawer();
    }

    private void Backdrop_Click(object sender, MouseButtonEventArgs e)
    {
        CloseDrawer();
    }

    private void CloseDrawer()
    {
        if (!_isDrawerOpen) return;
        _isDrawerOpen = false;

        // 遮罩淡出
        var fadeOut = new DoubleAnimation
        {
            From = 1, To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(120))
        };
        fadeOut.Completed += (_, _) =>
        {
            Backdrop.Visibility = Visibility.Collapsed;
        };
        Backdrop.BeginAnimation(UIElement.OpacityProperty, fadeOut);

        // 浮窗滑出
        var translate = FormPanel.RenderTransform as TranslateTransform
                        ?? new TranslateTransform { X = 0 };
        FormPanel.RenderTransform = translate;

        var slideOut = new DoubleAnimation
        {
            From = 0,
            To = 500,
            Duration = new Duration(TimeSpan.FromMilliseconds(150)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        slideOut.Completed += (_, _) =>
        {
            FormPanel.Visibility = Visibility.Collapsed;
            OpenFormBtn.Visibility = Visibility.Visible;
        };

        translate.BeginAnimation(TranslateTransform.XProperty, slideOut);
    }
}
