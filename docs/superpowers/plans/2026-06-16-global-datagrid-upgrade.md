# Global DataGrid Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the project's main DataGrid experience so columns are more readable by default, horizontal scrolling appears naturally for wide tables, and users can manually resize columns.

**Architecture:** Keep the change scoped to the existing WPF DataGrid system. Improve the shared `ModernGrid` styles in `Resources/Styles.xaml`, upgrade the `SmartColumns` helper to provide better default widths, and calibrate the column definitions in the main table views so the global behavior produces immediate, visible results.

**Tech Stack:** .NET 9, WPF, XAML, CommunityToolkit.Mvvm, existing custom helpers and shared styles

---

## File Structure

- Modify: `D:/03_Projects/EapWorkAssistant/Resources/Styles.xaml`
  - Responsibility: shared DataGrid visual behavior, spacing, resize-related defaults, scroll behavior styling
- Modify: `D:/03_Projects/EapWorkAssistant/Helpers/DataGridSmartColumns.cs`
  - Responsibility: shared default column width strategy for wide and text-heavy grids
- Modify: `D:/03_Projects/EapWorkAssistant/Views/WorkRecordView.xaml`
  - Responsibility: main work record table column widths
- Modify: `D:/03_Projects/EapWorkAssistant/Views/IssueView.xaml`
  - Responsibility: issue tracking table column widths
- Modify: `D:/03_Projects/EapWorkAssistant/Views/DashboardView.xaml`
  - Responsibility: dashboard summary table column widths

## Task 1: Upgrade shared DataGrid styles

**Files:**
- Modify: `D:/03_Projects/EapWorkAssistant/Resources/Styles.xaml`

- [ ] **Step 1: Inspect the existing DataGrid style block and identify the setters that control spacing, resizing, and scrolling**

Read the `ModernGrid`, `ModernGridColumnHeader`, `ModernGridCell`, and `ModernGridRow` style sections and note the current values for:

```text
FontSize
Padding
MinRowHeight
GridLinesVisibility
HorizontalGridLinesBrush
CanUserResizeColumns
CanUserReorderColumns
ScrollViewer.CanContentScroll
```

- [ ] **Step 2: Update `ModernGrid` to support comfortable reading and user resizing**

Edit the `ModernGrid` style so it explicitly enables user column resizing and keeps the grid comfortable for wide layouts:

```xml
<Setter Property="CanUserResizeColumns" Value="True"/>
<Setter Property="CanUserReorderColumns" Value="False"/>
<Setter Property="CanUserSortColumns" Value="True"/>
<Setter Property="ScrollViewer.CanContentScroll" Value="True"/>
<Setter Property="ScrollViewer.PanningMode" Value="Both"/>
<Setter Property="MinColumnWidth" Value="72"/>
<Setter Property="MinRowHeight" Value="48"/>
```

Keep the existing shared helpers enabled:

```xml
<Setter Property="local:DataGridCopyHelper.EnableCopy" Value="True"/>
<Setter Property="local:SmartColumns.Enable" Value="True"/>
```

- [ ] **Step 3: Increase header and cell spacing to reduce visual crowding**

Update the shared header and cell padding so all tables gain more breathing room:

```xml
<Style x:Key="ModernGridColumnHeader" TargetType="DataGridColumnHeader">
    <Setter Property="Padding" Value="18,12"/>
</Style>

<Style x:Key="ModernGridCell" TargetType="DataGridCell">
    <Setter Property="Padding" Value="18,12"/>
</Style>
```

Keep the rest of the style aligned with the existing design language.

- [ ] **Step 4: Run a build to validate XAML syntax after the shared style change**

Run:

```powershell
rtk dotnet build
```

Expected: build succeeds with `0 Error(s)`.

## Task 2: Upgrade `SmartColumns` to a readable default width strategy

**Files:**
- Modify: `D:/03_Projects/EapWorkAssistant/Helpers/DataGridSmartColumns.cs`

- [ ] **Step 1: Preserve the current attached-property lifecycle wiring**

Keep the existing attached property and event subscription structure intact:

```csharp
public static readonly DependencyProperty EnableProperty =
    DependencyProperty.RegisterAttached("Enable", typeof(bool), typeof(SmartColumns),
        new PropertyMetadata(false, OnPropertyChanged));
```

Do not change the public API or rename the helper.

- [ ] **Step 2: Replace the current star-column shrinking logic with explicit width profiles**

Refactor `AdjustStarColumns` into logic that:

```csharp
1. Skips grids with no columns
2. Skips columns the user has manually resized
3. Classifies columns by header text or binding path
4. Applies width presets for compact, medium, text-heavy, and action columns
```

Use a small internal model like:

```csharp
private sealed record ColumnWidthProfile(
    DataGridLength Width,
    double MinWidth,
    double? MaxWidth = null);
```

- [ ] **Step 3: Add deterministic width mappings for the current main grids**

Implement a helper method that recognizes the current columns and returns readable defaults. Cover at least these headers and paths:

```text
日期 / WorkDate
任务 / ProjectName
标题 / Title
类型 / WorkType
内容 / Content
工作成果 / Achievement
问题 / Problem
描述 / Description
根本原因 / RootCause
解决方案 / Solution
关键词 / Keywords
状态 / Status
优先级 / Priority
工时 / Hours
进度 / Progress
操作
```

Recommended intent:

