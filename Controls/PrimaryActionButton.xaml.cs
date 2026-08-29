using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EapWorkAssistant.Controls;

/// <summary>
/// 统一主操作按钮：主色胶囊 + 前置「+」图标 + 文字。
/// 复用 BtnPrimary 样式的全部视觉（背景 / 阴影 / 按压缩放），
/// 向外转发 Click 路由事件与 Command，供各列表页「新增 XXX」类主操作复用，
/// 消除此前三页逐字重复的按钮模板。
/// </summary>
public partial class PrimaryActionButton : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(PrimaryActionButton),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(
            nameof(Command), typeof(ICommand), typeof(PrimaryActionButton),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(
            nameof(CommandParameter), typeof(object), typeof(PrimaryActionButton),
            new PropertyMetadata(null));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    #region Click 路由事件（转发内部按钮的点击）

    public static readonly RoutedEvent ClickEvent =
        EventManager.RegisterRoutedEvent(
            nameof(Click), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(PrimaryActionButton));

    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    #endregion

    public PrimaryActionButton()
    {
        InitializeComponent();
    }

    private void InnerButton_Click(object sender, RoutedEventArgs e)
    {
        // 转发给外部订阅者（如各 View 的 OpenForm_Click），
        // 避免内部按钮的 Click 直接冒泡到 View 造成双重触发。
        RaiseEvent(new RoutedEventArgs(ClickEvent, this));
    }
}
