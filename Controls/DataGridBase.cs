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
        // 强制开启列宽拖拽（即使样式 Setter 被意外覆盖，也能保证可调整）
        CanUserResizeColumns = true;
        // 启用智能列宽校准 + 列宽记忆
        SmartColumns.SetEnable(this, true);

        // ⚠️ 主表一律只读：本项目的表格都是「展示 + 操作列按钮 / 双击打开抽屉」模式，
        // 没有内联编辑需求。若保持可编辑（DataGrid 默认 IsReadOnly=false），
        // 「双击条目快速编辑」会先让 DataGrid 进入单元格编辑模式，而带 StringFormat 的列
        // （如工时列的 `8.0h`）提交时 ConvertBack 解析失败，会把**源对象的字段静默写成 0**——
        // 曾导致「编辑记录时工时显示 0」（行对象 Hours 被回写为 0，EditRecord 再复制该 0）。
        // 如需可编辑表，请在该 DataGrid 上显式写 IsReadOnly="False"。
        IsReadOnly = true;
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
