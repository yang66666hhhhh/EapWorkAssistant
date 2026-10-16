using EapWorkAssistant.Helpers;
using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using System.ComponentModel;
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
    // 还原窗口几何时的最小可用尺寸（低于此值视为脏数据，回退默认尺寸）
    private const double MinRestoreWidth = 960;
    private const double MinRestoreHeight = 640;

    private readonly List<KeyBinding> _dynamicBindings = new();
    private bool _autoResponsive = true;

    public MainWindow()
    {
        InitializeComponent();
        // 窗口入场：先置全透明，Loaded 后淡入，消除启动"硬切"的出现感
        Opacity = 0;
        RestoreWindowState();
        Loaded += MainWindow_Loaded;
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += MainWindow_StateChanged;
        UpdateMaxRestoreVisual();
    }

    // ===== 自绘标题栏窗口按钮 =====

    private void TitleBarMinimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void TitleBarMaxRestore_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void TitleBarClose_Click(object sender, RoutedEventArgs e) => Close();

    private void MainWindow_StateChanged(object? sender, EventArgs e) => UpdateMaxRestoreVisual();

    /// <summary>切换右上角「最大化/还原」图标与提示文案。</summary>
    private void UpdateMaxRestoreVisual()
    {
        var maximized = WindowState == WindowState.Maximized;
        if (IconMaximize != null)
            IconMaximize.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        if (IconRestore != null)
            IconRestore.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
        if (TitleBarMaxRestore != null)
            TitleBarMaxRestore.ToolTip = maximized ? "向下还原" : "最大化";
    }

    /// <summary>
    /// 还原上次窗口尺寸/位置/最大化状态。
    /// - 尺寸低于最小可用值或未记录 → 沿用 XAML 默认尺寸；
    /// - 位置未记录或落在已拔掉的显示器上 → 沿用居中（WindowStartupLocation）；
    /// - 最大化状态在构造期设置，避免显示后跳变。
    /// </summary>
    private void RestoreWindowState()
    {
        var cfg = ConfigService.Instance;

        if (cfg.WindowWidth >= MinRestoreWidth && cfg.WindowHeight >= MinRestoreHeight)
        {
            Width = cfg.WindowWidth;
            Height = cfg.WindowHeight;
        }

        if (cfg.WindowLeft.HasValue && cfg.WindowTop.HasValue
            && IsWindowOnScreen(cfg.WindowLeft.Value, cfg.WindowTop.Value))
        {
            // 位置由配置决定，关闭 CenterScreen 自动居中
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = cfg.WindowLeft.Value;
            Top = cfg.WindowTop.Value;
        }

        if (cfg.WindowMaximized)
            WindowState = WindowState.Maximized;
    }

    /// <summary>
    /// 校验还原坐标是否落在当前虚拟屏幕内（多屏/换显示器场景下窗口可能落在不存在的区域）。
    /// 以窗口标题栏左侧一小片区域为探针，只要该点可见即可正常还原。
    /// </summary>
    private static bool IsWindowOnScreen(double left, double top)
    {
        var vl = SystemParameters.VirtualScreenLeft;
        var vt = SystemParameters.VirtualScreenTop;
        var vr = vl + SystemParameters.VirtualScreenWidth;
        var vb = vt + SystemParameters.VirtualScreenHeight;

        var probeX = left + 120;
        var probeY = top + 20;
        return probeX >= vl && probeX <= vr && probeY >= vt && probeY <= vb;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        PersistWindowState();
    }

    /// <summary>
    /// 关闭时保存窗口几何。最大化时 Left/Top/Width/Height 反映的是最大化尺寸，
    /// 必须改用 RestoreBounds 才能记住"还原后"的位置与大小。
    /// </summary>
    private void PersistWindowState()
    {
        var maximized = WindowState == WindowState.Maximized;
        var bounds = maximized
            ? RestoreBounds
            : new Rect(Left, Top, Width, Height);

        // 未记录的极端情况（RestoreBounds 可能为 Empty）跳过，避免写入 0/NaN 脏值
        if (bounds.IsEmpty
            || double.IsNaN(bounds.Left) || double.IsNaN(bounds.Top)
            || double.IsNaN(bounds.Width) || double.IsNaN(bounds.Height)
            || bounds.Width < MinRestoreWidth || bounds.Height < MinRestoreHeight)
            return;

        ConfigService.Instance.SaveWindowState(bounds.Width, bounds.Height, bounds.Left, bounds.Top, maximized);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateShortcutState();
        RegisterAllShortcuts();

        // 设置页修改快捷键后由自身重新注册（解耦 ViewModel 对 View 的直接引用）
        if (DataContext is MainViewModel vm)
        {
            vm.Settings.ShortcutsChanged += RegisterAllShortcuts;

            // 命令面板「新增工作记录」操作项：VM 抛事件，View 负责延迟到布局完成后开抽屉
            // （与 Ctrl+N 同链路：DataTemplate 重建后须等其订阅完成，Loaded 优先级必晚于订阅）
            vm.RequestNewFormDeferred += () => Dispatcher.BeginInvoke(
                vm.WorkRecord.RequestNewForm, System.Windows.Threading.DispatcherPriority.Loaded);

            // 还原侧边栏折叠状态：窄屏优先自动折叠，否则尊重用户上次手动设置
            var narrow = ActualWidth < AutoCollapseBelow;
            if (narrow || ConfigService.Instance.SidebarCollapsed)
            {
                ApplyCollapsedVisuals();
                vm.IsSidebarCollapsed = true;
            }
            // 用户显式保存了"宽屏下也折叠"的状态时，关闭响应式自动展开，避免启动即被还原
            _autoResponsive = !(ConfigService.Instance.SidebarCollapsed && !narrow);

            // 首启引导：从未完成过引导时自动弹出（设置页可手动重放，Esc/跳过均可关闭）
            if (!ConfigService.Instance.HasCompletedOnboarding)
                vm.ShowOnboardingCommand.Execute(null);
        }

        // 应用当前字体大小缩放（Initialize 时窗口尚未就绪，需在此处补设）
        ApplyUIScale();

        PlayWindowEnterAnimation();
    }

    /// <summary>折叠态视觉：列宽收至图标轨道 + 底部个人卡片收缩至仅露头像（无动画，用于启动还原）。</summary>
    private void ApplyCollapsedVisuals()
    {
        SidebarColumn.Width = new GridLength(CollapsedSidebarWidth, GridUnitType.Pixel);
        if (ProfileRingScale != null)
        {
            ProfileRingScale.ScaleX = 0;
            ProfileRingScale.ScaleY = 0;
        }
        if (ProfileCardRing != null) ProfileCardRing.Opacity = 0;
        if (ProfileInfo != null) ProfileInfo.Opacity = 0;
        if (ProfileMeta != null) ProfileMeta.Opacity = 0;
    }

    /// <summary>
    /// 窗口入场动画：淡入。
    /// 只做 Opacity 不做位移——Window 的 RenderTransform 会在移动期间露出窗口背后的桌面，观感穿帮。
    /// </summary>
    private void PlayWindowEnterAnimation()
    {
        var fade = new DoubleAnimation(1, MotionTokens.GetDuration("DurationEnter", 220))
        {
            EasingFunction = MotionTokens.GetEasing("EaseStandard")
        };
        BeginAnimation(OpacityProperty, fade);
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
            Duration = MotionTokens.GetDuration("DurationSidebar", 220)
        };
        SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, anim);

        // 底部卡片缩放收缩：以头像位置为原点缩小，卡片"缩进"头像里
        AnimateProfileScale(collapse);

        // 折叠状态即时落盘（下次启动还原）
        ConfigService.Instance.SidebarCollapsed = collapse;
    }

    /// <summary>装饰环收缩动画：以头像中心为原点，灰色卡片 Scale(1→0) + Opacity(1→0) 缩至消失，头像完全不动。</summary>
    private void AnimateProfileScale(bool collapse)
    {
        if (ProfileRingScale == null || ProfileCardRing == null) return;

        // 动画参数：cubic-bezier(0.4, 0, 0.2, 1) → EaseInOut；时长取自 DurationSlow 令牌（0.40s）
        var duration = MotionTokens.GetDuration("DurationSlow", 400);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        // 装饰环缩放：展开=1.0，折叠=0（完全缩至原点/头像中心）
        var targetScale = collapse ? 0.0 : 1.0;
        var scaleXAnim = new DoubleAnimation(targetScale, duration) { EasingFunction = ease };
        var scaleYAnim = new DoubleAnimation(targetScale, duration) { EasingFunction = ease };

        ProfileRingScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
        ProfileRingScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);

        // 装饰环淡出/淡入（配合缩放，确保折叠态完全不可见）
        var opacityTarget = collapse ? 0.0 : 1.0;
        var opacityAnim = new DoubleAnimation(opacityTarget, duration) { EasingFunction = ease };
        ProfileCardRing.BeginAnimation(UIElement.OpacityProperty, opacityAnim);

        // 文字信息淡出/淡入（头像保持不变，无需处理）
        var textTarget = collapse ? 0.0 : 1.0;
        var textAnim = new DoubleAnimation(textTarget, duration) { EasingFunction = ease };
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

        // 新增记录：切到工作记录页 → 打开「新增」抽屉 → 聚焦首个输入框
        AddBinding(cfg.ShortcutNew, Key.N, cfg.ShortcutNewEnabled, () =>
        {
            if (DataContext is not MainViewModel vm) return;

            // NavigateTo 可能被「未保存确认」拦下（用户取消则留在原页），此时不应打开抽屉
            vm.NavigateToCommand.Execute(ViewNames.WorkRecord);
            if (!ReferenceEquals(vm.CurrentView, vm.WorkRecord)) return;

            // 视图由 DataTemplate 在下一轮布局中重建，须等其 DataContextChanged 完成订阅后再发信号
            Dispatcher.BeginInvoke(
                vm.WorkRecord.RequestNewForm,
                System.Windows.Threading.DispatcherPriority.Loaded);
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
        if (DataContext is not MainViewModel vm) return;

        switch (e.Key)
        {
            case Key.Escape:
                vm.CloseSearchCommand.Execute(null);
                e.Handled = true;
                break;

            // ↑↓ 在结果列表内移动高亮（焦点保持在输入框，便于继续输入）
            case Key.Down:
                if (vm.ShowResults)
                {
                    vm.MoveSearchSelectionCommand.Execute(1);
                    ScrollSelectedResultIntoView();
                    e.Handled = true;
                }
                break;

            case Key.Up:
                if (vm.ShowResults)
                {
                    vm.MoveSearchSelectionCommand.Execute(-1);
                    ScrollSelectedResultIntoView();
                    e.Handled = true;
                }
                break;

            // Enter 打开高亮项；无高亮（如仍在搜索中）时退回立即搜索
            case Key.Enter:
                vm.ActivateSelectedResultCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    /// <summary>把命令面板当前高亮项滚动进可视区（键盘移动时才需要）。</summary>
    private void ScrollSelectedResultIntoView()
    {
        if (SearchResultsList?.SelectedItem == null) return;
        SearchResultsList.ScrollIntoView(SearchResultsList.SelectedItem);
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
