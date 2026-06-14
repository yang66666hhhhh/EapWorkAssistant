using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace EapWorkAssistant.Views;

public partial class MainWindow : Window
{
    private readonly List<KeyBinding> _dynamicBindings = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateShortcutState();
        RegisterAllShortcuts();

        // 应用当前字体大小缩放（Initialize 时窗口尚未就绪，需在此处补设）
        ApplyUIScale();
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

    private void RegisterAllShortcuts()
    {
        // 清除旧的动态绑定
        foreach (var b in _dynamicBindings)
            InputBindings.Remove(b);
        _dynamicBindings.Clear();

        var cfg = ConfigService.Instance;

        // 搜索
        AddBinding(cfg.ShortcutSearch, Key.F, () =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsSearchOpen = true;
                vm.SearchKeyword = string.Empty;
                Dispatcher.BeginInvoke(() => MainSearchBox.Focus(), System.Windows.Threading.DispatcherPriority.Render);
            }
        });

        // 新增记录
        AddBinding(cfg.ShortcutNew, Key.N, () =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.NavigateToCommand.Execute("WorkRecord");
                vm.WorkRecord.NewRecordCommand.Execute(null);
            }
        });

        // 保存记录
        AddBinding(cfg.ShortcutSave, Key.S, () =>
        {
            if (DataContext is MainViewModel vm && vm.CurrentView is WorkRecordViewModel wr)
                _ = wr.SaveRecordCommand.ExecuteAsync(null);
        });

        // 视图切换 1~5
        var views = new[] { "Dashboard", "WorkRecord", "Knowledge", "Issue", "Settings" };
        var defaultKeys = new[] { Key.D1, Key.D2, Key.D3, Key.D4, Key.D5 };
        var cfgKeys = new[] { cfg.ShortcutView1, cfg.ShortcutView2, cfg.ShortcutView3, cfg.ShortcutView4, cfg.ShortcutView5 };
        for (int i = 0; i < 5; i++)
        {
            var view = views[i];
            AddBinding(cfgKeys[i], defaultKeys[i], () =>
            {
                if (DataContext is MainViewModel vm)
                    vm.NavigateToCommand.Execute(view);
            });
        }

        // 更新搜索框占位文字
        if (SearchPlaceholder != null)
            SearchPlaceholder.Text = $"搜索... (Ctrl+{cfg.ShortcutSearch})";
    }

    private void AddBinding(string keyStr, Key fallback, Action action)
    {
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
                _ = vm.SearchCommand.ExecuteAsync(null);
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
