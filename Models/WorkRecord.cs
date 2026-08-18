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
    [ObservableProperty] private int _isDeleted;
    [ObservableProperty] private string? _deletedAt;
    [ObservableProperty] private string _createTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    /// <summary>导入匹配键：基于 日期|项目|内容 的稳定哈希，用于「跳过重复/覆盖更新」精准识别同一条记录。</summary>
    [ObservableProperty] private string _uniqueId = string.Empty;
}
