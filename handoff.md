# EAP Work Assistant — 交接摘要

> 生成时间：2026-10-16 14:25（会话交接用）
> 本轮主题：**P2 ⑨ 主动异常洞察（仪表盘提示卡：调休余额临界 / 连续未填 / 本月填报偏离 8h）**
> 当前状态：**测试 181/181 全绿 · 工作区干净 · 本地领先 `origin/master`（多笔未 push，按约定由真实终端执行）**
> 下一步：**P2 ⑩ 首启引导 + 搜索扩展**（⑩ → ⑪ 空态/骨架屏/count-up/Logo）

---

## 0. 项目速览

| 项 | 值 |
|---|---|
| 项目 | EapWorkAssistant（EAP 工时 / 问题 / 知识库管理桌面应用） |
| 技术栈 | .NET 10 (`net10.0-windows`) + WPF + MVVM（CommunityToolkit.Mvvm 8.4.2） |
| 图表 | LiveChartsCore 2.0.4（SkiaSharpView.WPF，Skia 画布渲染，**不响应 DynamicResource**） |
| 存储 | SQLite（软删除 `IsDeleted=1`，迁移靠 `PRAGMA user_version`，当前 `LatestSchemaVersion=10`） |
| 主题 | 自定义 indigo 双主题 ResourceDictionary，运行时由 `ThemeService` 覆盖画刷 |
| 仓库 | `C:\Users\Yang\Documents\EapWorkAssistant`（git，**仅本地 commit，不 push**） |
| 应用数据 | `%LOCALAPPDATA%\EapWorkAssistant\`（`config.json` / `eapwork.db` / `drafts.json` / `logs\error_YYYYMMDD.log`） |
| 测试基线 | **181 / 181**（`.sln` 不含测试项目，须显式指定测试 csproj） |

---

## 1. 本轮进度（已完成并提交）

### 1.1 本会话新增 commit（P1 ⑥ / P1 ⑦ / P2 ⑧ / P2 ⑨）

| commit | 主题 | 关联 |
|---|---|---|
| `b9d323c` | **ui: 图表主题深化（P1 ⑥）**——柱圆角 / 网格弱化 / tooltip 卡片化 / hover 反馈 | feat 代码 |
| `37f6108` | docs: AGENTS.md 增补 8.22 图表主题规范，handoff 标记 P1 ⑥ 完成 | 文档 |
| `7ba9014` | **feat: 出勤月度热力图（P1 ⑦）**——迷你日历整格底色 + 月份导航 | feat 代码 |
| `33b9966` | docs: AGENTS.md 增补 8.23 出勤热力图规范，handoff 标记 P1 ⑦ 完成 | 文档 |
| `ffa4bcc` | **feat: 列表多选批量操作（P2 ⑧）**——分页基类通用框架 + 三模块批量软删除 | feat 代码 |
| `fe17547` | **feat: 主动异常洞察（P2 ⑨）**——仪表盘提示卡（调休余额临界 / 连续未填 / 本月填报偏离 8h），9 文件 +825 行 | feat 代码 |
| （随本次提交） | docs: AGENTS.md 增补 8.25 异常洞察规范，handoff 标记 P2 ⑨ 完成 | 文档 |

### 1.2 上一批（承接上一会话，已提交）

| commit | 说明 |
|---|---|
| `d12bf7a` | fix: 修复编辑记录时工时显示为 0 |
| `62d71db` | feat: 窗口几何持久化 + 命令面板键盘导航（P0） |
| `83cae69` | feat: 表单草稿恢复（P0 ③） |
| `5d5af29` | fix: Ctrl+N 打开「新增记录」抽屉并聚焦首个输入框 |
| `882b205` | feat: 自绘标题栏（P1 ④） |
| `a25e18d` | ui: 图标尺寸统一为 IconSize 五档刻度（P1 ⑤） |

> ⚠️ **推送状态**：本地 `master` **领先 `origin/master` 多笔提交**（含本会话的 P2 ⑧/⑨ 代码与文档，**均未 push**）。本约定为「仅本地 commit，push 由用户真实终端执行」——开新窗口后请以真实终端 `git log --oneline origin/master..HEAD` 复核（该命令会列出全部待推送提交）。

### 1.3 各项交付要点

**⑥ 图表主题深化（P1）**
- 唯一同步入口 `DashboardViewModel.ApplyChartTheme()`；颜色源 `ThemeService.GetChartColors()` → `(Text, Grid, Accent, Card, TextStrong)` 五元组。**禁止在别处给图表写死配色**。
- 画布内度量常量（Skia 像素，非 XAML）：`ChartAxisTextSize=11` / `ChartTooltipTextSize=12` / `ChartGridStrokeThickness=0.8f` / `ChartCrosshairStrokeThickness=1f` / `PieHoverPushout=10`。
- 坐标轴：X 轴 `ShowSeparatorLines=false`（去竖向网格）+ `TicksPaint=null`；Y 轴仅留横向网格 + `ZeroPaint`。
- hover：X 轴 `CrosshairPaint` + `CrosshairSnapEnabled` + 标签底=卡片色；柱图靠准星 + tooltip（2.0.4 无原生柱体放大）；饼图 `HoverPushout=10`。
- 柱体 `Rx/Ry=4`（**顶/底两角同时生效**）+ `MaxBarWidth=28`；动画在 code-behind 从 `DurationNormal` 令牌读 + ease-out cubic。
- ⚠️ 饼图 tooltip 防重复：LiveCharts 自动带系列名+色点，`ToolTipLabelFormatter` **只输出数值** `$"{value:F1} h"`。
- **实证**：柱体=主题 accent `#6366F1`、顶/底圆角渐进收窄、横网格 5 条 0.8f 淡线、竖网格 0 条、hover 半透明高亮+数据点圆环+白底 tooltip+十字准星、饼图扇区外扩。

