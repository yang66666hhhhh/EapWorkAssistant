using EapWorkAssistant.Repositories;
using EapWorkAssistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace EapWorkAssistant.Services;

/// <summary>
/// 应用组合根（Composition Root）：集中登记 Repository / Service / ViewModel 的创建方式。
///
/// 背景：原先各 ViewModel 内直接 <c>new WorkRecordRepository()</c>，并跨模块依赖
/// <c>ConfigService.Instance</c> 等静态单例，导致：
///   ① 无法替换实现做单元测试；② 依赖关系散落各处、隐式且不可见；
///   ③ 仓储无法共享生命周期（连接/事务无法复用）。
/// 改为容器统一供给后，所有依赖关系集中在本文件，可一处替换、一处审查。
///
/// 用法：
///   - 生产代码：<c>ServiceContainer.Get&lt;T&gt;()</c>
///   - 单元测试：<c>ServiceContainer.UseProvider(fakeProvider)</c> 注入 Fake 实现，
///     测完调用 <c>ServiceContainer.Reset()</c> 还原。
/// </summary>
public static class ServiceContainer
{
    private static IServiceProvider? _provider;
    private static readonly object Lock = new();

    public static IServiceProvider Provider
    {
        get
        {
            lock (Lock)
            {
                return _provider ??= BuildProvider();
            }
        }
    }

    /// <summary>解析必需服务；未注册时抛出明确异常，避免返回 null 造成后续空引用。</summary>
    public static T Get<T>() where T : class => Provider.GetRequiredService<T>();

    /// <summary>解析可选服务（未注册返回 null）。</summary>
    public static T? GetOptional<T>() where T : class => Provider.GetService<T>();

    /// <summary>供单元测试替换为自定义容器（注册 Fake / Stub 实现）。</summary>
    public static void UseProvider(IServiceProvider provider)
    {
        lock (Lock)
        {
            _provider = provider;
        }
    }

    /// <summary>还原为默认生产容器（测试清理用）。</summary>
    public static void Reset()
    {
        lock (Lock)
        {
            _provider = null;
        }
    }

    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        RegisterRepositories(services);
        RegisterServices(services);
        RegisterViewModels(services);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 仓储：无状态（每次调用自建连接），注册为单例避免重复分配。
    /// </summary>
    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddSingleton<WorkRecordRepository>();
        services.AddSingleton<KnowledgeRepository>();
        services.AddSingleton<IssueRepository>();
        services.AddSingleton<LeaveRecordRepository>();
    }

    /// <summary>
    /// 服务：优先按抽象（接口）注册，实例型单例直接登记既有实例，
    /// 保持与改造前 <c>XxxService.Instance</c> 完全相同的对象身份，避免行为变化。
    /// </summary>
    private static void RegisterServices(IServiceCollection services)
    {
        // 对话框：ViewModel 依赖接口，便于测试时替换为不弹窗的 Fake
        services.AddSingleton<IDialogService>(DialogService.Instance);
        services.AddSingleton(DialogService.Instance);

        services.AddSingleton(ConfigService.Instance);
        services.AddSingleton(ThemeService.Instance);
        services.AddSingleton(ToastService.Instance);

        // 业务逻辑服务（依赖仓储，可单元测试注入 Fake）
        services.AddSingleton<WorkRecordImportService>();
        services.AddSingleton<CompLeaveBalanceService>();

        // 有状态业务服务
        // HolidayService 构造函数为 private，只能登记既有单例实例（按类型注册会在解析时才炸）
        services.AddSingleton(HolidayService.Instance);
        services.AddSingleton<ReportService>();
        services.AddSingleton<DocxReportService>();
        services.AddSingleton<AiService>();
        services.AddSingleton<ProfileService>();
    }

    /// <summary>
    /// 子 ViewModel：由 MainViewModel 长期持有，注册为单例。
    ///
    /// 注意：<b>MainViewModel 不在此注册</b> —— 它的生命周期由 XAML
    /// （MainWindow.xaml 中的 <c>&lt;vm:MainViewModel/&gt;</c>）拥有。
    /// 若在此重复注册，XAML 创建的实例与容器内的实例会是两个不同对象，
    /// 造成导航状态分裂。子 ViewModel 则由 MainViewModel 从容器解析。
    /// </summary>
    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<WorkRecordViewModel>();
        services.AddSingleton<KnowledgeViewModel>();
        services.AddSingleton<IssueViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<RecycleBinViewModel>();
    }
}
