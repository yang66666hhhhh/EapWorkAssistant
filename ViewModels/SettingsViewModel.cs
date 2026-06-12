using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EapWorkAssistant.Services;
using EapWorkAssistant.Views;
using System.Collections.ObjectModel;
using System.Windows;

namespace EapWorkAssistant.ViewModels;

public partial class SettingsViewModel : ObservableObject, IRefreshable
{
    [ObservableProperty]
    private ObservableCollection<string> _projects = new();

    [ObservableProperty]
    private ObservableCollection<string> _workTypes = new();

    [ObservableProperty]
    private string? _selectedProject;

    [ObservableProperty]
    private string? _selectedWorkType;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ContentTemplate> _contentTemplates = new();

    [ObservableProperty]
    private bool _enableShortcuts = true;

    [ObservableProperty]
    private bool _enableReminder = true;

    [ObservableProperty]
    private int _reminderHour = 17;

    [ObservableProperty]
    private int _reminderMinute = 30;

    public SettingsViewModel()
    {
        _ = RefreshAsync();
    }

    public Task RefreshAsync()
    {
        Projects = new ObservableCollection<string>(ConfigService.Instance.Projects);
        WorkTypes = new ObservableCollection<string>(ConfigService.Instance.WorkTypes);
        ContentTemplates = new ObservableCollection<ContentTemplate>(ConfigService.Instance.ContentTemplates);
        EnableShortcuts = ConfigService.Instance.EnableShortcuts;
        EnableReminder = ConfigService.Instance.EnableReminder;
        ReminderHour = ConfigService.Instance.ReminderHour;
        ReminderMinute = ConfigService.Instance.ReminderMinute;
        return Task.CompletedTask;
    }

    partial void OnEnableShortcutsChanged(bool value)
    {
        ConfigService.Instance.EnableShortcuts = value;
        StatusMessage = value ? "快捷键已启用" : "快捷键已禁用";
    }

    partial void OnEnableReminderChanged(bool value)
    {
        ConfigService.Instance.EnableReminder = value;
        StatusMessage = value ? "定时提醒已启用" : "定时提醒已禁用";
    }

    partial void OnReminderHourChanged(int value)
    {
        if (value >= 0 && value <= 23)
        {
            ConfigService.Instance.ReminderHour = value;
            StatusMessage = $"提醒时间已更新为 {value:D2}:{ReminderMinute:D2}";
        }
    }

    partial void OnReminderMinuteChanged(int value)
    {
        if (value >= 0 && value <= 59)
        {
            ConfigService.Instance.ReminderMinute = value;
            StatusMessage = $"提醒时间已更新为 {ReminderHour:D2}:{value:D2}";
        }
    }

    [RelayCommand]
    private void AddProject()
    {
        var dialog = new Views.ConfigItemDialog("添加任务", "");
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemValue))
        {
            ConfigService.Instance.AddProject(dialog.ItemValue.Trim());
            RefreshAsync();
            StatusMessage = "任务已添加";
        }
    }

    [RelayCommand]
    private void EditProject(string? project)
    {
        if (string.IsNullOrWhiteSpace(project)) return;
        var dialog = new Views.ConfigItemDialog("编辑任务", project);
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemValue))
        {
            ConfigService.Instance.UpdateProject(project, dialog.ItemValue.Trim());
            RefreshAsync();
            StatusMessage = "任务已更新";
        }
    }

    [RelayCommand]
    private void DeleteProject(string? project)
    {
        if (string.IsNullOrWhiteSpace(project)) return;
        if (!ConfirmDialog.Show($"确定要删除任务「{project}」吗？", "确认删除", ConfirmDialogType.Danger)) return;
        ConfigService.Instance.RemoveProject(project);
        RefreshAsync();
        StatusMessage = "任务已删除";
    }

    [RelayCommand]
    private void AddWorkType()
    {
        var dialog = new Views.ConfigItemDialog("添加工作类型", "");
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemValue))
        {
            ConfigService.Instance.AddWorkType(dialog.ItemValue.Trim());
            RefreshAsync();
            StatusMessage = "类型已添加";
        }
    }

    [RelayCommand]
    private void EditWorkType(string? workType)
    {
        if (string.IsNullOrWhiteSpace(workType)) return;
        var dialog = new Views.ConfigItemDialog("编辑工作类型", workType);
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemValue))
        {
            ConfigService.Instance.UpdateWorkType(workType, dialog.ItemValue.Trim());
            RefreshAsync();
            StatusMessage = "类型已更新";
        }
    }

    [RelayCommand]
    private void DeleteWorkType(string? workType)
    {
        if (string.IsNullOrWhiteSpace(workType)) return;
        if (!ConfirmDialog.Show($"确定要删除类型「{workType}」吗？", "确认删除", ConfirmDialogType.Danger)) return;
        ConfigService.Instance.RemoveWorkType(workType);
        RefreshAsync();
        StatusMessage = "类型已删除";
    }

    [RelayCommand]
    private void AddTemplate()
    {
        var dialog = new Views.ConfigItemDialog("添加模板名称", "");
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemValue))
        {
            var contentDialog = new Views.ConfigItemDialog("添加模板内容", "");
            contentDialog.Owner = Application.Current.MainWindow;
            if (contentDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(contentDialog.ItemValue))
            {
                ConfigService.Instance.AddContentTemplate(new ContentTemplate
                {
                    Name = dialog.ItemValue.Trim(),
                    Content = contentDialog.ItemValue.Trim()
                });
                RefreshAsync();
                StatusMessage = "模板已添加";
            }
        }
    }

    [RelayCommand]
    private void EditTemplate(ContentTemplate? template)
    {
        if (template == null) return;
        var dialog = new Views.ConfigItemDialog("编辑模板名称", template.Name);
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemValue))
        {
            var contentDialog = new Views.ConfigItemDialog("编辑模板内容", template.Content);
            contentDialog.Owner = Application.Current.MainWindow;
            if (contentDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(contentDialog.ItemValue))
            {
                ConfigService.Instance.UpdateContentTemplate(template.Name, new ContentTemplate
                {
                    Name = dialog.ItemValue.Trim(),
                    Content = contentDialog.ItemValue.Trim()
                });
                RefreshAsync();
                StatusMessage = "模板已更新";
            }
        }
    }

    [RelayCommand]
    private void DeleteTemplate(ContentTemplate? template)
    {
        if (template == null) return;
        if (!ConfirmDialog.Show($"确定要删除模板「{template.Name}」吗？", "确认删除", ConfirmDialogType.Danger)) return;
        ConfigService.Instance.RemoveContentTemplate(template.Name);
        RefreshAsync();
        StatusMessage = "模板已删除";
    }
}
