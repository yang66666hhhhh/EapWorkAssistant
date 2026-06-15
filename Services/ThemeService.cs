using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace EapWorkAssistant.Services;

/// <summary>
/// 主题服务：管理应用主题切换（亮/暗色、强调色、字体大小、界面密度）
/// </summary>
public class ThemeService : INotifyPropertyChanged
{
    public static ThemeService Instance { get; } = new ThemeService();
    public event PropertyChangedEventHandler? PropertyChanged;

    // ===== 强调色色板定义 =====
    // Primary, PrimaryHover, PrimaryDark, PrimaryLight, PrimarySoft
    private static readonly Dictionary<string, AccentPalette> AccentPalettes = new()
    {
        ["Indigo"]  = new AccentPalette("#6366F1", "#4F46E5", "#3730A3", "#EEF2FF", "#F5F3FF"),
        ["Violet"]  = new AccentPalette("#8B5CF6", "#7C3AED", "#5B21B6", "#F5F3FF", "#FAF5FF"),
        ["Blue"]    = new AccentPalette("#3B82F6", "#2563EB", "#1E40AF", "#EFF6FF", "#DBEAFE"),
        ["Emerald"] = new AccentPalette("#10B981", "#059669", "#065F46", "#ECFDF5", "#D1FAE5"),
        ["Rose"]    = new AccentPalette("#F43F5E", "#E11D48", "#9F1239", "#FFF1F2", "#FFE4E6"),
        ["Amber"]   = new AccentPalette("#F59E0B", "#D97706", "#92400E", "#FFFBEB", "#FEF3C7"),
    };

    // ===== 亮色模式 — "Crystal Clean" 清透极简 =====
    // 冷灰基底 + 纯白卡片 + 柔和阴影层级
    private static readonly ThemeColors LightColors = new()
    {
        Surface       = "#F1F5F9",   // slate-100 微冷灰蓝底色，有空间感
        SurfaceHover  = "#E2E8F0",   // slate-200 悬停态略深
        SurfaceAlt    = "#F8FAFC",   // slate-50  更浅的替代面（输入区域）
        CardColor     = "#FFFFFF",   // 纯白卡片，与底色形成清晰层次
        TextPrimary   = "#0F172A",   // slate-900 深邃主文字
        TextSecondary = "#475569",   // slate-600 次要文字
        TextTertiary  = "#94A3B8",   // slate-400 辅助文字
        Border        = "#E2E8F0",   // slate-200 柔和边框
        BorderHover   = "#CBD5E1",   // slate-300 悬停边框
        SidebarBg     = "#F0F4F8",   // 浅灰蓝侧边栏，与主区域形成反差
        SidebarText   = "#64748B",   // slate-500 侧边栏次要文字
        SidebarHover  = "#E2E8F0",   // slate-200 悬停态
        SidebarCardBg = "#DDE5EE",   // 个人资料卡略深，有区分感
        ScrollThumb       = "#CBD5E1",
        ScrollThumbHover  = "#94A3B8",
        ScrollThumbActive = "#64748B",
        Success       = "#10B981", SuccessLight = "#D1FAE5",
        Warning       = "#F59E0B", WarningLight = "#FEF3C7",
        Danger        = "#EF4444", DangerLight = "#FEE2E2",
    };

    // ===== 暗色模式 — "Midnight Velvet" 午夜丝绒 =====
    // 深靛蓝基底 + 三层灰阶 + 微光边框
    private static readonly ThemeColors DarkColors = new()
    {
        Surface       = "#151D2E",   // 深邃底色，略带靛蓝调（提亮）
        SurfaceHover  = "#243044",   // 悬停态（提亮）
        SurfaceAlt    = "#1C2739",   // 比 Surface 稍亮，输入区域
        CardColor     = "#212E42",   // 卡片面，比底色亮一级，有浮起感
        TextPrimary   = "#F1F5F9",   // slate-100 高对比主文字
        TextSecondary = "#94A3B8",   // slate-400 次要文字
        TextTertiary  = "#64748B",   // slate-500 辅助文字（提亮，原 slate-600 太暗）
        Border        = "#2A3A52",   // 微光边框（提亮，增强分隔感）
        BorderHover   = "#3B5476",   // 悬停时边框变亮
        SidebarBg     = "#0F1726",   // 比主区域更深的侧边栏（提亮）
        SidebarText   = "#7B8BA3",   // 侧边栏文字（提亮，增强可读性）
        SidebarHover  = "#1A2638",   // 悬停态，比底色略亮
        SidebarCardBg = "#182438",   // 个人资料卡
        ScrollThumb       = "#3B5476",
        ScrollThumbHover  = "#4E6F96",
        ScrollThumbActive = "#6A8FBA",
        Success       = "#34D399", SuccessLight = "#064E3B",
        Warning       = "#FBBF24", WarningLight = "#451A03",
        Danger        = "#F87171", DangerLight = "#450A0A",
    };

