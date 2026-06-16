# AGENTS.md — AI 编程约束文件

本文件定义了 AI 辅助编程时必须遵守的约束和规范。所有 AI 代码生成、修改、重构操作均须遵循以下规则。

## 项目概述

- **项目名称**: EAP Work Assistant v2.1
- **技术栈**: .NET 9.0 + WPF + CommunityToolkit.Mvvm 8.4.2 + SQLite/Dapper + LiveCharts2
- **架构模式**: MVVM（Model-View-ViewModel）
- **语言**: C# 12+，UI 使用中文标签

## 目录结构约束

```
EapWorkAssistant/
├── Data/                    # 数据库初始化（仅 DatabaseInitializer.cs）
├── Helpers/                 # IValueConverter 实现和工具类
├── Models/                  # 纯数据模型（POCO）
├── Repositories/            # 数据访问层（Dapper + SQLite）
├── Resources/               # XAML 资源字典（Styles.xaml）
├── Services/                # 业务逻辑服务（单例模式）
├── ViewModels/              # 视图模型（CommunityToolkit.Mvvm）
├── Views/                   # XAML 视图 + code-behind
├── App.xaml / App.xaml.cs   # 应用入口
└── EapWorkAssistant.csproj  # 项目文件
```

**规则：**
- 新增文件必须放入对应目录，不得在根目录随意创建
- 转换器（IValueConverter）放入 `Helpers/`，不得放入 `ViewModels/` 或 `Views/`
- 服务类放入 `Services/`，数据仓储放入 `Repositories/`
- 不要在 `Views/*.xaml.cs` 中编写业务逻辑，仅处理 UI 交互

## 命名规范

### 文件和类命名
- 模型类：`WorkRecord`, `Knowledge`, `Issue` — 使用名词，PascalCase
- 视图模型：`XxxViewModel` — 对应视图名 + ViewModel 后缀
- 视图：`XxxView.xaml` / `XxxView.xaml.cs` — PascalCase + View 后缀
- 服务类：`XxxService` — PascalCase + Service 后缀
- 仓储类：`XxxRepository` — PascalCase + Repository 后缀
- 转换器：`XxxConverter` — PascalCase + Converter 后缀
- 对话框：`XxxDialog.xaml` — PascalCase + Dialog 后缀

### 变量和方法命名
- 公开成员：PascalCase（`LoadData()`, `SelectedItem`）
- 私有字段：_camelCase（`_selectedItem`, `_isLoading`）
- 参数和局部变量：camelCase（`workRecord`, `itemCount`）
- 常量：PascalCase 或 UPPER_SNAKE_CASE
- 事件处理程序：`Xxx_Yyy` 格式（`Button_Click`, `DefaultView_Changed`）

### XAML 命名
- `x:Name` 仅在 code-behind 需要引用时添加，不得随意命名
- `x:Key` 使用 PascalCase（`CardStyle`, `PrimaryBrush`）
- Style 命名遵循 `功能+控件类型` 模式（`Select` 用于 ComboBox，`Card` 用于 Border）

## 数据模型字段参考

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

## 架构约束

### MVVM 原则
1. **ViewModel 不得引用 View**：ViewModel 中不得出现 `using System.Windows` 或任何 WPF UI 类型
2. **View 的 DataContext 绑定**：View 的 DataContext 在 code-behind 构造函数中设置
3. **数据绑定优先**：UI 状态通过绑定 ViewModel 属性控制，不在 code-behind 中直接操作 UI 元素（除非涉及动画、Storyboard 等 WPF 特有功能）
4. **命令 vs 事件**：简单交互用 Command 绑定，复杂 UI 交互（如动画、自定义控件行为）可用 code-behind 事件

### CommunityToolkit.Mvvm 用法
- 使用 `[ObservableProperty]` 标注属性，不使用手写 INotifyPropertyChanged
- 属性变更回调使用 `partial void OnXxxChanged(T value)` 模式
- 使用 `[RelayCommand]` 标注命令方法
- **禁止** 在同一类中混合手写 INPC 和 Source Generator 特性

### 服务层规范
- 全局服务使用**单例模式**：`ThemeService.Instance`, `ConfigService.Instance`, `ProfileService.Instance`
- 单例通过 `public static XxxService Instance { get; } = new();` 实现
- 服务初始化在 `App.xaml.cs` 的 `OnStartup` 中完成
- 不要在 View 或 ViewModel 中直接 new 服务实例

