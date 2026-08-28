using EapWorkAssistant.Controls;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EapWorkAssistant.Views;

public partial class IssueView : UserControl
{
    public IssueView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is IssueViewModel oldVm)
                oldVm.PanelCloseRequested -= OnPanelCloseRequested;
            if (e.NewValue is IssueViewModel newVm)
                newVm.PanelCloseRequested += OnPanelCloseRequested;
        };
    }

    private void OnPanelCloseRequested()
    {
        if (FormDrawer.IsOpen)
            FormDrawer.RequestClose();
    }

    private void FormField_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is IssueViewModel vm)
            vm.MarkDirty();
    }

    // ===== 浮窗抽屉：直接驱动 Drawer.IsOpen，遮罩/滑入动画与关闭逻辑均内化于 Drawer 控件 =====

    private void OpenForm_Click(object sender, RoutedEventArgs e)
    {
        if (FormDrawer.IsOpen) return;
        // 新增模式：安全重置表单（屏蔽绑定事件触发的脏标记）
        if (DataContext is IssueViewModel vm)
        {
            vm._suppressDirty = true;
            vm.CurrentItem = new Issue();
            vm.IsFormDirty = false;
            vm._suppressDirty = false;
        }
        FormDrawer.IsOpen = true;
    }

    private void EditRow_Click(object sender, RoutedEventArgs e)
    {
        // EditCommand 已通过 Command 绑定执行，此处只需打开抽屉
        FormDrawer.IsOpen = true;
    }

    // 关闭前脏检查：有未保存修改则弹确认，用户取消则阻止关闭
    private void FormDrawer_Closing(object sender, DrawerClosingEventArgs e)
    {
        if (DataContext is IssueViewModel vm && vm.IsFormDirty)
        {
            bool confirmed = ConfirmDialog.Show(
                "当前表单有未保存的修改，确定要放弃吗？",
                "放弃修改？",
                ConfirmDialogType.Warning,
                "放弃", "取消");
            if (!confirmed) e.Cancel = true;
        }
    }

    // 关闭完成后：重置脏标记并回到新增态（与原 CloseDrawer 回调一致）
    private void FormDrawer_Closed(object sender, RoutedEventArgs e)
    {
        if (DataContext is IssueViewModel vm)
        {
            vm.IsFormDirty = false;
            vm.NewCommand.Execute(null);
        }
    }

    // ===== 表格交互打磨：双击编辑 =====

    private void ItemsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FormDrawer.IsOpen) return;
        if (DataContext is not IssueViewModel vm) return;
        if (sender is DataGrid dg
            && dg.ContainerFromElement(e.OriginalSource as DependencyObject) is DataGridRow
            && dg.SelectedItem is Issue item)
        {
            vm.EditCommand.Execute(item);
            FormDrawer.IsOpen = true;
        }
    }
}