**⑦ 出勤月度热力图（P1）**
- `Models/DayStatus.cs` 新增 `HeatmapCell`；`AttendanceViewModel` 新增 `BuildHeatmap()`（区间变更重建月份边界）/ `RefreshHeatmapCells()`（按 `HeatmapMonth` 生成格子）/ `PrevHeatmapMonthCommand`·`NextHeatmapMonthCommand`（+ `CanPrev/CanNextMonth` 守卫）/ `OpenHeatmapDay()`。
- 格色判定 `ToDotColor(DayStatusItem)` 改为 `internal static`（供单测直调），与 `CustomCalendar.CreateDayItem` **共用同一套优先级**（amber 假日加班 → green 工作日已填 → 请假类型 → blue 周末加班 → red 应上班未填 → purple 法定假日 → gray 普通周末；**补班/假日判定先于周末**）。
- `DesignTokens.xaml` 补 6 个 `Leave*LightBrush` 淡色底；`AttendanceView.xaml` 加面板（Row 2：月导航 + 7 列星期表头 + `UniformGrid` + 图例），格色走 `DataTrigger` 映射 `DotColor → *LightBrush`。
- 月份范围取区间首月~末月，**默认展示「今天所在月」**（避免「本年」模式默认落 12 月空档）；标题格式 `yyyy年M月`。
- **实证**：10 月 35 格全量正确——**10/10 补班=green 而 10/11 周日=blue**（补班判定生效强证据）、10/1 amber、10/2-5 purple、10/14 年假、10/17 blue、其余 red；悬停 tooltip 经 UIA 读到 `2026-10-14 周三 · 请假·年假 8h`。

