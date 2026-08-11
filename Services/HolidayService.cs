using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EapWorkAssistant.Services;

/// <summary>
/// 中国法定节假日服务。从 holiday-cn 开源项目获取数据，按年缓存到本地。
/// 当网络不可用且无本地缓存时，使用内置的近期年份兜底数据（2024-2026），
/// 以保证调休余额等关键计算在离线场景下依然准确；若某年份三者皆缺失，
/// 则标记为"数据不可用"，由调用方提示用户，而非静默返回空集导致算错。
/// </summary>
public class HolidayService
{
    public static HolidayService Instance { get; } = new();

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant");

    private readonly HttpClient _httpClient;
    private readonly Dictionary<int, List<HolidayItem>> _cache = new();
    private readonly HashSet<int> _availableYears = new();

    // 内置兜底数据：覆盖近三年，离线时保证调休计算正确。
    // 数据来源：国务院办公厅每年发布的节假日安排通知。
    private static readonly Dictionary<int, List<HolidayItem>> Fallback = BuildFallback();

    private HolidayService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>
    /// 加载指定年份的节假日数据。优先级：本地缓存 → 在线 API → 内置兜底。
    /// 三者皆不可用时标记为"数据不可用"，由调用方提示用户。
    /// </summary>
    public async Task LoadYearAsync(int year)
    {
        if (_cache.ContainsKey(year)) return;

        var cacheFile = Path.Combine(CacheDir, $"holidays_{year}.json");

        // 1. 本地缓存
        if (File.Exists(cacheFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(cacheFile);
                var items = JsonSerializer.Deserialize<List<HolidayItem>>(json);
                if (items is { Count: > 0 })
                {
                    _cache[year] = items;
                    _availableYears.Add(year);
                    return;
                }
            }
            catch { /* 缓存损坏，重新拉取 */ }
        }

        // 2. 在线 API
        try
        {
            var url = $"https://raw.githubusercontent.com/NateScarlet/holiday-cn/master/{year}.json";
            var response = await _httpClient.GetFromJsonAsync<HolidayResponse>(url);
            if (response?.Days != null)
            {
                _cache[year] = response.Days;
                _availableYears.Add(year);

                Directory.CreateDirectory(CacheDir);
                var cacheJson = JsonSerializer.Serialize(response.Days);
                await File.WriteAllTextAsync(cacheFile, cacheJson);
                return;
            }
        }
        catch { /* 网络失败，使用兜底数据 */ }

        // 3. 内置兜底数据（离线时保证近期年份计算正确）
        if (Fallback.TryGetValue(year, out var fallback) && fallback.Count > 0)
        {
            _cache[year] = fallback;
            _availableYears.Add(year);
            // 持久化兜底数据，避免反复网络请求与重复告警
            try
            {
                Directory.CreateDirectory(CacheDir);
                await File.WriteAllTextAsync(cacheFile, JsonSerializer.Serialize(fallback));
            }
            catch { /* 忽略写入失败 */ }
            return;
        }

        // 4. 既无缓存、无网络、也无兜底 → 标记不可用（不写入缓存，便于联网后重新获取）
        _cache[year] = new List<HolidayItem>();
    }

    /// <summary>判断指定年份的节假日数据是否可用（本地缓存 / 在线获取 / 内置兜底 任一成功）</summary>
    public bool IsYearAvailable(int year) => _availableYears.Contains(year);

    /// <summary>判断指定日期是否为法定假日</summary>
    public bool IsHoliday(DateTime date)
    {
        if (!_cache.TryGetValue(date.Year, out var items)) return false;
        return items.Any(i => i.Date == date.Date && i.IsHoliday);
    }

    /// <summary>判断指定日期是否为补班日（调休上班）</summary>
    public bool IsMakeupWorkday(DateTime date)
    {
        if (!_cache.TryGetValue(date.Year, out var items)) return false;
        return items.Any(i => i.Date == date.Date && !i.IsHoliday);
    }

