# cccalendar UI 设计规范

> 目标：像一款长期使用的 Windows 工作软件，而不是生成式 AI 演示页。

## 1. 视觉原则

1. 信息优先：日期、任务状态和下一步操作永远比装饰更突出。
2. 平静克制：不使用紫蓝渐变、霓虹光、装饰光球、发光边缘或大面积玻璃效果。
3. 少卡片：页面区块使用留白和分隔线组织；卡片只用于重复项目、浮层和确实需要边界的工具。
4. 紧凑但不拥挤：常用信息可扫描，编辑操作靠近对象，不用多层弹窗。
5. Windows 习惯：右键菜单、键盘焦点、系统托盘、快捷键和窗口层级行为符合桌面软件预期。
6. AI 降权：AI 使用和其他工具相同的视觉语言，不使用星光图标、渐变头像或聊天气泡。

## 2. 设计令牌

### 2.1 字体

- 界面字体：Segoe UI Variable；中文回退为 Microsoft YaHei UI。
- 正文：14 px；辅助信息：12 px；紧凑表格：13 px。
- 面板标题：16 px；页面标题：20 px；只有时钟可使用 28-36 px。
- 字重使用 Regular、Semibold 两档，不使用负字距，不随视口宽度缩放字体。

### 2.2 间距与尺寸

- 基础间距：4、8、12、16、24 px。
- 普通控件高度：32 px；紧凑工具栏：28 px；主要输入：36 px。
- 图标按钮：32 x 32 px，图标 16 或 18 px，尺寸固定不随内容变化。
- 圆角：输入和按钮 10 px；菜单、浮层、独立重复项 12 px；弹窗与下拉 16 px。**上限 16 px**（2026-09-16 由用户裁决放开原 8 px 上限，以贴近参考实现的柔和观感）。
- 侧栏宽度：192-216 px；详情编辑面板：360-440 px。

### 2.3 颜色

浅色主题：

- 页面背景：`#F5F6F7`
- 主表面：`#FFFFFF`
- 主文字：`#20242A`
- 次文字：`#626A75`
- 边框：`#D9DEE5`
- 默认强调色：`#246BCE`

深色主题：

- 页面背景：`#181A1E`
- 主表面：`#22252A`
- 主文字：`#F0F2F4`
- 次文字：`#A8AFB8`
- 边框：`#383D45`
- 默认强调色：`#5794E6`

功能色：完成 `#2E7D4F`、警告 `#A76500`、逾期 `#C43D4B`、信息使用强调色。项目颜色由用户选择，但文字与背景必须满足可读性校验。

强调色只用于选中状态、主操作、今天和焦点，不铺满整个界面。状态不能只靠颜色表达，还需要图标、文本或形状。

### 2.4 设计令牌表（WPF 实现基线）

令牌全部声明在 `src/CcCalendar.Desktop/Themes/Theme.xaml`，颜色类在 `Themes/DarkTheme.xaml` 中覆写；间距/圆角/字号/控件尺寸/动效为跨主题常量，只在浅色字典中声明一次。

**间距**（`sys:Double`）：`SpacingXs` 4、`SpacingSm` 8、`SpacingMd` 12、`SpacingLg` 16、`SpacingXl` 24。

**圆角**（必须是 `CornerRadius` 类型，不能是 `sys:Double`——见 §11 的类型陷阱）：`RadiusMd` 6（小组件内部：进度条、滑块轨道、勾选框）、`RadiusSm` 10（按钮、输入框、列表项）、`RadiusLg` 12（卡片、面板、抬高表面）、`RadiusXl` 16（弹窗、下拉、上下文菜单、提示）。上限 16。

**字号**（`sys:Double`）：`FontCaptionSize` 12、`FontCompactSize` 13、`UiBodyFontSize` 14（正文）、`FontPanelTitleSize` 16、`FontBrandSize` 18（品牌字标与工具页统计数字）、`FontPageTitleSize` 20、`FontStatNumberSize` 24（统计卡 KPI 数字）、`FontClockSize` 32、`FontTimerSize` 44（倒计时/番茄钟大数字）。

