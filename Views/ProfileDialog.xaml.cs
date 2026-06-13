using EapWorkAssistant.Services;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

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

        // 加载已有头像
        RefreshAvatarPreview();

        NameInput.TextChanged += (_, _) =>
            AvatarPreview.Text = string.IsNullOrWhiteSpace(NameInput.Text) ? "U" : NameInput.Text[..1];

        // 悬浮效果
        AvatarBorder.MouseEnter += (_, _) =>
        {
            if (AvatarImageClip.Visibility == Visibility.Visible)
                AvatarOverlay.Visibility = Visibility.Visible;
        };
        AvatarBorder.MouseLeave += (_, _) => AvatarOverlay.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 快捷方法：显示个人信息编辑对话框
    /// </summary>
    public static new bool Show()
    {
        var dialog = new ProfileDialog { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true;
    }

    private void Avatar_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择头像图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.webp|所有文件|*.*",
            Multiselect = false
        };

        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(dlg.FileName, UriKind.Absolute);
                bitmap.DecodePixelWidth = 128;
                bitmap.EndInit();
                bitmap.Freeze();

                AvatarImageBrush.ImageSource = bitmap;
                AvatarImageClip.Visibility = Visibility.Visible;
                AvatarPreview.Visibility = Visibility.Collapsed;

                // 暂存路径，保存时再复制
                _pendingAvatarPath = dlg.FileName;
            }
            catch
            {
                ConfirmDialog.Alert("图片加载失败，请选择有效的图片文件");
            }
        }
    }

    private string? _pendingAvatarPath;

    private void RefreshAvatarPreview()
    {
        var p = ProfileService.Instance;
        if (p.HasAvatar && p.AvatarPath != null)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(p.AvatarPath, UriKind.Absolute);
                bitmap.DecodePixelWidth = 128;
                bitmap.EndInit();
                bitmap.Freeze();

                AvatarImageBrush.ImageSource = bitmap;
                AvatarImageClip.Visibility = Visibility.Visible;
                AvatarPreview.Visibility = Visibility.Collapsed;
            }
            catch
            {
                // 加载失败则显示首字母
            }
        }
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

        // 保存头像
        if (!string.IsNullOrEmpty(_pendingAvatarPath))
            p.SaveAvatar(_pendingAvatarPath);

        p.Save();
        p.NotifyAllChanged();

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
