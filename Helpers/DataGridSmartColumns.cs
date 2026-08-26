using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// DataGrid 智能列宽与列宽记忆（附加属性）。
/// - 智能默认：长文本列给舒适宽度，放不下时交给横向滚动，而非把所有列压窄。
/// - 列宽记忆：以 TableKey 为维度，把用户拖拽后的列宽持久化到本地 JSON，
///   下次打开（及数据刷新）时自动恢复，实现“记忆”。
///
/// 用法：在 DataGrid（或继承 DataGridBase）上设置 SmartColumns.Enable="True" 与 TableKey="WorkRecord"。
/// 注意：XAML 中显式声明的 Auto / * 列会被尊重（不会强制套用固定宽度），仅对像素列做智能校准 + 记忆。
/// </summary>
public static class SmartColumns
{
    public static readonly DependencyProperty EnableProperty =
        DependencyProperty.RegisterAttached("Enable", typeof(bool), typeof(SmartColumns),
            new PropertyMetadata(false, OnEnableChanged));

    public static readonly DependencyProperty TableKeyProperty =
        DependencyProperty.RegisterAttached("TableKey", typeof(string), typeof(SmartColumns),
            new PropertyMetadata(null, OnTableKeyChanged));

    private static readonly DependencyProperty HasAppliedDefaultsProperty =
        DependencyProperty.RegisterAttached("HasAppliedDefaults", typeof(bool), typeof(SmartColumns),
            new PropertyMetadata(false));

    private static readonly DependencyProperty IsAdjustingProperty =
        DependencyProperty.RegisterAttached("IsAdjusting", typeof(bool), typeof(SmartColumns),
            new PropertyMetadata(false));

    private static readonly DependencyProperty HasListenersProperty =
        DependencyProperty.RegisterAttached("HasListeners", typeof(bool), typeof(SmartColumns),
            new PropertyMetadata(false));

    private sealed record ColumnWidthProfile(
        DataGridLength Width,
        double MinWidth,
        double? MaxWidth = null);