> 阶梯为 12/13/14/16/18/20/24/32/44，由契约测试 `ViewsUseOnlyArchivedFontSizes` 守住：页面与窗口 XAML 不得出现阶梯外的字号字面量。
> **豁免**：`DesktopComponentWindow`、`DesktopWorkbenchWindow`、`QuickPanelWindow`、`ReminderPopupWindow` 是紧凑型表面——桌面组件可被用户缩到 220px 宽，字号由组件自身的缩放/字号设置驱动，提醒弹窗为 380×190 固定尺寸。这四个文件不在该契约的扫描范围内，改动它们需要人工布局核验。

**控件尺寸**（`sys:Double`）：`UiControlHeight` 32、`UiCompactControlHeight` 28、`ControlHeightPrimary` 36、`IconButtonSize` 32、`SidebarWidth` 200、`BrandBarHeight` 56、`NavigationItemHeight` 38。

**动效**：`MotionFast` 120ms、`MotionBase` 160ms（均为 `Duration`），`EasingStandard`（`CubicEase`，EaseInOut）。

**语义画刷**（浅色值 → 深色值由 `DarkTheme.xaml` 覆写）：

| 令牌 | 用途 |
|------|------|
| `PageBackgroundBrush` / `SurfaceBrush` / `SubtleSurfaceBrush` / `ControlSurfaceBrush` | 页面与表面 |
| `HoverBrush` / `PressedBrush` | 交互反馈 |
| `TextPrimaryBrush` / `TextSecondaryBrush` | 文字层级 |
| `BorderBrush` | 分隔线 |
| `AccentBrush` / `AccentHoverBrush` / `AccentPressedBrush` / `AccentSubtleBrush` | 强调色四态 |
| `SuccessBrush` / `WarningBrush` / `DangerBrush` | 功能色 |
| `FocusRingBrush` | 键盘焦点环 |
| `ScrollbarThumbBrush` / `ScrollbarThumbHoverBrush` | 滚动条 |
| `OverlayScrimBrush` | 模态与区域选择遮罩 |
| `ElevationPanelBrush` / `ElevationProminentBrush` | 抬高面板 |

**会议室看板专用色**（自绘控件无法绑定 XAML，由 `ViewModels\RoomBoardPalette.cs` 解析，解析失败回落默认值）。这是看板颜色的**唯一来源**：

| 令牌 | 浅色 | 深色 | 用途 |
|------|------|------|------|
| `RoomBoardGridSurfaceBrush` | `#FFFFFF` | `#22252A` | 空闲格底面 |
| `RoomBoardGridHourBrush` | `#D9DEE5` | `#383D45` | 整点分隔线 |
| `RoomBoardGridHalfHourBrush` | `#F0F2F5` | `#2C3038` | 半小时分隔线 |
| `RoomBoardOccupiedFillBrush` | `#E4E7EA` | `#2F333A` | 他人占用底色 |
| `RoomBoardOccupiedTextBrush` | `#5C6470` | `#A8AFB8` | 他人占用文字 |
| `RoomBoardOwnedFillBrush` | `#DCEBFF` | `#1C2A38` | 本人占用底色 |
| `RoomBoardOwnedTextBrush` | `#174A8B` | `#8FB8F0` | 本人占用文字 |
| `RoomBoardAccentBarBrush` | `#174A8B` | `#8FB8F0` | 本人占用左侧 3px 标记条 |
| `RoomBoardSelectedFreeFillBrush` | `#D8F0DF` | `#1C3A2A` | 选中且空闲底色 |
| `RoomBoardSelectedFreeTextBrush` | `#1F5B3A` | `#7FCFA2` | 选中且空闲文字 |
| `RoomBoardConflictFillBrush` | `#F9E0E3` | `#3A2026` | 选中但冲突底色 |
| `RoomBoardConflictTextBrush` | `#8F2231` | `#F09AA5` | 选中但冲突文字 |
| `RoomBoardHandleFillBrush` | `#FFFFFF` | `#22252A` | 选中块四角调节点填充 |
| `RoomBoardPreviewFillBrush` | `#33246BCE` | `#335794E6` | 拖拽预览填充 |

**约束**：看板文字与其底色的对比度必须 ≥ 4.5:1（WCAG AA），由 `ViewModels\ColorContrast.cs` 计算并在测试中断言。当前实测浅色 4.82 / 7.27 / 6.67 / 6.88，深色 5.73 / 7.15 / 6.72 / 6.98。

## 3. 主窗口布局

