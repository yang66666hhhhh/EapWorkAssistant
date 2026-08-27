using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EapWorkAssistant.Controls;

/// <summary>
/// 统一空状态组件：插画图标 + 标题 + 描述 + 可选操作按钮。
/// 用于各页在「无数据」时展示一致的引导视觉，避免散落的临时文案。
/// </summary>
public partial class EmptyState : UserControl
{
    public EmptyState()
    {
        InitializeComponent();
    }

    #region IconPathData

    public Geometry? IconPathData
    {
        get => (Geometry?)GetValue(IconPathDataProperty);
        set => SetValue(IconPathDataProperty, value);
    }

    public static readonly DependencyProperty IconPathDataProperty =
        DependencyProperty.Register(nameof(IconPathData), typeof(Geometry), typeof(EmptyState),
            new PropertyMetadata(null));

    #endregion

    #region Title

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyState),
            new PropertyMetadata(string.Empty));

    #endregion

    #region Description

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(EmptyState),
            new PropertyMetadata(string.Empty));

    #endregion

    #region ActionContent

    public string? ActionContent
    {
        get => (string?)GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public static readonly DependencyProperty ActionContentProperty =
        DependencyProperty.Register(nameof(ActionContent), typeof(string), typeof(EmptyState),
            new PropertyMetadata(string.Empty));

    #endregion

    #region ActionCommand

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public static readonly DependencyProperty ActionCommandProperty =
        DependencyProperty.Register(nameof(ActionCommand), typeof(ICommand), typeof(EmptyState),
            new PropertyMetadata(null));

    #endregion

    #region IsCompact

    /// <summary>紧凑模式：缩小插画与字号，适配 SettingsView 等 Expander 紧凑区空态。</summary>
    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(EmptyState),
            new PropertyMetadata(false));

    #endregion
}
