using EapWorkAssistant.Models;

namespace EapWorkAssistant.Services;

/// <summary>CSV 导入的校验结果。</summary>
public sealed class ImportValidationResult
{
    /// <summary>通过校验、可参与导入的记录。</summary>
    public List<WorkRecord> Valid { get; } = new();

    /// <summary>被跳过的原因（逐条），用于在确认弹窗中向用户说明。</summary>
    public List<string> SkippedReasons { get; } = new();
}

/// <summary>CSV 导入前的配置项一致性检查结果。</summary>
public sealed class ImportConfigCheck
{
    /// <summary>CSV 中存在、但配置里没有的项目名。</summary>
    public HashSet<string> MissingProjects { get; } = new(StringComparer.Ordinal);

    /// <summary>CSV 中存在、但配置里没有的工作类型。</summary>
    public HashSet<string> MissingWorkTypes { get; } = new(StringComparer.Ordinal);

    /// <summary>面向用户的提示文案（不阻断导入，仅告知将自动补齐）。</summary>
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// CSV 导入的纯业务逻辑：数据清洗校验 + 配置项一致性检查。
///
/// 从 <c>WorkRecordViewModel.ImportCsvAsync</c> 中抽出，原因：
///   ① 这段逻辑不依赖任何 WPF / ViewModel 状态，本就该独立；
///   ② 原先埋在 1300+ 行的 ViewModel 里无法单测，是导入缺陷的温床；
///   ③ 抽出后 ViewModel 只负责"弹窗 + 落库 + 刷新界面"的编排。
///
/// 设计上刻意<b>不引用 ConfigService / DialogService</b>：
/// 已知配置项由调用方以集合形式传入，因此本类是纯函数、可直接单元测试。
/// </summary>
public class WorkRecordImportService
{
    /// <summary>工时合法区间：必须为正且不超过 24 小时。</summary>
    public const double MinHours = 0;
    public const double MaxHours = 24;

    /// <summary>进度合法区间（超出则截断到边界，而不是丢弃整行）。</summary>
    public const int MinProgress = 0;
    public const int MaxProgress = 100;

    /// <summary>导入 CSV 要求的日期格式（强约束，非建议）。</summary>
    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// 清洗并校验解析出的记录：日期格式、必填字段、工时区间；进度越界则截断。
    /// 逐条独立判断，单条不合法只跳过该条并记原因，不影响其余记录。
    /// </summary>
    public ImportValidationResult Validate(IEnumerable<WorkRecord> records)
    {
        var result = new ImportValidationResult();

        foreach (var r in records)
        {
            // 日期格式校验
            if (!DateTime.TryParseExact(r.WorkDate, DateFormat,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _))
            {
                result.SkippedReasons.Add($"日期格式错误「{r.WorkDate}」，已跳过");
                continue;
            }

            // 必填字段
            if (string.IsNullOrWhiteSpace(r.ProjectName) || string.IsNullOrWhiteSpace(r.Content))
            {
                result.SkippedReasons.Add($"{r.WorkDate} 记录缺少任务或内容，已跳过");
                continue;
            }

            // 工时范围
            if (r.Hours <= MinHours || r.Hours > MaxHours)
            {
                result.SkippedReasons.Add($"{r.WorkDate}「{r.ProjectName}」工时 {r.Hours}h 不合理，已跳过");
                continue;
            }

            // 进度范围（越界截断而非丢弃）
            if (r.Progress < MinProgress || r.Progress > MaxProgress)
                r.Progress = Math.Clamp(r.Progress, MinProgress, MaxProgress);

            result.Valid.Add(r);
        }

        return result;
    }

    /// <summary>
    /// 检查 CSV 中的项目 / 工作类型是否已存在于配置中。
    /// 不存在的项目与类型会在导入时自动补齐，此处只负责"发现 + 生成提示文案"。
    /// </summary>
    /// <param name="valid">已通过校验的记录。</param>
    /// <param name="knownProjects">配置中已有的项目名。</param>
    /// <param name="knownWorkTypes">配置中已有的工作类型。</param>
    public ImportConfigCheck CheckConfig(IEnumerable<WorkRecord> valid,
        IReadOnlyCollection<string> knownProjects, IReadOnlyCollection<string> knownWorkTypes)
    {
        var check = new ImportConfigCheck();

        foreach (var r in valid)
        {
            if (!string.IsNullOrWhiteSpace(r.ProjectName) && !knownProjects.Contains(r.ProjectName))
                check.MissingProjects.Add(r.ProjectName);

            if (!string.IsNullOrWhiteSpace(r.WorkType) && !knownWorkTypes.Contains(r.WorkType))
                check.MissingWorkTypes.Add(r.WorkType);
        }

        if (check.MissingProjects.Count > 0)
            check.Warnings.Add(BuildWarning(check.MissingProjects.Count, "项目", check.MissingProjects));

        if (check.MissingWorkTypes.Count > 0)
            check.Warnings.Add(BuildWarning(check.MissingWorkTypes.Count, "类型", check.MissingWorkTypes));

        return check;
    }

    /// <summary>生成"N 个新 X 将自动加入配置：a、b、c…"提示，最多预览 5 个。</summary>
    private static string BuildWarning(int count, string noun, IEnumerable<string> items)
    {
        var preview = string.Join("、", items.Take(5));
        return $"{count} 个新{noun}将自动加入配置：{preview}{(count > 5 ? "…" : "")}";
    }
}
