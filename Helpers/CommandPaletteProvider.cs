namespace EapWorkAssistant.Helpers;

/// <summary>
/// 命令面板（Spotlight 搜索）静态条目源：页面导航 + 常用操作的**纯匹配逻辑**。
/// 与 <see cref="SearchNavigationHelper"/> 同定位——无 IO、无 WPF 依赖、可单测；
/// ViewModel 只负责把命中结果并入数据库搜索。
/// </summary>
/// <remarks>
/// 匹配规则：关键词按空白字符切分（支持空格分隔的多关键词），
/// **每个词都必须命中**（大小写不敏感的子串匹配）才算匹配该条目。
/// 命中范围 = 标题 + 别名 + 副标题（Content）。
/// </remarks>
public static class CommandPaletteProvider
{
    /// <summary>静态条目定义。</summary>
    public sealed class Entry
    {
        public required string Title { get; init; }
        /// <summary>副标题/说明（同时参与关键词匹配，如「热力图」「回收」）。</summary>
        public required string Content { get; init; }
        public required string Icon { get; init; }
        /// <summary>结果分类展示名：「页面」/「操作」。</summary>
        public required string Type { get; init; }
        /// <summary>点击后导航到的视图（<see cref="ViewNames"/> 常量）。</summary>
        public string NavigateTo { get; init; } = string.Empty;
        /// <summary>操作类条目：导航后再延迟发出「打开新增记录抽屉」信号（与 Ctrl+N 同链路）。</summary>
        public bool OpenNewForm { get; init; }
        /// <summary>别名/同义词（如「考勤」→ 出勤总览），同样参与匹配。</summary>
        public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    }

    /// <summary>全部静态条目。顺序即命令面板的展示顺序（操作置后）。</summary>
    public static IReadOnlyList<Entry> Entries { get; } = new[]
    {
        new Entry
        {
            Title = "工作台", Content = "仪表盘：统计卡 / 图表 / 异常洞察",
            Icon = "\U0001F4CA", Type = "页面", NavigateTo = ViewNames.Dashboard,
            Aliases = new[] { "仪表盘", "总览", "dashboard", "首页" }
        },
        new Entry
        {
            Title = "工作记录", Content = "填报 / 查看每日工作，Ctrl+N 快速新增",
            Icon = "\U0001F4DD", Type = "页面", NavigateTo = ViewNames.WorkRecord,
            Aliases = new[] { "记录", "工时", "日志", "填报" }
        },
        new Entry
        {
            Title = "出勤总览", Content = "月度热力图 / 请假 / 加班打卡",
            Icon = "\U0001F4C5", Type = "页面", NavigateTo = ViewNames.Attendance,
            Aliases = new[] { "出勤", "考勤", "热力图", "日历", "打卡" }
        },
        new Entry
        {
            Title = "知识库", Content = "沉淀技术知识与经验",
            Icon = "\U0001F4DA", Type = "页面", NavigateTo = ViewNames.Knowledge,
            Aliases = new[] { "知识", "文档", "wiki", "经验" }
        },
        new Entry
        {
            Title = "问题跟踪", Content = "问题记录 / 根因 / 解决方案",
            Icon = "\U0001F527", Type = "页面", NavigateTo = ViewNames.Issue,
            Aliases = new[] { "问题", "issue", "bug", "缺陷" }
        },
        new Entry
        {
            Title = "设置", Content = "主题 / 快捷键 / 启动行为 / AI 服务",
            Icon = "⚙\uFE0F", Type = "页面", NavigateTo = ViewNames.Settings,
            Aliases = new[] { "设置", "偏好", "选项", "配置", "settings" }
        },
        new Entry
        {
            Title = "回收站", Content = "恢复或彻底删除已删除内容",
            Icon = "\U0001F5D1\uFE0F", Type = "页面", NavigateTo = ViewNames.RecycleBin,
            Aliases = new[] { "回收", "恢复", "删除", "还原" }
        },
        new Entry
        {
            Title = "新增工作记录", Content = "Ctrl+N 打开记录抽屉并聚焦输入框",
            Icon = "➕", Type = "操作", NavigateTo = ViewNames.WorkRecord, OpenNewForm = true,
            Aliases = new[] { "新增", "新建", "添加", "记一笔" }
        },
    };

    /// <summary>
    /// 按关键词匹配静态条目。空白关键词返回空列表（命令面板此时展示初始态，不铺满静态条目）。
    /// </summary>
    public static IReadOnlyList<Entry> Match(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return Array.Empty<Entry>();

        var tokens = keyword.Trim()
            .Split(new[] { ' ', '\t', '\u3000' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return Array.Empty<Entry>();

        var result = new List<Entry>();
        foreach (var e in Entries)
        {
            var haystack = $"{e.Title} {string.Join(' ', e.Aliases)} {e.Content}".ToLowerInvariant();
            var allHit = true;
            foreach (var t in tokens)
            {
                if (!haystack.Contains(t.Trim().ToLowerInvariant(), StringComparison.Ordinal))
                {
                    allHit = false;
                    break;
                }
            }
            if (allHit) result.Add(e);
        }
        return result;
    }
}
