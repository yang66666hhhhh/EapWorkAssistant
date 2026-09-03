using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace EapWorkAssistant.Helpers
{
    /// <summary>
    /// 悬停微交互：鼠标进入时元素「上浮 + 阴影增强」，离开时回落，带过渡动画（非瞬切）。
    ///
    /// 为什么需要这个类（而不是用 Style Trigger）：
    /// 1. WPF 的 Effect / RenderTransform 在 Trigger 里只能整值替换，无法过渡，观感是"瞬跳"；
    /// 2. 资源字典里的 DropShadowEffect 是冻结的共享实例，直接对它 BeginAnimation 会抛异常，
    ///    且若绕过冻结限制会污染所有引用同一资源的元素。
    ///
    /// 做法：MouseEnter 时用 Clone() 从共享实例复制出一份「未冻结的本元素私有副本」挂上去，
    /// 对副本的 BlurRadius / Opacity 做动画；副本缓存在附加属性上，全程只克隆一次。
    /// 本类同时接管 RenderTransform（本地值优先级高于 Style Trigger，正好屏蔽 Trigger 的整值替换）。
    /// </summary>
    public static class HoverLift
    {
        /// <summary>悬停时阴影增强系数：BlurRadius / Opacity 均乘以该值。
        /// 对 ShadowMd(28 / 0.075) 而言即 42 / 0.1125，观感接近 ShadowLg(44 / 0.11)。</summary>
        private const double HoverShadowBoost = 1.5;

        public static readonly DependencyProperty EnableProperty =
            DependencyProperty.RegisterAttached(
                "Enable",
                typeof(bool),
                typeof(HoverLift),
                new PropertyMetadata(false, OnEnableChanged));

        public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);
        public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

        // 本元素私有的阴影副本（首次进入时克隆，之后复用）
        private static readonly DependencyProperty ShadowCloneProperty =
            DependencyProperty.RegisterAttached(
                "ShadowClone",
                typeof(DropShadowEffect),
                typeof(HoverLift),
                new PropertyMetadata(null));

        // 阴影基准值（克隆那一刻的 BlurRadius / Opacity），离开时回到该值
        private static readonly DependencyProperty BaseBlurRadiusProperty =
            DependencyProperty.RegisterAttached("BaseBlurRadius", typeof(double), typeof(HoverLift),
                new PropertyMetadata(0.0));

        private static readonly DependencyProperty BaseOpacityProperty =
            DependencyProperty.RegisterAttached("BaseOpacity", typeof(double), typeof(HoverLift),
                new PropertyMetadata(0.0));

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement fe)
                return;

            if ((bool)e.NewValue)
            {
                fe.MouseEnter += OnMouseEnter;
                fe.MouseLeave += OnMouseLeave;
                // 本地值优先级高于 Style Trigger，直接占住 RenderTransform，
                // 屏蔽样式中可能存在的整值 TranslateTransform 触发器替换
                fe.RenderTransform = new TranslateTransform();
            }
            else
            {
                fe.MouseEnter -= OnMouseEnter;
                fe.MouseLeave -= OnMouseLeave;
                fe.ClearValue(ShadowCloneProperty);
                fe.ClearValue(BaseBlurRadiusProperty);
                fe.ClearValue(BaseOpacityProperty);
            }
        }

        private static void OnMouseEnter(object sender, MouseEventArgs e) => Animate((FrameworkElement)sender, enter: true);

        private static void OnMouseLeave(object sender, MouseEventArgs e) => Animate((FrameworkElement)sender, enter: false);

        private static void Animate(FrameworkElement fe, bool enter)
        {
            var duration = MotionTokens.GetDuration("DurationFast", 150);
            var ease = MotionTokens.GetEasing("EaseStandard");
            double liftY = MotionTokens.GetDouble("MotionHoverLiftY", 2);

            // 1) 阴影过渡：克隆冻结的共享 Effect 得到可动画的私有副本
            var shadow = (DropShadowEffect?)fe.GetValue(ShadowCloneProperty);
            if (shadow is null && fe.Effect is DropShadowEffect shared)
            {
                // Freezable.Clone() 返回未冻结的可修改深拷贝，动画不会污染共享资源
                shadow = shared.Clone();
                shadow.BlurRadius = shared.BlurRadius;
                shadow.Opacity = shared.Opacity;
                fe.SetValue(BaseBlurRadiusProperty, shared.BlurRadius);
                fe.SetValue(BaseOpacityProperty, shared.Opacity);
                fe.SetValue(ShadowCloneProperty, shadow);
                fe.Effect = shadow;
            }

            if (shadow is not null)
            {
                double baseBlur = (double)fe.GetValue(BaseBlurRadiusProperty);
                double baseOpacity = (double)fe.GetValue(BaseOpacityProperty);

                shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty,
                    new DoubleAnimation(enter ? baseBlur * HoverShadowBoost : baseBlur, duration) { EasingFunction = ease });
                shadow.BeginAnimation(DropShadowEffect.OpacityProperty,
                    new DoubleAnimation(enter ? baseOpacity * HoverShadowBoost : baseOpacity, duration) { EasingFunction = ease });
            }

            // 2) 上浮过渡
            if (fe.RenderTransform is TranslateTransform translate)
            {
                translate.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(enter ? -liftY : 0, duration) { EasingFunction = ease });
            }
        }
    }
}
