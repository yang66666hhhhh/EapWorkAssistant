using System.Windows;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// 侧栏导航按钮的附加属性：徽标数字（如回收站待恢复条目数）。
/// 绑定到数量 &gt; 0 时自动显示彩色角标；为 0 时隐藏。
/// </summary>
public static class NavButtonProperties
{
    public static readonly DependencyProperty BadgeProperty =
        DependencyProperty.RegisterAttached(
            "Badge", typeof(int), typeof(NavButtonProperties), new PropertyMetadata(0));

    public static int GetBadge(DependencyObject obj) => (int)obj.GetValue(BadgeProperty);

    public static void SetBadge(DependencyObject obj, int value) => obj.SetValue(BadgeProperty, value);
}