### 数据访问层规范
- 使用 Dapper 进行 SQL 查询，不使用 Entity Framework
- 数据库连接使用 `System.Data.SQLite`
- Repository 方法使用异步（`async/await`）
- SQL 查询使用参数化语句，禁止字符串拼接

## WPF/XAML 约束

### 样式和资源
- 所有全局样式定义在 `Resources/Styles.xaml` 中
- 运行时可变的值使用 `DynamicResource`，静态值使用 `StaticResource`
- 颜色使用 `SolidColorBrush` 类型的资源键，不要直接写十六进制色值
- **Storyboard 中不能使用 DynamicResource**（WPF 冻结限制），动画目标值必须硬编码

### 主题系统
- 主题管理统一由 `ThemeService` 处理
- 支持三档字号（Small/Medium/Large）：通过 `LayoutTransform` + `ScaleTransform` 实现全局缩放
- 支持三档密度（Compact/Default/Comfortable）：通过 `Thickness` 类型的 DynamicResource 实现
- 密度资源键必须为 `Thickness` 类型，**不得**使用 `sys:Double`（WPF 无法将 Double 自动转换为 Thickness）
- 密度相关资源键：`DensityCardPad`, `DensityRowPad`, `DensityRowPadSm`, `DensityListPad`, `DensitySectionMargin`, `DensityRowMargin`, `DensityItemMargin`
- 字号缩放应用于 `MainContentArea` Grid（MainWindow 中 x:Name="MainContentArea"）

### 组件复用规范（核心原则：先查后建）

**强制执行流程 — 任何新增样式或组件前，必须走完以下 3 步：**

1. **查**：先读取 `Resources/Styles.xaml` 全文，搜索是否已有满足需求的样式或模板
2. **判**：评估已有样式是否能直接使用，或通过 `BasedOn` 扩展 / 局部覆盖满足需求
3. **建**：仅当确实没有可复用的样式时，才创建新样式，且**必须定义在 `Resources/Styles.xaml` 中作为共享样式**，禁止在 View 中内联定义仅该页面使用的样式

**判断决策树：**
```
需要新样式/组件？
├── Styles.xaml 中已有完全匹配的？→ 直接引用 StaticResource
├── 已有样式差一点能满足？→ 用 BasedOn 继承扩展，或加参数化变体
├── 已有类似功能但视觉不同？→ 评估是否可合并为一组变体（如 Tag/TagPrimary/TagSuccess）
└── 确认不存在任何可复用的？→ 在 Styles.xaml 中新建共享样式，附带中文注释说明用途
```

**禁止行为：**
- ❌ 在 View 的 `<UserControl.Resources>` 中定义仅该页面使用的样式（除非确实全局唯一且不会被复用）
- ❌ 在不同 View 中复制粘贴相同的内联样式而不抽取为共享样式
- ❌ 创建与 `Styles.xaml` 中已有样式功能重复的新样式
- ❌ 硬编码颜色值、圆角值、阴影参数（必须引用 Styles.xaml 中定义的资源键）

**已有可复用组件清单（Views/ 目录）：**
- `CustomCalendar`（`Views/CustomCalendar.xaml`）：日期选择，支持月份导航、有记录日期标记、今日快捷。所有需要日期选择的场景必须使用此组件，配合浮窗覆盖层模式
- `ConfirmDialog`（`Views/ConfirmDialog.xaml`）：确认对话框，支持 Danger/Warning/Info 类型
- `ConfigItemDialog`（`Views/ConfigItemDialog.xaml`）：配置项编辑对话框
- `ProfileDialog`（`Views/ProfileDialog.xaml`）：个人信息编辑对话框

**已有可复用样式清单（Resources/Styles.xaml）：**

