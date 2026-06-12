using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EapWorkAssistant.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private int _selectedIndex;

    public DashboardViewModel Dashboard { get; } = new();
    public WorkRecordViewModel WorkRecord { get; } = new();
    public KnowledgeViewModel Knowledge { get; } = new();
    public IssueViewModel Issue { get; } = new();
    public SettingsViewModel Settings { get; } = new();

    public MainViewModel()
    {
        CurrentView = Dashboard;
        _ = Dashboard.LoadDashboardAsync();
    }

    [RelayCommand]
    private void NavigateTo(string viewName)
    {
        CurrentView = viewName switch
        {
            "Dashboard" => Dashboard,
            "WorkRecord" => WorkRecord,
            "Knowledge" => Knowledge,
            "Issue" => Issue,
            "Settings" => Settings,
            _ => Dashboard
        };

        if (CurrentView is IRefreshable refreshable)
            _ = refreshable.RefreshAsync();
    }

    partial void OnSelectedIndexChanged(int value)
    {
        var views = new[] { "Dashboard", "WorkRecord", "Knowledge", "Issue", "Settings" };
        if (value >= 0 && views.Length > value)
            NavigateTo(views[value]);
    }
}
