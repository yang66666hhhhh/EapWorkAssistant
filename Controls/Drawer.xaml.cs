using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EapWorkAssistant.Controls;

/// <summary>
/// 侧滑抽屉面板：封装遮罩 + 右侧滑入面板 + 滑入/滑出动画与遮罩淡入淡出，
/// 内化遮罩点击关闭、Esc 关闭与默认头部的关闭按钮，并暴露 Closing（可取消）/ Closed 事件，
/// 供 View 执行脏检查确认与关闭后清理（如重置表单）。
/// 头部支持默认（HeaderIcon / HeaderTitle / HeaderSubtitle + 关闭按钮）或自定义（HeaderContent）；
/// 表单主体与底部操作栏分别通过 Body / FooterContent 由 View 提供（其 x:Name 仍归属 View 的 NameScope）。
/// </summary>
public partial class Drawer : UserControl
{
    private const int FadeMs = 120;
    private const int SlideInMs = 180;
    private const int SlideOutMs = 150;

    #region 依赖属性

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(
            nameof(IsOpen), typeof(bool), typeof(Drawer),
            new PropertyMetadata(false, OnIsOpenChanged));

    public static readonly DependencyProperty DrawerWidthProperty =
        DependencyProperty.Register(
            nameof(DrawerWidth), typeof(double), typeof(Drawer),
            new PropertyMetadata(520.0));

    public static readonly DependencyProperty HeaderIconProperty =
        DependencyProperty.Register(
            nameof(HeaderIcon), typeof(Geometry), typeof(Drawer),
            new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderTitleProperty =
        DependencyProperty.Register(
            nameof(HeaderTitle), typeof(string), typeof(Drawer),
            new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderSubtitleProperty =
        DependencyProperty.Register(
            nameof(HeaderSubtitle), typeof(string), typeof(Drawer),
            new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderContentProperty =
        DependencyProperty.Register(
            nameof(HeaderContent), typeof(object), typeof(Drawer),
            new PropertyMetadata(null, OnHeaderContentChanged));

    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(
            nameof(Body), typeof(object), typeof(Drawer),
            new PropertyMetadata(null));

    public static readonly DependencyProperty FooterContentProperty =
        DependencyProperty.Register(
            nameof(FooterContent), typeof(object), typeof(Drawer),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SaveCommandProperty =
        DependencyProperty.Register(
            nameof(SaveCommand), typeof(ICommand), typeof(Drawer),
            new PropertyMetadata(null));

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public double DrawerWidth
    {
        get => (double)GetValue(DrawerWidthProperty);
        set => SetValue(DrawerWidthProperty, value);
    }

    public Geometry? HeaderIcon
    {
        get => (Geometry?)GetValue(HeaderIconProperty);
        set => SetValue(HeaderIconProperty, value);
    }

    public string? HeaderTitle
    {
        get => (string?)GetValue(HeaderTitleProperty);
        set => SetValue(HeaderTitleProperty, value);
    }

    public string? HeaderSubtitle
    {
        get => (string?)GetValue(HeaderSubtitleProperty);
        set => SetValue(HeaderSubtitleProperty, value);
    }

    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public object? FooterContent
    {
        get => GetValue(FooterContentProperty);
        set => SetValue(FooterContentProperty, value);
    }

    public ICommand? SaveCommand
    {
        get => (ICommand?)GetValue(SaveCommandProperty);
        set => SetValue(SaveCommandProperty, value);
    }

    #endregion

    #region 关闭命令 / 事件

    private readonly ICommand _closeCommand;
    public ICommand CloseCommand => _closeCommand;

    public event EventHandler<DrawerClosingEventArgs>? Closing;

    public static readonly RoutedEvent ClosedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(Closed), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(Drawer));

    public event RoutedEventHandler Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    #endregion

    public Drawer()
    {
        InitializeComponent();
        _closeCommand = new CloseCommandImpl(RequestClose);
        UpdateHeaderVisibility();
    }

    /// <summary>请求关闭：触发 Closing（可取消）。若未被取消则开始关闭动画。</summary>
    public void RequestClose()
    {
        var args = new DrawerClosingEventArgs();
        Closing?.Invoke(this, args);
        if (!args.Cancel)
            IsOpen = false;
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((Drawer)d).ApplyIsOpen((bool)e.NewValue);
    }

    private static void OnHeaderContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((Drawer)d).UpdateHeaderVisibility();
    }

    private void UpdateHeaderVisibility()
    {
        bool hasCustom = HeaderContent != null;
        if (DefaultHeader != null)
            DefaultHeader.Visibility = hasCustom ? Visibility.Collapsed : Visibility.Visible;
        if (HeaderPresenter != null)
            HeaderPresenter.Visibility = hasCustom ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyIsOpen(bool open)
    {
        if (open) OpenAnim(); else CloseAnim();
    }

    private void OpenAnim()
    {
        Backdrop.Visibility = Visibility.Visible;
        Backdrop.Opacity = 0;
        Backdrop.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(FadeMs))));

        SidePanel.Visibility = Visibility.Visible;
        var tt = new TranslateTransform { X = DrawerWidth };
        SidePanel.RenderTransform = tt;
        var slideIn = new DoubleAnimation(DrawerWidth, 0,
            new Duration(TimeSpan.FromMilliseconds(SlideInMs)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        tt.BeginAnimation(TranslateTransform.XProperty, slideIn);
    }

    private void CloseAnim()
    {
        var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(FadeMs)));
        fade.Completed += (_, _) => Backdrop.Visibility = Visibility.Collapsed;
        Backdrop.BeginAnimation(UIElement.OpacityProperty, fade);

        var tt = SidePanel.RenderTransform as TranslateTransform ?? new TranslateTransform();
        SidePanel.RenderTransform = tt;
        var slide = new DoubleAnimation(0, DrawerWidth,
            new Duration(TimeSpan.FromMilliseconds(SlideOutMs)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        slide.Completed += (_, _) =>
        {
            SidePanel.Visibility = Visibility.Collapsed;
            RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
        };
        tt.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    private void Backdrop_Click(object sender, MouseButtonEventArgs e)
    {
        RequestClose();
    }

    private void SidePanel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            RequestClose();
        }
        else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            SaveCommand?.Execute(null);
        }
    }

    private sealed class CloseCommandImpl : ICommand
    {
        private readonly Action _execute;
        public CloseCommandImpl(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
#pragma warning disable CS0067 // 关闭命令的可用性恒定不变，事件永不触发属正常
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
    }
}

/// <summary>Drawer 关闭请求事件参数，View 可设置 Cancel 阻止关闭（如脏检查未确认）。</summary>
public class DrawerClosingEventArgs : CancelEventArgs
{
}
