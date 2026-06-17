using EapWorkAssistant.Helpers;
using EapWorkAssistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EapWorkAssistant.Views;

public partial class KnowledgeView : UserControl
{
    private bool _isDrawerOpen;

    public KnowledgeView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is KnowledgeViewModel oldVm)
                oldVm.PanelCloseRequested -= OnPanelCloseRequested;
            if (e.NewValue is KnowledgeViewModel newVm)
                newVm.PanelCloseRequested += OnPanelCloseRequested;
        };
    }

    private void OnPanelCloseRequested() => CloseDrawer();

    private void FormField_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is KnowledgeViewModel vm)
            vm.MarkDirty();
    }

    private void TagSuggestion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Content is string tag)
        {
            if (DataContext is KnowledgeViewModel vm)
            {
                var currentTags = vm.CurrentItem.Tags?.Trim() ?? "";
                if (string.IsNullOrEmpty(currentTags))
                {
                    vm.CurrentItem.Tags = tag;
                }
                else if (!currentTags.Contains(tag))
                {
                    vm.CurrentItem.Tags = $"{currentTags}, {tag}";
                }
                vm.MarkDirty();
            }
        }
    }

    // ===== 浮窗抽屉动画 =====

    private void OpenForm_Click(object sender, RoutedEventArgs e)
    {
        if (_isDrawerOpen) return;
        // 新增模式：重置表单
        if (DataContext is KnowledgeViewModel vm)
        {
            vm.CurrentItem = new EapWorkAssistant.Models.Knowledge();
            vm.IsFormDirty = false;
        }
        _isDrawerOpen = true;
        DrawerHelper.OpenDrawer(Backdrop, FormPanel, OpenFormBtn, 500);
    }

    private void EditItem_Click(object sender, RoutedEventArgs e)
    {
        if (_isDrawerOpen) return;
        if (DataContext is not KnowledgeViewModel vm) return;

        // 从按钮的 DataContext 获取当前列表项
        if (sender is FrameworkElement fe && fe.DataContext is EapWorkAssistant.Models.Knowledge item)
        {
            // 通过 EditCommand 将条目加载到编辑表单
            vm.EditCommand.Execute(item);
            _isDrawerOpen = true;
            DrawerHelper.OpenDrawer(Backdrop, FormPanel, OpenFormBtn, 500);
        }
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

        if (DataContext is KnowledgeViewModel vm && vm.IsFormDirty)
        {
            bool confirmed = ConfirmDialog.Show(
                "当前表单有未保存的修改，确定要放弃吗？",
                "放弃修改？",
                ConfirmDialogType.Warning,
                "放弃", "取消");
            if (!confirmed) return;
        }

        _isDrawerOpen = false;
        DrawerHelper.CloseDrawer(Backdrop, FormPanel, OpenFormBtn, null, 500);
    }
}
