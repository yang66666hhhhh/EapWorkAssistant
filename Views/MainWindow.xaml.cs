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
        // 添加Ctrl+F快捷键聚焦搜索框
        InputBindings.Add(new KeyBinding(new RelayCommand(FocusSearch), Key.F, ModifierKeys.Control));
    }

    private void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }

    private void UpdateShortcutState()
    {
        var enabled = ConfigService.Instance.EnableShortcuts;
        InputBindings.Clear();

        if (enabled)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                InputBindings.Add(new KeyBinding(vm.NavigateToCommand, Key.N, ModifierKeys.Control) { CommandParameter = "WorkRecord" });
                InputBindings.Add(new KeyBinding(vm.NavigateToCommand, Key.D1, ModifierKeys.Control) { CommandParameter = "Dashboard" });
                InputBindings.Add(new KeyBinding(vm.NavigateToCommand, Key.D2, ModifierKeys.Control) { CommandParameter = "WorkRecord" });
                InputBindings.Add(new KeyBinding(vm.NavigateToCommand, Key.D3, ModifierKeys.Control) { CommandParameter = "Knowledge" });
                InputBindings.Add(new KeyBinding(vm.NavigateToCommand, Key.D4, ModifierKeys.Control) { CommandParameter = "Issue" });
                InputBindings.Add(new KeyBinding(vm.NavigateToCommand, Key.D5, ModifierKeys.Control) { CommandParameter = "Settings" });
            }
        }
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
                vm.IsSearchPopupOpen = false;
                vm.SearchKeyword = string.Empty;
                SearchBox.Text = string.Empty;
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

    // 当从设置页面返回时刷新快捷键状态
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        UpdateShortcutState();
    }
}
