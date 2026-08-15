using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// DataGrid 附加属性：支持将 SelectedItems（非 DependencyProperty）绑定到 ViewModel 的 ObservableCollection。
/// 用法：&lt;DataGrid helpers:DataGridHelper.SelectedItemsList=&quot;{Binding MySelectedItems}&quot; /&gt;
/// </summary>
public static class DataGridHelper
{
    public static readonly DependencyProperty SelectedItemsListProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItemsList",
            typeof(INotifyCollectionChanged),
            typeof(DataGridHelper),
            new PropertyMetadata(null, OnSelectedItemsListChanged));

    public static void SetSelectedItemsList(DependencyObject element, INotifyCollectionChanged? value)
        => element.SetValue(SelectedItemsListProperty, value);

    public static INotifyCollectionChanged? GetSelectedItemsList(DependencyObject element)
        => (INotifyCollectionChanged?)element.GetValue(SelectedItemsListProperty);

    private static void OnSelectedItemsListChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dataGrid) return;

        // 解除旧的事件订阅
        if (e.OldValue is INotifyCollectionChanged oldCollection)
            dataGrid.SelectionChanged -= CreateSelectionChangedHandler(oldCollection);

        // 订阅新集合
        if (e.NewValue is INotifyCollectionChanged newCollection)
            dataGrid.SelectionChanged += CreateSelectionChangedHandler(newCollection);
    }

    private static SelectionChangedEventHandler CreateSelectionChangedHandler(INotifyCollectionChanged targetCollection)
    {
        return (sender, e) =>
        {
            if (sender is not DataGrid dg || targetCollection is not System.Collections.IList list) return;

            // 使用 Dispatcher 确保在 UI 线程同步集合
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                list.Clear();
                foreach (var item in dg.SelectedItems)
                    list.Add(item);
            });
        };
    }
}
