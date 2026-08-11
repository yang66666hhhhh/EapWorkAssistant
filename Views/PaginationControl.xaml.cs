using System.Windows;
using System.Windows.Controls;

namespace EapWorkAssistant.Views;

/// <summary>
/// 可复用分页控件：供知识库 / 问题库等列表复用。
/// 通过依赖属性暴露页码、页大小、总页数、总条数，以及四个翻页命令；
/// 具体取数逻辑由 ViewModel 提供命令实现（与 WorkRecordViewModel 分页命令一致）。
/// </summary>
public partial class PaginationControl : UserControl
{
    public static readonly DependencyProperty CurrentPageProperty =
        DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(PaginationControl), new PropertyMetadata(1));
    public static readonly DependencyProperty TotalPagesProperty =
        DependencyProperty.Register(nameof(TotalPages), typeof(int), typeof(PaginationControl), new PropertyMetadata(1));
    public static readonly DependencyProperty TotalCountProperty =
        DependencyProperty.Register(nameof(TotalCount), typeof(int), typeof(PaginationControl), new PropertyMetadata(0));
    public static readonly DependencyProperty PageSizeProperty =
        DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(PaginationControl), new PropertyMetadata(20));
    public static readonly DependencyProperty PageSizeOptionsProperty =
        DependencyProperty.Register(nameof(PageSizeOptions), typeof(int[]), typeof(PaginationControl), new PropertyMetadata(new[] { 10, 20, 50, 100 }));

    public static readonly DependencyProperty FirstPageCommandProperty =
        DependencyProperty.Register(nameof(FirstPageCommand), typeof(System.Windows.Input.ICommand), typeof(PaginationControl));
    public static readonly DependencyProperty PrevPageCommandProperty =
        DependencyProperty.Register(nameof(PrevPageCommand), typeof(System.Windows.Input.ICommand), typeof(PaginationControl));
    public static readonly DependencyProperty NextPageCommandProperty =
        DependencyProperty.Register(nameof(NextPageCommand), typeof(System.Windows.Input.ICommand), typeof(PaginationControl));
    public static readonly DependencyProperty LastPageCommandProperty =
        DependencyProperty.Register(nameof(LastPageCommand), typeof(System.Windows.Input.ICommand), typeof(PaginationControl));

    public int CurrentPage { get => (int)GetValue(CurrentPageProperty); set => SetValue(CurrentPageProperty, value); }
    public int TotalPages { get => (int)GetValue(TotalPagesProperty); set => SetValue(TotalPagesProperty, value); }
    public int TotalCount { get => (int)GetValue(TotalCountProperty); set => SetValue(TotalCountProperty, value); }
    public int PageSize { get => (int)GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }
    public int[] PageSizeOptions { get => (int[])GetValue(PageSizeOptionsProperty); set => SetValue(PageSizeOptionsProperty, value); }

    public System.Windows.Input.ICommand? FirstPageCommand { get => (System.Windows.Input.ICommand?)GetValue(FirstPageCommandProperty); set => SetValue(FirstPageCommandProperty, value); }
    public System.Windows.Input.ICommand? PrevPageCommand { get => (System.Windows.Input.ICommand?)GetValue(PrevPageCommandProperty); set => SetValue(PrevPageCommandProperty, value); }
    public System.Windows.Input.ICommand? NextPageCommand { get => (System.Windows.Input.ICommand?)GetValue(NextPageCommandProperty); set => SetValue(NextPageCommandProperty, value); }
    public System.Windows.Input.ICommand? LastPageCommand { get => (System.Windows.Input.ICommand?)GetValue(LastPageCommandProperty); set => SetValue(LastPageCommandProperty, value); }

    public PaginationControl()
    {
        InitializeComponent();
    }
}