| 样式键（x:Key） | 目标控件 | 用途 |
|---|---|---|
| `BtnPrimary` | Button | 主操作按钮（紫色实心） |
| `BtnSecondary` | Button | 次要操作按钮（描边） |
| `BtnSuccess` | Button | 成功/正向操作按钮 |
| `BtnWarning` | Button | 警告操作按钮 |
| `BtnDanger` | Button | 危险操作按钮 |
| `BtnGhost` | ButtonBase | 幽灵/文字按钮（透明背景） |
| `Input` | TextBox | 单行输入框 |
| `TextArea` | TextBox | 多行输入框（基于 Input 扩展） |
| `Select` | ComboBox | 下拉选择框 |
| `Label` | TextBlock | 表单字段标签 |
| `Heading1/2/3` | TextBlock | 三级标题样式 |
| `Card` | Border | 卡片容器 |
| `CardElevated` | Border | 带阴影的卡片容器 |
| `CardHover` | Border | 可交互卡片（悬停高亮） |
| `Tag` / `TagPrimary/Success/Warning/Danger` | Border | 状态标签（彩色圆角小标签） |
| `ModernToolTip` | ToolTip | 统一悬浮提示（卡片风格 + 柔和阴影 + 自动换行，模板使用 `ContentPresenter` 避免嵌套渲染） |
| `ModernGrid` | DataGrid | 数据表格基础样式 |
| `ModernGridColumnHeader` | DataGridColumnHeader | 表格列头样式 |
| `ModernGridCell` | DataGridCell | 表格单元格样式 |
| `ModernGridRow` | DataGridRow | 表格行样式 |
| `WorkTypeBadgeCell` | DataTemplate | 工作类型彩色标签单元格 |
| `ProgressBarCell` | DataTemplate | 进度条单元格 |
| `ModernScrollBar` / `ModernScrollViewer` | ScrollBar/ScrollViewer | 现代化滚动条 |
| `OverlayBackdrop` | Border | 表单遮罩层（深色半透明） |
| `CalendarOverlay` | Border | 日历遮罩层（浅色半透明） |
| `SidePanel` | Border | 右侧滑入表单面板 |
| `FloatingPanel` | Border | 浮层弹出面板（日历等） |
| `SectionExpander` | Expander | 现代化折叠面板 |
| `ToggleSwitch` | ToggleButton | 开关控件 |
| `DayCheckBox` | CheckBox | 休息日选择复选框 |
| `PageNumberButton` / `PageNumberActive` | Button | 分页页码按钮 |
| `PageNavButton` | Button | 翻页导航按钮 |
| `Divider` | Separator | 分隔线 |
| `DragSplitter` | GridSplitter | 拖拽分割条 |
| `StatusDot` | Ellipse | 圆点状态指示 |
| `IconCircle` | Border | 圆形图标背景 |
| `GridTextTrim` | TextBlock | DataGrid 文本截断基础样式（TextTrimming+NoWrap），列 ElementStyle 用 `BasedOn` 继承后加 ToolTip |
| `HighlightStarCell` | DataTemplate | 亮点星标单元格（星形图标 + IsHighlight==1 时可见） |
| `SearchInput` | TextBox | 无边框搜索输入框（嵌入带搜索图标的容器内使用） |
| `NavButton` | RadioButton | 侧栏导航按钮（选中态渐变背景 + 白色图标文字） |
| `ToggleChip` | RadioButton | 切换芯片按钮（如员工类型选择，选中态渐变背景） |

**已有可复用辅助类（Helpers/ 目录）：**
- `IssueStatusConverter`：问题状态英文→中文映射（Open→待处理）
- `IssuePriorityConverter`：优先级英文→中文映射（Low→低）
- `CountToVisibilityConverter`：集合 Count==0 时显示空状态
- `BoolToVisibilityConverter` / `BoolToIntConverter`：布尔值转换
- `Highlight`：搜索关键词高亮附加属性
- `DrawerHelper`：抽屉面板动画（OpenDrawer / CloseDrawer）
- `CalendarHelper`：日历浮窗定位与关闭（Show / Close），所有日历弹出都应通过此工具类
- `DataGridCopyHelper`：DataGrid 悬停预览 + 右键复制（附加属性 `EnableCopy`）。`ModernGrid` 样式已内置启用。长文本列会按字段路径或中文列头自动识别（如 `Content` / `Achievement` / `Problem` / `Description` / `RootCause` / `Solution` / `Keywords` / `Title`）。悬停预览由 `PreviewPopup` 单例提供，**这些列不需要额外设置 TextBlock.ToolTip**
- `CopyButton`：无边框文字链风格复制按钮（图标 + 文字，hover 显示下划线，点击变绿 ✓ 已复制）。用于 `PreviewPopup` 浮窗内
- `PreviewPopup`：通用悬停预览弹出层（单例），替代系统 ToolTip 解决鼠标移到浮窗上即消失的问题。支持 DataGridCell、ListBoxItem 等任意 FrameworkElement

