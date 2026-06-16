using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// DataGrid 智能列宽优化（附加属性）。
/// 默认优先保证宽表可读性，让长文本列先拥有舒适宽度，
/// 放不下时再交给横向滚动条，而不是把每一列都压窄。
///
/// 用法：在 DataGrid 上设置 local:SmartColumns.Enable="True"
/// </summary>
public static class SmartColumns
{
    public static readonly DependencyProperty EnableProperty =
        DependencyProperty.RegisterAttached("Enable", typeof(bool), typeof(SmartColumns),
            new PropertyMetadata(false, OnPropertyChanged));

    private static readonly DependencyProperty HasAppliedDefaultsProperty =
        DependencyProperty.RegisterAttached("HasAppliedDefaults", typeof(bool), typeof(SmartColumns),
            new PropertyMetadata(false));

    private sealed record ColumnWidthProfile(
        DataGridLength Width,
        double MinWidth,
        double? MaxWidth = null);

    public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);
    public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);

    private static bool GetHasAppliedDefaults(DependencyObject obj) => (bool)obj.GetValue(HasAppliedDefaultsProperty);
    private static void SetHasAppliedDefaults(DependencyObject obj, bool value) => obj.SetValue(HasAppliedDefaultsProperty, value);

    private static readonly DependencyPropertyDescriptor ItemsSourceDescriptor =
        DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(DataGrid));

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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

    private static void OnLoaded(object sender, RoutedEventArgs e) => ScheduleAdjust((DataGrid)sender);

    private static void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        ScheduleAdjust((DataGrid)sender);

    private static void OnItemsSourceChanged(object? sender, EventArgs e)
    {
        if (sender is DataGrid grid)
        {
            ScheduleAdjust(grid);
        }
    }

    private static void ScheduleAdjust(DataGrid grid)
    {
        grid.Dispatcher.BeginInvoke(new Action(() => AdjustColumns(grid)), DispatcherPriority.Background);
    }

    private static void AdjustColumns(DataGrid grid)
    {
        if (grid.Columns.Count == 0) return;

        var hasAppliedDefaults = GetHasAppliedDefaults(grid);

        foreach (var column in grid.Columns)
        {
            var profile = ResolveProfile(column);
            if (profile is null) continue;

            if (ShouldPreserveUserWidth(column, profile, hasAppliedDefaults))
            {
                continue;
            }

            ApplyProfile(column, profile);
        }

        SetHasAppliedDefaults(grid, true);
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
        if (Matches(header, path, "标题", "Title")) return Fixed(168, 150, 190);
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
        if (IsActionColumn(header, path)) return Fixed(136, 128, 152);

        // 未识别但原本是 Star 的列，给一个保守的舒展默认值，避免首屏过挤。
        if (column.Width.IsStar)
        {
            return Fixed(220, 180);
        }

        return null;
    }

    private static ColumnWidthProfile Fixed(double width, double minWidth, double? maxWidth = null) =>
        new(new DataGridLength(width, DataGridLengthUnitType.Pixel), minWidth, maxWidth);

    private static bool Matches(string? header, string? path, string expectedHeader, string expectedPath) =>
        string.Equals(header, expectedHeader, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, expectedPath, StringComparison.OrdinalIgnoreCase);

    private static bool IsActionColumn(string? header, string? path) =>
        string.Equals(header, "操作", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, "Actions", StringComparison.OrdinalIgnoreCase);

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
