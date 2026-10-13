# EAP Work Assistant — 交接摘要

> 生成时间：2026-10-13 17:24（会话交接用）
> 上一轮主题：**UI/UX 评审 + P0 三项落地 + P1 ④ 自绘标题栏**
> 当前状态：**测试 138/138 全绿 · 工作区干净 · 5 个 commit 未 push**

---

## 0. 项目速览

| 项 | 值 |
|---|---|
| 项目 | EapWorkAssistant（EAP 工时 / 问题 / 知识库管理桌面应用） |
| 技术栈 | .NET 10 (`net10.0-windows`) + WPF + MVVM（CommunityToolkit.Mvvm 8.4.2） |
| 存储 | SQLite（软删除 `IsDeleted=1`，迁移靠 `PRAGMA user_version`） |
| 主题 | 自定义 indigo 双主题 ResourceDictionary，运行时由 `ThemeService` 覆盖画刷 |
| 仓库 | `C:\Users\Yang\Documents\EapWorkAssistant`（git，**仅本地 commit，不 push**） |
| 应用数据 | `%LOCALAPPDATA%\EapWorkAssistant\`（`config.json` / `eapwork.db` / `drafts.json` / `logs\error_YYYYMMDD.log`） |

---

## 1. 本轮进度（已完成并提交）

### 1.1 上一批（承接上一会话，已提交）

| commit | 说明 |
|---|---|
| `1c142a3` | feat: 工作记录工时双口径与历史数据迁移 |
| `30b8f7b` | feat: 新增出勤总览模块 |
| `5244186` | docs: 同步 AGENTS.md 规范 |

### 1.2 本轮（本次会话新增，均已提交）

| commit | 主题 | 文件数 |
|---|---|---|
| `d12bf7a` | **fix: 修复编辑记录时工时显示为 0** | 3 |
| `62d71db` | **feat: 窗口几何持久化 + 命令面板键盘导航（P0）** | 9 |
| `83cae69` | **feat: 表单草稿恢复（P0 ③）** | 6 |
| `5d5af29` | **fix: Ctrl+N 打开「新增记录」抽屉并聚焦首个输入框** | 4 |
| `882b205` | **feat: 自绘标题栏（P1 ④）** | 4 |
| `a25e18d` | **ui: 图标尺寸统一为 IconSize 五档刻度（P1 ⑤）** | 17 |

> 全部**未 push**（按用户约定，push 由本人在真实终端执行）。`git log --oneline origin/main..HEAD` 可看未推送清单。

### 1.3 各项交付要点

**① 窗口几何持久化（P0）**
- `ConfigData` 新增 `WindowWidth/Height/Left/Top/Maximized/SidebarCollapsed`；`ConfigService.SaveWindowState(...)`，仅在 `OnClosing` 写一次（**不挂 `SizeChanged` 逐帧落盘**）。
- `MainWindow.RestoreWindowState()`（构造期）/ `PersistWindowState()`（`OnClosing`）/ `IsWindowOnScreen()`（虚拟屏幕探针）。
- ⚠️ 位置哨兵用 `double?`（null），**禁止 `double.NaN`**。

**② 命令面板键盘导航（P0）**
- 搜索结果 `ItemsControl` → `ListBox`，`SelectedIndex` 双向绑 `MainViewModel.SelectedSearchIndex`。
- ↑↓ 移动高亮 + `ScrollIntoView`（焦点留在输入框）· Enter 打开高亮项 · Esc 关闭。
- 位移规则唯一事实来源：`Helpers/SearchNavigationHelper.Move()`（纯静态，可单测）。

**③ 表单草稿恢复（P0）**
- 新增 `Services/DraftService.cs`：泛型存取（键 = `typeof(T).Name`），落 `drafts.json`，构造可传路径供测试隔离；IO/解析失败**一律吞异常 + Logger**。
- 三模块（知识库/问题跟踪/工作记录）：`FormDrawer_Closed` 首行存草稿（须在重置表单**之前**）→ `OpenForm_Click` 末尾询问恢复（**读取即消费**：`Get` 后立刻 `Clear`）。
- 恢复动作收进各 VM 的 `RestoreDraft()`（`_suppressDirty` 是 private，View 写不了）。

**④ Ctrl+N 修复**
- 范式：VM 事件 `OpenNewFormRequested` + View 在 `OnDataContextChanged` 订阅；MainWindow 走 `Dispatcher.BeginInvoke(RequestNewForm, DispatcherPriority.Loaded)`。
- `OpenNewForm()` 成为「新增记录」按钮与快捷键的**唯一入口**（抽屉已开时只聚焦不重置）。

**⑤ 自绘标题栏（P1 ④）**
- `shell:WindowChrome`（`CaptionHeight=40` / `ResizeBorderThickness=6` / `GlassFrameThickness=0` / `UseAeroCaptionButtons=False`）；窗口内容包 `Grid x:Name="RootLayout"`（Row0=标题栏 40px，Row1=侧边栏+内容区）。
- 标题栏底色 `SidebarBgBrush`（亮 `#F0F4F8` / 暗 `#0F1726`），与侧边栏无缝。
- 新增 `WindowControlButton` / `WindowCloseButton` 样式（⚠️ 必须带 `shell:WindowChrome.IsHitTestVisibleInChrome="True"`）。
- 截图产物：`outputs/titlebar-{light,dark,maximized}.png`。

