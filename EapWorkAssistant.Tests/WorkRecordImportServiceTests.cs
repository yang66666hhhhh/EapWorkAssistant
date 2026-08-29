using EapWorkAssistant.Models;
using EapWorkAssistant.Services;
using Xunit;

namespace EapWorkAssistant.Tests;

/// <summary>
/// WorkRecordImportService（CSV 导入校验 / 配置一致性检查）的单元测试。
/// 这段逻辑原先埋在 WorkRecordViewModel 内部无法测试，抽出后在此覆盖。
/// </summary>
public class WorkRecordImportServiceTests
{
    private readonly WorkRecordImportService _service = new();

    private static WorkRecord MakeRecord(
        string date = "2026-08-03", string project = "项目A", string content = "内容",
        double hours = 8, int progress = 50, string workType = "开发") => new()
    {
        WorkDate = date,
        ProjectName = project,
        Content = content,
        Hours = hours,
        Progress = progress,
        WorkType = workType
    };

    // ===== Validate：正常路径 =====

    [Fact]
    public void Validate_ValidRecord_PassesThrough()
    {
        var result = _service.Validate(new[] { MakeRecord() });

        Assert.Single(result.Valid);
        Assert.Empty(result.SkippedReasons);
    }

    // ===== Validate：日期格式 =====

    [Fact]
    public void Validate_SlashDate_IsSkipped()
    {
        // Excel 另存后常见的 2026/8/3 斜杠格式，必须被拦截而非静默入库
        var result = _service.Validate(new[] { MakeRecord(date: "2026/8/3") });

        Assert.Empty(result.Valid);
        Assert.Single(result.SkippedReasons);
        Assert.Contains("日期格式错误", result.SkippedReasons[0]);
    }

    [Fact]
    public void Validate_SerialNumberDate_IsSkipped()
    {
        // Excel 日期序列号，同样应被拦截
        var result = _service.Validate(new[] { MakeRecord(date: "46247") });

        Assert.Empty(result.Valid);
        Assert.Single(result.SkippedReasons);
    }

    // ===== Validate：必填字段 =====

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingProject_IsSkipped(string? project)
    {
        var record = MakeRecord();
        record.ProjectName = project!;

        var result = _service.Validate(new[] { record });

        Assert.Empty(result.Valid);
        Assert.Contains("缺少任务或内容", result.SkippedReasons[0]);
    }

    [Fact]
    public void Validate_MissingContent_IsSkipped()
    {
        var result = _service.Validate(new[] { MakeRecord(content: "  ") });

        Assert.Empty(result.Valid);
        Assert.Single(result.SkippedReasons);
    }

    // ===== Validate：工时区间 =====

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(24.5)]
    public void Validate_OutOfRangeHours_IsSkipped(double hours)
    {
        var result = _service.Validate(new[] { MakeRecord(hours: hours) });

        Assert.Empty(result.Valid);
        Assert.Contains("工时", result.SkippedReasons[0]);
    }

    [Fact]
    public void Validate_BoundaryHours_AreAccepted()
    {
        // 边界值：极小正数与 24 都应通过
        var result = _service.Validate(new[]
        {
            MakeRecord(hours: 0.5),
            MakeRecord(hours: 24)
        });

        Assert.Equal(2, result.Valid.Count);
        Assert.Empty(result.SkippedReasons);
    }

    // ===== Validate：进度截断 =====

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(150, 100)]
    [InlineData(50, 50)]
    public void Validate_ProgressOutOfRange_IsClamped(int input, int expected)
    {
        var result = _service.Validate(new[] { MakeRecord(progress: input) });

        // 进度越界是截断而非丢弃整行
        Assert.Single(result.Valid);
        Assert.Equal(expected, result.Valid[0].Progress);
    }

    // ===== Validate：部分失败不影响其余记录 =====

    [Fact]
    public void Validate_MixedRecords_KeepsValidAndReportsSkips()
    {
        var records = new[]
        {
            MakeRecord(),                          // 通过
            MakeRecord(date: "2026/8/3"),          // 日期错
            MakeRecord(project: ""),               // 缺任务
            MakeRecord(hours: 99)                  // 工时错
        };

        var result = _service.Validate(records);

        Assert.Single(result.Valid);
        Assert.Equal(3, result.SkippedReasons.Count);
    }

    // ===== CheckConfig =====

    [Fact]
    public void CheckConfig_UnknownProjectAndType_AreReported()
    {
        var records = new[] { MakeRecord(project: "新项目", workType: "新类型") };

        var check = _service.CheckConfig(records, new[] { "项目A" }, new[] { "开发" });

        Assert.Contains("新项目", check.MissingProjects);
        Assert.Contains("新类型", check.MissingWorkTypes);
        Assert.Equal(2, check.Warnings.Count);
    }

    [Fact]
    public void CheckConfig_AllKnown_ProducesNoWarnings()
    {
        var records = new[] { MakeRecord(project: "项目A", workType: "开发") };

        var check = _service.CheckConfig(records, new[] { "项目A" }, new[] { "开发" });

        Assert.Empty(check.MissingProjects);
        Assert.Empty(check.MissingWorkTypes);
        Assert.Empty(check.Warnings);
    }

    [Fact]
    public void CheckConfig_ManyMissing_TruncatesPreviewToFive()
    {
        var records = Enumerable.Range(1, 8)
            .Select(i => MakeRecord(project: $"P{i}"))
            .ToArray();

        var check = _service.CheckConfig(records, System.Array.Empty<string>(), new[] { "开发" });

        Assert.Equal(8, check.MissingProjects.Count);
        Assert.Single(check.Warnings);
        // 超过 5 个时以省略号收尾
        Assert.EndsWith("…", check.Warnings[0]);
    }
}
