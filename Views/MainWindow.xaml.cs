using EapWorkAssistant.Helpers;
using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EapWorkAssistant.Views;

public partial class MainWindow : Window
{
    // 侧边栏宽度：展开 240（建议范围 220–280）、折叠 72（图标轨道）
    private const double ExpandedSidebarWidth = 240;
    private const double CollapsedSidebarWidth = 72;
    // 响应式阈值：窄于 900 自动折叠，宽于 1200 自动展开
    private const double AutoCollapseBelow = 900;
    private const double AutoExpandAbove = 1200;

    private readonly List<KeyBinding> _dynamicBindings = new();
    private bool _autoResponsive = true;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        SizeChanged += MainWindow_SizeChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateShortcutState();
        RegisterAllShortcuts();

        // 设置页修改快捷键后由自身重新注册（解耦 ViewModel 对 View 的直接引用）
        if (DataContext is MainViewModel vm)
            vm.Settings.ShortcutsChanged += RegisterAllShortcuts;

        // 应用当前字体大小缩放（Initialize 时窗口尚未就绪，需在此处补设）
        ApplyUIScale();

        // 窄屏启动（如平板/小窗）默认折叠侧边栏，避免内容被挤压
        if (DataContext is MainViewModel mvm && ActualWidth < AutoCollapseBelow)
        {
            SidebarColumn.Width = new GridLength(CollapsedSidebarWidth, GridUnitType.Pixel);
            mvm.IsSidebarCollapsed = true;
            // 折叠态：装饰环缩至消失，文字不可见，头像保持不变
            if (ProfileRingScale != null)
            {
                ProfileRingScale.ScaleX = 0;
                ProfileRingScale.ScaleY = 0;
            }
            if (ProfileCardRing != null) ProfileCardRing.Opacity = 0;
            if (ProfileInfo != null) ProfileInfo.Opacity = 0;
            if (ProfileMeta != null) ProfileMeta.Opacity = 0;
        }
    }

    /// <summary>折叠/展开侧边栏（带平滑宽度动画），用户手动操作后交由用户控制，停止响应式自动切换。</summary>
    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var collapse = !vm.IsSidebarCollapsed;
        AnimateSidebar(collapse);
        vm.IsSidebarCollapsed = collapse;
        _autoResponsive = false;
    }

    /// <summary>平滑动画侧边栏列宽（展开 240 / 折叠 72），同时裁剪底部卡片至仅露头像。</summary>
    private void AnimateSidebar(bool collapse)
    {
        var target = collapse ? CollapsedSidebarWidth : ExpandedSidebarWidth;
        var anim = new GridLengthAnimation
        {
            From = SidebarColumn.Width,
            To = new GridLength(target, GridUnitType.Pixel),
            Duration = TimeSpan.FromMilliseconds(220)
        };
        SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, anim);

        // 底部卡片缩放收缩：以头像位置为原点缩小，卡片"缩进"头像里
        AnimateProfileScale(collapse);
    }

    /// <summary>装饰环收缩动画：以头像中心为原点，灰色卡片 Scale(1→0) + Opacity(1→0) 缩至消失，头像完全不动。</summary>
    private void AnimateProfileScale(bool collapse)
    {
        if (ProfileRingScale == null || ProfileCardRing == null) return;

        // 动画参数：0.4s, cubic-bezier(0.4, 0, 0.2, 1) → EaseInOut
        var duration = TimeSpan.FromMilliseconds(400);  // 0.4s
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        // 装饰环缩放：展开=1.0，折叠=0（完全缩至原点/头像中心）
        var targetScale = collapse ? 0.0 : 1.0;
        var scaleXAnim = new DoubleAnimation(targetScale, new Duration(duration)) { EasingFunction = ease };
        var scaleYAnim = new DoubleAnimation(targetScale, new Duration(duration)) { EasingFunction = ease };

        ProfileRingScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
        ProfileRingScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);

        // 装饰环淡出/淡入（配合缩放，确保折叠态完全不可见）
        var opacityTarget = collapse ? 0.0 : 1.0;
        var opacityAnim = new DoubleAnimation(opacityTarget, new Duration(duration)) { EasingFunction = ease };
        ProfileCardRing.BeginAnimation(UIElement.OpacityProperty, opacityAnim);

        // 文字信息淡出/淡入（头像保持不变，无需处理）
        var textTarget = collapse ? 0.0 : 1.0;
        var textAnim = new DoubleAnimation(textTarget, new Duration(duration)) { EasingFunction = ease };
        if (ProfileInfo != null) ProfileInfo.BeginAnimation(UIElement.OpacityProperty, textAnim);
        if (ProfileMeta != null) ProfileMeta.BeginAnimation(UIElement.OpacityProperty, textAnim);
    }

    /// <summary>响应式：窗口变窄自动折叠、变宽自动展开；用户手动操作后不再自动干预。</summary>
    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_autoResponsive || DataContext is not MainViewModel vm) return;
        if (ActualWidth < AutoCollapseBelow && !vm.IsSidebarCollapsed)
        {
            AnimateSidebar(true);
            vm.IsSidebarCollapsed = true;
        }
        else if (ActualWidth > AutoExpandAbove && vm.IsSidebarCollapsed)
        {
            AnimateSidebar(false);
            vm.IsSidebarCollapsed = false;
        }
    }

    private void ApplyUIScale()
    {
        var scaleStr = Application.Current.Resources["UIScale"];
        if (scaleStr is double scale && scale > 0)
        {
            MainContentArea.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        RegisterAllShortcuts();
    }

    private Key ParseKey(string keyStr, Key fallback)
    {
        if (!string.IsNullOrWhiteSpace(keyStr) && Enum.TryParse<Key>(keyStr, true, out var parsed))
            return parsed;
        return fallback;
    }

    public void RegisterAllShortcuts()
    {
        // 清除旧的动态绑定
        foreach (var b in _dynamicBindings)
            InputBindings.Remove(b);
        _dynamicBindings.Clear();

        var cfg = ConfigService.Instance;

        // 总开关关闭时不注册任何快捷键
        if (!cfg.EnableShortcuts)
        {
            if (SearchPlaceholder != null)
                SearchPlaceholder.Text = "搜索...";
            return;
        }

        // 搜索
        AddBinding(cfg.ShortcutSearch, Key.F, cfg.ShortcutSearchEnabled, () =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsSearchOpen = true;
                vm.SearchKeyword = string.Empty;
                Dispatcher.BeginInvoke(() => MainSearchBox.Focus(), System.Windows.Threading.DispatcherPriority.Render);
            }
        });

        // 新增记录
        AddBinding(cfg.ShortcutNew, Key.N, cfg.ShortcutNewEnabled, () =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.NavigateToCommand.Execute(ViewNames.WorkRecord);
                vm.WorkRecord.NewRecordCommand.Execute(null);
            }
        });

        // 保存记录
        AddBinding(cfg.ShortcutSave, Key.S, cfg.ShortcutSaveEnabled, () =>
        {
            if (DataContext is MainViewModel vm && vm.CurrentView is WorkRecordViewModel wr)
                wr.SaveRecordCommand.ExecuteAsync(null).SafeFire("保存记录失败");
        });

        // 视图切换 1~5
        var views = new[] { ViewNames.Dashboard, ViewNames.WorkRecord, ViewNames.Knowledge, ViewNames.Issue, ViewNames.Settings };
        var defaultKeys = new[] { Key.D1, Key.D2, Key.D3, Key.D4, Key.D5 };
        var cfgKeys = new[] { cfg.ShortcutView1, cfg.ShortcutView2, cfg.ShortcutView3, cfg.ShortcutView4, cfg.ShortcutView5 };
        var enabledFlags = new[] { cfg.ShortcutView1Enabled, cfg.ShortcutView2Enabled, cfg.ShortcutView3Enabled, cfg.ShortcutView4Enabled, cfg.ShortcutView5Enabled };
        for (int i = 0; i < 5; i++)
        {
            var view = views[i];
            AddBinding(cfgKeys[i], defaultKeys[i], enabledFlags[i], () =>
            {
                if (DataContext is MainViewModel vm)
                    vm.NavigateToCommand.Execute(view);
            });
        }

        // 更新搜索框占位文字
        if (SearchPlaceholder != null)
            SearchPlaceholder.Text = cfg.ShortcutSearchEnabled ? $"搜索... (Ctrl+{cfg.ShortcutSearch})" : "搜索...";
    }

    private void AddBinding(string keyStr, Key fallback, bool enabled, Action action)
    {
        if (!enabled) return;
        var key = ParseKey(keyStr, fallback);
        var binding = new KeyBinding(new RelayCommand(action), key, ModifierKeys.Control);
        _dynamicBindings.Add(binding);
        InputBindings.Add(binding);
    }

    private void UpdateShortcutState()
    {
        // 快捷键由 RegisterAllShortcuts 统一管理
    }

    private void ProfileCard_Click(object sender, MouseButtonEventArgs e)
    {
        ProfileDialog.Show();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.CloseSearchCommand.Execute(null);
            }
        }
        else if (e.Key == Key.Enter)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SearchCommand.ExecuteAsync(null).SafeFire("搜索失败");
            }
        }
    }

    private void SearchResult_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SearchResultItem item)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.NavigateToResultCommand.Execute(item);
            }
        }
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsSearchOpen = true;
            if (string.IsNullOrWhiteSpace(vm.SearchKeyword))
            {
                vm.ShowInitial = true;
                vm.ShowNoResults = false;
                vm.ShowResults = false;
            }
        }
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsSearchOpen)
        {
            var focused = FocusManager.GetFocusedElement(this);
            // 焦点没有转移到搜索下拉面板内部时才关闭
            if (focused is not DependencyObject dObj || !IsVisualChildOf(dObj, "SearchResultsPanel"))
            {
                vm.CloseSearchCommand.Execute(null);
            }
        }
    }

    private static bool IsVisualChildOf(DependencyObject element, string parentName)
    {
        while (element != null)
        {
            if (element is FrameworkElement fe && fe.Name == parentName)
                return true;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private void SearchOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.CloseSearchCommand.Execute(null);
        }
    }

    private class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
