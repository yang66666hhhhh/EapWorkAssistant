using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EapWorkAssistant.Views;

public partial class IssueView : UserControl
{
    private bool _isDrawerOpen;

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

    private void OnPanelCloseRequested() => CloseDrawer();

    private void FormField_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is IssueViewModel vm)
            vm.MarkDirty();
    }

    // ===== 浮窗抽屉动画 =====

    private void OpenForm_Click(object sender, RoutedEventArgs e)
    {
        if (_isDrawerOpen) return;
        // 新增模式：重置表单
        if (DataContext is IssueViewModel vm)
        {
            vm.CurrentItem = new EapWorkAssistant.Models.Issue();
            vm.IsFormDirty = false;
        }
        OpenDrawer();
    }

    private void EditRow_Click(object sender, RoutedEventArgs e)
    {
        // EditCommand 已通过 Command 绑定执行，此处只需打开抽屉
        OpenDrawer();
    }

    private void OpenDrawer()
    {
        if (_isDrawerOpen) return;
        _isDrawerOpen = true;
        DrawerHelper.OpenDrawer(Backdrop, FormPanel, OpenFormBtn, 500);
    }

    private void CloseForm_Click(object sender, RoutedEventArgs e)
    {
        CloseDrawer();
    }

    private void Backdrop_Click(object sender, MouseButtonEventArgs e)
    {
        CloseDrawer();
    }

    private void CloseDrawer()
    {
        if (!_isDrawerOpen) return;

        if (DataContext is IssueViewModel vm && vm.IsFormDirty)
        {
            bool confirmed = ConfirmDialog.Show(
                "当前表单有未保存的修改，确定要放弃吗？",
                "放弃修改？",
                ConfirmDialogType.Warning,
                "放弃", "取消");
            if (!confirmed) return;
        }

        _isDrawerOpen = false;
        DrawerHelper.CloseDrawer(Backdrop, FormPanel, OpenFormBtn, () =>
        {
            if (DataContext is IssueViewModel vm)
                vm.NewCommand.Execute(null);
        }, 500);
    }

    // ===== 表格交互打磨：双击编辑 =====

    private void ItemsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawerOpen) return;
        if (DataContext is not IssueViewModel vm) return;
        if (sender is DataGrid dg
            && dg.ContainerFromElement(e.OriginalSource as DependencyObject) is DataGridRow
            && dg.SelectedItem is Issue item)
        {
            vm.EditCommand.Execute(item);
            OpenDrawer();
        }
    }

    // ===== 抽屉内键盘快捷键：Esc 关闭 / Ctrl+S 保存 =====

    private void FormPanel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isDrawerOpen) return;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseDrawer();
        }
        else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            if (DataContext is IssueViewModel vm)
                vm.SaveCommand.Execute(null);
        }
    }
}