    // ===== 字体大小级别 =====
    private static readonly Dictionary<string, FontSizeConfig> FontSizes = new()
    {
        ["Small"]  = new FontSizeConfig(11.5, 11, 14, 12, 10),
        ["Medium"] = new FontSizeConfig(13, 12, 16, 13, 11),
        ["Large"]  = new FontSizeConfig(14.5, 13, 18, 14, 12),
    };

    // ===== 字体大小对应的全局缩放因子 =====
    private static readonly Dictionary<string, double> FontScaleFactors = new()
    {
        ["Small"]  = 0.93,
        ["Medium"] = 1.0,
        ["Large"]  = 1.07,
    };

    // ===== 界面密度对应的间距值 =====
    private static readonly Dictionary<string, DensityConfig> DensityValues = new()
    {
        ["Compact"]     = new DensityConfig(new(16), new(18,10,18,10), new(18,8,18,8),  new(14,8,14,8),  new(0,0,0,10), new(0,0,0,4), new(0,0,0,6)),
        ["Default"]     = new DensityConfig(new(22), new(18,14,18,14), new(18,12,18,12), new(14,12,14,12), new(0,0,0,16), new(0,0,0,6), new(0,0,0,10)),
        ["Comfortable"] = new DensityConfig(new(28), new(18,18,18,18), new(18,16,18,16), new(14,16,14,16), new(0,0,0,22), new(0,0,0,8), new(0,0,0,14)),
    };

    // ===== 当前状态 =====
    private string _themeMode = "Light";
    private string _accentColor = "Indigo";
    private string _fontSizeLevel = "Medium";
    private string _uiDensity = "Default";

    public string ThemeMode => _themeMode;
    public string AccentColor => _accentColor;
    public string FontSizeLevel => _fontSizeLevel;
    public string UIDensity => _uiDensity;
    public bool IsDarkMode => _themeMode == "Dark";

    /// <summary>
    /// 初始化主题（从配置加载）
    /// </summary>
    public void Initialize()
    {
        var cfg = ConfigService.Instance;
        _themeMode = cfg.ThemeMode;
        _accentColor = cfg.AccentColor;
        _fontSizeLevel = cfg.FontSizeLevel;
        _uiDensity = cfg.UIDensity;
        ApplyAll();
    }

