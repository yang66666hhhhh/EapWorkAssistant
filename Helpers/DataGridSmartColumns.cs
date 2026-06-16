using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EapWorkAssistant.Helpers;

/// <summary>
/// DataGrid 智能列宽优化（附加属性）。
/// 对 Star 宽度列进行内容采样：若该列大部分行为空，则自动收缩为窄宽度，
/// 将空间让给有内容的列。
///
/// 用法：在 DataGrid 上设置 local:SmartColumns.Enable="True"
/// </summary>
public static class SmartColumns
{
    public static readonly DependencyProperty EnableProperty =
        DependencyProperty.RegisterAttached("Enable", typeof(bool), typeof(SmartColumns),
            new PropertyMetadata(false, OnPropertyChanged));

    public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);
    public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);

    // ────────────── 事件订阅管理 ──────────────

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
    private static void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => ScheduleAdjust((DataGrid)sender);
    private static void OnItemsSourceChanged(object? sender, EventArgs e)
    {
        if (sender is DataGrid grid) ScheduleAdjust(grid);
    }

    // ────────────── 延迟调度（等数据绑定完成后再采样） ──────────────

    private static void ScheduleAdjust(DataGrid grid)
    {
        grid.Dispatcher.BeginInvoke(new Action(() => AdjustStarColumns(grid)),
            DispatcherPriority.Background);
    }

    // ────────────── 核心算法 ──────────────

    /// <summary>
    /// 采样率阈值：当列中有 ≥ 此比例的行包含非空内容时，保留 Star 宽度；
    /// 否则将该列收缩为 Auto（上限 MaxWidth）。
    /// </summary>
    private const double FillRatioThreshold = 0.25;

    /// <summary>
    /// 空列收缩后的最大宽度（像素）。
    /// </summary>
    private const double EmptyColumnMaxWidth = 60;

    private static void AdjustStarColumns(DataGrid grid)
    {
        if (grid.Items.Count == 0) return;

        var itemCount = grid.Items.Count;

        foreach (var column in grid.Columns)
        {
            if (!column.Width.IsStar) continue;

            var path = GetBindingPath(column);
            if (string.IsNullOrEmpty(path)) continue;

            // 采样：统计该列有内容的行数
            int filledRows = 0;
            for (int i = 0; i < itemCount; i++)
            {
                var item = grid.Items[i];
                var value = GetPropertyValue(item, path);
                if (!string.IsNullOrWhiteSpace(value?.ToString()))
                    filledRows++;
            }

            double fillRatio = (double)filledRows / itemCount;

            if (fillRatio >= FillRatioThreshold)
            {
                // 有足够内容 → 保持 Star 宽度，移除收缩限制
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                column.MaxWidth = double.PositiveInfinity;
            }
            else if (filledRows == 0)
            {
                // 完全为空 → 极致收缩
                column.Width = new DataGridLength(0, DataGridLengthUnitType.Auto);
                column.MinWidth = 36;
                column.MaxWidth = 36;
            }
            else
            {
                // 少量内容 → 收缩但保留可读宽度
                column.Width = new DataGridLength(0, DataGridLengthUnitType.Auto);
                column.MinWidth = 36;
                column.MaxWidth = EmptyColumnMaxWidth;
            }
        }
    }

    // ────────────── 工具方法 ──────────────

    private static string? GetBindingPath(DataGridColumn column)
    {
        return column switch
        {
            DataGridTextColumn textCol when textCol.Binding is System.Windows.Data.Binding b
                => b.Path?.Path,
            DataGridTemplateColumn tmplCol when tmplCol.ClipboardContentBinding is System.Windows.Data.Binding cb
                => cb.Path?.Path,
            _ => null
        };
    }

    private static object? GetPropertyValue(object? item, string? path)
    {
        if (item == null || string.IsNullOrEmpty(path)) return null;
        try
        {
            var prop = item.GetType().GetProperty(path);
            return prop?.GetValue(item);
        }
        catch { return null; }
    }
}
