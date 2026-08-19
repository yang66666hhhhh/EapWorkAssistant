# 靛蓝设计系统 · 补充组件规范

## 交付物

- **Ardot 设计文件**：`靛蓝设计系统 · 补充组件规范（按钮家族 & 密码输入）`
  - fileId: `716296385875347`
  - URL: https://ardot.tencent.com/file/716296385875347
- **导出 PNG**：`outputs/design-system/靛蓝设计系统_补充组件规范.png`

## 内容概要

本次补充针对此前 17 章设计规范中未完整覆盖的控件，补全设计稿并给出与 `Resources/Styles.xaml` 的映射关系。

### 1. 按钮家族 / Button Family

展示 Light & Dark 双主题下的 6 个按钮变体：

| 样式键 | 类型 | 视觉 |
|---|---|---|
| `BtnPrimary` | 主操作 | 靛蓝 #4338CA 实心，白字 |
| `BtnSecondary` | 次要操作 | 白底（Dark 为深底）+ 靛蓝描边，靛蓝字 |
| `BtnGhost` | 幽灵/文字按钮 | 透明底 + 靛蓝字 |
| `BtnSuccess` | 成功 | 绿 #16A34A 实心，白字 |
| `BtnWarning` | 警告 | 琥珀 #D97706 实心，白字 |
| `BtnDanger` | 危险 | 红 #DC2626 实心，白字 |

规格：高度 40，圆角 `RadiusMd=10`，字号 14 `Semi Bold`，字体 `Sarasa Gothic SC`。

### 2. 输入控件扩展 / Input Extensions

展示 Light & Dark 双主题下的 4 种输入控件：

- `Input` — 单行输入框
- `TextArea` — 多行输入框
- `PasswordBox` — 密码输入框（含显隐切换 eye toggle）
- `Select` — 下拉选择框

规格：输入框圆角 `RadiusSm=8`，边框使用 `BorderBrush`，占位符颜色 Light `#94A3B8` / Dark `#64748B`。

### 3. 代码映射

这些控件在 `Resources/Styles.xaml` 中均已定义；本次是将缺失的**设计稿**补齐到规范：

- 按钮家族对应 `BtnPrimary` / `BtnSecondary` / `BtnGhost` / `BtnSuccess` / `BtnWarning` / `BtnDanger`
- 输入控件对应 `Input` / `TextArea` / `Select`
- `PasswordBox` 尚未有独立样式，建议使用 `Input` 样式并额外放置 eye toggle

## 设计系统一致性

- 颜色、圆角、字体与已有 17 章靛蓝双主题规范保持一致。
- 深色主题下：`Secondary` 描边使用 `#6366F1`，`Ghost` 文字使用 `#818CF8`。
