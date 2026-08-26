using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EapWorkAssistant.Controls;

/// <summary>
/// 分页控件：统一全软件的分页交互。
/// 支持两种显示模式：
/// - Simple：首页 / 上一页 / 下一页 / 末页 + 当前页/总页数文字（Knowledge/Issue 等）。
/// - Numbered：在 Simple 基础上增加具体页码按钮（WorkRecord 等）。
/// </summary>
public enum PaginationDisplayMode
{
    Simple,
    Numbered
}

public partial class PaginationControl : UserControl
{
    // ===== 分页状态 =====
    public static readonly DependencyProperty CurrentPageProperty =
        DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(PaginationControl), new PropertyMetadata(1));
    public static readonly DependencyProperty TotalPagesProperty =
        DependencyProperty.Register(nameof(TotalPages), typeof(int), typeof(PaginationControl), new PropertyMetadata(1));
    public static readonly DependencyProperty TotalCountProperty =
        DependencyProperty.Register(nameof(TotalCount), typeof(int), typeof(PaginationControl), new PropertyMetadata(0));
    public static readonly DependencyProperty PageSizeProperty =
        DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(PaginationControl), new PropertyMetadata(20));
    public static readonly DependencyProperty PageSizeOptionsProperty =
        DependencyProperty.Register(nameof(PageSizeOptions), typeof(IEnumerable), typeof(PaginationControl), new PropertyMetadata(new[] { 10, 20, 50, 100 }));

    // ===== 显示模式 =====
    public static readonly DependencyProperty DisplayModeProperty =
        DependencyProperty.Register(nameof(DisplayMode), typeof(PaginationDisplayMode), typeof(PaginationControl), new PropertyMetadata(PaginationDisplayMode.Simple));
    public static readonly DependencyProperty VisiblePageNumbersProperty =
        DependencyProperty.Register(nameof(VisiblePageNumbers), typeof(IEnumerable), typeof(PaginationControl));

    // ===== 翻页命令 =====
    public static readonly DependencyProperty FirstPageCommandProperty =
        DependencyProperty.Register(nameof(FirstPageCommand), typeof(ICommand), typeof(PaginationControl));
    public static readonly DependencyProperty PrevPageCommandProperty =
        DependencyProperty.Register(nameof(PrevPageCommand), typeof(ICommand), typeof(PaginationControl));
    public static readonly DependencyProperty NextPageCommandProperty =
        DependencyProperty.Register(nameof(NextPageCommand), typeof(ICommand), typeof(PaginationControl));
    public static readonly DependencyProperty LastPageCommandProperty =
        DependencyProperty.Register(nameof(LastPageCommand), typeof(ICommand), typeof(PaginationControl));
    public static readonly DependencyProperty GoToPageCommandProperty =
        DependencyProperty.Register(nameof(GoToPageCommand), typeof(ICommand), typeof(PaginationControl));

    public int CurrentPage { get => (int)GetValue(CurrentPageProperty); set => SetValue(CurrentPageProperty, value); }
    public int TotalPages { get => (int)GetValue(TotalPagesProperty); set => SetValue(TotalPagesProperty, value); }
    public int TotalCount { get => (int)GetValue(TotalCountProperty); set => SetValue(TotalCountProperty, value); }
    public int PageSize { get => (int)GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }
    public IEnumerable PageSizeOptions { get => (IEnumerable)GetValue(PageSizeOptionsProperty)!; set => SetValue(PageSizeOptionsProperty, value); }

    public PaginationDisplayMode DisplayMode { get => (PaginationDisplayMode)GetValue(DisplayModeProperty); set => SetValue(DisplayModeProperty, value); }
    public IEnumerable? VisiblePageNumbers { get => (IEnumerable?)GetValue(VisiblePageNumbersProperty); set => SetValue(VisiblePageNumbersProperty, value); }

    public ICommand? FirstPageCommand { get => (ICommand?)GetValue(FirstPageCommandProperty); set => SetValue(FirstPageCommandProperty, value); }
    public ICommand? PrevPageCommand { get => (ICommand?)GetValue(PrevPageCommandProperty); set => SetValue(PrevPageCommandProperty, value); }
    public ICommand? NextPageCommand { get => (ICommand?)GetValue(NextPageCommandProperty); set => SetValue(NextPageCommandProperty, value); }
    public ICommand? LastPageCommand { get => (ICommand?)GetValue(LastPageCommandProperty); set => SetValue(LastPageCommandProperty, value); }
    public ICommand? GoToPageCommand { get => (ICommand?)GetValue(GoToPageCommandProperty); set => SetValue(GoToPageCommandProperty, value); }

    public PaginationControl()
    {
        InitializeComponent();
    }
}
