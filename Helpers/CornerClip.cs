using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EapWorkAssistant.Helpers
{
    /// <summary>
    /// 让元素（通常是带 CornerRadius 的 Border/卡片）按其自身圆角裁剪子元素。
    /// 解决“窄色带 / 内层高亮层在卡片大圆角处露出直角”的问题：
    /// WPF 的 ClipToBounds 仅按矩形裁剪，无法贴合圆角；而窄元素（如 3px 色带）
    /// 套用大 CornerRadius 时会被自动压缩，圆角几乎失效。
    /// 用法：在卡片 Border 上附加 local:CornerClip.Enable="True"，
    /// 元素会随尺寸变化自动以自身 CornerRadius 重建圆角裁剪区域。
    /// </summary>
    public static class CornerClip
    {
        public static readonly DependencyProperty EnableProperty =
            DependencyProperty.RegisterAttached(
                "Enable",
                typeof(bool),
                typeof(CornerClip),
                new PropertyMetadata(false, OnEnableChanged));

        public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);
        public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement fe)
                return;

            if ((bool)e.NewValue)
            {
                fe.Loaded += OnLoaded;
                fe.SizeChanged += OnSizeChanged;
                // 若元素在附加时已加载（如运行时切换），立即更新一次
                if (fe.IsLoaded)
                    UpdateClip(fe);
            }
            else
            {
                fe.Loaded -= OnLoaded;
                fe.SizeChanged -= OnSizeChanged;
                fe.Clip = null;
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e) => UpdateClip((FrameworkElement)sender);
        private static void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateClip((FrameworkElement)sender);

        private static void UpdateClip(FrameworkElement fe)
        {
            if (fe.ActualWidth <= 0 || fe.ActualHeight <= 0)
                return;

            // 取左上角半径作为统一圆角（卡片四角一致）
            double radius = fe is Border border ? border.CornerRadius.TopLeft : 0;

            fe.Clip = new RectangleGeometry(
                new Rect(0, 0, fe.ActualWidth, fe.ActualHeight),
                radius,
                radius);
        }
    }
}
