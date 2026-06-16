# Global Style System Refinement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refine the project's shared WPF style system so the app keeps its current indigo, rational product character while gaining better visual hierarchy, more polished component surfaces, and more consistent spacing and control sizing.

**Architecture:** Keep the work centered in `Resources/Styles.xaml`, because the shared style system is already the project's UI backbone. Make a small number of page-level calibration changes only where shared style refinements need local alignment, focusing on high-frequency screens instead of reworking layout structure.

**Tech Stack:** .NET 10 WPF, XAML resource dictionaries, CommunityToolkit.Mvvm, existing shared style system

---

## File Structure

- Modify: `D:/03_Projects/EapWorkAssistant/Resources/Styles.xaml`
  - Responsibility: shared design tokens and shared component styles
- Modify: `D:/03_Projects/EapWorkAssistant/Views/DashboardView.xaml`
  - Responsibility: local alignment for dashboard surfaces if shared spacing changes need light tuning
- Modify: `D:/03_Projects/EapWorkAssistant/Views/WorkRecordView.xaml`
  - Responsibility: local alignment for form/table/report card rhythm if shared spacing changes require it
- Modify: `D:/03_Projects/EapWorkAssistant/Views/IssueView.xaml`
  - Responsibility: local alignment for issue table and editor rhythm if needed
- Modify: `D:/03_Projects/EapWorkAssistant/Views/KnowledgeView.xaml`
  - Responsibility: local alignment for knowledge list/form rhythm if needed
- Modify: `D:/03_Projects/EapWorkAssistant/Views/SettingsView.xaml`
  - Responsibility: local alignment for grouped settings rhythm if needed

## Task 1: Refine shared design tokens

**Files:**
- Modify: `D:/03_Projects/EapWorkAssistant/Resources/Styles.xaml`

- [ ] **Step 1: Inspect the current color, shadow, radius, and spacing sections**

Read the token sections in `Styles.xaml` and note the current values for:

```text
Primary / PrimaryHover / PrimaryLight / PrimarySoft
Surface / SurfaceHover / SurfaceAlt
TextPrimary / TextSecondary / TextTertiary
Border / BorderHover
ShadowSm / ShadowMd / ShadowLg / ShadowFocus / ShadowPanel
DensityCardPad / DensityRowPad / DensityRowPadSm / DensitySectionMargin / DensityItemMargin
```

- [ ] **Step 2: Refine the surface and text hierarchy without changing the product’s indigo character**

Update the shared color tokens so they stay within the current indigo, rational family but create cleaner separation between:

```text
Page background
Card surface
Hover surface
Primary text
Secondary text
Weak text
```

Keep the changes scoped to existing resource keys rather than introducing a second competing palette.

- [ ] **Step 3: Refine the shadow hierarchy for quieter default surfaces and clearer elevated panels**

Tune the shadow resources so they follow this intent:

```text
ShadowSm: lighter default surface shadow
ShadowMd: hover / elevated card shadow
ShadowLg: stronger popup / floating surface shadow
ShadowFocus: refined focus halo, not harsh glow
ShadowPanel: clearer side-panel depth
```

Do not add new decorative shadow resources unless an existing key cannot express the needed role.

- [ ] **Step 4: Run a build to validate token edits**

Run:

```powershell
rtk dotnet build
```

Expected: build succeeds with `0 Error(s)`.

## Task 2: Refine shared card and panel systems

**Files:**
- Modify: `D:/03_Projects/EapWorkAssistant/Resources/Styles.xaml`

- [ ] **Step 1: Inspect the current shared surface styles**

Review these existing styles before editing:

```text
Card
CardElevated
CardHover
SidePanel
FloatingPanel
OverlayBackdrop
CalendarOverlay
```

- [ ] **Step 2: Upgrade card surfaces to feel more product-like and less box-like**

Adjust shared card styles so they improve:

```text
Corner radius consistency
Default border subtlety
Padding rhythm
Hover surface clarity
Shadow use on elevated surfaces
```

Do not create page-specific card variants in views unless reuse analysis proves they are necessary.

- [ ] **Step 3: Refine overlay and panel depth**

Tune panel-related styles so:

```text
SidePanel feels clearly above page content
FloatingPanel reads as a premium utility surface
Backdrop dimming supports hierarchy without becoming heavy
```

Preserve current interaction behavior and visibility rules.

- [ ] **Step 4: Run a build after card and panel refinements**

Run:

```powershell
rtk dotnet build
```

Expected: build succeeds with `0 Error(s)`.

## Task 3: Refine shared form and button systems

**Files:**
- Modify: `D:/03_Projects/EapWorkAssistant/Resources/Styles.xaml`

- [ ] **Step 1: Inspect the form and button style set**

Review these styles before changing them:

```text
Input
TextArea
Select
Label
SearchInput
BtnPrimary
BtnSecondary
BtnSuccess
BtnWarning
BtnDanger
BtnGhost
```

- [ ] **Step 2: Standardize control sizing and input rhythm**

Refine shared form controls so they produce more consistent product rhythm through:

```text
Input height
Internal padding
Focus border / focus surface behavior
Label spacing
Search field visual integration
```

Keep the semantics of `Input`, `TextArea`, and `Select` unchanged.

- [ ] **Step 3: Refine button hierarchy and interaction feedback**

Tune the shared button system so:

```text
Primary buttons feel more focused
Secondary buttons stay restrained
Ghost buttons remain clickable but lighter
Danger / warning / success states remain semantic without overpowering the palette
```

Preserve existing button resource keys and intended usage.

- [ ] **Step 4: Run a build after form and button refinements**

Run:

```powershell
rtk dotnet build
```

Expected: build succeeds with `0 Error(s)`.

## Task 4: Refine shared table and tooltip systems

**Files:**
- Modify: `D:/03_Projects/EapWorkAssistant/Resources/Styles.xaml`

- [ ] **Step 1: Inspect the current table-related shared styles**

Review:

```text
ModernGrid
ModernGridColumnHeader
ModernGridCell
ModernGridRow
GridTextTrim
ModernToolTip
```

- [ ] **Step 2: Improve table rhythm and hierarchy**

Refine the shared table system so:

```text
Headers read as information partitions
Cell padding and row height feel more deliberate
Hover and selected states stay refined
Text-heavy rows remain comfortable to scan
```

Do not introduce page-local inline table styles unless the shared system cannot cover the need.

- [ ] **Step 3: Refine truncated-text preview consistency**

Tune `ModernToolTip` and related table presentation details so the preview experience feels like part of the same design system. Keep existing behavior contracts intact, especially where `DataGridCopyHelper` already owns content-preview behavior.

- [ ] **Step 4: Run a build after table refinements**

Run:

```powershell
rtk dotnet build
```

Expected: build succeeds with `0 Error(s)`.

## Task 5: Calibrate high-frequency pages only where shared changes need local alignment

**Files:**
- Modify: `D:/03_Projects/EapWorkAssistant/Views/DashboardView.xaml`
- Modify: `D:/03_Projects/EapWorkAssistant/Views/WorkRecordView.xaml`
- Modify: `D:/03_Projects/EapWorkAssistant/Views/IssueView.xaml`
- Modify: `D:/03_Projects/EapWorkAssistant/Views/KnowledgeView.xaml`
- Modify: `D:/03_Projects/EapWorkAssistant/Views/SettingsView.xaml`

- [ ] **Step 1: Review whether shared style changes already solve the page-level issues**

Inspect the target pages and only prepare edits when shared style updates create obvious local mismatches in:

```text
Card padding rhythm
Form spacing
Grouped section spacing
Table container breathing room
```

- [ ] **Step 2: Make only small alignment edits in target views**

If a page needs local tuning, keep it limited to:

```text
Margin
Padding
Shared style usage cleanup
Minor container alignment
```

Do not redesign layout structure, replace controls, or create unrelated new inline styles.

- [ ] **Step 3: Run a build after page calibrations**

Run:

```powershell
rtk dotnet build
```

Expected: build succeeds with `0 Error(s)`.

## Task 6: Final verification and polish review

**Files:**
- Verify: `D:/03_Projects/EapWorkAssistant/Resources/Styles.xaml`
- Verify: `D:/03_Projects/EapWorkAssistant/Views/DashboardView.xaml`
- Verify: `D:/03_Projects/EapWorkAssistant/Views/WorkRecordView.xaml`
- Verify: `D:/03_Projects/EapWorkAssistant/Views/IssueView.xaml`
- Verify: `D:/03_Projects/EapWorkAssistant/Views/KnowledgeView.xaml`
- Verify: `D:/03_Projects/EapWorkAssistant/Views/SettingsView.xaml`

- [ ] **Step 1: Run the full build one last time**

Run:

```powershell
rtk dotnet build
```

Expected: build succeeds with `0 Error(s)`.

- [ ] **Step 2: Review the final diff for scope discipline**

Run:

```powershell
rtk git diff -- Resources/Styles.xaml Views/DashboardView.xaml Views/WorkRecordView.xaml Views/IssueView.xaml Views/KnowledgeView.xaml Views/SettingsView.xaml docs/superpowers/specs/2026-06-16-global-style-system-refinement-design.md docs/superpowers/plans/2026-06-16-global-style-system-refinement.md
```

Expected: changes stay focused on shared style refinement and small page-level alignment.

- [ ] **Step 3: Prepare the delivery summary**

Summarize:

```text
修改了什么
为什么这样改
影响哪些文件
如何验证
```