**⑧ 列表多选批量操作（P2）**
- **通用框架下沉到分页基类**：`PagedCollectionViewModelBase<T>` 新增 `SelectedItems` / `IsMultiSelectMode` / `HasSelection` + `ToggleMultiSelectCommand` + `BatchDeleteCommand`；子类只实现 `DeleteItemAsync` / `RestoreItemAsync` 两个钩子（Knowledge/Issue 各一行）。`ReloadPageAsync` 末尾统一清空选择。
- **核心逻辑与 UI 编排分离**：命令只做「确认对话框 + Toast（带撤销）」；删除走 `internal DeleteItemsAsync(IReadOnlyList<T>)`，可被单测直调（绕过 `DialogService`）。
- **工作记录例外**：`WorkRecordViewModel` 不继承基类，单独实现 `SelectedRecords` 套件（`BatchDeleteRecordsCommand` + `DeleteSelectedRecordsAsync`），额外刷新当日面板/日历/调休余额，当前页删空时回退一页。
- **View 零新增控件**：复用 `SelectionModeConverter`（`SelModeConv`）+ `DataGridHelper.SelectedItemsList`，二者与 `BoolToVis` 已**全局注册**到 `Resources/Styles.xaml`。
- ⚠️ 坑：「多选」开关必须用 `BtnGhost`（`TargetType=ButtonBase`）；`BtnPrimary/BtnSecondary/BtnDanger` 是 `TargetType=Button`，**套在 ToggleButton 上会编译报错**。
- **实证**（UIA 端到端，`outputs/batch-select-knowledge.png`）：切知识库 → 点「多选」→ ToggleState=On → `AddToSelection` 前两行均 `IsSelected=True`（证明已进 Extended 多选）→ 批量栏读到 `已选  2  项`（证明 `DataGridHelper` 同步链路通）→ 「删除选中」按钮 `IsEnabled=True`。

**⑨ 主动异常洞察（P2）**
- **分层**：`Services/InsightAnalyzer.cs` 纯静态无 IO（输入 `InsightInput` = 调休余额三元组 + `DayStatusItem` 列表 + 今天）→ 产出 `Models/InsightItem.cs`（`InsightSeverity` + `Kind`/`Title`/`Detail`/`ActionText`/`ActionTarget`）。`DashboardViewModel.LoadInsightsAsync` 只做「取数 → Analyze → 赋 `Insights`」。
- **三条规则**：① 调休余额 <8h 告警 / 已用 > 周末加班 → 透支风险；② 连续未填应上班日 ≥3 告警、≥5 风险；③ 本月已填工作日偏离 8h（少报 Warning / 超填 Info）。阈值都是 `public const`。
- ⚠️ 判定只信 `DayStatusItem`（`DayStatusService` + `DayStatusRules`），分析器**不自判节假日**；**今天不计入连续未填**（`i.Date < today`）。
- ⚠️ **节假日日历不可用整体降级**：`IsHolidayDataAvailable=false` 只返回一条 Info 提示（守卫须在 `GetRangeAsync` 之后判定），否则法定假日被误当工作日 → 假「未填」警报。
- ⚠️ 配色用 `DataTrigger` + `{x:Static models:InsightSeverity.*}`（字符串 `Value="Warning"` 匹配不上枚举）；`Border`/`Path` 的 `Background`/`Fill`/`Data` 必须**全部写成 Style Setter**（内联值优先级高于 Trigger）。
- ⚠️ **`x:Name` 放 `Border` 上不会成为 UIA `AutomationId`**（无 `AutomationPeer`）→ UI 验证改用文本锚点（`Name='需要留意'`）。
- **实证**（UIA 端到端，`outputs/insight-panel.png` / `insight-dashboard.png`）：仪表盘读出「需要留意」+ 条目 `本月 1 天填报不足 8h，合计少报 4h`（正是 `2026-10-10` 填报 4.0h 触发）+ 按钮「查看出勤」`enabled=True`；点击后导航切到「出勤总览」。

---

## 2. 已定约束（硬性红线，改动前必读）

> 完整版见 `AGENTS.md` 第 9 章「AI 行为约束红线」与 `.workbuddy/memory/MEMORY.md`。以下是最容易踩的几条。

### 2.1 数据 / 口径

