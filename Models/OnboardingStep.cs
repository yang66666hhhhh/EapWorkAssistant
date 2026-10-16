namespace EapWorkAssistant.Models;

/// <summary>
/// 首启引导的单步内容（纯数据，无交互逻辑；由 <see cref="ViewModels.MainViewModel.OnboardingSteps"/> 承载）。
/// </summary>
public class OnboardingStep
{
    /// <summary>步骤图标（emoji，与命令面板结果图标风格一致）。</summary>
    public string Icon { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
