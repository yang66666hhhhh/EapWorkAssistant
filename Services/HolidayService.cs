using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace EapWorkAssistant.Services;

/// <summary>
/// 中国法定节假日服务。从 holiday-cn 开源项目获取数据，按年缓存到本地。
/// </summary>
public class HolidayService
{
    public static HolidayService Instance { get; } = new();

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant");

    private readonly HttpClient _httpClient;
    private readonly Dictionary<int, List<HolidayItem>> _cache = new();

    private HolidayService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>
    /// 加载指定年份的节假日数据。优先读本地缓存，缓存不存在或过期时从 API 拉取。
    /// </summary>
    public async Task LoadYearAsync(int year)
    {
        if (_cache.ContainsKey(year)) return;

        var cacheFile = Path.Combine(CacheDir, $"holidays_{year}.json");

        // 尝试读取本地缓存
        if (File.Exists(cacheFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(cacheFile);
                var items = System.Text.Json.JsonSerializer.Deserialize<List<HolidayItem>>(json);
                if (items is { Count: > 0 })
                {
                    _cache[year] = items;
                    return;
                }
            }
            catch { /* 缓存损坏，重新拉取 */ }
        }

        // 从 API 拉取
        try
        {
            var url = $"https://raw.githubusercontent.com/NateScarlet/holiday-cn/master/{year}.json";
            var response = await _httpClient.GetFromJsonAsync<HolidayResponse>(url);
            if (response?.Days != null)
            {
                _cache[year] = response.Days;

                // 写入本地缓存
                Directory.CreateDirectory(CacheDir);
                var cacheJson = System.Text.Json.JsonSerializer.Serialize(response.Days);
                await File.WriteAllTextAsync(cacheFile, cacheJson);
            }
        }
        catch
        {
            // 网络失败时静默降级 — 不显示假日标记
            _cache[year] = new List<HolidayItem>();
        }
    }

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
}
