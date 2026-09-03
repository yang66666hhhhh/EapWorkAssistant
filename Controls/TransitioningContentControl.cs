using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using EapWorkAssistant.Helpers;

namespace EapWorkAssistant.Controls
{
    /// <summary>
    /// 带入场转场的内容控件：Content 变化时，新内容以「淡入 + 自下浮入」的方式出现（约 220ms）。
    /// 用于 MainWindow 的页面切换，消除 ContentControl 换 Content 时的"硬切"感。
    ///
    /// 设计取舍：
    /// - 只做 Enter、不做 Exit：旧页面在换 Content 的瞬间即销毁，若先播 Exit 再换内容，
    ///   Dashboard 的两张 LiveCharts 图表会被二次实例化，切页反而更慢、观感更差；
    /// - 动画整个控件而非 ContentPresenter：省去视觉树查找；Opacity / RenderTransform
    ///   均为渲染层属性，不触发布局，动画期间无布局跳动；
    /// - 首次内容设置（窗口启动）跳过转场，避免与窗口入场动画叠加播两次。
    /// </summary>
    public class TransitioningContentControl : ContentControl
    {
        /// <summary>首次内容设置不播转场（窗口启动时 DataContext 初始化即触发一次 Content 变化）。</summary>
        private bool _skipNextTransition = true;

        public TransitioningContentControl()
        {
            // 占住 RenderTransform，入场动画对 Y 做位移（渲染层，不触发布局）
            RenderTransform = new TranslateTransform();
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);

            if (_skipNextTransition)
            {
                _skipNextTransition = false;
                return;
            }
            if (newContent is null)
                return;

            PlayEnterTransition();
        }

        private void PlayEnterTransition()
        {
            var duration = MotionTokens.GetDuration("DurationEnter", 220);
            var ease = MotionTokens.GetEasing("EaseStandard");
            double enterY = MotionTokens.GetDouble("MotionPageEnterY", 12);

            // 先同步置为不可见并压到起始位置：保证换 Content 的第一帧不会闪现完整新页面
            Opacity = 0;
            if (RenderTransform is TranslateTransform t)
                t.Y = enterY;

            // 推迟到 Loaded 优先级再启动动画：确保 Opacity=0 的那一帧先完成渲染
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var fade = new DoubleAnimation(1, duration) { EasingFunction = ease };
                var rise = new DoubleAnimation(0, duration) { EasingFunction = ease };

                BeginAnimation(OpacityProperty, fade);
                if (RenderTransform is TranslateTransform transform)
                    transform.BeginAnimation(TranslateTransform.YProperty, rise);
            }), DispatcherPriority.Loaded);
        }
    }
}
