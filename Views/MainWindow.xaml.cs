using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace EapWorkAssistant.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateShortcutState();
        // 添加Ctrl+F快捷键打开搜索
        InputBindings.Add(new KeyBinding(new RelayCommand(() =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.OpenSearchCommand.Execute(null);
                Dispatcher.BeginInvoke(() => SearchBox.Focus(), System.Windows.Threading.DispatcherPriority.Render);
            }
        }), Key.F, ModifierKeys.Control));
    }

    private void UpdateShortcutState()
    {
        var enabled = ConfigService.Instance.EnableShortcuts;
        // 保留Ctrl+F快捷键（搜索始终可用）
        var searchBinding = InputBindings.OfType<KeyBinding>().FirstOrDefault(b => b.Key == Key.F);
        // 其他快捷键根据设置动态添加/移除
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
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