---

## 2. 已定约束（硬性红线，改动前必读）

> 完整版见 `AGENTS.md` 第 9 章「AI 行为约束红线」与 `.workbuddy/memory/MEMORY.md`。以下是**最容易踩**的几条。

### 2.1 数据 / 口径

| # | 约束 |
|---|---|
| 1 | **工时双口径**：`WorkRecord.Hours` = 填报工时（报给公司，每天 8h）；`WorkRecord.ActualHours` = 实际工时（自己真实投入）。**禁止「未填回落」**（`EffectiveActualHours` 已删除），未填就是 0，展示走 `ActualHoursText`（显示「—」）。 |
| 2 | **调休余额按【填报工时】算**；实际工时只记录/展示，**不参与任何余额或统计口径计算**。 |
| 3 | 工作日/假日判定唯一事实来源：`Services/DayStatusRules.cs`（纯静态）。**禁止另写判定**；改判定必须同时覆盖「周末 / 补班 / 法定假日」三种情形。 |
| 4 | 迁移：`LatestSchemaVersion` = 10；靠 `PRAGMA user_version` 只跑一次。**重跑迁移须先从备份恢复库**（备份 `eapwork.db.pre_*_时间戳` 存 `%LOCALAPPDATA%\EapWorkAssistant\`）。 |

### 2.2 WPF 绑定 / 表格

| # | 约束 |
|---|---|
| 5 | 只读计算属性（表达式体 `=>`）绑 `<Run Text=...>` 必须显式 `Mode=OneWay`（`Run.Text` 默认 TwoWay，否则运行时抛异常）。 |
| 6 | **主表统一只读**：`Controls/DataGridBase.cs` 构造已设 `IsReadOnly = true`，勿改。 |
| 7 | **表格排序范式**：View `DataGrid.Sorting` → code-behind `e.Handled=true` + `vm.ToggleSort(...)`；排序键必须绑**真值**（DateTime/double），不能是格式化字符串。 |
| 8 | **手工复制实体的方法（如 `EditRecord`）新增字段必须同步复制**，否则新字段恒为默认值。 |

### 2.3 配置 / 资源

| # | 约束 |
|---|---|
| 9 | **配置类里不要用 `double.NaN` 当哨兵**（`System.Text.Json` 无法序列化 NaN，一保存就抛异常）。 |
| 10 | 令牌以 `Resources/DesignTokens.xaml` 为准，禁止硬编码颜色/圆角/字号（`AGENTS.md` 9.2）。 |
| 11 | caption 区内的交互控件必须 `shell:WindowChrome.IsHitTestVisibleInChrome="True"`。 |
| 12 | **不要为「WindowChrome 最大化裁切」加 Margin 补丁**——实测客户端矩形恰等于工作区，加 Margin 只会露缝。 |

### 2.4 协作 / 流程

| # | 约束 |
|---|---|
| 13 | **仅本地 commit，不 push**（push 由用户在真实终端执行）。 |
| 14 | 按**主题拆 commit**，中文 commit message。 |
| 15 | `outputs/` 已在 `.gitignore`，截图/产物不会污染仓库。 |

---

## 3. 关键文件路径

### 3.1 本轮新增 / 重点改动

| 文件 | 作用 |
|---|---|
| `Helpers/SearchNavigationHelper.cs` | **新增** · 命令面板位移规则（纯静态，可单测） |
| `Services/DraftService.cs` | **新增** · 泛型草稿存取服务 |
| `Services/ConfigData.cs` | 窗口几何 + 侧边栏折叠字段（`double?` 哨兵） |
| `Services/ConfigService.cs` | `SaveWindowState(...)` + 只读 getter |
| `Views/MainWindow.xaml` / `.xaml.cs` | WindowChrome 自绘标题栏 · 窗口几何还原/保存 · Ctrl+N 接线 · 命令面板 KeyDown |
| `Views/WorkRecordView.xaml` / `.xaml.cs` | `OpenNewForm()` 唯一入口 · 视觉树找首个输入框 · 草稿接入 |
| `Views/KnowledgeView.xaml.cs`、`Views/IssueView.xaml.cs` | 草稿存/取接入（`SaveDraftOnClose` / `OfferDraftRestore`） |
| `ViewModels/MainViewModel.cs` | `SelectedSearchIndex` + `MoveSearchSelectionCommand` + `ActivateSelectedResultCommand` |
| `ViewModels/WorkRecordViewModel.cs` | `OpenNewFormRequested` 事件 + `RequestNewForm()` + `RestoreDraft()` |
| `ViewModels/{Knowledge,Issue}ViewModel.cs` | `RestoreDraft()` |
| `Controls/DataGridBase.cs` | 基类 `IsReadOnly = true`（修工时清零 bug） |
| `Resources/Styles.xaml` | `WindowControlButton` / `WindowCloseButton`（新增 `xmlns:shell`） |
| `Resources/DesignTokens.xaml` | 设计令牌（改样式先读这里） |

### 3.2 测试（`EapWorkAssistant.Tests/`）

本轮新增：`SearchNavigationHelperTests.cs`、`ConfigSerializationTests.cs`、`DraftServiceTests.cs`、`WorkRecordEditLoadTests.cs`
现有全部：`AttendanceViewModelTests` / `CompLeaveCalculatorTests` / `CsvParserTests` / `DashboardAggregationTests` / `DatabaseMigrationTests` / `DateTimeHelperTests` / `DayStatusServiceTests` / `IssueRepositoryTests` / `KnowledgeRepositoryTests` / `PaginationCalculatorTests` / `TrendCalculatorTests` / `WorkRecordImportServiceTests` / `WorkRecordRepositoryTests` / `TestDb.cs`

### 3.3 文档

- `AGENTS.md` — 工程规范，本轮新增 **8.16 窗口持久化 / 8.17 命令面板导航 / 8.18 草稿恢复 / 8.19 快捷键跨视图触发抽屉 / 8.20 自绘标题栏**
- `.workbuddy/memory/MEMORY.md` — 项目长期备忘（本会话已整理合并）
- `.workbuddy/memory/2026-10-13.md` — 当日工作日志

---

## 4. 构建 / 测试 / 验证（沙箱环境必读）

### 4.1 每回合先自检环境

沙箱会在「裸命令可用 / env 前缀可用 / 都挂」之间翻转。**不要只看 exit 0**（沙箱会伪造）。

```bash
# 首选：复用已有绕法脚本（Python 补全环境变量后 subprocess 跑 dotnet）
C:/Users/Yang/.workbuddy/binaries/python/versions/3.13.12/python.exe \
  "C:/Users/Yang/AppData/Local/Temp/eap_dotnet_run.py" dotnet build EapWorkAssistant.csproj -c Debug -v:minimal
