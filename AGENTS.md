# AGENTS.md — EAP Work Assistant 编程约束文件

> 本文件定义 AI 辅助编程时必须遵守的全部约束与规范。
> - **第 1–8 节**为「通用编码规范」，面向本项目所有代码（人工与 AI 生成均需遵守）。
> - **第 9 节**为「AI 行为约束红线」，是 AI 生成/修改代码的硬性规则；与前面各节冲突时以第 9 节为准。
> - **第 10 节**为「自查清单」，每次生成或修改代码后逐条核对。
>
> 本文档基于真实代码库核对（v2.1.x，.NET 10 / WPF / CommunityToolkit.Mvvm 8.4.2），所有令牌名、服务名、资产名均以代码现状为准，禁止臆造。

## 目录

1. [项目概述](#1-项目概述)
2. [目录结构与命名规范](#2-目录结构与命名规范)
3. [架构与编码原则](#3-架构与编码原则)
4. [数据模型字段参考](#4-数据模型字段参考)
5. [WPF / XAML 规范](#5-wpf--xaml-规范)
6. [事件处理与对话框](#6-事件处理与对话框)
7. [数据库约束](#7-数据库约束)
8. [常用模式（Cookbook）](#8-常用模式cookbook)
9. [AI 行为约束红线](#9-ai-行为约束红线)
10. [自查清单](#10-自查清单)

---

## 1. 项目概述

- **项目名称**：EAP Work Assistant v2.1
- **技术栈**：.NET 10 (`net10.0-windows`) + WPF + CommunityToolkit.Mvvm 8.4.2 + SQLite/Dapper + LiveCharts2
- **架构模式**：MVVM（Model-View-ViewModel）
- **语言**：C# 12+，UI 使用中文标签

---

## 2. 目录结构与命名规范

### 2.1 目录结构

```
EapWorkAssistant/
├── Data/                    # 数据库初始化（仅 DatabaseInitializer.cs）
├── Helpers/                 # 工具类、扩展方法与基础设施（Logger / DateTimeHelper / UiTimer 等）
│   └── Converters/          # IValueConverter 实现（namespace 仍为 EapWorkAssistant.Helpers）
├── Models/                  # 纯数据模型（POCO）
├── Repositories/            # 数据访问层（Dapper + SQLite）
├── Resources/               # XAML 资源字典（DesignTokens.xaml / Styles.xaml）
├── Services/                # 业务逻辑服务（单例模式）
├── ViewModels/              # 视图模型（CommunityToolkit.Mvvm）
├── Views/                   # XAML 视图 + code-behind
├── Controls/                # 自定义控件（代码型派生控件如 DataGridBase + XAML 型可复用 UserControl 如 CustomCalendar/StatCard/PaginationControl/MarkdownViewer）
├── App.xaml / App.xaml.cs   # 应用入口
└── EapWorkAssistant.csproj  # 项目文件
```

**规则：**
- 新增文件必须放入对应目录，不得在根目录随意创建。
- 转换器（IValueConverter）放入 `Helpers/Converters/`，不得放入 `ViewModels/` 或 `Views/`。
  - 其 namespace **固定为 `EapWorkAssistant.Helpers`**（有意与文件夹不同名）：XAML 里已有大量
    `clr-namespace:EapWorkAssistant.Helpers` 引用，改 namespace 会牵动全部视图，故保持不变。
- 服务类放入 `Services/`，数据仓储放入 `Repositories/`，自定义控件放入 `Controls/`。
- 不要在 `Views/*.xaml.cs` 中编写业务逻辑，仅处理 UI 交互。

### 2.2 文件与类命名

| 类型 | 命名 | 示例 |
|------|------|------|
| 模型类 | 名词，PascalCase | `WorkRecord`, `Knowledge`, `Issue` |
| 视图模型 | `XxxViewModel` | `WorkRecordViewModel` |
| 视图 | `XxxView.xaml` / `.xaml.cs` | `DashboardView.xaml` |
| 服务类 | `XxxService` | `ToastService` |
| 仓储类 | `XxxRepository` | `WorkRecordRepository` |
| 转换器 | `XxxConverter` | `IssueStatusConverter` |
| 对话框 | `XxxDialog.xaml` | `ConfirmDialog.xaml` |
| 自定义控件 | `Xxx`（继承既有控件） | `DataGridBase` |

### 2.3 变量与方法命名

- 公开成员：PascalCase（`LoadData()`, `SelectedItem`）
- 私有字段：`_camelCase`（`_selectedItem`, `_isLoading`）
- 参数和局部变量：camelCase（`workRecord`, `itemCount`）
- 常量：PascalCase 或 `UPPER_SNAKE_CASE`
- 事件处理程序：`Xxx_Yyy` 格式（`Button_Click`, `DefaultView_Changed`）

### 2.4 XAML 命名

- `x:Name` 仅在 code-behind 需要引用时添加，不得随意命名。
- `x:Key` 使用 PascalCase（`CardStyle`, `PrimaryBrush`）。
- Style 命名遵循「功能 + 控件类型」模式（`Select` 用于 ComboBox，`Card` 用于 Border）。

---

## 3. 架构与编码原则

### 3.1 MVVM 原则

1. **ViewModel 不得引用 View**：ViewModel 中不得出现 `using System.Windows` 或任何 WPF UI 类型。
2. **DataContext 绑定**：View 的 DataContext 在 code-behind 构造函数中设置。
3. **数据绑定优先**：UI 状态通过绑定 ViewModel 属性控制，不在 code-behind 中直接操作 UI 元素（除非涉及动画、Storyboard 等 WPF 特有功能）。
4. **命令 vs 事件**：简单交互用 Command 绑定，复杂 UI 交互（动画、自定义控件行为）可用 code-behind 事件。

### 3.2 CommunityToolkit.Mvvm 用法

- 使用 `[ObservableProperty]` 标注属性，不使用手写 INotifyPropertyChanged。
- 属性变更回调使用 `partial void OnXxxChanged(T value)` 模式。
- 使用 `[RelayCommand]` 标注命令方法（包括异步方法）。
- **禁止**在同一类中混合手写 INPC 和 Source Generator 特性。

### 3.3 服务层规范（单例）

- 全局服务使用**单例模式**：`ThemeService.Instance`, `ConfigService.Instance`, `ProfileService.Instance`, `ToastService`（静态类，无 `.Instance`）。
- 单例通过 `public static XxxService Instance { get; } = new();` 实现。
- 服务初始化在 `App.xaml.cs` 的 `OnStartup` 中完成。
- 不要在 View 或 ViewModel 中直接 `new` 服务实例；保持单例不被破坏（禁止改为依赖注入等其他模式）。

### 3.4 数据访问层规范

- 使用 Dapper 进行 SQL 查询，不使用 Entity Framework。
- 数据库连接使用 `System.Data.SQLite`。
- Repository 方法使用异步（`async/await`）。
- SQL 查询使用参数化语句，禁止字符串拼接。

### 3.5 异步与状态

- 按钮 / 用户操作使用 `[RelayCommand]`（异步方法同样标注 `[RelayCommand]`）。
- 对可能重复触发的操作加防重：`IsBusy` / `IsRunning` 守卫，或 `Helpers/UiTimer.cs` 防抖。
- 列表 / 数据容器应实现 `Loading → Content → Empty → Error` 四态切换（见第 9 节）。
- 禁止 fire-and-forget 裸调用 `_ = SomeAsync()`，必须改用 `SomeAsync().SafeFire("描述")`（见 8.1）。

### 3.6 代码组织

- 所有新增类 / 接口放入对应命名空间（`Models` / `Services` / `ViewModels` / `Controls` / `Helpers` / `Data` / `Views`）。
- 禁止在一个 `.cs` 文件中塞入多个不相关的类（DTO / 枚举 / 小型辅助类除外）。
- 文件命名必须与包含的类名一致（如 `WorkRecordRepository.cs` 包含 `WorkRecordRepository` 类）。

### 3.7 提交规范

Commit Message 使用中文，格式 `type: description`，常用 type：

| type | 含义 |
|------|------|
| `feat:` | 新功能 |
| `fix:` | 修复 bug |
| `ui:` | UI / 样式调整 |
| `refactor:` | 重构（不改变功能） |
| `docs:` | 文档更新 |
| `chore:` | 构建 / 工具链 |

示例：
```
feat: 添加主题切换功能（深色/浅色模式）
ui: 优化设置页面 ComboBox 样式对齐
fix: 修复界面密度切换无效的问题
refactor: 提取 ThemeService 统一管理主题逻辑
```

---

## 4. 数据模型字段参考

### WorkRecord
| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| WorkDate | DateTime | 工作日期 |
| ProjectName | string | 项目名称 |
| WorkType | string | 工作类型 |
| Content | string | 工作内容 |
| Achievement | string | **工作成果**（v2.1 新增） |
| Problem | string | 遇到的问题 |
| Solution | string | 解决方案 |
| IsHighlight | int | 是否亮点（0/1） |
| Hours | double | 工时 |

### Issue
| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| ProjectName | string | 项目名称 |
| Description | string | 问题描述 |
| RootCause | string | 根因分析 |
| Solution | string | 解决方案 |
| Status | string | **状态**（v2.1 新增）：Open / InProgress / Resolved / Closed |
| Priority | string | **优先级**（v2.1 新增）：Low / Medium / High / Critical |

### Knowledge
| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| Title | string | 标题 |
| Content | string | 内容 |
| Tags | string | 标签（逗号分隔） |
| Category | string | **分类**（v2.1 新增） |
| IsFavorite | int | **是否收藏**（v2.1 新增，0/1） |

---

## 5. WPF / XAML 规范

### 5.1 DesignTokens 令牌体系（唯一来源）

- **定位**：所有颜色、圆角、字号、间距、密度、动效时长、尺寸令牌的唯一来源，定义在 `Resources/DesignTokens.xaml`。
- **加载顺序**：`App.xaml` 的 `MergedDictionaries` 中 `DesignTokens.xaml` **必须位于** `Styles.xaml` 之前（已确认）。因此任何样式均可引用令牌。
- **引用规则**：
  - 颜色 → `*Brush` 资源键
  - 圆角 → `RadiusXxx`
  - 字号 → `FontSizeXxx`
  - 间距 → `SpaceXxx` / `CellPad` / `GapTopXxs`
  - 密度（均为 `Thickness`）→ `DensityXxx`
  - 动效时长（可直接用于 `Duration=`）→ `DurationXxx`
  - 尺寸 → `IconButtonSize` 等
- **缺失令牌的处理**：先去 `DesignTokens.xaml` 补齐定义再使用，**禁止**在页面硬编码。

#### 可用令牌速查

**颜色画刷**

| 令牌 | 用途 |
|------|------|
| `PrimaryBrush` / `PrimaryHoverBrush` / `PrimaryLightBrush` | 主色 / 悬停 / 极淡主色背景 |
| `SecondaryBrush` | 次要色 |
| `SuccessBrush` / `SuccessLightBrush` | 成功 / 极淡成功背景 |
| `WarningBrush` / `WarningLightBrush` | 警告 / 极淡警告背景 |
| `DangerBrush` / `DangerLightBrush` | 危险 / 极淡危险背景 |
| `InfoBrush` | 信息色 |
| `SurfaceBrush` / `SurfaceHoverBrush` / `SurfaceAltBrush` | 表面 / 悬停表面 / 替代表面 |
| `CardBrush` | 卡片背景（白底） |
| `TextPrimaryBrush` / `TextSecondaryBrush` / `TextTertiaryBrush` / `TextHintBrush` | 文本主/次/弱/提示（#999999） |
| `BorderBrush` / `BorderHoverBrush` | 边框 / 悬停边框（极淡灰） |
| `TextOnPrimaryBrush` / `OnPrimaryTextBrush` | 主色上的文字 |
| `AccentIndigoBrush` / `AccentVioletBrush` | 强调色（侧栏渐变等） |
| `SidebarBgBrush` / `SidebarTextBrush` / `SidebarHoverBrush` / `SidebarCardBgBrush` | 侧栏相关 |
| `ScrollThumbBrush` / `ScrollThumbHoverBrush` / `ScrollThumbActiveBrush` | 滚动条 |
| `OverlayMaskBrush` | 遮罩层 |
| `CalendarOverlayBrush` | 日历浮层 |

> 🚫 **不存在的令牌（禁止臆造）**：`BackgroundBrush` / `CardBackgroundBrush` / `HoverBrush` / `SelectedBrush`。

**圆角**

| 令牌 | 值 | 用途 |
|------|------|------|
| `RadiusHair` | 1 | 发丝角 |
| `RadiusXxs` | 4 | 小圆角 |
| `RadiusXs` | 6 | 标签等 |
| `RadiusSm` | 8 | 输入框 / Toast |
| `RadiusMd` | 10 | 按钮 / 普通卡片 |
| `RadiusLg` | 12 | — |
| `RadiusCard` | 16 | 卡片 / 模态 |
| `RadiusXl` | 22 | 大模态 |
| `RadiusPill` | 9999 | 胶囊 / 圆形 |
| `RadiusLeftCard` | 16,0,0,16 | 左侧单边圆角 |
| `RadiusSidePanel` | 18,0,0,18 | 右侧滑入面板 |
| `RadiusPopup` | 14 | 浮层弹窗 |

**间距（Thickness）**

| 令牌 | 值 |
|------|------|
| `SpaceXxs` | 2 |
| `SpaceXs` | 4 |
| `SpaceSm` | 8 |
| `SpaceMd` | 12 |
| `SpaceLg` | 16 |
| `SpaceXl` | 24 |
| `CellPad` | 10,6 |
| `GapTopXxs` | 0,3,0,0 |

> 🚫 前缀是 **`Space`** 不是 `Spacing`；不存在 `SpacingPage` / `SpacingSection` / `SpacingItem` / `SpacingMd`。

**密度（Thickness）**

| 令牌 | 值 |
|------|------|
| `DensityCardPad` | 24 |
| `DensityRowPad` | 20,14 |
| `DensityRowPadSm` | 18,11 |
| `DensityListPad` | 14,12 |
| `DensitySectionMargin` / `DensityRowMargin` / `DensityItemMargin` | 见定义 |

> 🚫 不存在 `DensityPagePad`。

**字号（sys:Double）**

| 令牌 | 值 | 令牌 | 值 |
|------|------|------|------|
| `FontSizeMicro` | 10 | `FontSizeLg` | 15 |
| `FontSizeXxs` | 10 | `FontSizeLead` | 14 |
| `FontSizeXs` | 11 | `FontSizeSection` | 16 |
| `FontSizeCaption` | 11 | `FontSizeSubtitle` | 17 |
| `FontSizeSm` | 12 | `FontSizeXl` | 18 |
| `FontSizeMd` | 13 | `FontSizeTitle` | 20 |
| `FontSizeBody` | 13 | `FontSizeDisplay` | 26 |
| `FontSizeBodySm` | 12.5 | | |

> 🚫 不存在 `FontSizeHero`；大标题用 `FontSizeDisplay` 或 `FontSizeTitle`。

**动效时长（sys:String，可直接用于 `Duration=`）**

| 令牌 | 值 |
|------|------|
| `DurationFast` | 0:0:0.15 |
| `DurationNormal` | 0:0:0.25 |
| `DurationSlow` | 0:0:0.40 |

**尺寸令牌**

| 令牌 | 值 | 令牌 | 值 |
|------|------|------|------|
| `IconButtonSize` | 34 | `SwitchThumbSize` | 20 |
| `ControlMinHeight` | 40 | `PagerSize` | 32 |
| `ControlMinHeightSm` | 36 | `ToolTipMaxWidth` | 400 |
| `InputHeight` | 42 | `IllustrationSize` | 72 |
| `SwitchTrackWidth` | 44 | `StatusDotSize` | 6 |
| `SwitchTrackHeight` | 24 | | |

### 5.2 样式与资源引用规则

- 所有全局样式定义在 `Resources/Styles.xaml` 中。
- 运行时可变的值使用 `DynamicResource`，静态值使用 `StaticResource`。
- 颜色使用 `SolidColorBrush` 类型的资源键，不要直接写十六进制色值。
- **Storyboard 中不能使用 DynamicResource**（WPF 冻结限制），动画目标值必须硬编码。
- **资源字典内禁止后向引用**：同一 `ResourceDictionary` 中，`{StaticResource ...}` 只能引用**已定义在前方**的资源。若资源 A 需要引用资源 B，则 B 必须排在 A 之前。跨 `MergedDictionaries` 时，先合并的字典中的资源可被后合并的字典引用；反之不行。
- **资源字典禁止重复 key**：`ResourceDictionary` 不允许同名 `x:Key`，重复 key 会在启动时 `DeferrableContent` 延迟加载阶段抛出 `XamlParseException`（表现常定位到 `App.xaml` 的 `MergedDictionaries` 行，而非真实出错文件）。对资源做「挪位类」重构（把条目从 X 处移到 Y 处）时，**必须 grep 确认旧位置已清空**，否则会留下两份同名定义。改动后建议 `grep -oE 'x:Key="[^"]+"' <文件> | sort | uniq -c | awk '$1>1'` 复查。
- **动效时长资源必须用 WPF 原生 `Duration` 类型**：动画相关 `DurationFast` / `DurationNormal` / `DurationSlow` 等令牌**禁止声明为 `sys:String`**（虽然字面量形如 `"0:0:0.25"` 可被 `DurationConverter` 解析，但通过 `{StaticResource}` 取出后类型是 `string`，塞给 `Timeline.Duration` 时类型不匹配会抛 `ArgumentException`，且编译期 `StaticResource` 不校验此类错误，运行时才暴露）。必须使用 WPF 原生 `Duration` 元素：`<Duration x:Key="DurationNormal">0:0:0.25</Duration>`，与 `Color` / `CornerRadius` / `Thickness` 同族，无需额外命名空间前缀。
- **依赖属性优先级陷阱**：XAML 中本地值（precedence 3）优先于 Style Trigger（precedence 5）。需要 DataTrigger 动态切换的属性，默认值必须写在 Style Setter 中，不能写在元素标签上：

```xml
<!-- ❌ 错误：DataTrigger 永远无法覆盖本地值 -->
<Border IsHitTestVisible="False">
    <Border.Style>
        <Style TargetType="Border">
            <Style.Triggers>
                <DataTrigger ...><Setter Property="IsHitTestVisible" Value="True"/></DataTrigger>
            </Style.Triggers>
        </Style>
    </Border.Style>
</Border>

<!-- ✅ 正确：默认值在 Style Setter 中 -->
<Border>
    <Border.Style>
        <Style TargetType="Border">
            <Setter Property="IsHitTestVisible" Value="False"/>
            <Style.Triggers>
                <DataTrigger ...><Setter Property="IsHitTestVisible" Value="True"/></DataTrigger>
            </Style.Triggers>
        </Style>
    </Border.Style>
</Border>
```

### 5.3 主题系统

- 主题管理统一由 `ThemeService` 处理。
- 支持三档字号（Small/Medium/Large）：通过 `LayoutTransform` + `ScaleTransform` 实现全局缩放。
- 支持三档密度（Compact/Default/Comfortable）：通过 `Thickness` 类型的 DynamicResource 实现。
- 密度资源键必须为 `Thickness` 类型，**不得**使用 `sys:Double`（WPF 无法将 Double 自动转换为 Thickness）。
- 字号缩放应用于 `MainContentArea` Grid（MainWindow 中 `x:Name="MainContentArea"`）。
- 主题切换互斥：`SettingsViewModel` 中 `IsLightTheme` 与 `IsDarkTheme` 必须互斥联动（见 8.17）。

### 5.4 组件复用规范（先查后建）

**强制执行流程 — 任何新增样式或组件前，必须走完以下 3 步：**

1. **查**：先读取 `Resources/Styles.xaml` 全文，搜索是否已有满足需求的样式或模板。
2. **判**：评估已有样式是否能直接使用，或通过 `BasedOn` 扩展 / 局部覆盖满足需求。
3. **建**：仅当确实没有可复用的样式时，才创建新样式，且**必须定义在 `Resources/Styles.xaml` 中作为共享样式**，禁止在 View 中内联定义仅该页面使用的样式。

**判断决策树：**

```
需要新样式/组件？
├── Styles.xaml 中已有完全匹配的？→ 直接引用 StaticResource
├── 已有样式差一点能满足？→ 用 BasedOn 继承扩展，或加参数化变体
├── 已有类似功能但视觉不同？→ 评估是否可合并为一组变体（如 Tag/TagPrimary/TagSuccess）
└── 确认不存在任何可复用的？→ 在 Styles.xaml 中新建共享样式，附带中文注释说明用途
```

**禁止行为：**
- ❌ 在 View 的 `<UserControl.Resources>` 中定义仅该页面使用的样式（除非确实全局唯一且不会被复用）。
- ❌ 在不同 View 中复制粘贴相同的内联样式而不抽取为共享样式。
- ❌ 创建与 `Styles.xaml` 中已有样式功能重复的新样式。
- ❌ 硬编码颜色值、圆角值、阴影参数（必须引用令牌 / 资源键）。

### 5.5 可复用组件清单

**`Controls/` 目录（自定义控件：代码型派生控件 + XAML 型可复用 UserControl）：**

- `DataGridBase`（`Controls/DataGridBase.cs`）：`: DataGrid` 派生，自动套用 `ModernGrid` 样式 + 列宽记忆（`TableKey`）。所有 DataGrid 必须用此类。
- `CustomCalendar`（`Controls/CustomCalendar.xaml`）：日期选择，支持月份导航、有记录日期标记、今日快捷。所有需要日期选择的场景必须使用此组件，配合浮窗覆盖层模式。
- `StatCard`（`Controls/StatCard.xaml`）：统计卡片（图标 + 数值 + 标签），默认 `CardElevated` 样式。
- `PaginationControl`（`Controls/PaginationControl.xaml`）：统一分页（支持 Simple / Numbered）。
- `MarkdownViewer`（`Controls/MarkdownViewer.xaml`）：Markdown 渲染查看器，绑定 `MarkdownText`。

**`Views/` 目录（对话框 / 窗口型可复用组件）：**

- `ConfirmDialog`（`Views/ConfirmDialog.xaml`）：确认对话框，支持 Danger/Warning/Info 类型。
- `ConfigItemDialog`（`Views/ConfigItemDialog.xaml`）：配置项编辑对话框。
- `ProfileDialog`（`Views/ProfileDialog.xaml`）：个人信息编辑对话框。

> 在消费方 XAML 中引用 `Controls/` 下的控件，统一使用 `xmlns:controls="clr-namespace:EapWorkAssistant.Controls"` 前缀（如 `<controls:StatCard>`）。

### 5.6 可复用样式清单（Resources/Styles.xaml）

| 样式键 | 目标控件 | 用途 |
|---|---|---|
| `BtnPrimary` / `BtnSecondary` / `BtnSuccess` / `BtnWarning` / `BtnDanger` | Button | 各语义操作按钮 |
| `BtnGhost` | ButtonBase | 幽灵 / 文字按钮（透明背景） |
| `Input` / `TextArea` / `SearchInput` | TextBox | 单行 / 多行 / 无边框搜索输入框 |
| `Select` | ComboBox | 下拉选择框（含透明覆盖层也必须套用） |
| `Label` | TextBlock | 表单字段标签 |
| `Heading1/2/3` | TextBlock | 三级标题样式 |
| `Card` / `CardElevated` / `CardHover` | Border | 卡片容器（普通 / 带阴影 / 可交互） |
| `Tag` / `TagPrimary/Success/Warning/Danger` | Border | 状态标签（彩色圆角小标签） |
| `ModernToolTip` | ToolTip | **全局隐式样式**（深色紧凑 + 自动换行），无需指定 `Style` |
| `ModernGrid` | DataGrid | 数据表格基础样式（内置 `DataGridCopyHelper.EnableCopy`） |
| `ModernGridColumnHeader` / `ModernGridCell` / `ModernGridRow` | 表头 / 单元格 / 行 | 表格三件套 |
| `WorkTypeBadgeCell` / `ProgressBarCell` / `HighlightStarCell` | DataTemplate | 工作类型标签 / 进度条 / 亮点星标单元格 |
| `GridTextTrim` | TextBlock | DataGrid 文本截断基础样式，列 `ElementStyle` 用 `BasedOn` 继承后加 ToolTip |
| `ModernScrollBar` / `ModernScrollViewer` | ScrollBar/ScrollViewer | 现代化滚动条 |
| `OverlayBackdrop` / `CalendarOverlay` | Border | 表单 / 日历遮罩层 |
| `SidePanel` / `FloatingPanel` | Border | 右侧滑入面板 / 浮层弹出面板 |
| `SectionExpander` | Expander | 现代化折叠面板 |
| `ToggleSwitch` | ToggleButton | 开关控件 |
| `DayCheckBox` | CheckBox | 休息日选择复选框 |
| `PageNumberButton` / `PageNumberActive` / `PageNavButton` | Button | 分页页码 / 翻页按钮 |
| `Divider` / `DragSplitter` | Separator/GridSplitter | 分隔线 / 拖拽分割条 |
| `StatusDot` / `IconCircle` | Ellipse/Border | 圆点状态指示 / 圆形图标背景 |
| `NavButton` / `ToggleChip` | RadioButton | 侧栏导航 / 切换芯片按钮 |

**统一引用规则：**
- 按钮统一使用 `BtnPrimary` / `BtnSecondary` / `BtnSuccess` / `BtnWarning` / `BtnGhost` / `BtnDanger`。
- ComboBox 统一使用 `Select` 样式（包括 `Opacity="0"` 的透明覆盖层）。
- TextBox 统一使用 `Input`（单行）/ `TextArea`（多行）；搜索输入框使用 `SearchInput`。
- ToolTip 为全局隐式样式，直接使用 `<ToolTip Content="..."/>` 或 `ToolTip="{Binding Field}"` 即可，**无需指定 Style**。
- DataGrid 必须使用 `ModernGrid` + `ModernGridColumnHeader` + `ModernGridCell` + `ModernGridRow`。
- 禁止使用 WPF 原生控件：当项目已有自定义组件时，必须优先复用（如 `DatePicker`、`Calendar` 等不得引入）。

### 5.7 卡片边框规范（2026-08-26 新增）

- **普通卡片**：无边框，仅用 `CardBrush` 背景 + `RadiusMd` 圆角 + `SpaceMd` 内边距（靠 `Style="{StaticResource Card}"` 实现，自带 1px `BorderBrush` 极淡边框，与 `StatCard` / `CardHover` 等卡片家族一致）。
- **强调卡片**（如统计数据 `StatCard`）：无边框，可使用主色 / 成功的极淡变体（`PrimaryLightBrush` / `SuccessLightBrush` 等）作为背景。
- **分隔性卡片**（如提示区）：可用 `1px` 极淡 `BorderBrush` 作为分隔，**禁止 2px 及以上边框**。
- **禁止彩色边框**作为强调手段（如蓝色 / 红色描边），除非是错误状态（`DangerBrush`）或警告状态（`WarningBrush`）。
- ⚠️ 不要用 `AccentBannerGradient` 之类的彩色渐变做「边框 / 外框」包裹 —— dashboard 的「今日记录」「试用期进度」卡片已移除该渐变外框，改用标准 `Card` 样式（统一 `RadiusMd` 圆角 + `SpaceMd` 内边距）。
- ⚠️ 内边距统一引 `SpaceMd`，不存在 `SpacingMd` / `CardBackgroundBrush`。

### 5.8 表格 / 列表规范（DataGridBase）

- 所有 `DataGrid` / `ListView` 必须继承 **`Controls/DataGridBase`**（统一套用 `ModernGrid` 样式 + 启用智能列宽 `SmartColumns`），不得再使用原生 `<DataGrid>` 或仅套 `ModernGrid` 样式。
- XAML 引用需 `xmlns:controls="clr-namespace:EapWorkAssistant.Controls"`。
- 每张表必须设置唯一 `TableKey`（如 `WorkRecord` / `RecycleBin` / `Issue` / `Dashboard` / `WorkRecordAll`），作为列宽记忆在 `ColumnWidthStore`（本地 JSON）中的存储维度。
- **列宽记忆已落地**：`DataGridSmartColumns` 在用户拖拽列宽后，将像素宽度持久化到 `%LOCALAPPDATA%/EapWorkAssistant/columnwidths.json`，下次打开 / 数据刷新时自动恢复。**约束：所有表格列均应使用固定像素宽度（`Width="N"`），不得声明为 `Auto` 或 `*`（`Star`）**——`Auto`/`Star` 列 WPF 不支持分隔线拖拽，会导致整列（乃至整表）列宽无法调整。回收站表格已全部改为固定像素（类型 110 / 标题摘要 360 / 删除时间 160 / 操作 140），与「工作记录·全部记录」表行为一致。
- 表格必须配合 `ModernGridColumnHeader` / `ModernGridCell` / `ModernGridRow`，并启用 `DataGridCopyHelper`（ModernGrid 已内置）实现长文本悬停预览。
- 列宽拖拽：`ModernGrid` / `DataGridBase` 已支持 `CanUserResizeColumns="True"`。

### 5.9 控件陷阱

**ToggleButton 陷阱**：WPF `ToggleButton` 的 `Click` 事件会在 `IsChecked` 自动切换**之后**触发；若在 Click 中手动设置 `IsChecked` 会导致双重切换。
- **正确做法**：使用 `PreviewMouseLeftButtonDown` 事件，设置 `e.Handled = true` 阻止默认行为，再手动控制 `IsChecked`：

```csharp
private void SomeButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    e.Handled = true;
    var btn = (ToggleButton)sender;
    btn.IsChecked = !(btn.IsChecked == true);
    // 后续逻辑...
}
```

**ComboBox 样式**：所有 ComboBox 必须使用 `Style="{StaticResource Select}"` 统一样式，**包括不可见覆盖层**（即使 `Opacity="0"`，否则弹出面板会用默认 WPF 样式）。需要中文显示标签但绑定英文值时，使用 `ItemTemplate` + `IValueConverter`。

**x:Name 与编译**：在 XAML 中添加 `x:Name` 会在 `.g.cs` 自动生成字段；增量编译可能导致 `.g.cs` 未更新，出现「当前上下文中不存在名称」错误。**修复方法**：执行 `dotnet clean` 后重新 `dotnet build`。

### 5.10 通用工具与帮助类

通用工具方法放在 `Helpers/` 目录，按功能分类；禁止在 ViewModel 或 Service 中重复实现相同逻辑。

| 类 / 资产 | 状态 | 说明 |
|------|------|------|
| `Helpers/WorkRecordIdentityHelper.cs` | ✅ 存在 | UniqueId 生成与匹配 |
| `Helpers/DateTimeHelper.cs` | ✅ 存在 | 日期格式化、相对时间 |
| `Helpers/TaskExtensions.cs`（`SafeFire`） | ✅ 存在 | 异步异常统一处理（见 8.1） |
| `Helpers/UiTimer.cs` | ✅ 存在 | 防抖计时器（列表搜索即用） |
| `Helpers/DataGridSmartColumns.cs` + `Helpers/ColumnWidthStore.cs` | ✅ 存在 | 智能列宽 + 列宽记忆 |
| `Helpers/DataGridCopyHelper.cs` | ✅ 存在 | DataGrid 悬停预览 + 右键复制 |
| `Helpers/CalendarHelper.cs` | ✅ 存在 | 日历浮窗定位与关闭（`Show` / `Close`） |
| `Helpers/DrawerHelper.cs` | ✅ 存在 | 抽屉面板动画（`OpenDrawer` / `CloseDrawer`） |
| `Highlight` | ✅ 存在 | 搜索关键词高亮附加属性 |
| `PreviewPopup` | ✅ 存在 | 通用悬停预览弹出层（单例） |
| `CopyButton` | ✅ 存在 | 无边框文字链风格复制按钮 |
| 字符串归一化 / 哈希 | ⚠️ 散落 | 目前以 Converter 形式（`StringEqualsConverter` 等）散落，**尚无 `StringHelper.cs`**；新增字符串工具请直接建 `Helpers/StringHelper.cs` |
| 用户配置读写 | ✅ `Services/ConfigService.cs` | **非 `Helpers/ConfigHelper.cs`**；新增配置读写请复用 `ConfigService`，勿新建重复实现 |

**DataGrid 内容列悬停预览**：`ModernGrid` 内置 `DataGridCopyHelper.EnableCopy="True"`，为长文本列（Header 为 `内容` / `工作成果` / `问题` / `描述` / `根本原因` / `解决方案` / `关键词` / `标题`，或 Binding.Path 为 `Content` / `Achievement` / `Problem` / `Description` / `RootCause` / `Solution` / `Keywords` / `Title`）自动提供悬停预览（300ms 后弹 `PreviewPopup`，含完整文本 + 复制按钮）与右键复制。**不要**在这些列上额外设置 `TextBlock.ToolTip`，否则会出现两个浮窗。

**FindVisualParent 正确用法**：`DataGridCopyHelper.FindVisualParent<T>` 对 Visual 元素使用 `VisualTreeHelper.GetParent`（可穿越 DataTemplate 边界），对 ContentElement（如 `Run`）先用 `LogicalTreeHelper.GetParent` 回到 `TextBlock` 再切到 VisualTree。**禁止在需要穿越 DataTemplate 时使用 `LogicalTreeHelper`**。

---

## 6. 事件处理与对话框

### 6.1 SettingsView 的特殊处理

- SettingsView 中的 ToggleButton 使用 `PreviewMouseLeftButtonDown` 而非 `Click`（原因见 5.9）。
- 字体 / 密度按钮在 code-behind 中更新视觉高亮（`UpdateFontSizeButtons()` / `UpdateDensityButtons()`），同时通过 ViewModel 属性触发 ThemeService 执行实际变更。
- 不要在 SettingsView code-behind 中直接调用 ThemeService，应通过 ViewModel 属性间接调用。

### 6.2 对话框

- 自定义对话框（`ConfirmDialog` / `ConfigItemDialog` / `ProfileDialog`）使用 `Window` 类型，`WindowStartupLocation="CenterOwner"`。
- 对话框通过 `ShowDialog()` 模态显示。
- 不要在对话框中直接操作主窗口状态。

---

## 7. 数据库约束

- 数据库文件位置：`%LOCALAPPDATA%\EapWorkAssistant\eapwork.db`
- 自动备份目录：`%LOCALAPPDATA%\EapWorkAssistant\backups\`
- 表结构变更必须在 `DatabaseInitializer.cs` 中更新建表语句**和迁移代码**。
- 新增列使用 `ALTER TABLE ... ADD COLUMN` 并带默认值，用 try/catch 包裹以兼容已升级的数据库。
- 新增索引使用 `CREATE INDEX IF NOT EXISTS` 语句，同样用 try/catch 包裹。
- 已有索引：`idx_workrecord_workdate`, `idx_workrecord_project`, `idx_issue_project`, `idx_issue_status`, `idx_knowledge_category`, `idx_knowledge_tags`, `idx_knowledge_isfavorite`
- 保留 30 天备份，备份文件名含日期。

**迁移代码模板：**
```csharp
// 新增列
try { migrateCmd.CommandText = "ALTER TABLE Xxx ADD COLUMN Yyy TEXT DEFAULT 'default'"; migrateCmd.ExecuteNonQuery(); } catch { }

// 新增索引
try { migrateCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_xxx_yyy ON Xxx(Yyy)"; migrateCmd.ExecuteNonQuery(); } catch { }
```

---

## 8. 常用模式（Cookbook）

### 8.1 SafeFire（异步异常统一处理）
`Helpers/TaskExtensions.cs` 提供 `Task.SafeFire(string errorMessage)` 扩展方法，用于安全触发 fire-and-forget 异步操作。内部 await Task 并用 try/catch + `ToastService.Error` 反馈异常。**所有 `_ = SomeAsync()` 调用都应替换为 `SomeAsync().SafeFire("描述")`**。

```csharp
// ✅ 正确
LoadAllRecordsAsync().SafeFire("加载记录失败");

// ❌ 错误（异常被静默吞掉）
_ = LoadAllRecordsAsync();
```

### 8.2 查询版本号（竞态防护）
`WorkRecordViewModel` 使用 `_queryGeneration` 整型计数器防止筛选器快速切换时旧查询结果覆盖新结果。每次 `LoadAllRecordsAsync` 调用时 `++_queryGeneration`，await 返回后检查 `if (gen != _queryGeneration) return;` 丢弃过期结果。

### 8.3 自动保存（DispatcherTimer）
WorkRecordViewModel 使用 `DispatcherTimer` 实现自动保存。关键约束：
- `StartAutoSaveTimer()` 根据 `ConfigService.Instance.AutoSaveInterval` 设置间隔。
- `PauseAutoSaveTimer()` 在离开工作记录页面时调用（`MainViewModel.NavigateTo` 中处理）。
- `_isAutoSaving` 标记防止重入：Tick handler 是 `async void`，如果 save 耗时超过 interval，第二次 Tick 会重入。
- 必须在 try/finally 中重置 `_isAutoSaving = false`。

### 8.4 图表点击导航（LiveCharts2）
DashboardViewModel 通过 `DataPointerDownCommand` 绑定实现图表点击事件。**命令参数类型为 `IEnumerable<ChartPoint>?`**（不是单个 `ChartPoint`），需用 `.FirstOrDefault()` 取出第一个点。柱状图使用 `point.Index` 推算日期跳转到工作记录；饼图使用 `point.Index` 从 `ProjectPieSeries` 数组获取项目名跳转。**注意**：LiveCharts2 的 `ChartPoint` 没有 `Series` 属性，必须通过索引访问外部系列数组。

### 8.5 Toast 错误反馈
`ToastService` 是静态类，方法直接调用（不需要 `.Instance`）：
```csharp
ToastService.Error($"保存失败：{ex.Message}");
ToastService.Success("问题已保存");
ToastService.Info("数据库已自动备份", "数据安全");
```

### 8.6 多关键词搜索
WorkRecordRepository.SearchAsync 支持空格分隔多关键词 AND 匹配。将输入按空格拆分，为每个关键词生成独立的 `LIKE` 条件并用 `AND` 连接。单关键词时退化为简单 LIKE 查询。

### 8.7 状态 / 优先级中文映射
`Helpers/IssueStatusConverter.cs` 包含 `IssueStatusConverter` 和 `IssuePriorityConverter`，将英文值映射为中文标签（如 Open → 待处理，Critical → 紧急）。XAML 中使用 `ItemTemplate` + 转换器实现中文显示、英文存储。

### 8.8 CSV 导入 / 导出
`ExportService` 使用**状态机解析器** `ParseCsvRows()` 处理 CSV，正确处理引号内的逗号、换行符和转义引号（`""`）。**禁止使用 `File.ReadAllLines`**，因为内容字段可能包含换行。导出时 `EscapeCsv` 对含逗号、引号、`\n`、`\r` 的字段加引号包裹。

### 8.9 报告内容解析（ParseContentItems）
`ReportService.ParseContentItems` 将多行文本拆分为 `(string Text, bool IsSubItem)` 列表。识别规则按优先级：
1. 顶层编号：`1、` `1.` `1．` `1)` `（1）` `(1)` → 主项
2. 符号标记：`-` `•` `·` `*` `+` → 子项
3. 字母编号：`a.` `b)` → 子项
4. 圆圈数字：`①` `②` `③` → 子项
5. 缩进识别：Tab 或 2+ 空格开头且无编号 → 子项
6. 其他文本：合并到上一条（续行处理）

### 8.10 数据库备份
`DatabaseBackupService` 使用 SQLite 原生备份流程，**禁止直接 `File.Copy` 活跃数据库**：
```csharp
srcConn.Open();
// 1. 将 WAL 日志刷入主文件
new SQLiteCommand("PRAGMA wal_checkpoint(FULL);", srcConn).ExecuteNonQuery();
// 2. 使用 SQLite backup API
srcConn.BackupDatabase(dstConn, "main", "main", -1, null, 0);
```

### 8.11 导航与自动保存协调
`MainViewModel.NavigateTo` 负责在视图切换时：
1. 离开 WorkRecord 时调用 `WorkRecord.PauseAutoSaveTimer()`
2. 进入 WorkRecord 时调用 `WorkRecord.StartAutoSaveTimer()`
3. 仅当 `CurrentView` 实际变化时才调用 `RefreshAsync()`，避免重复加载

### 8.12 主题切换互斥
`SettingsViewModel` 中 `IsLightTheme` 和 `IsDarkTheme` 必须互斥联动。在 `OnIsLightThemeChanged(true)` 中设置 `IsDarkTheme = false`，反之亦然。避免两个按钮同时高亮。

### 8.13 数字输入验证
工时（double）和进度（int）TextBox 使用 `PreviewTextInput` 事件限制输入：
```csharp
// 工时：允许数字和最多一个小数点
e.Handled = !double.TryParse(textBox.Text + e.Text, out _) && newText != ".";
// 进度：仅允许整数
e.Handled = !int.TryParse(e.Text, out _);
```

### 8.14 全局异常兜底（App.xaml.cs）
`App.xaml.cs` 注册了三类未捕获异常处理器，确保应用不会因意外异常直接崩溃：
- `DispatcherUnhandledException`：UI 线程异常，设置 `e.Handled = true` 阻止崩溃
- `AppDomain.CurrentDomain.UnhandledException`：非 UI 线程异常（通常无法恢复）
- `TaskScheduler.UnobservedTaskException`：Task 中未 await 的异常，调用 `e.SetObserved()`

所有异常统一由 `LogAndNotify(Exception ex)` 处理：
1. 写入日志文件 `%LOCALAPPDATA%\EapWorkAssistant\logs\error_{日期}.log`
2. 通过 `ToastService.Error` 通知用户

### 8.15 表单重置
知识库和问题跟踪的「新增」按钮在打开抽屉前必须重置表单：
```csharp
if (DataContext is KnowledgeViewModel vm)
{
    vm.CurrentItem = new Knowledge();
    vm.IsFormDirty = false;
}
```

---

## 9. AI 行为约束红线

> 本节是 AI 生成 / 修改代码的硬性规则，与前面各节冲突时以本节为准。本节整合了原「AI 操作红线（20 条）」与「EAP 适配版 10 条」，去重并核对真实代码库。前面各节已详述的技术细节，此处仅列规则并交叉引用，避免重复。

### 9.0 红线速查（硬性禁止，违反即无效）

| # | 禁止项 | 详述位置 |
|---|--------|----------|
| 1 | 删除用户文件（`rm` / `del` 等永久删除） | 通用安全 |
| 2 | 修改 `.csproj` 的 NuGet 包版本（除非用户明确要求） | 3.3 |
| 3 | 引入新的 NuGet 包（除非用户明确要求并确认） | 通用安全 |
| 4 | 更改项目目标框架（当前 `net10.0-windows`） | 1 |
| 5 | 破坏单例模式（改为依赖注入等） | 3.3 |
| 6 | 在 ViewModel 中引入 WPF 命名空间 | 3.1 |
| 7 | 硬编码颜色 / 圆角 / 字号 / 间距 / 动效时长 / 固定尺寸（图标等豁免除外） | 5.1 / 5.2 |
| 8 | 在 Storyboard 中使用 DynamicResource | 5.2 |
| 9 | 修改 XAML 时意外删除 Grid / ColumnDefinitions 等结构性标签 | 通用 |
| 10 | 修改后不验证编译通过 | 9.6 |
| 11 | 新增数据库字段不同步迁移代码 | 7 |
| 12 | fire-and-forget 裸调用 `_ = SomeAsync()` | 3.5 / 8.1 |
| 13 | 需要 DataTrigger 动态覆盖的属性写在元素本地值上 | 5.2 |
| 14 | 直接 `File.Copy` 活跃 SQLite 数据库 | 8.10 |
| 15 | 批量数据库写入不包裹事务 | 7 |
| 16 | 移除全局异常处理（App.xaml.cs 三类处理器） | 8.14 |
| 17 | 关键服务静默吞异常（必须 Toast 通知用户） | 3.3 / 8.14 |
| 18 | 创建重复样式（未先查 Styles.xaml） | 5.4 |
| 19 | 在 DataGridCopyHelper 管理的长文本列上设额外 ToolTip | 5.6 / 5.10 |
| 20 | 在需要穿越 DataTemplate 时使用 LogicalTreeHelper | 5.10 |
| 21 | 资源字典挪位重构后旧位置同名 key 未删除（重复 key 致启动崩溃） | 5.2 |

### 9.1 禁止臆测
- 遇到不确定的字段名、API 签名、表结构时，**必须先读源码**，不允许凭记忆补全。
- 涉及多文件调用链（View → ViewModel → Service）必须一次性读完完整链路再动手。
- 禁止生成 `// TODO: 这里需要...` 或 `// 请手动补充...` 的占位代码——要么完整实现，要么不做。

### 9.2 UI / 样式一致性（令牌以 DesignTokens.xaml 为准）
- 禁止硬编码颜色值、圆角、字号、间距、动效时长。
- 除图标、头像、状态圆点等**确需固定尺寸**的控件外，禁止在 XAML 写 `CornerRadius="8"`、`FontSize="14"`、`Margin="12"`、`Width="140"`、`Duration="200"` 等字面量。
- 所有值必须引用 `Resources/DesignTokens.xaml` 中的 `StaticResource`（见 5.1 完整令牌表）。
- 若 `DesignTokens.xaml` 缺少所需令牌，应**先在该文件补齐定义再使用**，禁止在页面硬编码。
- ⚠️ 不要臆造键名（如 `BackgroundBrush` / `CardBackgroundBrush` / `SpacingMd` / `FontSizeHero` 等均不存在）。

### 9.3 组件复用
- 禁止在多个页面重复写相同的 UI 结构（空状态占位、卡片、列表项模板、页面标题栏）。
- 任一 UI 模式出现两次以上，必须抽取为 `UserControl`（建议放 `Controls/` 目录）。
- **现状（已存在可复用资产）**：`Controls/StatCard.xaml`、`Controls/PaginationControl.xaml`、表格 `ModernGrid` 系列（5.6）。
- **待补（原规则称「已存在」，实际不存在，新写时优先抽取）**：
  - 空状态占位 `EmptyStateControl`（当前各页为内联 XAML）
  - 页面标题栏 `PageHeader`（当前各页为内联 XAML）
  - 仪表盘卡片 `DataCard`（当前用 `StatCard` 替代）
  - 👉 不要写「引用 `Controls/EmptyStateControl`」这类代码，它尚未存在。

### 9.4 异步与状态
- 按钮 / 用户操作使用 `[RelayCommand]`（异步方法同样标注），并对可能重复触发的操作加防重（`IsBusy` / `UiTimer`）。⚠️ 原规则写 `AsyncRelayCommand`，代码库实际用 `[RelayCommand]`，以代码库现有约定为准。
- 列表 / 数据容器应实现 `Loading → Content → Empty → Error` 四态：当前基线 `IsBusy`(Loading) + Empty 占位 + `ToastService` 报错为主；**Error 态未统一内嵌**，新增列表请补齐 Error 分支。
- 全局异常必须走 `Services/ToastService`（实际类名，**非 `ToastNotificationService`**），不允许 `MessageBox.Show` 直接暴露（调试除外）。当前代码库已无 `MessageBox.Show`。

### 9.5 数据安全
- 删除操作必须经「回收站」中转，禁止直接 `DELETE`（业务删除统一走软删除 / 入回收站）。✅ 现状符合。
- 清空回收站前必须弹二次确认弹窗（现有 `Views/ConfirmDialog.xaml` 可用）。✅ 现状符合。
- 关键表单（工作记录 / 问题跟踪编辑）提交前必须做前端校验，不合规数据阻止入库；校验错误高亮指示（红色边框 / 提示文字）并在 Toast 汇总。✅ 现状部分符合（WorkRecord 已含校验，其余表单按需补齐）。
- ⚠️ **回收站「30 天自动物理删除」当前未实现**。代码库仅有 `DatabaseBackupService.MaxBackupDays = 30`（这是备份清理，与回收站无关），回收站无自动过期定时任务。此条作为**目标规范**保留，需另立任务实现（建议在 `RecycleBinViewModel` 增加过期清理命令，并在应用启动 / 进入回收站时触发）。

### 9.6 测试与验证
- 修改核心逻辑（Repository 层、Import/Export 服务）后必须确保现有单元测试全部通过。
- 新增功能涉及复杂逻辑时，必须同步补充对应单元测试。
- 删除 / 修改公共 API 或公共控件前，必须先检查所有引用处，确保不破坏现有功能。
- **编译验证**：执行 `dotnet build -c Debug` 确认 0 error。仅当 `bin/Debug` 目录的编译产物正被其他进程占用（如 WPF 应用运行中、IDE 调试 / Hot-Reload 持锁）时，Windows 文件独占锁才会导致失败 / 卡顿；此时改用隔离输出 `dotnet build -c Debug -p:OutDir=bin/_verify_xxx/`（每次不同后缀）。CI / 远程沙箱（如 Codex）不共享本机运行实例，通常可直接 build。

### 9.7 代码组织
- 所有新增类 / 接口必须放在对应命名空间（3.6）。
- 禁止在一个 `.cs` 文件中塞入多个不相关的类（DTO / 枚举 / 小型辅助类除外）。
- 文件命名必须与包含的类名一致。

### 9.8 清理临时目录
- **允许清理（无需询问）**：`bin/` `obj/` `TestResults/` `_verify_*` `_temp_*` `*.tmp` `*.log` `*.cache`
- **禁止清理**：`*.cs` `*.xaml` `*.csproj` `*.sln` `Resources/` `Assets/` `Database/*.db` `*.sqlite` `.git/` `DesignTokens.xaml` `Styles.xaml`

### 9.9 表格 / 列表规范
- 所有 `DataGrid` / `ListView` 必须继承 `Controls/DataGridBase`（详见 5.8）。
- 每张表必须设置唯一 `TableKey`（列宽记忆维度）。
- 表格必须配合 `ModernGrid` 系列样式 + `DataGridCopyHelper`（长文本悬停预览）。
- ⚠️ 显式声明的 `Auto` / `*` 列会被尊重，不会强制套用固定宽度。

### 9.10 通用工具与帮助类
- 通用工具方法放在 `Helpers/`（5.10 现状表），禁止在 ViewModel 或 Service 中重复实现相同逻辑。
- 字符串工具尚无 `StringHelper.cs`，新增请直接建；用户配置读写走 `ConfigService`（非 `ConfigHelper`）。

---

## 10. 自查清单

> 每次生成或修改代码后，逐条核对。✅ 表示符合，❌ 表示需修正。

**基础规范**
- [ ] 新增文件是否放入了正确的目录？
- [ ] 命名是否符合规范（PascalCase / camelCase）？
- [ ] ViewModel 是否引入了 WPF 类型？
- [ ] 新增的颜色 / 间距是否使用了资源键而非硬编码？
- [ ] 编译是否通过（`dotnet build -c Debug` 0 error；bin/Debug 被占用则改用 `-c Debug -p:OutDir=bin/_verify_xxx/`）？
- [ ] 是否有遗留的 TODO 或临时调试代码？

**组件复用**
- [ ] 新增样式前是否已读取 `Resources/Styles.xaml`，确认无已有可复用样式？
- [ ] 新创建的样式是否已放入 `Styles.xaml` 作为共享样式（而非 View 内联）？
- [ ] 是否复用了已有自定义组件（CustomCalendar / ConfirmDialog / StatCard 等），而非引入 WPF 原生控件？
- [ ] DataGrid 是否继承了 `DataGridBase` 并设唯一 `TableKey`？ToolTip 是否为全局隐式样式（无需指定 Style）？
- [ ] ComboBox 是否使用了 `Select` 样式（包括 `Opacity=0` 的覆盖层）？
- [ ] 搜索框是否使用了 `SearchInput`？导航栏是否使用了 `NavButton`？
- [ ] 星标列是否使用了 `HighlightStarCell`？
- [ ] DataGrid 的长文本预览列是否**避免**设置额外 ToolTip（`PreviewPopup` 已提供）？
- [ ] 日历浮窗是否通过 `CalendarHelper.Show()` / `CalendarHelper.Close()` 管理？
- [ ] 需要穿越 DataTemplate 查找父控件时，是否使用了 `VisualTreeHelper`（而非 `LogicalTreeHelper`）？

**WPF / XAML**
- [ ] DynamicResource 是否用于了 Thickness 类型（而非 sys:Double）？
- [ ] Storyboard 中是否避免了 DynamicResource？
- [ ] 需要 DataTrigger 动态切换的属性是否写在 Style Setter 中（而非元素本地值）？
- [ ] DataTrigger 比较值类型是否与绑定源类型匹配？
- [ ] 是否引用了**真实存在**的 DesignTokens 令牌（未臆造键名）？

**数据库**
- [ ] 新增模型字段是否同步更新了 `DatabaseInitializer.cs` 的建表和迁移代码？
- [ ] 新增数据库索引是否使用了 `CREATE INDEX IF NOT EXISTS`？
- [ ] 批量写入是否包裹在事务中？
- [ ] 数据库备份是否使用了 WAL checkpoint + BackupDatabase API（而非 File.Copy）？

**异步与错误处理**
- [ ] 所有异步调用是否使用了 `.SafeFire()` 而非 `_ = ` 裸调用？
- [ ] 关键操作是否添加了 try/catch + Toast 错误反馈？
- [ ] 快速切换筛选器是否有竞态防护（查询版本号或 CancellationToken）？
- [ ] DispatcherTimer Tick handler 是否有重入保护？
- [ ] 全局异常处理是否完整（DispatcherUnhandledException + AppDomain + TaskScheduler）？
- [ ] 关键服务的 catch 块是否通知了用户（Toast），而非静默吞掉？

**数据校验**
- [ ] ViewModel SaveAsync 是否校验了所有必填字段？
- [ ] 数字输入 TextBox 是否添加了 PreviewTextInput 验证？
- [ ] CSV 解析是否正确处理了引号内逗号和换行符（使用状态机而非 ReadAllLines）？
- [ ] LiveCharts2 代码是否避免了访问 `ChartPoint.Series`（不存在的属性）？

**导航与生命周期**
- [ ] NavigateTo 是否仅在视图实际切换时才触发 RefreshAsync？
- [ ] 自动保存计时器在离开工作记录页面时是否暂停？
- [ ] 单例服务的事件订阅是否有对应的取消订阅路径？
- [ ] 知识库 / 问题跟踪「新增」按钮是否在打开抽屉前重置了表单？

**令牌 / 资产名核查**
- [ ] 是否误用了不存在的资产名（`EmptyStateControl` / `FontSizeHero` / `BackgroundBrush` / `SpacingMd` 等）？
- [ ] 修改核心逻辑 / 公共 API 后是否跑过测试？
