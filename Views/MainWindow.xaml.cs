using EapWorkAssistant.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace EapWorkAssistant.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
}
