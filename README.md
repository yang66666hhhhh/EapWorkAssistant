# EAP Work Assistant v2.0

试用期工程师工作助手 — 高效记录工作、积累亮点、生成转正述职报告

## 功能特性

### 核心功能

- **工作台仪表盘** - 一目了然查看工作概览
  - 今日/本周/本月工时统计
  - 试用期进度追踪（时间进度、记录覆盖率）
  - 本周工时趋势图表（LiveCharts2）
  - 本月项目分布饼图
  - 工作亮点展示
  - 自定义日历视图
  - 一键生成转正述职报告

- **工作记录** - 每日工作内容管理
  - 新增/编辑/删除工作记录
  - 支持多项目、多工作类型
  - 问题记录与解决方案
  - 亮点标记（用于述职报告）
  - 生成日报/周报/月报
  - 内容模板快速填充

- **知识库** - 技术知识积累
  - 支持标题、内容、标签
  - 关键词搜索

- **问题跟踪** - 设备问题管理
  - 记录问题描述、根因分析、解决方案
  - 按项目分类

- **系统设置** - 个性化配置
  - 主题切换（深色/浅色模式）
  - 字号调节（小/中/大三档，全局缩放）
  - 界面密度（紧凑/默认/宽松三档）
  - 默认视图选择
  - 自动保存间隔设置
  - 任务列表管理
  - 工作类型管理
  - 内容模板管理
  - 个人信息设置
  - 快捷键开关
  - 定时提醒设置

### 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+N` | 快速新增工作记录 |
| `Ctrl+S` | 保存当前记录 |
| `Esc` | 关闭面板 |
| `Ctrl+1` | 切换到工作台 |
| `Ctrl+2` | 切换到工作记录 |
| `Ctrl+3` | 切换到知识库 |
| `Ctrl+4` | 切换到问题跟踪 |
| `Ctrl+5` | 切换到设置 |

### 数据安全

- 自动备份：每天启动时自动备份数据库，保留30天
- 数据存储位置：`%LOCALAPPDATA%\EapWorkAssistant\`

### 智能提醒

- 下班时间（17:30后）自动检查是否已记录工作
- 如果未记录，弹窗提醒用户记录

### 全局搜索（Spotlight 风格）

- 浮动弹窗式搜索界面（Ctrl+K 唤出）
- 支持搜索工作记录、知识库、问题跟踪
- 按 Enter 搜索，按 Esc 关闭
- 点击结果可跳转到对应页面

### 数据导出

- 支持导出日报/周报/月报（文本格式）
- 支持导出CSV格式（可用Excel打开）
- CSV文件包含中文BOM头，确保Excel正确显示

## 技术栈

- **框架**: .NET 9.0 + WPF
- **架构**: MVVM (CommunityToolkit.Mvvm 8.4.2)
- **数据库**: SQLite + Dapper
- **图表**: LiveCharts2 (SkiaSharp)
- **UI**: 自定义现代深色风格控件

## 项目结构

```
EapWorkAssistant/
├── Data/                    # 数据库初始化
├── Helpers/                 # 转换器和辅助类
├── Models/                  # 数据模型
├── Repositories/            # 数据访问层
├── Resources/               # 样式资源（Styles.xaml）
├── Services/                # 业务服务（ThemeService, ConfigService 等）
├── ViewModels/              # 视图模型
├── Views/                   # 视图界面
├── App.xaml                 # 应用入口
├── AGENTS.md                # AI 编程约束文件
└── EapWorkAssistant.csproj  # 项目文件
```

## 运行要求

- Windows 10/11
- .NET 9.0 Runtime

## 开发环境

1. 安装 [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
2. 克隆项目
   ```bash
   git clone https://github.com/yang66666hhhhh/EapWorkAssistant.git
   ```
3. 运行项目
   ```bash
   cd EapWorkAssistant
   dotnet run
   ```

## 数据备份

数据库文件位于：`%LOCALAPPDATA%\EapWorkAssistant\eapwork.db`

备份文件位于：`%LOCALAPPDATA%\EapWorkAssistant\backups\`

建议定期手动备份数据库文件。

## AI 辅助开发

本项目包含 `AGENTS.md` 文件，定义了 AI 辅助编程时需要遵守的架构约束、命名规范、WPF 陷阱提醒等规则。使用 AI 编程工具时请确保该文件存在于项目根目录。

## 许可证

MIT License
