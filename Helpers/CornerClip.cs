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

            // UIElement.Clip 作用于「渲染后的全部输出」，包含 Effect 绘制的投影。
            // 若严格按自身尺寸裁剪，卡片的 DropShadowEffect 投影会被整圈切光（阴影完全不显示）。
            //
            // 因此元素带 Effect 时需要外扩裁剪区。但**不能**简单地「矩形外扩 pad + 圆角半径也 +pad」：
            // 外扩后的圆角矩形，其圆角弧心会从 (r, r) 保持不变、半径却变成 r+pad，
            // 导致卡片自身边角处（如左上角 (0,0)，距弧心 1.414r）从「被裁掉」变成「被保留」，
            // 子元素直角会从圆角缺口戳出来——正好抵消 CornerClip 存在的意义。
            //
            // 正确裁剪区 = 「外扩矩形」减去「四个缺角」：
            //   缺角 = 卡片外接矩形 − 圆角矩形（即圆角裁掉的那四块三角状区域）
            // 这样：卡片边界内的裁剪曲线与原始行为**逐像素一致**；卡片边界外完全放开，投影得以绘制。
            double pad = fe.Effect is null ? 0 : GetShadowPad();

            fe.Clip = BuildClipGeometry(
                new Rect(-pad, -pad, fe.ActualWidth + pad * 2, fe.ActualHeight + pad * 2),
                new Rect(0, 0, fe.ActualWidth, fe.ActualHeight),
                radius);
        }

        /// <summary>
        /// 构造裁剪几何：outer（外扩矩形）−（cardRect − roundedRect）。
        /// pad = 0 时 outer == cardRect，结果等价于纯圆角矩形，与原行为一致。
        /// </summary>
        private static Geometry BuildClipGeometry(Rect outer, Rect cardRect, double radius)
        {
            var outerGeo = new RectangleGeometry(outer);

            // 圆角为 0：无缺角，直接返回外扩矩形
            if (radius <= 0)
                return outerGeo;

            var rounded = new RectangleGeometry(cardRect, radius, radius);

            // 缺角 = 卡片外接矩形 − 圆角矩形
            var notches = new CombinedGeometry(GeometryCombineMode.Exclude,
                                               new RectangleGeometry(cardRect),
                                               rounded);

            return new CombinedGeometry(GeometryCombineMode.Exclude, outerGeo, notches);
        }

        /// <summary>投影外扩量的缓存（令牌值为常量，只需读一次）</summary>
        private static double? _shadowPadCache;

        /// <summary>
        /// 读取 DesignTokens 中的 ClipShadowPad 令牌（投影外扩量）。
        /// 取不到（如设计器 / 单元测试环境无 Application）时回退到 20px。
        /// </summary>
        private static double GetShadowPad()
        {
            if (_shadowPadCache.HasValue)
                return _shadowPadCache.Value;

            double pad = 20;
            // 注意：ResourceDictionary 只实现 IDictionary，没有泛型 TryGetValue，须用 Contains + 索引器
            var resources = Application.Current?.Resources;
            if (resources != null && resources.Contains("ClipShadowPad") && resources["ClipShadowPad"] is double d)
                pad = d;

            _shadowPadCache = pad;
            return pad;
        }
    }
}
