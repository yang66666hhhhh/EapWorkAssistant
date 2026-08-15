namespace EapWorkAssistant.Models;

/// <summary>回收站统一条目（跨工作记录 / 知识库 / 问题跟踪三种实体）。</summary>
public class RecycleItem
{
    /// <summary>实体类型键：WorkRecord / Knowledge / Issue</summary>
    public string EntityType { get; set; } = "";

    public int Id { get; set; }

    /// <summary>中文类型标签：工作记录 / 知识库 / 问题跟踪</summary>
    public string TypeLabel { get; set; } = "";

    public string Title { get; set; } = "";

    public string Detail { get; set; } = "";

    /// <summary>删除时间（用于排序和展示）</summary>
    public string? DeletedAt { get; set; }
}
