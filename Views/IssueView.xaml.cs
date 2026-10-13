using EapWorkAssistant.Controls;
using EapWorkAssistant.Helpers;
using EapWorkAssistant.Models;
using EapWorkAssistant.Services;
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
        OfferDraftRestore();
    }

    /// <summary>打开新建抽屉后：若存在上次未完成的草稿，询问是否继续编辑。</summary>
    private void OfferDraftRestore()
    {
        if (DataContext is not IssueViewModel vm) return;

        var draft = DraftService.Instance.Get<Issue>();
        if (draft == null) return;

        var savedAt = DraftService.Instance.GetSavedAt<Issue>();
        var title = string.IsNullOrWhiteSpace(draft.Title) ? "（未填写标题）" : draft.Title;
        bool restore = ConfirmDialog.Show(
            $"发现上次未完成的问题草稿（保存于 {savedAt}），是否继续编辑？\n\n标题：{title}",
            "恢复草稿？",
            ConfirmDialogType.Info,
            "恢复", "放弃");
        // 读取即消费：无论恢复与否都清掉，避免反复打扰。
        DraftService.Instance.Clear<Issue>();
        if (!restore) return;

        vm.RestoreDraft(draft);
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

    // 关闭完成后：静默留存草稿 → 重置脏标记并回到新增态
    private void FormDrawer_Closed(object sender, RoutedEventArgs e)
    {
        if (DataContext is IssueViewModel vm)
        {
            SaveDraftOnClose(vm);
            vm.IsFormDirty = false;
            vm.NewCommand.Execute(null);
        }
    }

    /// <summary>关闭抽屉时静默保留「新增」草稿，供下次打开新建时恢复。</summary>
    private static void SaveDraftOnClose(IssueViewModel vm)
    {
        if (vm.CurrentItem.Id > 0) return;  // 编辑态数据本就在库中，不占草稿位
        if (!vm.HasUnsavedInput()) return;  // 空表单不写文件
        DraftService.Instance.Save(vm.CurrentItem);
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