| # | 约束 |
|---|---|
| 1 | **工时双口径**：`WorkRecord.Hours` = 填报工时（报给公司，每天 8h）；`WorkRecord.ActualHours` = 实际工时（自己真实投入）。**禁止「未填回落」**（`EffectiveActualHours` 已删除），未填就是 0，展示走 `ActualHoursText`（显示「—」）。 |
| 2 | **调休余额按【填报工时】算**；实际工时只记录/展示，**不参与任何余额或统计口径计算**。 |
| 3 | 工作日/假日判定唯一事实来源：`Services/DayStatusRules.cs`（纯静态，`IsWorkdayDue(date,isHoliday,isMakeup)=!isHoliday && (!IsWeekend(date)\|\|isMakeup)`）。**禁止另写判定**；改判定必须同时覆盖「周末 / 补班 / 法定假日」三种情形并补单测。 |
| 4 | 迁移：`LatestSchemaVersion` = 10；靠 `PRAGMA user_version` 只跑一次。**重跑迁移须先从备份恢复库**（备份 `eapwork.db.pre_*_时间戳` 存 `%LOCALAPPDATA%\EapWorkAssistant\`）。 |

### 2.2 WPF 绑定 / 表格

| # | 约束 |
|---|---|
| 5 | 只读计算属性（表达式体 `=>`）绑 `<Run Text=...>` 必须显式 `Mode=OneWay`（`Run.Text` 默认 TwoWay，否则运行时抛异常）。 |
| 6 | **主表统一只读**：`Controls/DataGridBase.cs` 构造已设 `IsReadOnly = true`，勿改。 |
| 7 | **表格排序范式**：View `DataGrid.Sorting` → code-behind `e.Handled=true` + `vm.ToggleSort(...)`；排序键必须绑**真值**（DateTime/double），不能是格式化字符串。 |
| 8 | **手工复制实体的方法（如 `EditRecord`）新增字段必须同步复制**，否则新字段恒为默认值。 |
| 9 | **ViewModel 禁止引用 WPF 命名空间**（红线 6）；Skia 图表颜色经 VM 属性透传，不经 Brush。 |

### 2.3 配置 / 资源 / 主题

| # | 约束 |
|---|---|
| 10 | **配置类里不要用 `double.NaN` 当哨兵**（`System.Text.Json` 无法序列化 NaN，一保存就抛异常）；位置哨兵用 `double?`（null）。 |
| 11 | 令牌以 `Resources/DesignTokens.xaml` 为准，禁止硬编码颜色/圆角/字号/间距/动效时长/固定尺寸（`AGENTS.md` 9.2、8.21）。 |
| 12 | ⚠️ **运行时色彩以 `ThemeService` 覆盖值为准**：启动/切换主题时 `SetColor`/`UpdateBrush` **覆盖 DesignTokens 同名键**。Indigo+Light 实测 `PrimaryLight=#EEF2FF`、`SuccessLight=#D1FAE5`、`DangerLight=#FEE2E2`（均与令牌硬编码值不同）；`WarningLight/InfoLight/Leave*` 未被覆盖。**做像素比对必须用运行时值**（曾把 green 误判成病假——二者同为 `#D1FAE5`）。 |
| 13 | caption 区内的交互控件必须 `shell:WindowChrome.IsHitTestVisibleInChrome="True"`。 |
| 14 | **不要为「WindowChrome 最大化裁切」加 Margin 补丁**——实测客户端矩形恰等于工作区，加 Margin 只会露缝。 |

### 2.4 协作 / 流程

| # | 约束 |
|---|---|
| 15 | **仅本地 commit，不 push**（push 由用户在真实终端执行）。 |
| 16 | 按**主题拆 commit**，中文 commit message。 |
| 17 | `outputs/` 已在 `.gitignore`，截图/产物不会污染仓库。 |
| 18 | 每个 P 项收尾动作：编译 → 全量测试 → 必要时 UI/截图验证 → 更新 `AGENTS.md` → 按主题 commit。 |
| 19 | **列表多选批量**：通用框架在 `PagedCollectionViewModelBase<T>`（AGENTS.md §8.24）；「多选」开关组件必须用 `BtnGhost`（`TargetType=ButtonBase`），批量删除是**软删除**（提示可在回收站恢复）。 |
| 20 | **主动异常洞察**：规则集中在纯静态 `Services/InsightAnalyzer`（AGENTS.md §8.25）；出勤判定只信 `DayStatusItem`，**节假日数据不可用时整体降级不报**（否则假「未填」警报）；`InsightItem` 配色走 `DataTrigger` + `{x:Static models:InsightSeverity.*}`（字符串值匹配不上枚举）。 |

