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
    /// <summary>
    /// 填报工时（报给公司/领导的口径）。工控现场常限制单日填报不超过 8 小时，
    /// 故通常就是「每天 8 小时」；调休余额也按本口径计算（公司只认它）。
    /// </summary>
    [ObservableProperty]
    private double _hours;

    /// <summary>
    /// 实际工时（自己真实投入的时长）。与 <see cref="Hours"/>（填报工时）并存：
    /// 填报口径受公司上限压制、无法反映真实投入，实际工时仅供自己查看。
    /// 0 表示未填写（**不回落**成填报工时，避免"没填却显示有值"的误导）。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActualHoursText))]
    [NotifyPropertyChangedFor(nameof(HasExplicitActualHours))]
    private double _actualHours;

    /// <summary>实际工时展示文本：未填写时显示「—」。</summary>
    public string ActualHoursText => ActualHours > 0 ? $"{ActualHours:F1}h" : "—";

    /// <summary>是否填写了实际工时（false 时列表里弱化显示为「—」）。</summary>
    public bool HasExplicitActualHours => ActualHours > 0;

    [ObservableProperty] private int _progress;
    [ObservableProperty] private int _isHighlight;
    [ObservableProperty] private string _highlightNote = string.Empty;
    [ObservableProperty] private int _isDeleted;
    [ObservableProperty] private string? _deletedAt;
    [ObservableProperty] private string _createTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    /// <summary>导入匹配键：基于 日期|项目|内容 的稳定哈希，用于「跳过重复/覆盖更新」精准识别同一条记录。</summary>
    [ObservableProperty] private string _uniqueId = string.Empty;
}
