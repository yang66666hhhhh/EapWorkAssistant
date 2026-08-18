using System.Security.Cryptography;
using System.Text;
using EapWorkAssistant.Models;

namespace EapWorkAssistant.Helpers;

/// <summary>CSV 导入模式</summary>
public enum ImportMode
{
    /// <summary>跳过重复（默认）：库中已存在则保留不动，只新增全新记录</summary>
    SkipDuplicate,
    /// <summary>覆盖更新：以 CSV 为准，全量替换匹配到的记录（保留 Id 与 CreateTime）</summary>
    Overwrite,
    /// <summary>全部新增：忽略重复，强制追加为新条目</summary>
    Append
}

/// <summary>
/// 工作记录导入身份与匹配计划辅助。
/// UniqueId 基于不可变业务字段（日期|项目|内容）生成，同一内容无论何时导出都稳定一致，
/// 用于导入时精准匹配「同一条记录」，支撑 跳过/覆盖/追加 三种模式。
/// </summary>
public static class WorkRecordIdentityHelper
{
    /// <summary>基于不可变业务字段生成稳定唯一标识（16 位 URL 安全 Base64 缩写）。</summary>
    public static string GenerateUniqueId(WorkRecord r)
    {
        var raw = $"{r.WorkDate.Trim()}|{r.ProjectName.Trim()}|{r.Content.Trim()}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(bytes).Replace("/", "_").Replace("+", "-").Substring(0, 16);
    }

    /// <summary>
    /// 纯计数：在给定模式下，对一组已校验记录预计的 (新增, 覆盖, 跳过) 数量。
    /// 不修改入参，便于在确认弹窗中预览三种模式的结果。
    /// </summary>
    public static (int Insert, int Update, int Skip) CountPlan(
        IReadOnlyList<WorkRecord> records, IReadOnlyDictionary<string, int> map, ImportMode mode)
    {
        int ins = 0, upd = 0, sk = 0;
        foreach (var r in records)
        {
            var natural = string.IsNullOrWhiteSpace(r.UniqueId)
                ? GenerateUniqueId(r)
                : r.UniqueId.Trim();
            var exists = map.ContainsKey(natural);
            switch (mode)
            {
                case ImportMode.SkipDuplicate:
                    if (exists) sk++; else ins++;
                    break;
                case ImportMode.Overwrite:
                    if (exists) upd++; else ins++;
                    break;
                case ImportMode.Append:
                    ins++;
                    break;
            }
        }
        return (ins, upd, sk);
    }
}