    /// <summary>
    /// 切换亮/暗色模式
    /// </summary>
    public void SetThemeMode(string mode)
    {
        if (_themeMode == mode) return;
        _themeMode = mode;
        ConfigService.Instance.ThemeMode = mode;
        ApplyAll();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThemeMode)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDarkMode)));
    }

    /// <summary>
    /// 切换强调色
    /// </summary>
    public void SetAccentColor(string color)
    {
        if (_accentColor == color || !AccentPalettes.ContainsKey(color)) return;
        _accentColor = color;
        ConfigService.Instance.AccentColor = color;
        ApplyAccentColor();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccentColor)));
    }

    /// <summary>
    /// 切换字体大小
    /// </summary>
    public void SetFontSizeLevel(string level)
    {
        if (_fontSizeLevel == level || !FontSizes.ContainsKey(level)) return;
        _fontSizeLevel = level;
        ConfigService.Instance.FontSizeLevel = level;
        ApplyFontSize();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontSizeLevel)));
    }

    /// <summary>
    /// 切换界面密度
    /// </summary>
    public void SetUIDensity(string density)
    {
        if (_uiDensity == density) return;
        _uiDensity = density;
        ConfigService.Instance.UIDensity = density;
        ApplyDensity();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UIDensity)));
    }

    // ===== 应用主题 =====

    private void ApplyAll()
    {
        ApplyThemeColors();
        ApplyAccentColor();
        ApplyFontSize();
        ApplyDensity();
    }

    private void ApplyThemeColors()
    {
        var colors = IsDarkMode ? DarkColors : LightColors;
        var res = Application.Current.Resources;

        SetColor(res, "Surface", colors.Surface);
        SetColor(res, "SurfaceHover", colors.SurfaceHover);
        SetColor(res, "SurfaceAlt", colors.SurfaceAlt);
        SetColor(res, "CardColor", colors.CardColor);
        SetColor(res, "TextPrimary", colors.TextPrimary);
        SetColor(res, "TextSecondary", colors.TextSecondary);
        SetColor(res, "TextTertiary", colors.TextTertiary);
        SetColor(res, "Border", colors.Border);
        SetColor(res, "BorderHover", colors.BorderHover);
        SetColor(res, "SidebarBg", colors.SidebarBg);
        SetColor(res, "SidebarText", colors.SidebarText);
        SetColor(res, "SidebarHover", colors.SidebarHover);
        SetColor(res, "SidebarCardBg", colors.SidebarCardBg);
        SetColor(res, "ScrollThumb", colors.ScrollThumb);
        SetColor(res, "ScrollThumbHover", colors.ScrollThumbHover);
        SetColor(res, "ScrollThumbActive", colors.ScrollThumbActive);

        // 更新 SolidColorBrush
        UpdateBrush(res, "SurfaceBrush", colors.Surface);
        UpdateBrush(res, "SurfaceHoverBrush", colors.SurfaceHover);
        UpdateBrush(res, "SurfaceAltBrush", colors.SurfaceAlt);
        UpdateBrush(res, "CardBrush", colors.CardColor);
        UpdateBrush(res, "TextPrimaryBrush", colors.TextPrimary);
        UpdateBrush(res, "TextSecondaryBrush", colors.TextSecondary);
        UpdateBrush(res, "TextTertiaryBrush", colors.TextTertiary);
        UpdateBrush(res, "BorderBrush", colors.Border);
        UpdateBrush(res, "BorderHoverBrush", colors.BorderHover);
        UpdateBrush(res, "SidebarBgBrush", colors.SidebarBg);
        UpdateBrush(res, "SidebarTextBrush", colors.SidebarText);
        UpdateBrush(res, "SidebarHoverBrush", colors.SidebarHover);
        UpdateBrush(res, "SidebarCardBgBrush", colors.SidebarCardBg);
        UpdateBrush(res, "ScrollThumbBrush", colors.ScrollThumb);
        UpdateBrush(res, "ScrollThumbHoverBrush", colors.ScrollThumbHover);
        UpdateBrush(res, "ScrollThumbActiveBrush", colors.ScrollThumbActive);

        // 语义色（Success/Warning/Danger 在暗色下更柔和）
        SetColor(res, "Success", colors.Success);
        SetColor(res, "SuccessLight", colors.SuccessLight);
        SetColor(res, "Warning", colors.Warning);
        SetColor(res, "WarningLight", colors.WarningLight);
        SetColor(res, "Danger", colors.Danger);
        SetColor(res, "DangerLight", colors.DangerLight);
        UpdateBrush(res, "SuccessBrush", colors.Success);
        UpdateBrush(res, "SuccessLightBrush", colors.SuccessLight);
        UpdateBrush(res, "WarningBrush", colors.Warning);
        UpdateBrush(res, "WarningLightBrush", colors.WarningLight);
        UpdateBrush(res, "DangerBrush", colors.Danger);
        UpdateBrush(res, "DangerLightBrush", colors.DangerLight);
    }

    private void ApplyAccentColor()
    {
        if (!AccentPalettes.TryGetValue(_accentColor, out var palette)) return;
        var res = Application.Current.Resources;

        // 深色模式下，将 PrimaryLight/PrimarySoft 与深色表面混合，避免刺眼的亮色背景
        var lightColor = IsDarkMode
            ? BlendColors(ParseColor(DarkColors.Surface), ParseColor(palette.Primary), 0.25)
            : ParseColor(palette.PrimaryLight);
        var softColor = IsDarkMode
            ? BlendColors(ParseColor(DarkColors.Surface), ParseColor(palette.Primary), 0.12)
            : ParseColor(palette.PrimarySoft);

        SetColor(res, "Primary", palette.Primary);
        SetColor(res, "PrimaryHover", palette.PrimaryHover);
        SetColor(res, "PrimaryLight", lightColor);
        SetColor(res, "PrimarySoft", softColor);
        SetColor(res, "AccentIndigo", palette.Primary);   // 兼容旧引用
        SetColor(res, "AccentViolet", palette.PrimaryHover);

        UpdateBrush(res, "PrimaryBrush", palette.Primary);
        UpdateBrush(res, "PrimaryHoverBrush", palette.PrimaryHover);
        UpdateBrush(res, "PrimaryLightBrush", lightColor);
        UpdateBrush(res, "PrimarySoftBrush", softColor);
        UpdateBrush(res, "AccentIndigoBrush", palette.Primary);
        UpdateBrush(res, "AccentVioletBrush", palette.PrimaryHover);

        // 替换渐变画笔（原实例可能被 Freeze）
        var c1 = ParseColor(palette.Primary);
        var c2 = ParseColor(palette.PrimaryHover);
        res["PrimaryGradient"] = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(c1, 0),
                new GradientStop(c2, 1),
            }
        };
        res["AccentGradientH"] = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0.5),
            EndPoint = new System.Windows.Point(1, 0.5),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(c1, 0),
                new GradientStop(c2, 0.5),
                new GradientStop(c1, 1),
            }
        };

        // 替换焦点阴影（原实例可能被 Freeze）
        res["ShadowFocus"] = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 10, ShadowDepth = 0, Opacity = 0.15, Color = c1
        };

        // 横幅渐变画笔（跟随强调色，用于 Dashboard 的"今日提醒"和"试用期进度"横幅）
        var cLight = IsDarkMode
            ? LerpColor(c1, 0.55)   // 暗色下降低饱和度，更柔和
            : LerpColor(c1, 0.80);  // 亮色下保持鲜明
        res["AccentBannerGradient"] = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0.5),
            EndPoint = new System.Windows.Point(1, 0.5),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(c1, 0),
                new GradientStop(c2, 0.5),
                new GradientStop(cLight, 1),
            }
        };
    }

    private void ApplyFontSize()
    {
        if (!FontSizes.TryGetValue(_fontSizeLevel, out var sizes)) return;
        var res = Application.Current.Resources;

        // 更新全局字体大小键值（如果存在的话）
        if (res.Contains("BaseFontSize")) res["BaseFontSize"] = sizes.Base;
        if (res.Contains("SmallFontSize")) res["SmallFontSize"] = sizes.Small;
        if (res.Contains("Heading3FontSize")) res["Heading3FontSize"] = sizes.Heading3;
        if (res.Contains("BodyFontSize")) res["BodyFontSize"] = sizes.Body;
        if (res.Contains("CaptionFontSize")) res["CaptionFontSize"] = sizes.Caption;

        // 更新全局缩放因子（驱动 LayoutTransform）
        if (FontScaleFactors.TryGetValue(_fontSizeLevel, out var scale))
        {
            if (res.Contains("UIScale")) res["UIScale"] = scale;

            // 直接对主内容区 Grid 设置 LayoutTransform（ScaleTransform 不支持 DynamicResource）
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    var grid = mainWindow.FindName("MainContentArea") as FrameworkElement;
                    if (grid != null)
                    {
                        grid.LayoutTransform = new ScaleTransform(scale, scale);
                    }
                }
            }
            catch { /* 窗口尚未初始化时忽略 */ }
        }
    }

    private void ApplyDensity()
    {
        if (!DensityValues.TryGetValue(_uiDensity, out var d)) return;
        var res = Application.Current.Resources;

        if (res.Contains("DensityCardPad"))      res["DensityCardPad"]      = d.CardPad;
        if (res.Contains("DensityRowPad"))       res["DensityRowPad"]       = d.RowPad;
        if (res.Contains("DensityRowPadSm"))     res["DensityRowPadSm"]     = d.RowPadSm;
        if (res.Contains("DensityListPad"))       res["DensityListPad"]      = d.ListPad;
        if (res.Contains("DensitySectionMargin")) res["DensitySectionMargin"] = d.SectionMargin;
        if (res.Contains("DensityRowMargin"))     res["DensityRowMargin"]    = d.RowMargin;
        if (res.Contains("DensityItemMargin"))    res["DensityItemMargin"]   = d.ItemMargin;
    }

    // ===== 工具方法 =====

    private static void SetColor(ResourceDictionary res, string key, string hexColor)
    {
        var color = ParseColor(hexColor);
        if (res.Contains(key))
            res[key] = color;
        else
            res.Add(key, color);
    }

    private static void SetColor(ResourceDictionary res, string key, Color color)
    {
        if (res.Contains(key))
            res[key] = color;
        else
            res.Add(key, color);
    }

    private static void UpdateBrush(ResourceDictionary res, string key, string hexColor)
    {
        var color = ParseColor(hexColor);
        // WPF 中 XAML 定义的 Brush 会被 Freeze，不能直接修改 Color
        // 始终创建新的 unfrozen 画刷替换资源条目
        var newBrush = new SolidColorBrush(color);
        res[key] = newBrush;
    }

    private static void UpdateBrush(ResourceDictionary res, string key, Color color)
    {
        var newBrush = new SolidColorBrush(color);
        res[key] = newBrush;
    }

    private static Color ParseColor(string hex)
    {
        return (Color)ColorConverter.ConvertFromString(hex.StartsWith("#FF") ? hex : "#FF" + hex.TrimStart('#'));
    }

    /// <summary>
    /// 将颜色向白色方向插值（降低饱和度），factor 0=原色 1=纯白
    /// </summary>
    private static Color LerpColor(Color c, double factor)
    {
        var r = (byte)(c.R + (255 - c.R) * factor);
        var g = (byte)(c.G + (255 - c.G) * factor);
        var b = (byte)(c.B + (255 - c.B) * factor);
        return Color.FromRgb(r, g, b);
    }

    /// <summary>
    /// 将两个颜色按比例混合，factor 0=纯c1 1=纯c2
    /// </summary>
    private static Color BlendColors(Color c1, Color c2, double factor)
    {
        var r = (byte)(c1.R + (c2.R - c1.R) * factor);
        var g = (byte)(c1.G + (c2.G - c1.G) * factor);
        var b = (byte)(c1.B + (c2.B - c1.B) * factor);
        return Color.FromRgb(r, g, b);
    }

    /// <summary>
    /// 获取可用的强调色列表
    /// </summary>
    public static string[] GetAccentColorNames => AccentPalettes.Keys.ToArray();

    /// <summary>
    /// 获取强调色预览色值（用于 UI 展示色块）
    /// </summary>
    public static string GetAccentPreviewColor(string name)
    {
        return AccentPalettes.TryGetValue(name, out var p) ? p.Primary : "#4F46E5";
    }
}