**统一引用规则：**
- 按钮样式统一使用 `BtnPrimary`、`BtnSecondary`、`BtnSuccess`、`BtnWarning`、`BtnGhost`、`BtnDanger`
- ComboBox 统一使用 `Select` 样式
- TextBox 统一使用 `Input`（单行）或 `TextArea`（多行）样式；搜索输入框（搭配搜索图标时）使用 `SearchInput` 样式
- ToolTip 统一使用 `ModernToolTip` 样式，配合 `Content="{Binding 字段名}"` 绑定内容。**例外**：DataGrid 中由 `DataGridCopyHelper` 管理悬停预览的长文本列（如 `内容` / `工作成果` / `问题` / `描述` / `根本原因` / `解决方案` / `关键词` / `标题`）**不需要**额外设置 ToolTip，`PreviewPopup` 已提供预览 + 复制功能
- DataGrid 必须使用 `ModernGrid` + `ModernGridColumnHeader` + `ModernGridCell` + `ModernGridRow` 组合
- 禁止使用 WPF 原生控件：当项目已有自定义组件时，必须优先复用，不得引入 WPF 原生控件（如 `DatePicker`、`Calendar` 等）

### ToggleButton 陷阱
- WPF ToggleButton 的 `Click` 事件会在 `IsChecked` 自动切换**之后**触发
- 如果在 Click 事件中手动设置 `IsChecked`，会导致双重切换（值变回原值）
- **正确做法**：使用 `PreviewMouseLeftButtonDown` 事件，设置 `e.Handled = true` 阻止默认行为，然后手动控制 `IsChecked`
- 代码模板：
```csharp
private void SomeButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    e.Handled = true;
    var btn = (ToggleButton)sender;
    btn.IsChecked = !(btn.IsChecked == true);
    // 后续逻辑...
}
```

### ComboBox 样式
- 所有 ComboBox 必须使用 `Style="{StaticResource Select}"` 统一样式
- **包括不可见覆盖层**：即使 ComboBox 设置了 `Opacity="0"` 作为透明输入覆盖层，也必须添加 Select 样式，否则弹出面板会使用默认 WPF 样式
- 如果需要中文显示标签但绑定英文值，使用 `ItemTemplate` + `IValueConverter`
- ComboBox 布局应 `HorizontalAlignment="Right" VerticalAlignment="Center"`

### x:Name 与编译
- 在 XAML 中添加 `x:Name` 会在 `.g.cs` 自动生成字段
- 增量编译可能导致 `.g.cs` 未更新，出现 "当前上下文中不存在名称" 错误
- **修复方法**：执行 `dotnet clean` 后重新 `dotnet build`

## 事件处理约束

### SettingsView 的特殊处理
- SettingsView 中的 ToggleButton 使用 `PreviewMouseLeftButtonDown` 而非 `Click`（原因见上文 ToggleButton 陷阱）
- 字体/密度按钮在 code-behind 中更新视觉高亮（`UpdateFontSizeButtons()` / `UpdateDensityButtons()`），同时通过 ViewModel 属性触发 ThemeService 执行实际变更
- 不要在 SettingsView code-behind 中直接调用 ThemeService，应通过 ViewModel 属性间接调用

### 对话框
- 自定义对话框（ConfirmDialog, ConfigItemDialog, ProfileDialog）使用 `Window` 类型，`WindowStartupLocation="CenterOwner"`
- 对话框通过 `ShowDialog()` 模态显示
- 不要在对话框中直接操作主窗口状态

## 数据库约束

- 数据库文件位置：`%LOCALAPPDATA%\EapWorkAssistant\eapwork.db`
- 自动备份目录：`%LOCALAPPDATA%\EapWorkAssistant\backups\`
- 表结构变更必须在 `DatabaseInitializer.cs` 中更新建表语句**和迁移代码**
- 新增列使用 `ALTER TABLE ... ADD COLUMN` 并带默认值，用 try/catch 包裹以兼容已升级的数据库
- 新增索引使用 `CREATE INDEX IF NOT EXISTS` 语句，同样用 try/catch 包裹
- 已有索引：`idx_workrecord_workdate`, `idx_workrecord_project`, `idx_issue_project`, `idx_issue_status`, `idx_knowledge_category`, `idx_knowledge_tags`, `idx_knowledge_isfavorite`
- 保留 30 天备份，备份文件名含日期

### 迁移代码模板
```csharp
// 新增列
try { migrateCmd.CommandText = "ALTER TABLE Xxx ADD COLUMN Yyy TEXT DEFAULT 'default'"; migrateCmd.ExecuteNonQuery(); } catch { }