```text
+----------------+--------------------------------------------------+
| 今天           | 搜索                           快速新增  设置     |
| 日历           +--------------------------------------------------+
| 项目           | 页面标题 / 视图切换 / 筛选                       |
| 待办           +--------------------------------------------------+
| 记录           |                                                  |
| 助理           |                 当前工作区                       |
| 统计           |                                                  |
| 工具           |                                                  |
|                |                                                  |
| 设置           |                                                  |
+----------------+--------------------------------------------------+
```

- 左侧导航保持固定宽度，可折叠为仅图标模式。
- 页面标题区紧凑，不使用营销式大标题和功能介绍。
- 常规新增/编辑使用右侧详情面板；危险操作使用确认对话框。
- 列表、看板、四象限和日历共用筛选条件，切换视图不丢失上下文。
- 空状态只说明当前为空并提供一个明确动作，不展示插画或长篇教程。

## 4. 日历与桌面工作台

- 月历固定 7 列、最多 6 行，布局不因内容变化跳动。
- 日期格顶部显示公历和农历；日程区域最多显示 3 项，其余为“还有 N 项”。
- 跨天事项用连续色带，普通日程用短色条，待办用方形状态标记。
- 周末、节假日、调休和今天使用不同但克制的文字/底色组合。
- 点击日期更新右侧日程与待办；双击日期打开快速新增。
- 本周模式保留同一信息语法，只减少为一行 7 天。
- 桌面组件背景允许透明、纯色、云母或亚克力；内容可读性优先于壁纸透出程度。

## 5. 待办与项目

- 四象限是待办默认视图，四个区域使用标题和边框区分，不用四块饱和大色块。
- 任务项显示完成框、标题、截止时间、项目色条和子任务进度；次要字段按需展开。
- 看板列固定宽度并支持横向滚动；拖动时明确显示目标位置。
- 甘特图的时间轴与任务列表对齐，依赖线只在选中或悬停时增强，避免视觉噪音。

## 6. AI 助理界面

AI 页面采用“工作记录 + 命令输入”，不用聊天应用样式：

```text
+---------------------------------------------------------------+
| 范围：cccalendar 项目       模型：本地/在线       数据权限     |
+---------------------------------------------------------------+
| 10:14  你：把评审后的事项安排到本周                           |
|                                                               |
| 助理分析了 4 个待办，发现 1 个时间冲突。                       |
|                                                               |
| 变更预览                                                      |
| +  周二 14:00-15:30  登录页修改                               |
| +  周三 09:00-10:00  接口联调                                 |
| !  周四安排与项目会议重叠                                    |
|                                      取消  修改  确认应用      |
+---------------------------------------------------------------+
| 输入安排、查询或粘贴会议记录...                               |
+---------------------------------------------------------------+
```

- 不使用机器人头像、星光、渐变气泡、“魔法生成”等文案。
- 工具调用展示为简洁的操作记录；失败时说明哪一步失败以及数据是否已写入。
- 任何写操作预览都显示对象、时间、项目和冲突；删除使用明确的危险色按钮。
- 在线模型旁显示联网状态和本次将发送的数据范围。
- 快速面板里的 AI 是单行命令入口，复杂对话转入主窗口并保留上下文。

## 7. 交互与动效

- 常规过渡 120-180 ms，只使用淡入、位移和尺寸变化。
- 尊重 Windows 减少动画和高对比度设置。
- 所有图标按钮提供工具提示；常用操作同时支持键盘。
- 拖放不是唯一操作方式，右键菜单和详情面板提供等价命令。
- 加载、离线、失败、空数据、权限拒绝和冲突必须有明确状态。
- 删除、覆盖、批量修改和 AI 写操作可撤销或进入回收站。

## 8. 文案规则

- 使用“新增日程”“移到明天”“确认应用”等直接命令。
- 不使用“开启高效人生”“让 AI 为你赋能”等宣传文案。
- 不在界面中长期展示操作教程；帮助信息进入工具提示或帮助页。
- 日期、时间和错误信息使用一致格式，错误需要给出下一步。

## 9. 视觉验收

每个主要页面完成时至少验证 1920 x 1080 和 1366 x 768；桌面组件另外验证浅色、深色和复杂壁纸背景。验收截图需要检查：

