using CommunityToolkit.Mvvm.ComponentModel;

namespace EapWorkAssistant.Models;

public partial class WorkRecord : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _workDate = DateTime.Now.ToString("yyyy-MM-dd");
    [ObservableProperty] private string _projectName = string.Empty;
    [ObservableProperty] private string _workType = string.Empty;
    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private string _achievement = string.Empty;
    [ObservableProperty] private string _problem = string.Empty;
    [ObservableProperty] private string _solution = string.Empty;
    [ObservableProperty] private double _hours;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private int _isHighlight;
    [ObservableProperty] private string _highlightNote = string.Empty;
    [ObservableProperty] private string _createTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
