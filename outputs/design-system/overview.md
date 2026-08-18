# 靛蓝双主题统一组件库规范 · 交付概述

## 本次完成内容

在原有 11 章组件规范基础上，继续把代码中真实存在但未进规范的 6 类组件补齐，最终形成一套共 **17 章**的统一组件库规范。

## 章节清单

| 章节 | 名称 | 核心内容 |
|---|---|---|
| 01 | 封面 | 规范标题、双主题风格标签、元信息 |
| 02 | 设计变量 | 浅色 / 深色对比色板、半径、阴影、字号三档、字体 |
| 03 | 按钮 Button | 主按钮 Indigo→Violet 渐变、次级、幽灵、链接、语义色、图标/加载态 |
| 04 | 表单 Form | 输入框、选择框、复选框、开关、必填校验 |
| 05 | 表格 Table | 浅色/深色表格、状态标签、分页 |
| 06 | 弹窗与通知 Modal & Toast | 确认弹窗双层阴影、四种 Toast 类型与带操作按钮示例 |
| 07 | 设计基础与原则 | 现代靛蓝理念、双主题同源机制、间距/圆角/阴影/字阶尺度、无障碍要点 |
| 08 | 导航 Navigation | 浅色/深色应用骨架：侧栏 NavButton、顶栏、搜索框、头像占位 |
| 09 | 数据可视化 Charts | LiveCharts2 双主题配色 + 柱状示意，Axis/Series/Tooltip 必须绑定 ThemeService |
| 10 | 反馈与状态 Feedback | 加载、空状态、错误态+重试、进度条、状态标签 Tag 样例 |
| 11 | 分割条 Splitter | 基于 GridSplitter 的 DragSplitter 样式：浅/深布局示意、垂直/水平分割、默认/悬停/拖拽三态 |
| 12 | 日期选择 DatePicker | ModernDatePicker / ModernCalendar / CustomCalendar，含 CalendarOverlay、选中日 / 今日 / 有记录日状态 |
| 13 | 折叠面板 SectionExpander | SettingsView 分组折叠面板：展开/折叠、旋转箭头、悬停高亮 |
| 14 | 列表 ListBox | ModernListBox / ModernListBoxItem：图标+标题+副标题、选中态、悬停态 |
| 15 | 滚动条 ScrollBar | ModernScrollBar / ModernScrollViewer：细轨道、圆角滑块、默认/悬停/拖拽三态 |
| 16 | 右键菜单 ContextMenu | 全局隐式右键菜单：图标+文字项、分隔线、禁用态、悬停高亮 |
| 17 | 工具提示 ToolTip | 全局隐式 ModernToolTip：深色紧凑气泡、白字、自动换行、150ms 延迟 |

## 与项目的对应关系

- 所有色值、圆角、阴影均来自 `Services/ThemeService.cs` 与 `Resources/Styles.xaml`。
- 新增章节直接对应到项目现有资源键：
  - `ModernDatePicker` / `ModernCalendar` / `CustomCalendar` / `CalendarOverlay`
  - `SectionExpander`
  - `ModernListBox` / `ModernListBoxItem`
  - `ModernScrollBar` / `ModernScrollViewer`
  - 隐式全局 `ContextMenu`
  - 隐式全局 `ToolTip`
- 图表章节补上了「LiveCharts2 深色适配」规范缺口，已在 `ThemeService` 新增 `GetChartColors()`、在 `DashboardViewModel` 订阅主题切换并 `ApplyChartTheme()` 落地（见下「实施进展」）。

## 交付文件

17 张 PNG 导出图位于 `outputs/design-system/`：

01_cover.png · 02_tokens.png · 03_buttons.png · 04_forms.png · 05_tables.png · 06_modals_toast.png · 07_foundation.png · 08_navigation.png · 09_charts.png · 10_feedback.png · 11_splitter.png · 12_datepicker.png · 13_expander.png · 14_listbox.png · 15_scrollbar.png · 16_contextmenu.png · 17_tooltip.png

## 实施进展（代码改动）

经用户确认后，已将规范落地为源码改动（隔离编译验证 0 错误）：

1. **设计→代码对照表** `outputs/design-system/style-mapping.md`：17 章设计逐一映射到 `Styles.xaml` 资源键与对应 View/ViewModel，作为实施索引（纯文档）。
2. **修复硬编码白色** `Views/RecycleBinView.xaml`：批量操作栏「恢复 / 彻底删除」按钮内 4 处 `#FFFFFF` → `{DynamicResource TextOnPrimaryBrush}`，对齐第 02 章按钮文字规范。
3. **LiveCharts2 主题绑定** `Services/ThemeService.cs` + `ViewModels/DashboardViewModel.cs`：
   - `ThemeService.GetChartColors()` 返回当前主题的轴文字 / 网格 / 强调色 `SKColor`；
   - `DashboardViewModel` 构造函数订阅 `ThemeService.PropertyChanged`，新增 `ApplyChartTheme()` 设置坐标轴 `LabelsPaint`/`NamePaint`/`SeparatorsPaint` 与柱图主色，在数据加载与主题（明暗 / 强调色）切换时重绘。
   - 修复深色模式下图表轴文字 / 网格线不可见的问题。

## 交付文件

- 17 张章节 PNG：`outputs/design-system/01_cover.png` … `17_tooltip.png`
- 规范概述：`outputs/design-system/overview.md`
- 设计→代码对照表：`outputs/design-system/style-mapping.md`
