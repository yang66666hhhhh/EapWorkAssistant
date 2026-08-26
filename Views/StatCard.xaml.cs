using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EapWorkAssistant.Views;

/// <summary>
/// 统一统计卡片组件：左侧色带 + 标题/数值/单位 + 图标。
/// 内部已处理圆角裁剪，避免窄色带在卡片大圆角处露出直角。
/// </summary>
public partial class StatCard : UserControl
{
    public StatCard()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyCardStyle();
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
            new PropertyMetadata(string.Empty));

    #endregion

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
}
