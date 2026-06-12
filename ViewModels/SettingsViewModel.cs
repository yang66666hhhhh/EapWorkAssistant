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

    public SettingsViewModel()
    {
        _ = RefreshAsync();
    }

    public Task RefreshAsync()
    {
        Projects = new ObservableCollection<string>(ConfigService.Instance.Projects);
        WorkTypes = new ObservableCollection<string>(ConfigService.Instance.WorkTypes);
        return Task.CompletedTask;
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
}