```

- 判据：见到 `已成功生成` / 无 `error` 行。
- ⚠️ `.sln` **不含测试项目**，测试命令必须显式指定 `EapWorkAssistant.Tests/EapWorkAssistant.Tests.csproj`。

### 4.2 测试基线

```bash
C:/Users/Yang/.workbuddy/binaries/python/versions/3.13.12/python.exe \
  "C:/Users/Yang/AppData/Local/Temp/eap_dotnet_run.py" \
  dotnet test EapWorkAssistant.Tests/EapWorkAssistant.Tests.csproj -c Debug -v:minimal
```

- 基线：**138 / 138 全绿**（有 119 个 `[Fact]/[Theory]` 特性，`[Theory]` 展开后 138 个用例）。

### 4.3 可复用脚本（`%TEMP%`）

| 脚本 | 用途 |
|---|---|
| `eap_dotnet_run.py` | 编译 / 测试（自动补 `APPDATA` 等环境变量） |
| `eap_smoke_run.py` | 启动冒烟：跑 9s，判据 = 无新 `error_*.log` |
| `eap_persist_test.py` | 端到端窗口持久化：启 exe → `WM_CLOSE` → 读 `config.json` |
| `eap_ctrln5_bom.ps1` | Ctrl+N UI 验证（含强制前台逻辑） |

### 4.4 UI 自动化验证 WPF（踩坑全记录 → 已沉淀为技能 `wpf-ui-automation-verify`）

- **Bash 不能调 `powershell.exe`**（安全策略拦截）→ 用 PowerShell 工具，但该工具**本会话无 stdout** → 脚本必须**写结果到文件再 Read**。
- ⚠️ **PS 5.1 读无 BOM 的 UTF-8 `.ps1` 会按 ANSI(GBK) 解码 → 中文乱码甚至坏语法**。执行前加 BOM：`printf '\xEF\xBB\xBF' > x.ps1 && cat src.ps1 >> x.ps1`。
- ⚠️ **键盘注入需要「真·前台」**：`SetForegroundWindow` 会被前台锁挡住（返回 True 但前台仍是宿主）。须 `AttachThreadInput` + `BringWindowToTop` + `SetActiveWindow` + `SetForegroundWindow` + `SwitchToThisWindow` 组合。鼠标 `InvokePattern` **不需要**前台。
- UIA 定位要点：导航项是 **RadioButton**（`SelectionItemPattern.Select()`）；自定义控件（`PrimaryActionButton`）需**向上遍历祖先**找 `ControlType==Button`；**Owned 模态窗口不在 `RootElement.Children`**，要 `$win.FindFirst(Descendants, ...)`。
- `$TRUE` 是 PS 保留变量，脚本里换名。
- 探针法排查「点了没反应」：临时 `Logger.Error("[xxx-probe] ...")` → 跑 → 读 `logs/error_YYYYMMDD.log` → **定位后必须移除探针**。
- 造测试数据用 Python `json.dumps(..., ensure_ascii=False)` 写 UTF-8，**别用 PS `Set-Content`**（双编码乱码）。
- 截图：`PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT=0x2)` 可捕获**非前台** WPF 窗口；`Add-Type` 包装类含 `System.Drawing.Bitmap` 时须加 `-ReferencedAssemblies 'System.Drawing'`。

---

## 5. 待办清单

### 5.1 P1 剩余（评审已定，按序推进）

| # | 项 | 要点 | 状态 |
|---|---|---|---|
| ⑤ | **图标尺寸刻度 tokens** | 现状图标尺寸散落 `11/12/14/16/18/22/36` → 在 `DesignTokens.xaml` 加 `IconSizeXs/Sm/Md/Lg/Hero`，像 `Radius` 那样统一 | ✅ 已完成（`a25e18d`） |
| ⑥ | **图表主题深化** | `DashboardViewModel.ApplyChartTheme()`（约 360 行）目前只「取色」→ 加柱圆角、弱化/去掉粗网格、tooltip 卡片化、hover 数据点放大、动画时长并入 `Duration` 令牌 | ⏳ 未开始 |
| ⑦ | **出勤月度热力图** | `Views/AttendanceView.xaml` 加迷你日历/热力图，复用现有圆点语义（amber/green/blue/red/purple/gray） | ⏳ 未开始 |

### 5.2 P2（评审已列，未排期）

| # | 项 |
|---|---|
| ⑧ | 列表多选批量（工作记录/知识库/问题；回收站已有 `SelectionMode=Extended` 可参考） |
| ⑨ | 主动异常洞察（调休余额临界 / 连续未填 / 填报偏离 8h）→ 仪表盘提示卡 |
| ⑩ | 首启引导；全局搜索扩展到设置/出勤/回收站 |
| ⑪ | 空状态插画 + 列表骨架屏；StatCard 数值 count-up 动画；侧边栏 Logo 换矢量图标 |

### 5.3 未决 / 需用户拍板

- **`ShortcutNewEnabled` 已被改为 `true`**（为让 Ctrl+N 生效）。用户若想关闭：设置 → 快捷键 → 新增。
- 备份文件待用户确认后清理：`%LOCALAPPDATA%\EapWorkAssistant\` 下 `config.json.bak_ctrln_test`、`config.json.bak_darktest`、以及早前的 `eapwork.db.pre_actualhours_20261013_144047`。
- 数据脏点：`2026-10-10` 现为 填报 4.0 / 实际 0.0（受早前回写 bug 影响），需用户在界面手动改回正确值。

---

## 6. 开新窗口后的建议起手式

1. 先读 `AGENTS.md`（第 9 章红线 + 第 8 章本轮新增规范）+ `.workbuddy/memory/MEMORY.md`。
2. 自检环境：跑一次 `eap_dotnet_run.py dotnet --version`，确认编译链可用。
3. 跑一次全量测试确认基线仍是 **138/138**。
4. 从 **P1 ⑥ 图表主题深化** 开始（⑤ 图标刻度已于 `a25e18d` 完成）；或按用户当次指令。
5. 每个 P1 项完成后：编译 → 全量测试 → 必要时 UI/截图验证 → 更新 `AGENTS.md` → 按主题 commit（不 push）。
