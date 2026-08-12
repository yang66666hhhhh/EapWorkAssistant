using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace EapWorkAssistant.Views;

public partial class SettingsView
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private SettingsViewModel? VM => DataContext as SettingsViewModel;

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (VM != null)
        {
            UpdateFontSizeButtons(VM.SelectedFontSize);
            UpdateDensityButtons(VM.SelectedDensity);

            // 初始化所有 ToggleSwitch 状态
            if (ToggleDashStats != null) ToggleDashStats.IsChecked = VM.ShowDashStats;
            if (ToggleDashReminder != null) ToggleDashReminder.IsChecked = VM.ShowDashReminder;
            if (ToggleDashProbation != null) ToggleDashProbation.IsChecked = VM.ShowDashProbation;
            if (ToggleDashCharts != null) ToggleDashCharts.IsChecked = VM.ShowDashCharts;
            if (ToggleDashHighlights != null) ToggleDashHighlights.IsChecked = VM.ShowDashHighlights;
            if (ToggleDashRecent != null) ToggleDashRecent.IsChecked = VM.ShowDashRecent;
            if (ToggleAutoStart != null) ToggleAutoStart.IsChecked = VM.AutoStart;
            if (ToggleMinimizeToTray != null) ToggleMinimizeToTray.IsChecked = VM.MinimizeToTray;
            if (ToggleEnableShortcuts != null) ToggleEnableShortcuts.IsChecked = VM.EnableShortcuts;
            if (ToggleEnableReminder != null) ToggleEnableReminder.IsChecked = VM.EnableReminder;

            // 初始化 API 密钥密码框（默认遮罩显示，不暴露明文）
            if (AiApiKeyBox != null)
                AiApiKeyBox.Password = VM?.AiApiKey ?? string.Empty;
        }
    }

    // ===== API 密钥：密码框遮罩 + 明文切换 =====
    private void AiApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (VM != null)
            VM.AiApiKey = AiApiKeyBox.Password;
    }

    private void AiApiKeyToggle_Click(object sender, RoutedEventArgs e)
    {
        if (VM == null) return;
        bool revealing = AiApiKeyReveal.Visibility == Visibility.Visible;
        if (revealing)
        {
            // 切回遮罩：把当前明文同步回密码框（保持圆点长度正确）
            AiApiKeyBox.Password = VM.AiApiKey;
            AiApiKeyReveal.Visibility = Visibility.Collapsed;
            AiApiKeyBox.Visibility = Visibility.Visible;
            AiApiKeyEye.Data = (Geometry)FindResource("IconEye");
        }
        else
        {
            // 切换明文：用双向绑定的文本框展示，编辑即写回 VM
            AiApiKeyBox.Visibility = Visibility.Collapsed;
            AiApiKeyReveal.Visibility = Visibility.Visible;
            AiApiKeyEye.Data = (Geometry)FindResource("IconEyeOff");
        }
    }

    // ===== 外观与主题 =====
    private void LightTheme_Click(object sender, MouseButtonEventArgs e)
    {
        if (VM != null)
        {
            VM.IsLightTheme = true;
            RefreshThemeButtonBindings();
        }
    }

    private void DarkTheme_Click(object sender, MouseButtonEventArgs e)
    {
        if (VM != null)
        {
            VM.IsDarkTheme = true;
            RefreshThemeButtonBindings();
        }
    }

    /// <summary>主题切换后强制刷新转换器绑定，使按钮背景色跟随新主题</summary>
    private void RefreshThemeButtonBindings()
    {
        // 等待 DynamicResource 更新完毕后刷新
        Dispatcher.BeginInvoke(() =>
        {
            if (LightThemeBtn != null)
            {
                LightThemeBtn.GetBindingExpression(Border.BackgroundProperty)?.UpdateTarget();
                LightThemeBtn.GetBindingExpression(Border.BorderBrushProperty)?.UpdateTarget();
            }
            if (DarkThemeBtn != null)
            {
                DarkThemeBtn.GetBindingExpression(Border.BackgroundProperty)?.UpdateTarget();
                DarkThemeBtn.GetBindingExpression(Border.BorderBrushProperty)?.UpdateTarget();
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void AccentColor_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string name } && VM != null)
        {
            VM.SelectedAccentColor = name;
        }
    }

    private void FontSizeSmall_Click(object sender, MouseButtonEventArgs e)
    {
        if (VM != null) { VM.SelectedFontSize = "Small"; UpdateFontSizeButtons("Small"); }
    }

    private void FontSizeMedium_Click(object sender, MouseButtonEventArgs e)
    {
        if (VM != null) { VM.SelectedFontSize = "Medium"; UpdateFontSizeButtons("Medium"); }
    }

    private void FontSizeLarge_Click(object sender, MouseButtonEventArgs e)
    {
        if (VM != null) { VM.SelectedFontSize = "Large"; UpdateFontSizeButtons("Large"); }
    }

    private void UpdateFontSizeButtons(string selected)
    {
        void Highlight(Border btn)
        {
            btn.SetResourceReference(Border.BackgroundProperty, "PrimaryLightBrush");
            btn.SetResourceReference(Border.BorderBrushProperty, "PrimaryBrush");
            if (btn.Child is TextBlock tb)
                tb.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryBrush");
        }
        void Reset(Border btn)
        {
            btn.SetResourceReference(Border.BackgroundProperty, "CardBrush");
            btn.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            if (btn.Child is TextBlock tb)
                tb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }

        if (FontSizeSmallBtn != null) { if (selected == "Small") Highlight(FontSizeSmallBtn); else Reset(FontSizeSmallBtn); }
        if (FontSizeMediumBtn != null) { if (selected == "Medium") Highlight(FontSizeMediumBtn); else Reset(FontSizeMediumBtn); }
        if (FontSizeLargeBtn != null) { if (selected == "Large") Highlight(FontSizeLargeBtn); else Reset(FontSizeLargeBtn); }
    }

    private void DensityCompact_Click(object sender, MouseButtonEventArgs e)
    {
        if (VM != null) { VM.SelectedDensity = "Compact"; UpdateDensityButtons("Compact"); }
    }

    private void DensityDefault_Click(object sender, MouseButtonEventArgs e)
    {
        if (VM != null) { VM.SelectedDensity = "Default"; UpdateDensityButtons("Default"); }
    }

    private void DensityComfortable_Click(object sender, MouseButtonEventArgs e)
    {
        if (VM != null) { VM.SelectedDensity = "Comfortable"; UpdateDensityButtons("Comfortable"); }
    }

    private void UpdateDensityButtons(string selected)
    {
        void Highlight(Border btn)
        {
            btn.SetResourceReference(Border.BackgroundProperty, "PrimaryLightBrush");
            btn.SetResourceReference(Border.BorderBrushProperty, "PrimaryBrush");
            if (btn.Child is TextBlock tb)
                tb.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryBrush");
        }
        void Reset(Border btn)
        {
            btn.SetResourceReference(Border.BackgroundProperty, "CardBrush");
            btn.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            if (btn.Child is TextBlock tb)
                tb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }

        if (DensityCompactBtn != null) { if (selected == "Compact") Highlight(DensityCompactBtn); else Reset(DensityCompactBtn); }
        if (DensityDefaultBtn != null) { if (selected == "Default") Highlight(DensityDefaultBtn); else Reset(DensityDefaultBtn); }
        if (DensityComfortableBtn != null) { if (selected == "Comfortable") Highlight(DensityComfortableBtn); else Reset(DensityComfortableBtn); }
    }

    // ===== Dashboard 布局 =====
    private void DashStats_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton tb && VM != null)
        {
            e.Handled = true;
            var newVal = !(tb.IsChecked == true);
            tb.IsChecked = newVal;
            VM.ShowDashStats = newVal;
        }
    }

    private void DashReminder_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton tb && VM != null)
        {
            e.Handled = true;
            var newVal = !(tb.IsChecked == true);
            tb.IsChecked = newVal;
            VM.ShowDashReminder = newVal;
        }
    }

    private void DashProbation_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton tb && VM != null)
        {
            e.Handled = true;
            var newVal = !(tb.IsChecked == true);
            tb.IsChecked = newVal;
            VM.ShowDashProbation = newVal;
        }
    }

    private void DashCharts_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton tb && VM != null)
        {
            e.Handled = true;
            var newVal = !(tb.IsChecked == true);
            tb.IsChecked = newVal;
            VM.ShowDashCharts = newVal;
        }
    }

    private void DashHighlights_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton tb && VM != null)
        {
            e.Handled = true;
            var newVal = !(tb.IsChecked == true);
            tb.IsChecked = newVal;
            VM.ShowDashHighlights = newVal;
        }
    }

    private void DashRecent_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton tb && VM != null)
        {
            e.Handled = true;
            var newVal = !(tb.IsChecked == true);
            tb.IsChecked = newVal;
            VM.ShowDashRecent = newVal;
        }
    }

    // ===== 启动与行为 =====
    private void AutoStart_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton tb && VM != null)
        {
            e.Handled = true;
            var newVal = !(tb.IsChecked == true);
            tb.IsChecked = newVal;
            VM.AutoStart = newVal;
        }
    }

    private void MinimizeToTray_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton tb && VM != null)
        {
            e.Handled = true;
            var newVal = !(tb.IsChecked == true);
            tb.IsChecked = newVal;
            VM.MinimizeToTray = newVal;
        }
    }

    private void DefaultView_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is string view && VM != null)
        {
            VM.DefaultView = view;
        }
    }

    private void AutoSaveInterval_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is int interval && VM != null)
        {
            VM.AutoSaveInterval = interval;
        }
    }

    // ===== 通用设置 =====
    private void ShortcutToggle_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton tb && VM != null && tb.Tag is string tag)
        {
            e.Handled = true;
            var newVal = !(tb.IsChecked == true);
            tb.IsChecked = newVal;
            switch (tag)
            {
                case "Search": VM.ShortcutSearchEnabled = newVal; break;
                case "New": VM.ShortcutNewEnabled = newVal; break;
                case "Save": VM.ShortcutSaveEnabled = newVal; break;
                case "View1": VM.ShortcutView1Enabled = newVal; break;
                case "View2": VM.ShortcutView2Enabled = newVal; break;
                case "View3": VM.ShortcutView3Enabled = newVal; break;
                case "View4": VM.ShortcutView4Enabled = newVal; break;
                case "View5": VM.ShortcutView5Enabled = newVal; break;
            }
        }
    }

    private void EnableShortcuts_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton tb && VM != null)
        {
            e.Handled = true;
            var newVal = !(tb.IsChecked == true);
            tb.IsChecked = newVal;
            VM.EnableShortcuts = newVal;
        }
    }

    private void EnableReminder_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton tb && VM != null)
        {
            e.Handled = true;
            var newVal = !(tb.IsChecked == true);
            tb.IsChecked = newVal;
            VM.EnableReminder = newVal;
        }
    }
}
