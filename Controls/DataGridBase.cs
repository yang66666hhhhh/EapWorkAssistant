using System.Windows;
using System.Windows.Controls;
using EapWorkAssistant.Helpers;

namespace EapWorkAssistant.Controls;

/// <summary>
/// 通用表格基类：统一套用 ModernGrid 样式并启用智能列宽 + 列宽记忆。
/// 所有主表（工作记录 / 回收站 / 问题 / 仪表盘）继承此类即可获得一致的表格体验，
/// 无需在每个 View 重复声明 Style 与 SmartColumns 附加属性。
///
/// 用法：
///   &lt;controls:DataGridBase TableKey="WorkRecord" ItemsSource="{Binding Records}" /&gt;
///
/// TableKey 为该表在列宽记忆存储中的唯一维度，不同表必须不同。
/// </summary>
public class DataGridBase : DataGrid
{
    public DataGridBase()
    {
        // 默认套用全局 ModernGrid 样式（App.xaml 已合并，资源在加载时解析）
        SetResourceReference(StyleProperty, "ModernGrid");
        // 启用智能列宽校准 + 列宽记忆
        SmartColumns.SetEnable(this, true);
    }

    public static readonly DependencyProperty TableKeyProperty =
        DependencyProperty.Register(
            nameof(TableKey),
            typeof(string),
            typeof(DataGridBase),
            new PropertyMetadata(null, OnTableKeyChanged));

    public string? TableKey
    {
        get => (string?)GetValue(TableKeyProperty);
        set => SetValue(TableKeyProperty, value);
    }

    private static void OnTableKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        SmartColumns.SetTableKey(d, (string?)e.NewValue);
}