    public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);
    public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);

    public static void SetTableKey(DependencyObject obj, string? value) => obj.SetValue(TableKeyProperty, value);
    public static string? GetTableKey(DependencyObject obj) => (string?)obj.GetValue(TableKeyProperty);

    private static bool GetHasAppliedDefaults(DependencyObject o) => (bool)o.GetValue(HasAppliedDefaultsProperty);
    private static void SetHasAppliedDefaults(DependencyObject o, bool v) => o.SetValue(HasAppliedDefaultsProperty, v);
    private static bool GetIsAdjusting(DependencyObject o) => (bool)o.GetValue(IsAdjustingProperty);
    private static void SetIsAdjusting(DependencyObject o, bool v) => o.SetValue(IsAdjustingProperty, v);
    private static bool GetHasListeners(DependencyObject o) => (bool)o.GetValue(HasListenersProperty);
    private static void SetHasListeners(DependencyObject o, bool v) => o.SetValue(HasListenersProperty, v);

    private static readonly DependencyPropertyDescriptor ItemsSourceDescriptor =
        DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(DataGrid));

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;

        if ((bool)e.NewValue)
        {
            grid.Loaded += OnLoaded;
            grid.DataContextChanged += OnDataContextChanged;
            ItemsSourceDescriptor.AddValueChanged(grid, OnItemsSourceChanged);
        }
        else
        {
            grid.Loaded -= OnLoaded;
            grid.DataContextChanged -= OnDataContextChanged;
            ItemsSourceDescriptor.RemoveValueChanged(grid, OnItemsSourceChanged);
        }
    }

    private static void OnTableKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // TableKey 可能在 Enable 之后设置，变化时重新校准以应用对应维度的记忆
        if (d is DataGrid grid && GetEnable(grid))
            ScheduleAdjust(grid);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var grid = (DataGrid)sender;
        ScheduleAdjust(grid);
        AttachColumnListeners(grid);
    }

    private static void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        ScheduleAdjust((DataGrid)sender);

    private static void OnItemsSourceChanged(object? sender, EventArgs e)
    {
        if (sender is DataGrid grid)
            ScheduleAdjust(grid);
    }

    private static void ScheduleAdjust(DataGrid grid)
    {
        if (grid.Columns.Count == 0) return;
        grid.Dispatcher.BeginInvoke(new Action(() => AdjustColumns(grid)), DispatcherPriority.Background);
    }

    private static void AdjustColumns(DataGrid grid)
    {
        if (grid.Columns.Count == 0) return;

        SetIsAdjusting(grid, true);
        try
        {
            var tableKey = GetTableKey(grid);
            var hasAppliedDefaults = GetHasAppliedDefaults(grid);

            for (int i = 0; i < grid.Columns.Count; i++)
            {
                var column = grid.Columns[i];
                var key = ColumnKey(column, i);

                // 1) 持久化的用户列宽记忆优先
                if (!string.IsNullOrEmpty(tableKey)
                    && ColumnWidthStore.TryGet(tableKey, key, out double saved)
                    && saved > 1)
                {
                    column.Width = new DataGridLength(saved, DataGridLengthUnitType.Pixel);
                    continue;
                }

                // 2) 尊重 XAML 中显式声明的 Auto / * 列（如回收站的 Auto / 2* 列）
                if (column.Width.UnitType is DataGridLengthUnitType.Auto or DataGridLengthUnitType.Star)
                    continue;

                // 3) 已显式固定宽度的列：用智能默认档位校准（与现有 XAML 宽度一致，无视觉变化）
                var profile = ResolveProfile(column);
                if (profile is null) continue;
                if (ShouldPreserveUserWidth(column, profile, hasAppliedDefaults)) continue;
                ApplyProfile(column, profile);
            }

            SetHasAppliedDefaults(grid, true);
        }
        finally
        {
            SetIsAdjusting(grid, false);
        }
    }

    private static bool ShouldPreserveUserWidth(
        DataGridColumn column,
        ColumnWidthProfile profile,
        bool hasAppliedDefaults)
    {
        if (!hasAppliedDefaults) return false;
        if (column.Width.UnitType != DataGridLengthUnitType.Pixel) return false;
        if (profile.Width.UnitType != DataGridLengthUnitType.Pixel) return false;
        if (column.Width.Value <= 0) return false;

        return Math.Abs(column.Width.Value - profile.Width.Value) > 0.5;
    }

    private static void ApplyProfile(DataGridColumn column, ColumnWidthProfile profile)
    {
        column.MinWidth = profile.MinWidth;
        column.MaxWidth = profile.MaxWidth ?? double.PositiveInfinity;
        column.Width = profile.Width;
    }

    private static ColumnWidthProfile? ResolveProfile(DataGridColumn column)
    {
        var header = column.Header?.ToString()?.Trim();
        var path = GetBindingPath(column);

        // 宽表默认优先可读性，不再为了塞进视口而压缩长文本列。
        if (Matches(header, path, "日期", "WorkDate")) return Fixed(136, 120, 156);
        if (Matches(header, path, "任务", "ProjectName")) return Fixed(160, 140, 180);
        if (Matches(header, path, "标题", "Title")) return Star(2, 180);       // 主标识符：弹性宽度，最小180
        if (Matches(header, path, "类型", "WorkType")) return Fixed(100, 92, 112);
        if (Matches(header, path, "内容", "Content")) return Fixed(300, 240);
        if (Matches(header, path, "工作成果", "Achievement")) return Fixed(240, 220);
        if (Matches(header, path, "问题", "Problem")) return Fixed(240, 220);
        if (Matches(header, path, "描述", "Description")) return Fixed(280, 240);
        if (Matches(header, path, "根本原因", "RootCause")) return Fixed(260, 220);
        if (Matches(header, path, "解决方案", "Solution")) return Fixed(260, 220);
        if (Matches(header, path, "关键词", "Keywords")) return Fixed(150, 128, 180);
        if (Matches(header, path, "状态", "Status")) return Fixed(92, 84, 104);
        if (Matches(header, path, "优先级", "Priority")) return Fixed(84, 76, 96);
        if (Matches(header, path, "工时", "Hours")) return Fixed(80, 76, 92);
        if (Matches(header, path, "进度", "Progress")) return Fixed(144, 132, 156);

        // 回收站专用列
        if (Matches(header, path, "摘要", "Detail")) return Fixed(200, 160);   // 次要信息，固定合理宽度
        if (Matches(header, path, "删除时间", "DeletedAt")) return Fixed(148, 130, 165); // datetime 精确适配

        if (IsActionColumn(header, path)) return Fixed(136, 128, 152);

        // 未识别但原本是 Star 的列，保留弹性布局（给一个合理的 star 权重）
        if (column.Width.IsStar)
        {
            return Star(1, 160);
        }

        return null;
    }

    private static ColumnWidthProfile Fixed(double width, double minWidth, double? maxWidth = null) =>
        new(new DataGridLength(width, DataGridLengthUnitType.Pixel), minWidth, maxWidth);

    private static ColumnWidthProfile Star(double starWeight = 1, double minWidth = 100) =>
        new(new DataGridLength(starWeight, DataGridLengthUnitType.Star), minWidth);

    private static bool Matches(string? header, string? path, string expectedHeader, string expectedPath) =>
        string.Equals(header, expectedHeader, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, expectedPath, StringComparison.OrdinalIgnoreCase);

    private static bool IsActionColumn(string? header, string? path) =>
        string.Equals(header, "操作", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, "Actions", StringComparison.OrdinalIgnoreCase);

    private static string ColumnKey(DataGridColumn column, int index)
    {
        var header = column.Header?.ToString()?.Trim();
        var path = GetBindingPath(column);
        if (!string.IsNullOrEmpty(header) && !string.IsNullOrEmpty(path)) return $"{header}|{path}";
        if (!string.IsNullOrEmpty(path)) return path!;
        if (!string.IsNullOrEmpty(header)) return header!;
        return $"col{index}";
    }

    private static void AttachColumnListeners(DataGrid grid)
    {
        if (GetHasListeners(grid)) return;
        SetHasListeners(grid, true);

        foreach (var column in grid.Columns)
        {
            var c = column;
            var desc = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, c.GetType());
            desc.AddValueChanged(c, (_, _) => OnColumnWidthChanged(grid, c));
        }
    }

    private static void OnColumnWidthChanged(DataGrid grid, DataGridColumn column)
    {
        // 程序化设置（加载/校准）期间忽略，避免回写噪声
        if (GetIsAdjusting(grid)) return;

        var tableKey = GetTableKey(grid);
        if (string.IsNullOrEmpty(tableKey)) return;

        // 仅持久化像素宽度（用户拖拽结果），Auto/* 不需要记忆
        if (column.Width.UnitType != DataGridLengthUnitType.Pixel) return;

        var key = ColumnKey(column, grid.Columns.IndexOf(column));
        ColumnWidthStore.SetWidth(tableKey, key, column.Width.Value);
    }

    private static string? GetBindingPath(DataGridColumn column)
    {
        return column switch
        {
            DataGridTextColumn textCol when textCol.Binding is System.Windows.Data.Binding binding
                => binding.Path?.Path?.Trim(),
            DataGridTemplateColumn tmplCol when tmplCol.ClipboardContentBinding is System.Windows.Data.Binding binding
                => binding.Path?.Path?.Trim(),
            _ => null
        };
    }
}