// 新增索引
try { migrateCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_xxx_yyy ON Xxx(Yyy)"; migrateCmd.ExecuteNonQuery(); } catch { }
```

## 常用模式

### SafeFire（异步异常统一处理）
`Helpers/TaskExtensions.cs` 提供 `Task.SafeFire(string errorMessage)` 扩展方法，用于安全触发 fire-and-forget 异步操作。内部 await Task 并用 try/catch + `ToastService.Error` 反馈异常。**所有 `_ = SomeAsync()` 调用都应替换为 `SomeAsync().SafeFire("描述")`**。

```csharp
// ✅ 正确
LoadAllRecordsAsync().SafeFire("加载记录失败");

// ❌ 错误（异常被静默吞掉）
_ = LoadAllRecordsAsync();
```

### 查询版本号（竞态防护）
`WorkRecordViewModel` 使用 `_queryGeneration` 整型计数器防止筛选器快速切换时旧查询结果覆盖新结果。每次 `LoadAllRecordsAsync` 调用时 `++_queryGeneration`，await 返回后检查 `if (gen != _queryGeneration) return;` 丢弃过期结果。

### 自动保存（DispatcherTimer）
WorkRecordViewModel 使用 `DispatcherTimer` 实现自动保存。关键约束：
- `StartAutoSaveTimer()` 根据 `ConfigService.Instance.AutoSaveInterval` 设置间隔
- `PauseAutoSaveTimer()` 在离开工作记录页面时调用（`MainViewModel.NavigateTo` 中处理）
- `_isAutoSaving` 标记防止重入：Tick handler 是 `async void`，如果 save 耗时超过 interval，第二次 Tick 会重入
- 必须在 try/finally 中重置 `_isAutoSaving = false`

### 图表点击导航（LiveCharts2）
DashboardViewModel 通过 `DataPointerDownCommand` 绑定实现图表点击事件。**命令参数类型为 `IEnumerable<ChartPoint>?`**（不是单个 `ChartPoint`），需用 `.FirstOrDefault()` 取出第一个点。柱状图使用 `point.Index` 推算日期跳转到工作记录；饼图使用 `point.Index` 从 `ProjectPieSeries` 数组获取项目名跳转。**注意**：LiveCharts2 的 `ChartPoint` 没有 `Series` 属性，必须通过索引访问外部系列数组。

### Toast 错误反馈
`ToastService` 是静态类，方法直接调用（不需要 `.Instance`）：
```csharp
ToastService.Error($"保存失败：{ex.Message}");
ToastService.Success("问题已保存");
ToastService.Info("数据库已自动备份", "数据安全");
```

### 多关键词搜索
WorkRecordRepository.SearchAsync 支持空格分隔多关键词 AND 匹配。将输入按空格拆分，为每个关键词生成独立的 `LIKE` 条件并用 `AND` 连接。单关键词时退化为简单 LIKE 查询。

### 状态/优先级中文映射
`Helpers/IssueStatusConverter.cs` 包含 `IssueStatusConverter` 和 `IssuePriorityConverter`，将英文值映射为中文标签（如 Open → 待处理，Critical → 紧急）。XAML 中使用 `ItemTemplate` + 转换器实现中文显示、英文存储。

### CSV 导入/导出
`ExportService` 使用**状态机解析器** `ParseCsvRows()` 处理 CSV，正确处理引号内的逗号、换行符和转义引号（`""`）。**禁止使用 `File.ReadAllLines`**，因为内容字段可能包含换行。导出时 `EscapeCsv` 对含逗号、引号、`\n`、`\r` 的字段加引号包裹。

### 报告内容解析（ParseContentItems）
`ReportService.ParseContentItems` 将多行文本拆分为 `(string Text, bool IsSubItem)` 列表。识别规则按优先级：
1. 顶层编号：`1、` `1.` `1．` `1)` `（1）` `(1)` → 主项
2. 符号标记：`-` `•` `·` `*` `+` → 子项
3. 字母编号：`a.` `b)` → 子项
4. 圆圈数字：`①` `②` `③` → 子项
5. 缩进识别：Tab 或 2+ 空格开头且无编号 → 子项
6. 其他文本：合并到上一条（续行处理）

### 数据库备份
`DatabaseBackupService` 使用 SQLite 原生备份流程，**禁止直接 `File.Copy` 活跃数据库**：
```csharp
srcConn.Open();
// 1. 将 WAL 日志刷入主文件
new SQLiteCommand("PRAGMA wal_checkpoint(FULL);", srcConn).ExecuteNonQuery();
// 2. 使用 SQLite backup API
srcConn.BackupDatabase(dstConn, "main", "main", -1, null, 0);
```

### WPF 依赖属性优先级陷阱
XAML 中**本地值（precedence 3）优先于 Style Trigger（precedence 5）**。如果需要 DataTrigger 动态切换属性，默认值必须写在 Style Setter 中，**不能写在元素标签上**：
```xml
<!-- ❌ 错误：DataTrigger 永远无法覆盖本地值 -->
<Border IsHitTestVisible="False">
    <Border.Style>
        <Style TargetType="Border">
            <Style.Triggers>
                <DataTrigger ...>
                    <Setter Property="IsHitTestVisible" Value="True"/>  <!-- 无效 -->