- 没有文字截断、控件位移或重叠。
- 没有卡片套卡片、装饰渐变或无意义大留白。
- 日历格、看板列、工具栏和图标按钮尺寸稳定。
- 键盘焦点、悬停、禁用、错误和选中状态完整。
- AI 页面看起来属于日历应用，而不是独立聊天产品。

## 10. WPF 实现基线

- 全局主题必须为 TextBox、ComboBox、CheckBox、DatePicker、ProgressBar、ListBox、ListBoxItem、TabControl、TabItem 和 DataGrid 提供一致样式，页面不得依赖系统默认绿色进度条或旧式选中态。
- 基础控件必须覆盖悬停、按下、键盘焦点、选中和禁用状态；主要按钮使用独立的强调色悬停/按下状态，不能在悬停时退回灰色普通按钮。
- 页面标题、区块标题、辅助文字和图标按钮分别复用 PageTitleStyle、SectionTitleStyle、CaptionTextStyle 和 IconButtonStyle，不在页面内重新定义同义尺寸。
- 主窗口侧栏固定 200 px，品牌栏高 56 px，导航项高 38 px；选中项同时使用浅色背景、文字色和左侧 3 px 标记。全局搜索必须显示可见水印。
- 今天页首屏顺序为日期信息、今日日程/待办、天气详情；没有真实数据来源时不显示固定为 0 的统计数字。
- 月历工作区右侧保留 280 px 选中日期详情面板；单击日期同步详情，双击日期新增日程，今天与选中状态使用不同视觉语义。
- 工具中心使用“时间工具、专注、截图与贴屏、剪贴板”四个页签，每次只显示一个工作区，不使用超高单页承载全部工具。
- 四象限和看板列是布局区域，不使用页面底色制造嵌套卡片；只有任务等独立重复项可以使用主表面和边框。
- AI 页面将每日计划、周报和撤销放在命令栏，将模型与读写权限放在独立上下文栏，最小窗口下不得挤在同一行。

## 11. 令牌类型陷阱（WPF 特有，务必按此实现）

`DynamicResource` 把资源值直接赋给目标属性，**不经过目标的类型转换器**；而资源字典里 `sys:Double` 的值是裸 `Double`。因此令牌的 CLR 类型必须与消费属性的类型一致，否则编译期无警告、运行时才炸：

- **圆角必须声明为 `CornerRadius`**，不能用 `sys:Double`。`Border.CornerRadius` / `Button` 模板需要 `CornerRadius` 值，绑定 `sys:Double` 会在 `Arrange` 阶段抛 `InvalidCastException: Unable to cast object of type 'System.Double' to type 'System.Windows.CornerRadius'`。
  正确写法：`<CornerRadius x:Key="RadiusSm">4</CornerRadius>`（属性语法，复用 WPF 内建的 `CornerRadius` 类型转换器）。
- 间距/字号/尺寸用 `sys:Double` 是正确的，因为它们最终消费在 `double` 属性上（`Width`/`Height`/`FontSize`）。多值属性（`Margin`/`Padding`/`BorderThickness`）需要 `Thickness` 类型，不能直接用 `sys:Double`。
- 时长用 `Duration`，缓动用 `CubicEase` 等具体类型，不要用字符串。
- **每个主题令牌必须有契约测试断言「具体 CLR 类型」**，并且最好把令牌真正赋给一个目标控件并强制布局一次。「键存在」「值是某个数值」都不足以证明它能被目标属性消费。

## 12. 视觉回归测试的可靠性要求

改自绘控件或主题时，视觉断言必须确认「去掉修复后它会失败」，否则很可能是假通过：

- `RenderTargetBitmap.Render(visual)` 会按该视觉在视觉树中的**偏移**作画。子控件不在 (0,0) 时（例如看板位于 44px 小时刻度列之后），直接渲染会在图中留下空带；需要先用 `VisualBrush` 包一层再从 (0,0) 重绘，或显式平移。
- 目标视觉在 `InvalidateVisual()` 之后若没有重新参与一次渲染过程，`Render` 可能命中**缓存的旧内容**；`VisualBrush` 尤其会直接复用既有 bitmap cache。
- 因此：断言渲染像素之前，先跑一次布局与 `DispatcherPriority.Render` 队列；并且优先选择「与真实启动路径一致」的构造顺序（例如先挂主题字典再构建视图）。

