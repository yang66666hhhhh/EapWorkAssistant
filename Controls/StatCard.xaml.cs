using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EapWorkAssistant.Controls;

/// <summary>
/// 统一统计卡片组件：左侧色带 + 标题/数值/单位 + 图标。
/// 内部已处理圆角裁剪，避免窄色带在卡片大圆角处露出直角。
/// 数值变化时以 ease-out 滚动过渡（count-up，P2 ⑪）；非数字内容（如「—」）直接透传。
/// </summary>
public partial class StatCard : UserControl
{
    public StatCard()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyCardStyle();
            TryAnimateTo(Value); // 加载前已就位的初始值，此处从 0 滚起补上首载动画
        };
    }

    #region Click 路由事件

    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    public static readonly RoutedEvent ClickEvent =
        EventManager.RegisterRoutedEvent(nameof(Click), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(StatCard));

    private void CardBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        e.Handled = true;
    }

    #endregion

    #region CardStyle

    public Style? CardStyle
    {
        get => (Style?)GetValue(CardStyleProperty);
        set => SetValue(CardStyleProperty, value);
    }

    public static readonly DependencyProperty CardStyleProperty =
        DependencyProperty.Register(nameof(CardStyle), typeof(Style), typeof(StatCard),
            new PropertyMetadata(null, OnCardStyleChanged));

    private static void OnCardStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((StatCard)d).ApplyCardStyle();
    }

    private void ApplyCardStyle()
    {
        if (CardBorder == null) return;
        CardBorder.Style = CardStyle ?? (TryFindResource("CardElevated") as Style);
    }

    #endregion

    #region IsCompact

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(StatCard),
            new PropertyMetadata(false));

    #endregion

    #region Title

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatCard),
            new PropertyMetadata(string.Empty));

    #endregion

    #region Value

    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatCard),
            new PropertyMetadata(string.Empty, OnValueChanged));

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((StatCard)d).TryAnimateTo((string?)e.NewValue);
    }

    #endregion

    // ===== 数值滚动（count-up，P2 ⑪）=====

    /// <summary>实际展示文本：动画期间为插值，静止时与 Value 一致（非数字内容原样透传）。</summary>
    public string? DisplayValue => (string?)GetValue(DisplayValueProperty);

    public static readonly DependencyPropertyKey DisplayValuePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DisplayValue), typeof(string), typeof(StatCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DisplayValueProperty = DisplayValuePropertyKey.DependencyProperty;

    /// <summary>最近一次动画目标数值；null 表示当前显示内容不是数字（不参与滚动）。仅在控件加载后赋值。</summary>
    private double? _currentNumeric;

    /// <summary>是否存在未完成的滚动动画（决定续滚起点取当前插值还是上一次目标值）。</summary>
    private bool _isAnimating;

    /// <summary>滚动输出的小数位数，由目标文本推断（"12.5" → 1 位，"42" → 0 位）。</summary>
    private int _decimals;

    /// <summary>把数字格式化为展示文本（保留推断出的小数位）。</summary>
    private string FormatNumeric(double value) => value.ToString("F" + _decimals, CultureInfo.CurrentCulture);

    /// <summary>尝试把文本解析为数字（当前文化优先，Invariant 兜底）。</summary>
    private static bool TryParseValue(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>推断目标文本的小数位数（基于小数点字符出现位置）。</summary>
    private static int CountDecimals(string text)
    {
        var idx = text.LastIndexOf('.');
        return idx < 0 ? 0 : text.Length - idx - 1;
    }

    /// <summary>
    /// 数值变化入口：控件已加载且内容可解析 → ease-out 滚动到新值；否则直接透传。
    /// 起点：滚动中取当前插值（动画激活时 GetValue 返回动画值）→ 上一次目标值 → 0（首载从零滚起）。
    /// </summary>
    private void TryAnimateTo(string? newValue)
    {
        var text = newValue ?? string.Empty;

        // 未加载或非数字：取消在途动画并透传（不记 _currentNumeric，保证首载仍可从 0 滚起）
        if (!IsLoaded || !TryParseValue(text, out var target))
        {
            BeginAnimation(AnimValueProperty, null);
            _isAnimating = false;
            SetValue(DisplayValuePropertyKey, text);
            return;
        }

        _decimals = CountDecimals(text);
        // 起点：滚动中取当前插值（动画激活时 GetValue 返回动画值）；否则取上一次目标值；首载从 0 滚起
        var from = _isAnimating ? AnimValue : (_currentNumeric ?? 0);

        _currentNumeric = target;
        if (Math.Abs(target - from) < 0.0001)
        {
            SetValue(DisplayValuePropertyKey, text);
            return;
        }

        var duration = TryFindResource("DurationNormal") is Duration d && d.HasTimeSpan
            ? d
            : new Duration(TimeSpan.FromMilliseconds(250));

        var animation = new DoubleAnimation(from, target, duration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        _isAnimating = true;
        animation.Completed += (_, _) =>
        {
            _isAnimating = false;
            SetValue(DisplayValuePropertyKey, text);
        };
        BeginAnimation(AnimValueProperty, animation, HandoffBehavior.SnapshotAndReplace);
        SetValue(DisplayValuePropertyKey, FormatNumeric(from)); // 立即离开起点，避免第一帧停留在旧文本
    }

    /// <summary>内部动画驱动属性：DoubleAnimation 写入，回调同步展示文本。</summary>
    /// ⚠️ 必须是非 nullable double——WPF 动画不支持 Nullable&lt;T&gt; 属性（double? 注册会运行时逐卡报「不可动画」）。
    private double AnimValue
    {
        get => (double)GetValue(AnimValueProperty);
        set => SetValue(AnimValueProperty, value);
    }

    private static readonly DependencyProperty AnimValueProperty =
        DependencyProperty.Register(nameof(AnimValue), typeof(double), typeof(StatCard),
            new PropertyMetadata(0.0, OnAnimValueChanged));

    private static void OnAnimValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (StatCard)d;
        if (e.NewValue is double v) card.SetValue(DisplayValuePropertyKey, card.FormatNumeric(v));
    }

    #region Unit

    public string? Unit
    {
        get => (string?)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(StatCard),
            new PropertyMetadata(string.Empty));

    #endregion

    #region SubText

    public string? SubText
    {
        get => (string?)GetValue(SubTextProperty);
        set => SetValue(SubTextProperty, value);
    }

    public static readonly DependencyProperty SubTextProperty =
        DependencyProperty.Register(nameof(SubText), typeof(string), typeof(StatCard),
            new PropertyMetadata(string.Empty));

    #endregion

    #region BarBrush

    public Brush? BarBrush
    {
        get => (Brush?)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public static readonly DependencyProperty BarBrushProperty =
        DependencyProperty.Register(nameof(BarBrush), typeof(Brush), typeof(StatCard),
            new PropertyMetadata(null));

    #endregion

    #region BarWidth

    public double BarWidth
    {
        get => (double)GetValue(BarWidthProperty);
        set => SetValue(BarWidthProperty, value);
    }

    public static readonly DependencyProperty BarWidthProperty =
        DependencyProperty.Register(nameof(BarWidth), typeof(double), typeof(StatCard),
            new PropertyMetadata(3.0));

    #endregion

    #region IconBackgroundBrush

    public Brush? IconBackgroundBrush
    {
        get => (Brush?)GetValue(IconBackgroundBrushProperty);
        set => SetValue(IconBackgroundBrushProperty, value);
    }

    public static readonly DependencyProperty IconBackgroundBrushProperty =
        DependencyProperty.Register(nameof(IconBackgroundBrush), typeof(Brush), typeof(StatCard),
            new PropertyMetadata(null));

    #endregion

    #region IconForegroundBrush

    public Brush? IconForegroundBrush
    {
        get => (Brush?)GetValue(IconForegroundBrushProperty);
        set => SetValue(IconForegroundBrushProperty, value);
    }

    public static readonly DependencyProperty IconForegroundBrushProperty =
        DependencyProperty.Register(nameof(IconForegroundBrush), typeof(Brush), typeof(StatCard),
            new PropertyMetadata(null));

    #endregion

    #region IconPathData

    public Geometry? IconPathData
    {
        get => (Geometry?)GetValue(IconPathDataProperty);
        set => SetValue(IconPathDataProperty, value);
    }

    public static readonly DependencyProperty IconPathDataProperty =
        DependencyProperty.Register(nameof(IconPathData), typeof(Geometry), typeof(StatCard),
            new PropertyMetadata(null));

    #endregion

    #region InnerPadding

    public Thickness InnerPadding
    {
        get => (Thickness)GetValue(InnerPaddingProperty);
        set => SetValue(InnerPaddingProperty, value);
    }

    public static readonly DependencyProperty InnerPaddingProperty =
        DependencyProperty.Register(nameof(InnerPadding), typeof(Thickness), typeof(StatCard),
            new PropertyMetadata(new Thickness(18, 14, 18, 14)));

    #endregion

    #region TrendText

    /// <summary>
    /// 趋势文案，例如 "12.4% 较上月"。为空时不显示趋势胶囊。
    /// </summary>
    public string? TrendText
    {
        get => (string?)GetValue(TrendTextProperty);
        set => SetValue(TrendTextProperty, value);
    }

    public static readonly DependencyProperty TrendTextProperty =
        DependencyProperty.Register(nameof(TrendText), typeof(string), typeof(StatCard),
            new PropertyMetadata(string.Empty));

    #endregion

    #region TrendUp

    /// <summary>
    /// 趋势方向：true 表示增长（红，遵循中国涨红习惯），false 表示下降（绿）。
    /// </summary>
    public bool TrendUp
    {
        get => (bool)GetValue(TrendUpProperty);
        set => SetValue(TrendUpProperty, value);
    }

    public static readonly DependencyProperty TrendUpProperty =
        DependencyProperty.Register(nameof(TrendUp), typeof(bool), typeof(StatCard),
            new PropertyMetadata(false));

    #endregion
}
