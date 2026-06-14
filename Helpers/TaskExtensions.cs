namespace EapWorkAssistant.Helpers;

/// <summary>
/// Task 扩展方法：安全触发异步操作，异常时通过 Toast 反馈
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// 安全触发异步操作，捕获异常并通过 Toast 提示用户。
    /// 用法：SomeAsyncMethod().SafeFire("加载数据失败")
    /// </summary>
    public static async void SafeFire(this Task task, string errorMessage = "操作失败")
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Services.ToastService.Error($"{errorMessage}：{ex.Message}");
        }
    }
}