<!-- ✅ 正确：默认值在 Style Setter 中 -->
<Border>
    <Border.Style>
        <Style TargetType="Border">
            <Setter Property="IsHitTestVisible" Value="False"/>
            <Style.Triggers>
                <DataTrigger ...>
                    <Setter Property="IsHitTestVisible" Value="True"/>  <!-- 生效 -->
```

### 导航与自动保存协调
`MainViewModel.NavigateTo` 负责在视图切换时：
1. 离开 WorkRecord 时调用 `WorkRecord.PauseAutoSaveTimer()`
2. 进入 WorkRecord 时调用 `WorkRecord.StartAutoSaveTimer()`
3. 仅当 `CurrentView` 实际变化时才调用 `RefreshAsync()`，避免重复加载

### FindVisualParent 正确用法
`DataGridCopyHelper.FindVisualParent<T>` 对 Visual 元素使用 `VisualTreeHelper.GetParent`（可穿越 DataTemplate 边界），对 ContentElement（如 `Run`）先用 `LogicalTreeHelper.GetParent` 回到 `TextBlock` 再切到 VisualTree。**禁止在需要穿越 DataTemplate 时使用 `LogicalTreeHelper`**，它无法从 DataTemplate 内部找到父级 DataGridCell。

### DataGrid 内容列悬停预览
`ModernGrid` 样式内置 `DataGridCopyHelper.EnableCopy="True"`，为长文本列（如 Header 为 `内容` / `工作成果` / `问题` / `描述` / `根本原因` / `解决方案` / `关键词` / `标题`，或 Binding.Path 为 `Content` / `Achievement` / `Problem` / `Description` / `RootCause` / `Solution` / `Keywords` / `Title`）自动提供：
- **悬停预览**：鼠标停留 300ms 后弹出 `PreviewPopup`，显示完整文本 + 复制按钮
- **右键菜单**：复制单元格 / 复制整行

**不要**在这些由 `DataGridCopyHelper` 接管的长文本列上额外设置 `TextBlock.ToolTip`，否则会出现两个浮窗（PreviewPopup + ToolTip）。

### 主题切换互斥
`SettingsViewModel` 中 `IsLightTheme` 和 `IsDarkTheme` 必须互斥联动。在 `OnIsLightThemeChanged(true)` 中设置 `IsDarkTheme = false`，反之亦然。避免两个按钮同时高亮。

### 数字输入验证
工时（double）和进度（int）TextBox 使用 `PreviewTextInput` 事件限制输入：
```csharp
// 工时：允许数字和最多一个小数点
e.Handled = !double.TryParse(textBox.Text + e.Text, out _) && newText != ".";
// 进度：仅允许整数
e.Handled = !int.TryParse(e.Text, out _);
```

### 全局异常兜底（App.xaml.cs）
`App.xaml.cs` 注册了三类未捕获异常处理器，确保应用不会因意外异常直接崩溃：
- `DispatcherUnhandledException`：UI 线程异常，设置 `e.Handled = true` 阻止崩溃
- `AppDomain.CurrentDomain.UnhandledException`：非 UI 线程异常（通常无法恢复）
- `TaskScheduler.UnobservedTaskException`：Task 中未 await 的异常，调用 `e.SetObserved()`

所有异常统一由 `LogAndNotify(Exception ex)` 处理：
1. 写入日志文件 `%LOCALAPPDATA%\EapWorkAssistant\logs\error_{日期}.log`
2. 通过 `ToastService.Error` 通知用户

### 表单重置
知识库和问题跟踪的「新增」按钮在打开抽屉前必须重置表单：
```csharp
if (DataContext is KnowledgeViewModel vm)
{
    vm.CurrentItem = new Knowledge();
    vm.IsFormDirty = false;
}
```

## 提交规范

### Commit Message 格式
使用中文，格式为 `type: description`，常用 type：
- `feat:` — 新功能
- `fix:` — 修复 bug
- `ui:` — UI/样式调整
- `refactor:` — 重构（不改变功能）
- `docs:` — 文档更新
- `chore:` — 构建/工具链

### 示例
```
feat: 添加主题切换功能（深色/浅色模式）
ui: 优化设置页面 ComboBox 样式对齐
fix: 修复界面密度切换无效的问题
refactor: 提取 ThemeService 统一管理主题逻辑
```

## AI 操作红线

1. **禁止删除用户文件**：不得执行 `rm`、`del` 等永久删除操作
2. **禁止修改 .csproj 的 NuGet 包版本**：除非用户明确要求
3. **禁止引入新的 NuGet 包**：除非用户明确要求并确认
4. **禁止更改项目目标框架**：当前为 `net9.0-windows`
5. **禁止破坏单例模式**：不得将 `ThemeService.Instance` 等改为依赖注入或其他模式
6. **禁止在 ViewModel 中引入 WPF 命名空间**：保持 MVVM 纯净性
7. **禁止硬编码颜色值**：新代码中颜色必须引用 Styles.xaml 中的资源键
8. **禁止在 Storyboard 中使用 DynamicResource**：会导致运行时异常
9. **修改 XAML 前必须确认结构完整性**：不要意外删除 Grid、ColumnDefinitions 等结构性标签
10. **修改后必须验证编译通过**：执行 `dotnet build` 确认 0 error
11. **新增数据库字段必须同步迁移代码**：在 `DatabaseInitializer.cs` 中同时更新建表语句和 ALTER TABLE 迁移
12. **禁止使用 fire-and-forget 裸调用**：所有 `_ = SomeAsync()` 必须替换为 `SomeAsync().SafeFire("描述")`
13. **禁止在 XAML 元素标签上设置需要 DataTrigger 动态覆盖的属性**：默认值必须写在 Style Setter 中
14. **禁止直接 File.Copy 活跃 SQLite 数据库**：必须使用 WAL checkpoint + BackupDatabase API
15. **批量数据库写入必须包裹在事务中**：使用 `BeginTransaction/Commit/Rollback`
16. **禁止移除全局异常处理**：`App.xaml.cs` 中的 `DispatcherUnhandledException`、`AppDomain.UnhandledException`、`TaskScheduler.UnobservedTaskException` 是发布质量的底线保障
17. **关键服务禁止静默吞异常**：`ConfigService.Save`、`ProfileService.Save/SaveAvatar` 等写操作必须 catch 并用 `ToastService.Error` 通知用户
18. **禁止创建重复样式**：新增任何样式/组件前，必须先读取 `Resources/Styles.xaml` 检查是否已有可复用的样式。已有则直接引用，差一点则基于扩展，确实不存在才新建且必须放入 Styles.xaml 作为共享样式。详见"组件复用规范"章节
19. **禁止在 DataGridCopyHelper 管理的长文本列上设置额外 ToolTip**：`ModernGrid` 已内置 `EnableCopy`，`PreviewPopup` 会提供悬停预览。额外 ToolTip 会导致两个浮窗同时出现
20. **禁止在需要穿越 DataTemplate 时使用 LogicalTreeHelper**：`FindVisualParent` 必须对 Visual 元素使用 `VisualTreeHelper.GetParent`，否则无法从 DataTemplate 内部找到父级控件

## 代码审查清单

每次修改代码后，AI 应自查以下项目：

**基础规范：**
- [ ] 新增文件是否放入了正确的目录？
- [ ] 命名是否符合规范（PascalCase/camelCase）？
- [ ] ViewModel 是否引入了 WPF 类型？
- [ ] 新增的颜色/间距是否使用了资源键而非硬编码？
- [ ] 编译是否通过（dotnet build 0 error）？
- [ ] 是否有遗留的 TODO 或临时调试代码？

**组件复用：**
- [ ] 新增样式前是否已读取 `Resources/Styles.xaml`，确认无已有可复用样式？
- [ ] 新创建的样式是否已放入 `Styles.xaml` 作为共享样式（而非 View 内联）？
- [ ] 是否复用了已有的自定义组件（CustomCalendar、ConfirmDialog 等），而非引入 WPF 原生控件？
- [ ] DataGrid 是否使用了 `ModernGrid` 系列样式？ToolTip 是否使用了 `ModernToolTip`？
- [ ] ToggleButton 是否使用了 PreviewMouseLeftButtonDown？
- [ ] ComboBox 是否使用了 Select 样式？（包括 Opacity=0 的覆盖层 ComboBox）
- [ ] 多个 View 中出现相同内联样式时，是否已抽取为 Styles.xaml 中的共享样式？
- [ ] DataGrid 列的 ElementStyle 是否 `BasedOn="{StaticResource GridTextTrim}"` 后加列特有 ToolTip？
- [ ] 搜索框是否使用了 `SearchInput` 样式？导航栏是否使用了 `NavButton`？
- [ ] 星标列是否使用了 `HighlightStarCell` DataTemplate？
- [ ] 所有 ToolTip 是否统一使用了 `ModernToolTip` 样式（而非纯字符串 `ToolTip="..."`）？
- [ ] DataGrid 的长文本预览列是否**避免**设置额外 ToolTip（`DataGridCopyHelper.PreviewPopup` 已提供悬停预览）？
- [ ] 日历浮窗是否通过 `CalendarHelper.Show()` / `CalendarHelper.Close()` 管理定位与显隐？
- [ ] 需要穿越 DataTemplate 查找父控件时，是否使用了 `VisualTreeHelper`（而非 `LogicalTreeHelper`）？

**WPF/XAML：**
- [ ] DynamicResource 是否用于了 Thickness 类型（而非 sys:Double）？
- [ ] Storyboard 中是否避免了 DynamicResource？
- [ ] 需要 DataTrigger 动态切换的属性是否写在 Style Setter 中（而非元素本地值）？
- [ ] DataTrigger 比较值类型是否与绑定源类型匹配？

**数据库：**
- [ ] 新增模型字段是否同步更新了 `DatabaseInitializer.cs` 的建表和迁移代码？
- [ ] 新增数据库索引是否使用了 `CREATE INDEX IF NOT EXISTS`？
- [ ] 批量写入是否包裹在事务中？
- [ ] 数据库备份是否使用了 WAL checkpoint + BackupDatabase API（而非 File.Copy）？

**异步与错误处理：**
- [ ] 所有异步调用是否使用了 `.SafeFire()` 而非 `_ = ` 裸调用？
- [ ] 关键操作是否添加了 try/catch + Toast 错误反馈？
- [ ] 快速切换筛选器是否有竞态防护（查询版本号或 CancellationToken）？
- [ ] DispatcherTimer Tick handler 是否有重入保护？
- [ ] 全局异常处理是否完整（DispatcherUnhandledException + AppDomain + TaskScheduler）？
- [ ] 关键服务的 catch 块是否通知了用户（Toast），而非静默吞掉？

**数据校验：**
- [ ] ViewModel SaveAsync 是否校验了所有必填字段？
- [ ] 数字输入 TextBox 是否添加了 PreviewTextInput 验证？
- [ ] CSV 解析是否正确处理了引号内逗号和换行符（使用状态机而非 ReadAllLines）？
- [ ] LiveCharts2 代码是否避免了访问 `ChartPoint.Series`（不存在的属性）？

**导航与生命周期：**
- [ ] NavigateTo 是否仅在视图实际切换时才触发 RefreshAsync？
- [ ] 自动保存计时器在离开工作记录页面时是否暂停？
- [ ] 单例服务的事件订阅是否有对应的取消订阅路径？
- [ ] 知识库/问题跟踪「新增」按钮是否在打开抽屉前重置了表单？
