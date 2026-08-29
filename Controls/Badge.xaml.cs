using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EapWorkAssistant.Controls;

/// <summary>
/// 统一徽章：圆角胶囊 + 文本，支持自定义背景/前景画刷。
/// 复用 Tag 样式外观（RadiusXs + Padding 8,4）；背景/前景缺省取 SurfaceBrush / TextSecondaryBrush。
/// 用法：
///   &lt;controls:Badge Text="待处理" BackgroundBrush="{DynamicResource WarningLightBrush}"/&gt;
///   &lt;controls:Badge Text="{Binding Status, Converter={x:Static local:IssueStatusConverter.Instance}}"
///                   BackgroundBrush="{Binding Status, Converter={x:Static local:IssueStatusToBrushConverter.Instance}}"/&gt;
/// </summary>
public partial class Badge : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(Badge), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BackgroundBrushProperty =
        DependencyProperty.Register(nameof(BackgroundBrush), typeof(Brush), typeof(Badge), new PropertyMetadata(null));

    public static readonly DependencyProperty ForegroundBrushProperty =
        DependencyProperty.Register(nameof(ForegroundBrush), typeof(Brush), typeof(Badge), new PropertyMetadata(null));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush BackgroundBrush
    {
        get => (Brush)GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    public Brush ForegroundBrush
    {
        get => (Brush)GetValue(ForegroundBrushProperty);
        set => SetValue(ForegroundBrushProperty, value);
    }

    public Badge()
    {
        InitializeComponent();
        // 缺省取令牌画刷，调用方可在 XAML 覆盖
        if (BackgroundBrush == null && Application.Current != null)
            BackgroundBrush = (Brush)Application.Current.FindResource("SurfaceBrush");
        if (ForegroundBrush == null && Application.Current != null)
            ForegroundBrush = (Brush)Application.Current.FindResource("TextSecondaryBrush");
    }
}
