using EapWorkAssistant.Models;
using EapWorkAssistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EapWorkAssistant.Views;

public partial class AttendanceView : UserControl
{
    public AttendanceView()
    {
        InitializeComponent();
    }

    /// <summary>统计卡点击 → 下钻到对应类型筛选。</summary>
    private void StatCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Controls.StatCard card || DataContext is not AttendanceViewModel vm)
            return;

        var statKey = card.Title switch
        {
            "正常上班" => "Normal",
            "周末加班" => "Weekend",
            "假日加班" => "Holiday",
            "请假" => "Leave",
            "未填" => "Unfilled",
            _ => "All"
        };
        vm.FilterByStatCommand.Execute(statKey);
    }

    // ===== 表格交互打磨：双击某天 → 跳工作记录（未填行 = 去补录）=====

    private void AttendanceGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not AttendanceViewModel vm) return;
        if (sender is DataGrid dg
            && dg.ContainerFromElement(e.OriginalSource as DependencyObject) is DataGridRow
            && dg.SelectedItem is DayStatusItem item)
        {
            vm.OpenDay(item);
        }
    }

    /// <summary>
    /// 列头点击排序：拦截 DataGrid 内置排序，交给 ViewModel 内存排序（与工作记录页同范式）。
    /// SortMemberPath 为空的列（如「星期」）不会触发本事件，因此不会误排序。
    /// </summary>
    private void AttendanceGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (DataContext is AttendanceViewModel vm && !string.IsNullOrEmpty(e.Column.SortMemberPath))
            vm.ToggleSort(e.Column.SortMemberPath);
    }

    // ===== P1 ⑦ 热力图交互：点击格子 → 跳工作记录（未填日 = 去补录；区间外灰格无 Item 不响应）=====

    private void HeatmapCell_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AttendanceViewModel vm
            && sender is FrameworkElement { DataContext: HeatmapCell cell })
        {
            vm.OpenHeatmapDay(cell);
        }
    }
}
