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
}