```text
内容类列：240-320 起步
中等信息列：120-180 起步
紧凑列：72-96
操作列：120-140
```

- [ ] **Step 4: Avoid stomping on user-resized columns during refresh**

Before applying a width profile, check whether a column is already in a manual pixel width state that likely came from user interaction. Keep that width instead of resetting it on every load:

```csharp
if (column.Width.UnitType == DataGridLengthUnitType.Pixel &&
    !double.IsNaN(column.Width.DisplayValue) &&
    column.Width.DisplayValue > 0)
{
    return;
}
```

Adapt this guard carefully so default startup widths can still be applied.

- [ ] **Step 5: Add short Chinese comments that explain why the helper prefers readable defaults**

Add concise comments only around the non-obvious width classification logic, for example:

```csharp
// 宽表默认优先可读性，不再为了塞进视口而压缩长文本列。
```

- [ ] **Step 6: Run a build to validate the helper refactor**

Run:

```powershell
rtk dotnet build
```

Expected: build succeeds with `0 Error(s)`.

## Task 3: Calibrate the work record tables

**Files:**
- Modify: `D:/03_Projects/EapWorkAssistant/Views/WorkRecordView.xaml`

- [ ] **Step 1: Update the current-day record grid widths**

Adjust the table under `ItemsSource="{Binding Records}"` so the fixed-width columns are no longer overly narrow. Update widths along these lines:

```xml
星标列: 36-40
任务: 150-170
类型: 96-108
内容: 260+
工作成果: 220+
工时: 76-84
进度: 140-150
问题: 220+
操作: 132-144
```

Keep the existing templates, commands, and copy behavior unchanged.

- [ ] **Step 2: Update the all-records grid widths**

Adjust the table under `ItemsSource="{Binding AllRecords}"` with the same reading-first philosophy. Keep date visible and content-oriented columns comfortably wide:

```xml
日期: 132-140
任务: 150-170
内容: 260+
工作成果: 220+
问题: 220+
```

Do not add extra ToolTips to the content column because `DataGridCopyHelper` already handles preview.

- [ ] **Step 3: Run a build after the work record table changes**

Run:

```powershell
rtk dotnet build
```

Expected: build succeeds with `0 Error(s)`.

## Task 4: Calibrate the issue tracking table

**Files:**
- Modify: `D:/03_Projects/EapWorkAssistant/Views/IssueView.xaml`

- [ ] **Step 1: Expand the issue table’s medium and long-text columns**

Adjust widths with the current view model bindings preserved:

```xml
任务: 140-160
标题: 160-180
描述: 260+
状态: 88-96
优先级: 80-90
根本原因: 240+
解决方案: 240+
关键词: 140-160
操作: 132-144
```

- [ ] **Step 2: Keep long-text columns single-line with existing hover access**

Retain the current `GridTextTrim` based styles and `ModernToolTip` usage for:

```text
标题
描述
根本原因
解决方案
关键词
```

Do not convert any of these columns to multiline display.

- [ ] **Step 3: Run a build after the issue table changes**

Run:

```powershell
rtk dotnet build
```

Expected: build succeeds with `0 Error(s)`.

## Task 5: Calibrate the dashboard summary table

**Files:**
- Modify: `D:/03_Projects/EapWorkAssistant/Views/DashboardView.xaml`

- [ ] **Step 1: Update the recent-records table widths for readable summaries**

Adjust the widths for the dashboard summary table while keeping it slightly more compact than the work record page:

```xml
日期: 132-140
任务: 150-160
类型: 92-100
内容: 240+
工作成果: 200+
工时: 76-84
进度: 140-150
问题: 200+
```

- [ ] **Step 2: Verify the summary table still behaves like a summary, not a cramped mini-grid**

Check that the table remains readable in the dashboard card context and allows horizontal scrolling instead of compressing the text-heavy columns too aggressively.

- [ ] **Step 3: Run a build after the dashboard table changes**

Run:

```powershell
rtk dotnet build
```

Expected: build succeeds with `0 Error(s)`.

## Task 6: Final verification

**Files:**
- Verify: `D:/03_Projects/EapWorkAssistant/Resources/Styles.xaml`
- Verify: `D:/03_Projects/EapWorkAssistant/Helpers/DataGridSmartColumns.cs`
- Verify: `D:/03_Projects/EapWorkAssistant/Views/WorkRecordView.xaml`
- Verify: `D:/03_Projects/EapWorkAssistant/Views/IssueView.xaml`
- Verify: `D:/03_Projects/EapWorkAssistant/Views/DashboardView.xaml`

- [ ] **Step 1: Run the full build one last time**

Run:

```powershell
rtk dotnet build
```

Expected: build succeeds with `0 Error(s)`.

- [ ] **Step 2: Review the final diff for scope**

Run:

```powershell
rtk git diff -- Resources/Styles.xaml Helpers/DataGridSmartColumns.cs Views/WorkRecordView.xaml Views/IssueView.xaml Views/DashboardView.xaml docs/superpowers/specs/2026-06-16-global-datagrid-upgrade-design.md docs/superpowers/plans/2026-06-16-global-datagrid-upgrade.md
```

Expected: only the shared table styles, helper, target views, and planning docs are changed.

- [ ] **Step 3: Prepare a concise implementation summary for the user**

Summarize:

```text
修改了什么
为什么这样改
影响哪些文件
如何验证
```