---

## 3. 关键文件路径

### 3.1 本轮（P1 ⑥⑦ / P2 ⑧ / P2 ⑨）新增 / 重点改动

| 文件 | 作用 |
|---|---|
| `Services/ThemeService.cs` | `GetChartColors()` 五元组（Text/Grid/Accent/Card/TextStrong）；主题切换广播 |
| `ViewModels/DashboardViewModel.cs` | `ApplyChartTheme()`（图表主题唯一入口）+ 画布度量常量 |
| `Views/DashboardView.xaml` / `.xaml.cs` | tooltip 三属性绑定 + tooltip 位置；code-behind 设 `AnimationsSpeed`/`EasingFunction` |
| `Models/DayStatus.cs` | **新增** `HeatmapCell`（Date/IsCurrentMonth/DotColor/Tooltip/Item） |
| `ViewModels/AttendanceViewModel.cs` | `BuildHeatmap` / `RefreshHeatmapCells` / `Prev·NextHeatmapMonthCommand` / `ToDotColor`（→ `internal static`）/ `OpenHeatmapDay` |
| `Views/AttendanceView.xaml` / `.xaml.cs` | 热力图面板（Row 2）+ 格色 DataTrigger + `HeatmapCell_Click`；`x:Name` 锚点（`HeatmapPanel`/`PrevHeatmapBtn`/`NextHeatmapBtn`/`HeatmapTitle`） |
| `Resources/DesignTokens.xaml` | 补 6 个 `Leave*LightBrush` 淡色底令牌 |
| `ViewModels/PagedCollectionViewModelBase.cs` | **P2 ⑧** 通用多选批量框架（`SelectedItems`/`IsMultiSelectMode`/`BatchDeleteCommand`/`DeleteItemsAsync`）+ 两个抽象钩子 |
| `ViewModels/{Knowledge,Issue}ViewModel.cs` | **P2 ⑧** 实现 `DeleteItemAsync`/`RestoreItemAsync` 钩子（各接仓储） |
| `ViewModels/WorkRecordViewModel.cs` | **P2 ⑧** 独立多选套件（`SelectedRecords`/`BatchDeleteRecordsCommand`/`DeleteSelectedRecordsAsync`） |
| `Views/{WorkRecord,Knowledge,Issue}View.xaml` | **P2 ⑧** 多选 ToggleButton + 批量操作栏 + DataGrid `SelectionMode`/`SelectedItemsList` 绑定 |
| `Resources/Styles.xaml` | **P2 ⑧** 全局注册 `BoolToVis` / `SelModeConv` 转换器实例 |
| `Models/InsightItem.cs` | **P2 ⑨** 洞察条目模型（`InsightSeverity` + `Kind`/`Title`/`Detail`/`ActionText`/`ActionTarget`/`HasAction`） |
| `Services/InsightAnalyzer.cs` | **P2 ⑨** 纯静态分析器（三条规则 + 聚合入口 + 阈值常量 + 假日数据降级） |
| `ViewModels/DashboardViewModel.cs` | **P2 ⑨** `Insights`/`HasInsights` + `LoadInsightsAsync`（取 45 天日状态 + 年度调休余额）+ `OpenInsightCommand`；构造注入 `CompLeaveBalanceService` + `DayStatusService` |
| `Views/DashboardView.xaml` | **P2 ⑨** 「需要留意」洞察卡（`ItemsControl` + 严重度 `DataTrigger` 配色 + 操作按钮跳转） |
| `Services/ConfigData.cs`·`ConfigService.cs`·`ViewModels/SettingsViewModel.cs` | **P2 ⑨** 新增 `ShowDashInsights` 开关（与既有 `ShowDash*` 完全同构） |