// ===== 辅助类型 =====

internal record AccentPalette(string Primary, string PrimaryHover, string PrimaryDark, string PrimaryLight, string PrimarySoft);

internal class ThemeColors
{
    public string Surface { get; init; } = "#F1F5F9";
    public string SurfaceHover { get; init; } = "#E2E8F0";
    public string SurfaceAlt { get; init; } = "#F8FAFC";
    public string CardColor { get; init; } = "#FFFFFF";
    public string TextPrimary { get; init; } = "#0F172A";
    public string TextSecondary { get; init; } = "#475569";
    public string TextTertiary { get; init; } = "#94A3B8";
    public string Border { get; init; } = "#E2E8F0";
    public string BorderHover { get; init; } = "#CBD5E1";
    public string SidebarBg { get; init; } = "#0F172A";
    public string SidebarText { get; init; } = "#94A3B8";
    public string SidebarHover { get; init; } = "#162033";
    public string SidebarCardBg { get; init; } = "#1E293B";
    public string ScrollThumb { get; init; } = "#CBD5E1";
    public string ScrollThumbHover { get; init; } = "#94A3B8";
    public string ScrollThumbActive { get; init; } = "#64748B";
    public string Success { get; init; } = "#10B981";
    public string SuccessLight { get; init; } = "#D1FAE5";
    public string Warning { get; init; } = "#F59E0B";
    public string WarningLight { get; init; } = "#FEF3C7";
    public string Danger { get; init; } = "#EF4444";
    public string DangerLight { get; init; } = "#FEE2E2";
}

internal record FontSizeConfig(double Base, double Small, double Heading3, double Body, double Caption);

internal record DensityConfig(
    Thickness CardPad, Thickness RowPad, Thickness RowPadSm, Thickness ListPad,
    Thickness SectionMargin, Thickness RowMargin, Thickness ItemMargin);
