using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EapWorkAssistant.Helpers
{
    /// <summary>
    /// 主题色过渡：把 SolidColorBrush 的颜色变化做成 Color 动画，消除明暗/强调色切换时的"瞬跳"。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么不能直接在旧 Brush 上动画：</b>XAML 资源字典里的 Brush 会被 Freeze，
    /// 对冻结实例调用 BeginAnimation 会抛 InvalidOperationException。因此每次都新建未冻结的
    /// SolidColorBrush 替换资源条目 —— 这与 ThemeService 原本 UpdateBrush 的做法一致。</para>
    ///
    /// <para><b>为什么能"接得上"：</b>新建的 Brush 起始 Color 取自旧 Brush 的<b>当前有效值</b>
    /// （<see cref="DependencyObject.GetValue"/> 返回含动画在内的当前值，而非基值）。
    /// 这样即使上一次过渡尚未结束就再次切换，也能从眼睛看到的那个颜色继续过渡，不会回跳。
    /// 资源条目一替换，所有 DynamicResource 引用者立刻指向新 Brush，
    /// 而新 Brush 的第一帧就是旧色，视觉上完全连续。</para>
    ///
    /// <para><b>落地（Handoff）：</b>动画默认 FillBehavior.HoldEnd 会让动画对象长期持有属性。
    /// 故在 Completed 里先把基值设为目标色，再清除动画 —— 顺序不可颠倒，
    /// 否则清除后属性会回落到起始色（旧色），造成"闪回"。</para>
    ///
    /// <para><b>回退开关：</b><see cref="Enabled"/> 置 false 即退化为直接赋值（瞬切），
    /// 用于在低端机出现卡顿时一键关闭，无需回滚代码。</para>
    /// </remarks>
    public static class ColorTransition
    {
        /// <summary>
        /// 总开关：false 时退化为直接替换（瞬切），行为与改造前完全一致。
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// 抑制过渡（用于首次初始化、批量应用）。与 <see cref="Enabled"/> 的区别：
        /// Enabled 是"用户/运维开关"，Suppress 是"本次调用不走动画"的临时标志。
        /// </summary>
        public static bool Suppressed { get; set; }

        /// <summary>
        /// 将资源字典中的 SolidColorBrush 过渡到目标颜色。
        /// </summary>
        /// <param name="res">目标资源字典（通常是 Application.Current.Resources）。</param>
        /// <param name="key">Brush 资源键。</param>
        /// <param name="target">目标颜色。</param>
        public static void Apply(ResourceDictionary res, string key, Color target)
        {
            // 旧值：取当前有效色（含正在进行的动画值）作为过渡起点。
            // 注意：必须先 GetValue 再做任何替换，替换后旧实例的有效值就不再更新了。
            var old = res.Contains(key) ? res[key] as SolidColorBrush : null;
            var from = old is null ? target : (Color)old.GetValue(SolidColorBrush.ColorProperty);

            // 新建未冻结实例替换资源条目（冻结实例无法动画）
            var brush = new SolidColorBrush(from);
            res[key] = brush;

            // 无需过渡的情形：功能关闭 / 本次抑制 / 首次创建 / 颜色没变
            if (!Enabled || Suppressed || old is null || from == target)
            {
                brush.Color = target;
                return;
            }

            var anim = new ColorAnimation(target, MotionTokens.GetDuration("DurationTheme", 280))
            {
                EasingFunction = MotionTokens.GetEasing("EaseStandard")
            };

            // 动画结束落地：先写基值，再清动画（顺序颠倒会导致颜色闪回起点）
            anim.Completed += (_, _) =>
            {
                brush.Color = target;
                brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            };

            brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        }
    }
}
