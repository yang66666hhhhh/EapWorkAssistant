using EapWorkAssistant.Repositories;

namespace EapWorkAssistant.Services;

/// <summary>调休余额计算结果。</summary>
public sealed record CompLeaveBalance(
    /// <summary>年度累计加班工时（周末 + 法定假日）。</summary>
    double OvertimeHours,
    /// <summary>年度已使用调休工时。</summary>
    double CompLeaveUsed,
    /// <summary>可用调休余额（加班工时 - 已调休）。</summary>
    double CompLeaveAvailable);

/// <summary>
/// 调休余额计算：汇总全年工作记录工时、全年"调休"类请假工时，
/// 再交给 <see cref="CompLeaveCalculator"/> 结合节假日/补班日历算出余额。
///
/// 从 <c>WorkRecordViewModel.LoadCompLeaveBalanceAsync</c> 中抽出，
/// 让 ViewModel 只负责「取结果 → 赋值 → 出错弹 Toast」。
///
/// 说明：本服务会自行确保指定年份的节假日数据已加载（<c>LoadYearAsync</c> 幂等），
/// 调用方无需重复加载；数据是否可用通过 <see cref="IsHolidayDataAvailable"/> 查询，
/// 由调用方决定如何提示用户（保持本类不含 UI 依赖）。
/// </summary>
public class CompLeaveBalanceService
{
    /// <summary>计入"已使用调休"的请假类型。</summary>
    public const string CompLeaveTypeName = "调休";

    private readonly WorkRecordRepository _recordRepo;
    private readonly LeaveRecordRepository _leaveRepo;

    public CompLeaveBalanceService(WorkRecordRepository recordRepo, LeaveRecordRepository leaveRepo)
    {
        _recordRepo = recordRepo;
        _leaveRepo = leaveRepo;
    }

    /// <summary>指定年份的节假日数据是否已可用（未联网获取成功时为 false）。</summary>
    public bool IsHolidayDataAvailable(int year) => HolidayService.Instance.IsYearAvailable(year);

    /// <summary>计算指定年份的调休余额。</summary>
    public async Task<CompLeaveBalance> CalculateAsync(int year)
    {
        var yearStart = $"{year:D4}-01-01";
        var yearEnd = $"{year:D4}-12-31";

        // 确保假日数据已加载（幂等，重复调用不会重复拉取）
        await HolidayService.Instance.LoadYearAsync(year);

        // 1. 全年工作记录 → (日期, 工时) 序列，跳过日期无法解析的脏数据
        var allRecords = await _recordRepo.GetByDateRangeAsync(yearStart, yearEnd);
        var workTuples = new List<(DateTime Date, double Hours)>();
        foreach (var r in allRecords)
        {
            if (DateTime.TryParse(r.WorkDate, out var date))
                workTuples.Add((date, r.Hours));
        }

        // 2. 全年"调休"类请假工时
        var compHours = (await _leaveRepo.GetByYearAsync(year))
            .Where(l => l.LeaveType == CompLeaveTypeName)
            .Select(l => l.Hours);

        // 3. 计算余额（节假日/补班判定走 HolidayService）
        var (overtimeHours, compUsed, available) = CompLeaveCalculator.Compute(
            workTuples,
            compHours,
            HolidayService.Instance.IsHoliday,
            HolidayService.Instance.IsMakeupWorkday);

        return new CompLeaveBalance(overtimeHours, compUsed, available);
    }
}
