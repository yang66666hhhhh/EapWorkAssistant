namespace EapWorkAssistant.Models;

/// <summary>
/// 洞察严重度。数值越大越紧急，卡片排序与配色据此决定。
/// </summary>
public enum InsightSeverity
{
    /// <summary>提示（蓝）：中性提醒，不影响判断。</summary>
    Info = 0,

    /// <summary>注意（琥珀）：建议尽快处理。</summary>
    Warning = 1,

    /// <summary>风险（红）：可能造成实际损失，如调休透支、长时间漏填。</summary>
    Danger = 2
}

/// <summary>
/// 主动异常洞察条目（P2 ⑨）。
///
/// 由 <see cref="Services.InsightAnalyzer"/> 纯函数产出，仪表盘只负责渲染。
/// 配色**不写死在模型里**：View 按 <see cref="Severity"/> 用 DataTrigger 映射到
/// DesignTokens 的 Danger / Warning / Info 色板（Model 与 ViewModel 均不引用 WPF，红线 6）。
/// </summary>
public class InsightItem
{
    /// <summary>分类标识（取值见 <see cref="Services.InsightAnalyzer"/> 的 Kind* 常量），供测试与后续埋点区分。</summary>
    public required string Kind { get; init; }

    public InsightSeverity Severity { get; init; }

    /// <summary>一句话结论（卡片主行）。</summary>
    public required string Title { get; init; }

    /// <summary>补充说明 / 处理建议（卡片次行，可为空）。</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>操作按钮文案；为空表示该条不提供跳转。</summary>
    public string? ActionText { get; init; }

    /// <summary>操作跳转目标（<see cref="Helpers.ViewNames"/> 取值）；为空表示无跳转。</summary>
    public string? ActionTarget { get; init; }

    /// <summary>
    /// 需要定位到的具体日期（可空）。设置时点击操作会**直接跳到该日**（工作记录页选中当天），
    /// 让用户不必自己翻找；多条日期聚合的洞察取**最早那天**（与 Detail 中日期列表的先后一致）。
    /// </summary>
    public DateTime? ActionDate { get; init; }

    /// <summary>是否提供跳转操作（供 View 控制按钮可见性）。</summary>
    public bool HasAction => !string.IsNullOrEmpty(ActionText) && !string.IsNullOrEmpty(ActionTarget);

    /// <summary>是否可定位到具体日期（供测试与调试区分「按日定位」与「仅切页」）。</summary>
    public bool HasActionDate => ActionDate.HasValue;
}