### 3.2 上一批（P0 / P1 ④⑤）关键文件

| 文件 | 作用 |
|---|---|
| `Helpers/SearchNavigationHelper.cs` | 命令面板位移规则（纯静态，可单测） |
| `Services/DraftService.cs` | 泛型草稿存取（键 = `typeof(T).Name`） |
| `Services/ConfigData.cs` / `ConfigService.cs` | 窗口几何 + 侧边栏字段（`double?` 哨兵）；`SaveWindowState(...)` |
| `Views/MainWindow.xaml` / `.xaml.cs` | WindowChrome 自绘标题栏 · 窗口几何还原/保存 · Ctrl+N 接线 · 命令面板 KeyDown |
| `Views/WorkRecordView.xaml` / `.xaml.cs` | `OpenNewForm()` 唯一入口 · 视觉树找首个输入框 · 草稿接入 |
| `Views/{Knowledge,Issue}View.xaml.cs` | 草稿存/取接入（`SaveDraftOnClose` / `OfferDraftRestore`） |
| `ViewModels/MainViewModel.cs` | `SelectedSearchIndex` + `MoveSearchSelectionCommand` + `ActivateSelectedResultCommand` |
| `Controls/DataGridBase.cs` | 基类 `IsReadOnly = true`（修工时清零 bug） |
| `Resources/Styles.xaml` | `WindowControlButton` / `WindowCloseButton`（含 `xmlns:shell`） |

### 3.3 测试（`EapWorkAssistant.Tests/`）

- 本轮新增：**`AttendanceHeatmapTests.cs`（18 条）** + **`BatchSelectionTests.cs`（5 条，P2 ⑧）** + **`InsightAnalyzerTests.cs`（20 条，P2 ⑨）**
- 上一批新增：`SearchNavigationHelperTests` / `ConfigSerializationTests` / `DraftServiceTests` / `WorkRecordEditLoadTests`
- 现有全部：`AttendanceViewModelTests` / `CompLeaveCalculatorTests` / `CsvParserTests` / `DashboardAggregationTests` / `DatabaseMigrationTests` / `DateTimeHelperTests` / `DayStatusServiceTests` / `IssueRepositoryTests` / `KnowledgeRepositoryTests` / `PaginationCalculatorTests` / `TrendCalculatorTests` / `WorkRecordImportServiceTests` / `WorkRecordRepositoryTests` / `TestDb.cs`

### 3.4 文档

- `AGENTS.md` — 工程规范；本轮新增 **8.22 图表主题 / 8.23 出勤热力图 / 8.24 列表多选批量 / 8.25 主动异常洞察**
- `.workbuddy/memory/MEMORY.md` — 项目长期备忘
- `.workbuddy/memory/2026-10-15.md` — P1 ⑥⑦ + P2 ⑧ 工作日志
- `.workbuddy/memory/2026-10-16.md` — P2 ⑨ 工作日志

---

## 4. 构建 / 测试 / 验证（沙箱环境必读）

### 4.1 每回合先自检环境

沙箱会在「裸命令可用 / env 前缀可用 / 都挂」之间翻转。**不要只看 exit 0**（沙箱会伪造）。

```bash
# 首选：复用绕法脚本（Python 补全环境变量后 subprocess 跑 dotnet）
C:/Users/Yang/.workbuddy/binaries/python/versions/3.13.12/python.exe \
  "C:/Users/Yang/AppData/Local/Temp/eap_dotnet_run.py" dotnet build EapWorkAssistant.csproj -c Debug -v:minimal
```

- 判据：见到 `已成功生成` / 无 `error` 行。
- ⚠️ `.sln` **不含测试项目**，测试命令必须显式指定 `EapWorkAssistant.Tests/EapWorkAssistant.Tests.csproj`。

