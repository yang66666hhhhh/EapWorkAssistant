using EapWorkAssistant.Services;
using System.Windows;

namespace EapWorkAssistant.Views;

public partial class ProfileDialog : Window
{
    public ProfileDialog()
    {
        InitializeComponent();
        var p = ProfileService.Instance;
        NameInput.Text = p.Name;
        RoleInput.Text = p.Role;
        IndustryInput.Text = p.Industry;
        DepartmentInput.Text = p.Department;
        FocusInput.Text = p.Focus;
        AvatarPreview.Text = p.AvatarInitial;
        ProbationRadio.IsChecked = p.IsProbation;
        RegularRadio.IsChecked = !p.IsProbation;
        NameInput.TextChanged += (_, _) =>
            AvatarPreview.Text = string.IsNullOrWhiteSpace(NameInput.Text) ? "U" : NameInput.Text[..1];
    }

    /// <summary>
    /// 快捷方法：显示个人信息编辑对话框
    /// </summary>
    public static new bool Show()
    {
        var dialog = new ProfileDialog { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ConfirmDialog.Alert("姓名不能为空");
            return;
        }

        var p = ProfileService.Instance;
        p.Name = name;
        p.Role = RoleInput.Text.Trim();
        p.Industry = IndustryInput.Text.Trim();
        p.Department = DepartmentInput.Text.Trim();
        p.Focus = FocusInput.Text.Trim();
        p.IsProbation = ProbationRadio.IsChecked == true;
        p.Save();
        p.NotifyAllChanged();

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
