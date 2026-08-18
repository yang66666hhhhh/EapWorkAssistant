# 设计规范 → 代码实现对照表

> 本文档把《靛蓝设计系统 · 双主题组件规范》（`.ardot` 设计稿，共 17 章）逐一映射到项目真实代码，作为「设计落地」的实施索引。
> 设计规范文件：`靛蓝设计系统 · 双主题组件规范`（Ardot fileId `716199005514782`）。
> 代码权威来源：`Resources/Styles.xaml`、`Services/ThemeService.cs`、`Views/*`、`ViewModels/*`。

## 核心约定（双主题如何工作）

| 设计概念 | 代码落地 |
|---|---|
| 浅色主题 "Crystal Clean" | `ThemeService.LightColors`（`Surface #F1F5F9` / `Card #FFFFFF` …） |
| 深色主题 "Midnight Velvet" | `ThemeService.DarkColors`（`Surface #151D2E` / `Card #212E42` …） |
| 运行时换肤 | `ThemeService.ApplyThemeColors()` 把颜色写入 `Application.Current.Resources` |
| XAML 取色 | 全部用 `DynamicResource XxxBrush` / `DynamicResource Xxx`，**禁止硬编码色值** |
| 强调色 | `ThemeService.AccentPalettes`（Indigo/Violet/Blue/Emerald/Rose/Amber），影响 `Primary*` |

---

## 章节 ↔ 代码映射

| 章 | 设计主题 | 对应代码（`Styles.xaml` 资源键 / 组件） | 使用位置 |
|---|---|---|---|
| 01 | 封面 / Token | `Primary #4338CA`、`PrimaryHover`、`PrimaryGradient`、`Violet #7C3AED` | 全局 |
| 02 | 按钮 | `BtnPrimary`/`BtnSecondary`/`BtnSuccess`/`BtnWarning`/`BtnDanger`/`BtnGhost`；前景 `TextOnPrimaryBrush`(#CCFFFFFF) | 全站按钮 |
| 03 | 表单 | `Input`/`TextArea`/`Select`/`SearchInput`/`Label`；`ToggleSwitch`、`DayCheckBox` | 各录入页、SettingsView |
| 04 | 表格 | `ModernGrid`+`ModernGridColumnHeader`/`ModernGridCell`/`ModernGridRow`、`WorkTypeBadgeCell`、`ProgressBarCell`、`HighlightStarCell`、`GridTextTrim` | WorkRecordView / IssueView / DashboardView |
| 05 | 模态 & Toast | `OverlayBackdrop`、`SidePanel`、`FloatingPanel`、`CalendarOverlay`；`Services/ToastService.cs`、`Views/ConfirmDialog.xaml` | 弹窗与通知 |
| 06 | 基础件 | `Card`/`CardElevated`/`CardHover`、`Tag`/`TagPrimary`/`TagSuccess`/`TagWarning`/`TagDanger`、`StatusDot`、`IconCircle`、`Divider`、`Heading1/2/3`、图标库（`IconTrash` 等） | 全站 |
| 07 | 基础件·图标 | 图标统一用 `StaticResource IconXxx`（Path Data 集中管理） | 全站 |
| 08 | 导航 | `NavButton`、`ToggleChip`、`PageNumberButton`/`PageNumberActive`/`PageNavButton` | 侧栏 + 分页 |
| 09 | 图表 | `LiveChartsCore.SkiaSharpView.WPF`（CartesianChart / PieChart），配色在 `ViewModels/DashboardViewModel.cs` 构建 | DashboardView |
| 10 | 反馈 | `ToastService`（Success/Error/Info/Warning）、`ConfirmDialog`、`ModernToolTip` 隐式样式 | 通知与确认 |
| 11 | 分割条 | `DragSplitter` 样式 + `GridSplitter`；`Density*` 间距资源 | 可拖拽分栏 |
| 12 | 日期选择 | `ModernDatePicker`/`ModernCalendar` 样式 + `Views/CustomCalendar.xaml` + `CalendarOverlay`、`CalendarHelper` | WorkRecordView / LeaveDialog |
| 13 | 折叠面板 | `SectionExpander` | SettingsView |
| 14 | 列表 | `ModernListBox` / `ModernListBoxItem` | KnowledgeView / LeaveDialog |
| 15 | 滚动条 | `ModernScrollBar` / `ModernScrollViewer`（全局隐式）；`ScrollThumb*` 画笔 | 所有可滚动区 |
| 16 | 右键菜单 | ContextMenu 全局隐式样式（`Helpers/DataGridCopyHelper.cs` 触发复制） | DataGrid 右键 |
| 17 | 工具提示 | `ModernToolTip`（全局隐式，`InitialShowDelay=150ms`） | 表单元格 / 图标按钮 |

---

## 关键 Token 速查（颜色）

| 语义 | 浅色 | 深色 | 资源键 |
|---|---|---|---|
| 主强调 | #4338CA | 随 Palette | `PrimaryBrush` |
| 主强调浅底 | #EEF2FF | 混合 Surface | `PrimaryLightBrush` |
| 表面 | #F1F5F9 | #151D2E | `SurfaceBrush` |
| 卡片 | #FFFFFF | #212E42 | `CardBrush` |
| 主文字 | #0F172A | #F1F5F9 | `TextPrimaryBrush` |
| 次文字 | #475569 | #94A3B8 | `TextSecondaryBrush` |
| 辅助文字 | #94A3B8 | #64748B | `TextTertiaryBrush` |
| 边框 | #E2E8F0 | #2A3A52 | `BorderBrush` |
| 成功 | #10B981 | #34D399 | `SuccessBrush` |
| 警告 | #F59E0B | #FBBF24 | `WarningBrush` |
| 危险 | #EF4444 | #F87171 | `DangerBrush` |
| 强调色上文字 | #CCFFFFFF | #CCFFFFFF | `TextOnPrimaryBrush` |
| 滚动条滑块 | #CBD5E1 | #3B5476 | `ScrollThumbBrush` |

---

## 实施状态（截至本次）

- [x] 设计规范 17 章全部完成并导出 PNG
- [x] **RecycleBinView 4 处硬编码 `#FFFFFF` → `TextOnPrimaryBrush`**（对齐第 02 章按钮文字规范）
- [x] **LiveCharts2 图表主题绑定**：`DashboardViewModel` 订阅 `ThemeService`，新增 `ApplyChartTheme()`，坐标轴文字/网格跟随主题、柱图主色跟随强调色（对齐第 09 章）
- 后续可选：把 `BtnSecondary`、密码框样式等补进第 03 章；补充「对话框变体」小节（ConfigItemDialog/ProfileDialog/LeaveDialog 复用第 05 章外壳）
