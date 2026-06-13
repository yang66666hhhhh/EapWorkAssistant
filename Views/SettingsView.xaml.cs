using EapWorkAssistant.Services;
using EapWorkAssistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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
        }
    }

    // ===== 外观与主题 =====
    private void LightTheme_Click(object sender, MouseButtonEventArgs e)
    {
        if (VM != null) VM.IsLightTheme = true;
    }

    private void DarkTheme_Click(object sender, MouseButtonEventArgs e)
    {
        if (VM != null) VM.IsDarkTheme = true;
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
        var primaryBrush = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
        var primaryLightBrush = (System.Windows.Media.Brush)FindResource("PrimaryLightBrush");
        var borderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
        var cardBrush = (System.Windows.Media.Brush)FindResource("CardBrush");
        var textPrimaryBrush = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush");
        var textSecondaryBrush = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");

        void Highlight(Border btn)
        {
            btn.Background = primaryLightBrush;
            btn.BorderBrush = primaryBrush;
        }
        void Reset(Border btn)
        {
            btn.Background = cardBrush;
            btn.BorderBrush = borderBrush;
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
        var primaryBrush = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
        var primaryLightBrush = (System.Windows.Media.Brush)FindResource("PrimaryLightBrush");
        var borderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
        var cardBrush = (System.Windows.Media.Brush)FindResource("CardBrush");

        void Highlight(Border btn)
        {
            btn.Background = primaryLightBrush;
            btn.BorderBrush = primaryBrush;
        }
        void Reset(Border btn)
        {
            btn.Background = cardBrush;
            btn.BorderBrush = borderBrush;
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
            ConfigService.Instance.ShowDashStats = newVal;
            VM.StatusMessage = newVal ? "统计卡片已显示" : "统计卡片已隐藏";
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
            ConfigService.Instance.ShowDashReminder = newVal;
            VM.StatusMessage = newVal ? "今日提醒已显示" : "今日提醒已隐藏";
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
            ConfigService.Instance.ShowDashProbation = newVal;
            VM.StatusMessage = newVal ? "试用期进度已显示" : "试用期进度已隐藏";
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
            ConfigService.Instance.ShowDashCharts = newVal;
            VM.StatusMessage = newVal ? "图表区域已显示" : "图表区域已隐藏";
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
            ConfigService.Instance.ShowDashHighlights = newVal;
            VM.StatusMessage = newVal ? "工作亮点已显示" : "工作亮点已隐藏";
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
            ConfigService.Instance.ShowDashRecent = newVal;
            VM.StatusMessage = newVal ? "最近记录已显示" : "最近记录已隐藏";
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
            ConfigService.Instance.AutoStart = newVal;
            AutoStartService.ApplyAutoStart(newVal);
            VM.StatusMessage = newVal ? "开机自启动已启用" : "开机自启动已禁用";
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
            ConfigService.Instance.MinimizeToTray = newVal;
            VM.StatusMessage = newVal ? "最小化到托盘已启用" : "最小化到托盘已禁用";
        }
    }

    private void DefaultView_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is string view && VM != null)
        {
            VM.DefaultView = view;
            ConfigService.Instance.DefaultView = view;
            VM.StatusMessage = $"默认视图已设为 {VM.DefaultViewLabel}";
        }
    }

    private void AutoSaveInterval_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is int interval && VM != null)
        {
            VM.AutoSaveInterval = interval;
            ConfigService.Instance.AutoSaveInterval = interval;
            VM.StatusMessage = $"自动保存间隔已设为 {interval} 分钟";
        }
    }

    // ===== 通用设置 =====
    private void EnableShortcuts_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton tb && VM != null)
        {
            e.Handled = true;
            var newVal = !(tb.IsChecked == true);
            tb.IsChecked = newVal;
            VM.EnableShortcuts = newVal;
            ConfigService.Instance.EnableShortcuts = newVal;
            VM.StatusMessage = newVal ? "快捷键已启用" : "快捷键已禁用";
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
            ConfigService.Instance.EnableReminder = newVal;
            VM.StatusMessage = newVal ? "定时提醒已启用" : "定时提醒已禁用";
        }
    }
}