    /// <summary>获取假日名称（如"国庆节"），非假日返回 null</summary>
    public string? GetHolidayName(DateTime date)
    {
        if (!_cache.TryGetValue(date.Year, out var items)) return null;
        return items.FirstOrDefault(i => i.Date == date.Date && i.IsHoliday)?.Name;
    }

    /// <summary>获取指定月份的法定假日日期列表</summary>
    public List<DateTime> GetHolidaysForMonth(int year, int month)
    {
        if (!_cache.TryGetValue(year, out var items)) return new List<DateTime>();
        return items
            .Where(i => i.IsHoliday && i.Date.Year == year && i.Date.Month == month)
            .Select(i => i.Date)
            .ToList();
    }

    /// <summary>获取指定月份的补班日列表</summary>
    public List<DateTime> GetMakeupDaysForMonth(int year, int month)
    {
        if (!_cache.TryGetValue(year, out var items)) return new List<DateTime>();
        return items
            .Where(i => !i.IsHoliday && i.Date.Year == year && i.Date.Month == month)
            .Select(i => i.Date)
            .ToList();
    }

    // ── API 响应模型 ──

    private class HolidayResponse
    {
        [JsonPropertyName("days")]
        public List<HolidayItem> Days { get; set; } = new();
    }

    public class HolidayItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("holiday")]
        public bool IsHoliday { get; set; }
    }

    // ── 内置兜底数据构建 ──

    private static Dictionary<int, List<HolidayItem>> BuildFallback()
    {
        var dict = new Dictionary<int, List<HolidayItem>>();

        // 2024
        dict[2024] = Build(new[] {
            ("2024-01-01","2024-01-01","元旦"),
            ("2024-02-10","2024-02-17","春节"),
            ("2024-04-04","2024-04-06","清明节"),
            ("2024-05-01","2024-05-05","劳动节"),
            ("2024-06-10","2024-06-10","端午节"),
            ("2024-09-15","2024-09-17","中秋节"),
            ("2024-10-01","2024-10-07","国庆节"),
        }, new[] { "2024-02-04","2024-02-18","2024-04-07","2024-04-28","2024-05-11","2024-09-14","2024-09-29","2024-10-12" });

        // 2025
        dict[2025] = Build(new[] {
            ("2025-01-01","2025-01-01","元旦"),
            ("2025-01-28","2025-02-04","春节"),
            ("2025-04-04","2025-04-06","清明节"),
            ("2025-05-01","2025-05-05","劳动节"),
            ("2025-05-31","2025-06-02","端午节"),
            ("2025-10-01","2025-10-08","国庆节/中秋节"),
        }, new[] { "2025-01-26","2025-02-08","2025-04-27","2025-09-28","2025-10-11" });

        // 2026
        dict[2026] = Build(new[] {
            ("2026-01-01","2026-01-03","元旦"),
            ("2026-02-15","2026-02-23","春节"),
            ("2026-04-04","2026-04-06","清明节"),
            ("2026-05-01","2026-05-05","劳动节"),
            ("2026-06-19","2026-06-21","端午节"),
            ("2026-09-25","2026-09-27","中秋节"),
            ("2026-10-01","2026-10-07","国庆节"),
        }, new[] { "2026-01-04","2026-02-14","2026-02-28","2026-05-09","2026-09-20","2026-10-10" });

        return dict;
    }

    private static List<HolidayItem> Build((string Start, string End, string Name)[] ranges, string[] makeup)
    {
        var list = new List<HolidayItem>();
        foreach (var r in ranges)
        {
            var start = DateTime.Parse(r.Start, CultureInfo.InvariantCulture);
            var end = DateTime.Parse(r.End, CultureInfo.InvariantCulture);
            for (var d = start; d <= end; d = d.AddDays(1))
                list.Add(new HolidayItem { Name = r.Name, Date = d.Date, IsHoliday = true });
        }
        foreach (var m in makeup)
            list.Add(new HolidayItem { Name = "", Date = DateTime.Parse(m, CultureInfo.InvariantCulture).Date, IsHoliday = false });
        return list;
    }
}
