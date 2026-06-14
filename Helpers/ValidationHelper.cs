using System.Windows;

namespace EapWorkAssistant.Helpers;

public static class ValidationHelper
{
    public static readonly DependencyProperty IsInvalidProperty =
        DependencyProperty.RegisterAttached("IsInvalid", typeof(bool), typeof(ValidationHelper),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static void SetIsInvalid(DependencyObject element, bool value) =>
        element.SetValue(IsInvalidProperty, value);

    public static bool GetIsInvalid(DependencyObject element) =>
        (bool)element.GetValue(IsInvalidProperty);
}
