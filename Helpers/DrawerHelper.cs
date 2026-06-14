using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EapWorkAssistant.Helpers;

public static class DrawerHelper
{
    private const int FadeMs = 120;
    private const int SlideInMs = 180;
    private const int SlideOutMs = 150;

    public static void OpenDrawer(
        FrameworkElement backdrop,
        FrameworkElement formPanel,
        FrameworkElement openBtn,
        double slideDistance = 500)
    {
        backdrop.Visibility = Visibility.Visible;
        backdrop.Opacity = 0;
        backdrop.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(FadeMs))
        });

        formPanel.Visibility = Visibility.Visible;
        openBtn.Visibility = Visibility.Collapsed;

        var translate = new TranslateTransform { X = slideDistance };
        formPanel.RenderTransform = translate;
        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
        {
            From = slideDistance, To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(SlideInMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    public static void CloseDrawer(
        FrameworkElement backdrop,
        FrameworkElement formPanel,
        FrameworkElement openBtn,
        Action? onClosed = null,
        double slideDistance = 500)
    {
        var fadeOut = new DoubleAnimation
        {
            From = 1, To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(FadeMs))
        };
        fadeOut.Completed += (_, _) => backdrop.Visibility = Visibility.Collapsed;
        backdrop.BeginAnimation(UIElement.OpacityProperty, fadeOut);

        var translate = formPanel.RenderTransform as TranslateTransform
                        ?? new TranslateTransform { X = 0 };
        formPanel.RenderTransform = translate;

        var slideOut = new DoubleAnimation
        {
            From = 0, To = slideDistance,
            Duration = new Duration(TimeSpan.FromMilliseconds(SlideOutMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        slideOut.Completed += (_, _) =>
        {
            formPanel.Visibility = Visibility.Collapsed;
            openBtn.Visibility = Visibility.Visible;
            onClosed?.Invoke();
        };
        translate.BeginAnimation(TranslateTransform.XProperty, slideOut);
    }
}