### 4.2 测试基线（当前 181/181）

```bash
C:/Users/Yang/.workbuddy/binaries/python/versions/3.13.12/python.exe \
  "C:/Users/Yang/AppData/Local/Temp/eap_dotnet_run.py" \
  dotnet test EapWorkAssistant.Tests/EapWorkAssistant.Tests.csproj -c Debug -v:minimal
```

- 基线：**181 / 181 全绿**（161 基线 + P2 ⑨ 新增 20 条洞察分析单测）。

### 4.3 可复用脚本（`%TEMP%`）

| 脚本 | 用途 |
|---|---|
| `eap_dotnet_run.py` | 编译 / 测试（自动补 `APPDATA` 等环境变量） |
| `eap_smoke_run.py` | 启动冒烟：跑 9s，判据 = 无新 `error_*.log` |
| `eap_ctrln5_bom.ps1` | Ctrl+N UI 验证（含强制前台逻辑） |
| `chart_verify_v4*.ps1` / `chart_v1*.ps1` | 图表 hover 验证（强制前台 + 滚轮 + 沉降延时） |

### 4.4 UI 自动化验证 WPF（踩坑 → 已沉淀为技能 `wpf-ui-automation-verify`）

- **Bash 不能调 `powershell.exe`**（安全策略拦截）→ 用 PowerShell 工具，但该工具**无 stdout** → 脚本必须**写结果到文件再 Read**。
- ⚠️ **PS 5.1 读无 BOM 的 UTF-8 `.ps1` 会按 ANSI(GBK) 解码 → 中文乱码甚至坏语法**。执行前加 BOM：`printf '\xEF\xBB\xBF' > x.ps1 && cat src.ps1 >> x.ps1`。
- ⚠️ **跨 PS 命令子进程不存活**（进程树被清理）→ 启应用 + 操作 + 截图必须在**单条命令**内完成。
- ⚠️ **PS 内 `&` 调原生 exe 静默无操作**；`Start-Process` + 重定向被坑杀 → 分析逻辑内联 C#（`Add-Type`，含 `System.Drawing.Bitmap` 须加 `-ReferencedAssemblies 'System.Drawing'`）。
- ⚠️ **UIA `BoundingRectangle` 在窗口 resize/滚动期间滞后**（稳定后仍会变）→ 悬停坐标用截图 `Capture()` 返回值实时校正窗口尺寸；滚轮后等 2s 再读 rect。
- ⚠️ **PS 进程 DPI 不觉知**：`GetSystemMetrics` 被虚拟化（1536×864 vs 实际 1920×1080）→ 禁用 screen metrics 钳制悬停点，改用截图实际尺寸。
- ⚠️ **截「干净」图必须先移开光标**（`SetCursorPos` 到侧边栏 + 1.8s），否则残留 hover 态误导形态判断。`SetCursorPos` 用**物理屏坐标**。
- ⚠️ **WPF `ToolTip` 是独立 Popup 窗口，`PrintWindow(hwnd)` 抓不到**（图表 tooltip 画在 Skia 画布内所以能截）→ 格子 tooltip 只能走 UIA：`AutomationElement.FromPoint(坐标)` + `FindAll(Descendants, ControlType=ToolTip)`。
- 键盘注入需**真·前台**：`SetForegroundWindow` 被前台锁挡 → 组合 `AttachThreadInput` + `BringWindowToTop` + `SetActiveWindow` + `SetForegroundWindow` + `SwitchToThisWindow`。⚠️ `GetCurrentThreadId` 在 **kernel32.dll** 不在 user32.dll。鼠标 `InvokePattern` **不需**前台。
- UIA 定位要点：导航项是 **RadioButton**（`SelectionItemPattern.Select()`）；自定义控件（`PrimaryActionButton`）需**向上遍历祖先**找 `ControlType==Button`；Owned 模态窗口不在 `RootElement.Children`，要 `$win.FindFirst(Descendants, ...)`。`$TRUE` 是 PS 保留变量，脚本里换名。
- 探针法排查「点了没反应」：临时 `Logger.Error("[xxx-probe] ...")` → 跑 → 读 `logs/error_YYYYMMDD.log` → **定位后必须移除探针**。
- 造测试数据用 Python `json.dumps(..., ensure_ascii=False)` 写 UTF-8，**别用 PS `Set-Content`**（双编码乱码）。验证临时数据用 `UniqueId LIKE '-H%'` 标记 + DB 备份对比计数，事后精确删除。

