using EapWorkAssistant.Models;
using EapWorkAssistant.Repositories;
using EapWorkAssistant.Services;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EapWorkAssistant.Services;

public class ReportService
{
    private readonly WorkRecordRepository _repo = new();
    private readonly KnowledgeRepository _knowledgeRepo = new();
    private readonly IssueRepository _issueRepo = new();

    /// <summary>
    /// 将记录的 Content 字段（可能包含多行编号文本）拆分为独立的条目列表。
    /// 自动去除原始编号（如 "1、" "2." 等），返回纯文本条目。
    /// </summary>
    private static List<string> ParseContentItems(string content)
    {
        var items = new List<string>();
        if (string.IsNullOrWhiteSpace(content)) return items;

        var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            // 去除行首编号：支持 "1、" "1." "1．" "1)" "（1）" 等常见格式
            var cleaned = Regex.Replace(line.Trim(), @"^[\(（]?\d+[\)）、.．]\s*", "");
            if (!string.IsNullOrWhiteSpace(cleaned))
                items.Add(cleaned);
        }
        return items;
    }

    public async Task<string> GenerateDailyReportAsync(string date)
    {
        var records = await _repo.GetByDateAsync(date);
        if (!records.Any())
            return $"{date} 暂无工作记录";

        var sb = new StringBuilder();
        sb.AppendLine($"{date} 工作日报");
        sb.AppendLine();

        int index = 1;
        foreach (var r in records)
        {
            var items = ParseContentItems(r.Content);
            foreach (var item in items)
            {
                sb.AppendLine($"{index}. {item}");
                index++;
            }
        }

        var problems = records.Where(r => !string.IsNullOrWhiteSpace(r.Problem)).ToList();
        if (problems.Any())
        {
            sb.AppendLine();
            sb.AppendLine("遇到问题：");
            foreach (var p in problems)
                sb.AppendLine($"  - {p.Problem}");

            sb.AppendLine();
            sb.AppendLine("解决方案：");
            foreach (var p in problems)
                sb.AppendLine($"  - {p.Solution}");
        }

        return sb.ToString();
    }

    public async Task<string> GenerateWeeklyReportAsync(DateTime weekEnd)
    {
        var weekStart = weekEnd.AddDays(-(int)weekEnd.DayOfWeek + 1);
        if (weekEnd.DayOfWeek == DayOfWeek.Sunday)
            weekStart = weekEnd.AddDays(-6);

        var startDate = weekStart.ToString("yyyy-MM-dd");
        var endDate = weekEnd.ToString("yyyy-MM-dd");

        var records = await _repo.GetByDateRangeAsync(startDate, endDate);
        if (!records.Any())
            return $"{startDate} ~ {endDate} 暂无工作记录";

        var sb = new StringBuilder();
        sb.AppendLine($"{startDate} ~ {endDate} 周报");
        sb.AppendLine();

        // ===== 本周完成（按项目分组） =====
        sb.AppendLine("【本周完成】");
        var byProject = records.GroupBy(r => r.ProjectName).OrderByDescending(g => g.Sum(r => r.Hours));
        foreach (var group in byProject)
        {
            sb.AppendLine();
            sb.AppendLine($"■ {group.Key}");
            var allItems = new List<string>();
            foreach (var r in group.OrderBy(r => r.WorkDate))
                allItems.AddRange(ParseContentItems(r.Content));

            int idx = 1;
            foreach (var item in allItems)
            {
                sb.AppendLine($"  {idx}. {item}");
                idx++;
            }
        }

        // ===== 问题处理 =====
        var problems = records.Where(r => !string.IsNullOrWhiteSpace(r.Problem)).ToList();
        if (problems.Any())
        {
            sb.AppendLine();
            sb.AppendLine("【问题处理】");
            foreach (var p in problems)
                sb.AppendLine($"  - {p.Problem} → {p.Solution}");
        }

        // ===== 学习内容 =====
        var learning = records.Where(r => r.WorkType == "学习").ToList();
        if (learning.Any())
        {
            sb.AppendLine();
            sb.AppendLine("【学习内容】");
            int idx = 1;
            foreach (var l in learning)
            {
                var items = ParseContentItems(l.Content);
                foreach (var item in items)
                {
                    sb.AppendLine($"  {idx}. {item}");
                    idx++;
                }
            }
        }

        // ===== 工时统计 =====
        sb.AppendLine();
        sb.AppendLine("【工时统计】");
        var totalHours = records.Sum(r => r.Hours);
        sb.AppendLine($"  总工时：{totalHours:F1} 小时");
        var byType = records.GroupBy(r => r.WorkType);
        foreach (var group in byType)
        {
            var hours = group.Sum(r => r.Hours);
            var pct = totalHours > 0 ? (hours / totalHours * 100).ToString("F0") : "0";
            sb.AppendLine($"  {group.Key}：{hours:F1} 小时（{pct}%）");
        }

        return sb.ToString();
    }

    public async Task<string> GenerateMonthlyReportAsync(string yearMonth)
    {
        var records = await _repo.GetByMonthAsync(yearMonth);
        if (!records.Any())
            return $"{yearMonth} 暂无工作记录";

        var sb = new StringBuilder();
        sb.AppendLine($"{yearMonth} 月报");
        sb.AppendLine();

        // ===== 工时统计 =====
        var totalHours = records.Sum(r => r.Hours);
        sb.AppendLine("【工时统计】");
        sb.AppendLine($"  总工时：{totalHours:F1} 小时");
        sb.AppendLine();
        sb.AppendLine("  工时分布：");
        var byType = records.GroupBy(r => r.WorkType);
        foreach (var group in byType)
        {
            var hours = group.Sum(r => r.Hours);
            var pct = totalHours > 0 ? (hours / totalHours * 100).ToString("F0") : "0";
            sb.AppendLine($"    - {group.Key}：{hours:F1} 小时（{pct}%）");
        }

        sb.AppendLine();
        sb.AppendLine("  项目投入：");
        var byProjectHours = records.GroupBy(r => r.ProjectName)
            .OrderByDescending(g => g.Sum(r => r.Hours));
        foreach (var group in byProjectHours)
        {
            var hours = group.Sum(r => r.Hours);
            sb.AppendLine($"    - {group.Key}：{hours:F1} 小时");
        }

        // ===== 完成事项（按项目分组，展开多行内容） =====
        sb.AppendLine();
        sb.AppendLine("【完成事项】");
        var byProject = records.Where(r => r.Progress >= 80)
            .GroupBy(r => r.ProjectName)
            .OrderByDescending(g => g.Sum(r => r.Hours));
        foreach (var group in byProject)
        {
            sb.AppendLine();
            sb.AppendLine($"■ {group.Key}");
            var allItems = new List<string>();
            foreach (var r in group.OrderBy(r => r.WorkDate))
                allItems.AddRange(ParseContentItems(r.Content));

            int idx = 1;
            foreach (var item in allItems)
            {
                sb.AppendLine($"  {idx}. {item}");
                idx++;
            }
        }

        // ===== 问题处理 =====
        var problems = records.Where(r => !string.IsNullOrWhiteSpace(r.Problem)).ToList();
        if (problems.Any())
        {
            sb.AppendLine();
            sb.AppendLine("【问题处理】");
            sb.AppendLine($"  共处理 {problems.Count} 个问题");
            foreach (var p in problems)
                sb.AppendLine($"  - {p.Problem} → {p.Solution}");
        }

        // ===== 学习总结 =====
        var learning = records.Where(r => r.WorkType == "学习").ToList();
        if (learning.Any())
        {
            sb.AppendLine();
            sb.AppendLine("【学习内容】");
            int idx = 1;
            foreach (var l in learning.OrderBy(r => r.WorkDate))
            {
                var items = ParseContentItems(l.Content);
                foreach (var item in items)
                {
                    sb.AppendLine($"  {idx}. {item}");
                    idx++;
                }
            }
        }

        return sb.ToString();
    }

    public async Task<string> GenerateProbationReportAsync(string startDate, string endDate)
    {
        var records = (await _repo.GetByDateRangeAsync(startDate, endDate)).ToList();
        var knowledge = (await _knowledgeRepo.GetAllAsync()).ToList();
        var issues = (await _issueRepo.GetAllAsync()).ToList();

        if (!records.Any())
            return "试用期内暂无工作记录";

        var settings = ProbationSettings.Load();
        var start = DateTime.Parse(startDate);
        var end = DateTime.Parse(endDate);
        var daysPassed = (end - start).Days;

        var sb = new StringBuilder();

        // ===== 标题 =====
        sb.AppendLine("试用期工作总结");
        sb.AppendLine($"试用期：{startDate} ~ {endDate}（共 {daysPassed} 天）");
        sb.AppendLine();

        // ===== 一、数据统计 =====
        var totalHours = records.Sum(r => r.Hours);
        var totalTasks = records.Count();
        var recordedDays = records.Select(r => r.WorkDate).Distinct().Count();
        var avgHours = recordedDays > 0 ? totalHours / recordedDays : 0;
        var problemRecords = records.Where(r => !string.IsNullOrWhiteSpace(r.Problem)).ToList();

        sb.AppendLine("一、数据统计");
        sb.AppendLine($"  总工时：{totalHours:F1} 小时");
        sb.AppendLine($"  任务总数：{totalTasks} 项");
        sb.AppendLine($"  有记录天数：{recordedDays} 天");
        sb.AppendLine($"  日均工时：{avgHours:F1} 小时");
        sb.AppendLine($"  解决问题：{problemRecords.Count} 个");
        sb.AppendLine($"  知识积累：{knowledge.Count} 篇");

        // 工时分布
        sb.AppendLine();
        sb.AppendLine("  工时分布：");
        var byType = records.GroupBy(r => r.WorkType).OrderByDescending(g => g.Sum(r => r.Hours));
        foreach (var group in byType)
        {
            var hours = group.Sum(r => r.Hours);
            var pct = totalHours > 0 ? (hours / totalHours * 100).ToString("F0") : "0";
            sb.AppendLine($"    - {group.Key}：{hours:F1} 小时（{pct}%）");
        }
        sb.AppendLine();

        // ===== 二、项目参与概览 =====
        sb.AppendLine("二、项目参与概览");
        var byProject = records.GroupBy(r => r.ProjectName)
            .OrderByDescending(g => g.Sum(r => r.Hours));
        foreach (var p in byProject)
        {
            var hours = p.Sum(r => r.Hours);
            var count = p.Count();
            var pct = totalHours > 0 ? (hours / totalHours * 100).ToString("F0") : "0";
            sb.AppendLine($"  ■ {p.Key}：{count} 项任务，累计 {hours:F1} 小时（{pct}%）");
        }
        sb.AppendLine();

        // ===== 三、重点工作成果 =====
        sb.AppendLine("三、重点工作成果");
        var highlights = records.Where(r => r.IsHighlight == 1).ToList();
        if (highlights.Any())
        {
            sb.AppendLine($"  共标记 {highlights.Count} 项工作亮点：");
            sb.AppendLine();
            int idx = 1;
            foreach (var h in highlights.OrderByDescending(h => h.Hours).Take(15))
            {
                var items = ParseContentItems(
                    !string.IsNullOrWhiteSpace(h.HighlightNote) ? h.HighlightNote : h.Content);
                foreach (var item in items)
                {
                    sb.AppendLine($"  {idx}. 【{h.ProjectName}】{item}");
                    idx++;
                }
                if (!string.IsNullOrWhiteSpace(h.Achievement))
                    sb.AppendLine($"     → 成果：{h.Achievement}");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("  （未标记亮点，以下为工时最多的重点工作）");
            sb.AppendLine();
            int idx = 1;
            foreach (var r in records.OrderByDescending(r => r.Hours).Take(10))
            {
                var items = ParseContentItems(r.Content);
                foreach (var item in items)
                {
                    sb.AppendLine($"  {idx}. 【{r.ProjectName}】{item}");
                    idx++;
                }
                if (!string.IsNullOrWhiteSpace(r.Achievement))
                    sb.AppendLine($"     → 成果：{r.Achievement}");
                sb.AppendLine();
            }
        }

        // ===== 四、每周工作进展 =====
        sb.AppendLine("四、每周工作进展");
        var byWeek = records
            .Where(r => !string.IsNullOrWhiteSpace(r.WorkDate))
            .GroupBy(r =>
            {
                if (DateTime.TryParse(r.WorkDate, out var d))
                    return System.Globalization.CultureInfo.CurrentCulture.Calendar
                        .GetWeekOfYear(d, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
                return 0;
            })
            .Where(g => g.Key > 0)
            .OrderBy(g => g.Key);
        foreach (var week in byWeek)
        {
            var weekHours = week.Sum(r => r.Hours);
            var weekCount = week.Count();
            var weekDates = week.Select(r => r.WorkDate)
                .Where(d => !string.IsNullOrWhiteSpace(d) && DateTime.TryParse(d, out _))
                .Select(d => DateTime.Parse(d))
                .OrderBy(d => d)
                .ToList();

            var wStart = weekDates.Any() ? weekDates.First().ToString("MM/dd") : "?";
            var wEnd = weekDates.Any() ? weekDates.Last().ToString("MM/dd") : "?";
            sb.AppendLine($"  第{week.Key}周（{wStart}~{wEnd}）：{weekCount} 项任务，{weekHours:F1} 小时");

            var weekHighlights = week.Where(r => r.IsHighlight == 1).Take(3).ToList();
            foreach (var h in weekHighlights)
            {
                var note = !string.IsNullOrWhiteSpace(h.HighlightNote) ? h.HighlightNote : h.Content;
                var firstLine = ParseContentItems(note).FirstOrDefault() ?? note;
                sb.AppendLine($"    ★ {firstLine}");
            }
        }
        sb.AppendLine();

        // ===== 五、问题解决能力 =====
        sb.AppendLine("五、问题解决能力");
        sb.AppendLine($"  共处理 {problemRecords.Count} 个技术问题");
        sb.AppendLine();
        foreach (var p in problemRecords.Take(10))
        {
            sb.AppendLine($"  问题：{p.Problem}");
            sb.AppendLine($"  方案：{p.Solution}");
            sb.AppendLine();
        }

        if (issues.Any())
        {
            sb.AppendLine($"  问题跟踪库共记录 {issues.Count} 个设备问题");
            foreach (var issue in issues.Take(5))
            {
                sb.AppendLine($"  ■ [{issue.ProjectName}] {issue.Title}");
                if (!string.IsNullOrWhiteSpace(issue.RootCause))
                    sb.AppendLine($"    根因：{issue.RootCause}");
            }
            sb.AppendLine();
        }

        // ===== 六、知识沉淀 =====
        sb.AppendLine("六、知识沉淀");
        if (knowledge.Any())
        {
            sb.AppendLine($"  共积累 {knowledge.Count} 篇知识文章");
            var byTag = knowledge.Where(k => !string.IsNullOrWhiteSpace(k.Tags))
                .GroupBy(k => k.Tags.Split(',')[0].Trim());
            foreach (var tag in byTag.Take(5))
            {
                sb.AppendLine($"  ■ {tag.Key}：{tag.Count()} 篇");
            }
        }
        else
        {
            sb.AppendLine("  暂无知识库记录");
        }
        sb.AppendLine();

        // ===== 七、学习与成长 =====
        var learning = records.Where(r => r.WorkType == "学习").ToList();
        if (learning.Any())
        {
            sb.AppendLine("七、学习与成长");
            sb.AppendLine($"  试用期内共进行 {learning.Count} 次学习，累计 {learning.Sum(l => l.Hours):F1} 小时");
            sb.AppendLine();
            int idx = 1;
            foreach (var l in learning.OrderBy(r => r.WorkDate))
            {
                var items = ParseContentItems(l.Content);
                foreach (var item in items)
                {
                    sb.AppendLine($"  {idx}. {item}");
                    idx++;
                }
            }
            sb.AppendLine();
        }

        // ===== 结尾 =====
        sb.AppendLine("以上为试用期工作总结，恳请领导审阅。");

        return sb.ToString();
    }
}
