using System.Windows;
using System.Windows.Media.Animation;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 自定义动画：平滑过渡 <see cref="ColumnDefinition.Width"/>（<see cref="GridLength"/>）。
/// 用于侧边栏折叠/展开时的宽度动画，WPF 原生不提供 GridLength 的过渡动画。
/// </summary>
public class GridLengthAnimation : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty =
        DependencyProperty.Register(nameof(From), typeof(GridLength), typeof(GridLengthAnimation),
            new PropertyMetadata(new GridLength(0)));

    public static readonly DependencyProperty ToProperty =
        DependencyProperty.Register(nameof(To), typeof(GridLength), typeof(GridLengthAnimation),
            new PropertyMetadata(new GridLength(0)));

    /// <summary>起始宽度（像素）。</summary>
    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    /// <summary>目标宽度（像素）。</summary>
    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        var from = From.IsAbsolute
            ? From.Value
            : (defaultOriginValue is GridLength o && o.IsAbsolute ? o.Value : 0);
        var to = To.IsAbsolute
            ? To.Value
            : (defaultDestinationValue is GridLength d && d.IsAbsolute ? d.Value : 0);

        var progress = animationClock.CurrentProgress ?? 0;
        // 三次缓动（ease-in-out），替代 AnimationTimeline.EasingFunction（基类未提供该属性）
        var eased = progress < 0.5
            ? 4 * progress * progress * progress
            : 1 - Math.Pow(-2 * progress + 2, 3) / 2;
        return new GridLength(from + (to - from) * eased, GridUnitType.Pixel);
    }
}