---

## 5. 待办清单

### 5.1 P1（评审已定）—— ✅ 全部完成

| # | 项 | 状态 |
|---|---|---|
| ⑤ | 图标尺寸刻度 tokens（`IconSizeXs12/Sm14/Md16/Lg20/Hero40`） | ✅ `a25e18d` |
| ⑥ | 图表主题深化（柱圆角 / 网格弱化 / tooltip 卡片化 / hover 反馈） | ✅ `b9d323c` `37f6108` |
| ⑦ | 出勤月度热力图（迷你日历整格底色 + 月份导航） | ✅ `7ba9014` `33b9966` |

### 5.2 P2（进行中）

| # | 项 | 参考要点 |
|---|---|---|
| ⑧ | **列表多选批量操作** ✅ 已完成（`ffa4bcc`；框架在分页基类，规范见 AGENTS.md §8.24） | 工作记录 / 知识库 / 问题；批量软删除 + 撤销 |
| ⑨ | **主动异常洞察** ✅ 已完成（`fe17547`；纯静态 `InsightAnalyzer` + 仪表盘「需要留意」卡，规范见 AGENTS.md §8.25） | 调休余额临界 / 连续未填 / 填报偏离 8h → 仪表盘提示卡 |
| ⑩ | **首启引导 + 搜索扩展** | 全局搜索扩展到设置 / 出勤 / 回收站 |
| ⑪ | **观感打磨** | 空状态插画 + 列表骨架屏；StatCard 数值 count-up 动画；侧边栏 Logo 换矢量图标 |

### 5.3 未决 / 需用户拍板

| # | 事项 | 说明 |
|---|---|---|
| a | **DraftService 损坏加固** | `error_20261015.log` 存 24 行 `草稿解析失败（Knowledge）`（11:44，非本次引入）；`drafts.json` 损坏时静默丢弃。建议：损坏时**备份坏文件而非静默丢弃**。**待用户确认是否立项**。 |
| b | **备份文件清理** | `%LOCALAPPDATA%\EapWorkAssistant\` 下 `config.json.bak_ctrln_test`、`config.json.bak_darktest`、`config.json.bak_chartverify`、`eapwork.db.pre_actualhours_20261013_144047` 等，待用户确认后清理。 |
| c | **数据脏点 `2026-10-10`** | 现为 填报 4.0 / 实际 0.0（早前回写 bug 影响），**需用户在界面手动改回正确值**。 |
| d | **`ShortcutNewEnabled`** | 已被改为 `true`（为让 Ctrl+N 生效）。用户若想关闭：设置 → 快捷键 → 新增。 |

---

## 6. 开新窗口后的建议起手式

1. 先读 `AGENTS.md`（第 9 章红线 + 第 8 章 8.21~8.23 近期规范）+ `.workbuddy/memory/MEMORY.md` + 本文件。
2. 自检环境：跑一次 `eap_dotnet_run.py dotnet --version`，确认编译链可用。
3. 跑一次全量测试确认基线（当前 **181/181**）。
4. 从 **P2 队列**（⑧ 多选批量、⑨ 主动异常洞察 **均已完成**，接下来 ⑩ 首启引导 + 搜索扩展）开始；或按用户当次指令。
5. 每个 P 项完成后：编译 → 全量测试 → 必要时 UI/截图验证 → 更新 `AGENTS.md` → 按主题 commit（不 push）。
6. 真实终端复核推送状态：`git status` + `git log --oneline origin/master..HEAD`。
