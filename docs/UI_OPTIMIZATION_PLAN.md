# cccalendar UI 优化方案（借鉴 deepseek-harness Web UI）

> 状态：方案待确认，尚未开始编码
> 编写日期：2026-09-16
> 当前版本：0.6.6（`version.json`）
> 关联文档：[UI_DESIGN.md](UI_DESIGN.md)（现有规范）、[DESIGN.md](DESIGN.md)（产品基线）
> 参考实现：`D:\github_program\deepseek-harness\web-ui-extract`（DSH Web 客户端 UI 提取，1321 文件）

---

## 1. 本轮目标与边界

### 1.1 目标

在不改变产品功能、不重写技术栈的前提下，把 cccalendar 的 WPF 界面从「有主题、无体系」提升到「有完整设计令牌体系」，并让**会议室看板**这个核心高频界面真正跟随主题与组件外观设置。

### 1.2 明确不做（本轮边界）

| 不做 | 原因 |
|------|------|
| 不新建 Web 界面、不改技术栈 | 共享面板已实现，本轮只做 UI 优化；WPF + 安装包形态保持 |
| 不改业务逻辑、不改 HTTP 契约 | UI 优化不应触碰 `RoomBookingApiClient`、预约同步与冲突判定 |
| 不重写 28 个 XAML 页面 | 按主题令牌 + 重点页面推进，不做无收益的平移重写 |
| 不引入第三方 UI 库 / 不引 WPF UI 框架 | 沿用 ADR-0001 与 UI_DESIGN §10 的「单一 Theme.xaml」基线 |
| 不重命名、不删除任何现有资源键 | 见 §4.1 兼容性原则 |

### 1.3 为什么这轮值得做（体检结论）

我先对现状做了全量体检，结论是**视觉不一致的根因不在页面，而在令牌层缺失**：

| 体检项 | 实测结果 | 判断 |
|--------|----------|------|
| `Themes\Theme.xaml` 声明的资源键 | 30 个（16 个画刷 + 9 个命名样式 + 3 个 `sys:Double` + 1 个转换器） | 颜色够用，其余全缺 |
| 间距 / 圆角 / 动效令牌 | **0 个** | 圆角靠字面量：`2`×3、`3`×3、`4`×8、`5`×1、`6`×4、`8`×4 |
| 字号令牌 | **0 个**（字号全靠字面量） | 23 个文件共 112 处硬编码 `FontSize="N"` |
| 硬编码十六进制颜色（非 Themes 目录） | 33 处，其中 **`RoomBookingBoardControl.cs` 独占 18 处** | 会议室看板完全不受主题控制 |
| 未定义却被引用的资源 | **`UiTextEffect`**（`Theme.xaml:29` 引用，仅 `DesktopAppearanceController.cs:66` 对桌面窗口注入） | 潜在缺陷，见 §3.1 |
| 「跟随系统字体缩放」实际生效范围 | `UiBodyFontSize` 只被 `Theme.xaml:28` 一处消费；桌面窗口缩放靠 `DesktopAppearanceController.cs:63-65` 覆写 | 主窗口不参与缩放 |

---

## 2. 从 deepseek-harness 借鉴什么（以及明确不借鉴什么）

DSH 的价值**不是**它的代码，而是它把「设计令牌 → 语义别名 → 组件」这条链的纪律。下表是本方案逐条采纳/拒绝的结论。

### 2.1 采纳（可翻译到 WPF）

| # | DSH 做法 | DSH 证据 | WPF 对应落地 |
|---|----------|----------|--------------|
| A1 | **三层令牌**：静态调色板 → 语义别名 → 组件只读语义层 | `ui-theme/src/styles/design-platform.css`（73 静态 × 2 + 97 别名 × 2） | `Theme.xaml` 保持语义键（`SurfaceBrush`…）不变，**新增**静态层与间距/圆角/字号令牌；页面禁止直接写字面色 |
| A2 | **最小主题契约只有 13 个令牌**就足以整体换肤 | `ui-theme/src/client/index.ts:131-145` | 据此确认现有 16 个画刷键**不应该再扩张**：需要新观感时加**语义**键，不加调色板键 |
| A3 | **抬高面的边框是「第一层阴影」**：抬高面 `border:0` + 带 0.5px 描边的 elevation 阴影，二者不并存 | `ui-theme/src/styles/gradient-shadow-text.css:17-34` + `docs/web-styling.md:24` | 新增 `ElevationPanelBrush`/`ElevationProminentBrush` 与 `RadiusLg`；规定「抬高面板不再叠 `BorderBrush` 实线」 |
| A4 | **状态色用单一色相派生填充**，避免填充与文字脱钩 | `ui-primitives/src/Tag.module.css` 的 `color-mix(in srgb, <state> 10%, transparent)` | 看板与徽标统一改为「一个状态色 → 派生底/边/字」的函数（§5） |
| A5 | **滚动条用间接变量**，抬高面在自身上重绑 thumb 色 | `ui-theme/src/styles/scrollbar.css:16-26`、`Menu.module.css:26-27` | 新增 `ScrollbarThumbBrush`/`ScrollbarThumbHoverBrush`；浮层（ContextMenu/ToolTip）内部重绑更深的 thumb |
| A6 | **容器驱动而非断点驱动**布局 | `ui-layout/src/client/columns.ts`、`AppFrame.tsx:134-158` | 关键页面在 `WpfRuntimeHost` 下按 **980×640 / 1366×768 / 1920×1080** 三档验收（UI_DESIGN §9 只要求两档，这里补最小档） |
| A7 | **动效克制**：交互 120ms，显隐 150–160ms，几何 300ms；`prefers-reduced-motion` 逐个组件退出 | `base.css:11-14` + 全库 `transition` 统计 | 新增 `MotionFast`/`MotionBase`/`EasingStandard`；会议室看板与列表悬停统一 120ms |
| A8 | **文字与背景对比度按阅读距离分级** | DSH 的 `--dsw-alias-label-caption` 仅 ~2.3:1，其自身定位是聊天记录 | **反向采纳**：会议室看板与桌面组件需远距离可读，见 §5.4 对比度下限 |
| A9 | 字号用**字形阶梯而非裸数字**，并保留 CJK 回退顺序 | `base.css:6-10`（刻意不写裸 `monospace` 尾巴，避免 Windows CJK 落到 SimSun） | 现有 `Segoe UI Variable, Microsoft YaHei UI` 保持；新增字号令牌而非改字体 |
| A10 | **状态只靠 `aria-checked` 等语义驱动，不靠并行类名** | `ui-primitives/src/Switch.module.css` | 自定义控件（看板、四象限）状态一律从业务状态派生，不引入第二套视觉状态标记 |
| A11 | 悬停只影响交互反馈，**不给静止态加装饰** | 全库 hover 均为 `interactive-bg-hover` 令牌 | 新增 `HoverBrush`/`PressedBrush` 的既有用法扩展到看板与列表项 |

### 2.2 明确拒绝（保留 cccalendar 现有立场）

| DSH 做法 | 拒绝理由 |
|----------|----------|
| 三栏会话壳 / 会话树侧栏 | cccalendar 是九项导航的工作台，不是会话产品 |
| 22–24px 气泡、胶囊按钮（`radius:18/24`）、渐隐遮罩 | 违反 UI_DESIGN §1.2「不用大面积玻璃效果」、§2.2「圆角最大不超过 8px」 |
| 紫蓝渐变、发光、装饰光球 | UI_DESIGN §1.2 明确禁止 |
| Markdown 阅读栏宽度轴（`clamp(680px,64%,920px)`） | 面向长文阅读，工作台要的是满幅信息密度 |
| 星光 / 渐变头像 / 「魔法生成」文案 | UI_DESIGN §1.6「AI 降权」明确禁止（**并见 §3.3 的既有违规**） |
| 把 z-index 阶梯迁过来 | DSH 自身的层级是历史堆积（100/1000/1100 混用，其注释亦承认无令牌层），不值得照抄 |
| 间距用 2px 增量（1,2,3,5,7…） | 与 UI_DESIGN §2.2 的 4/8/12/16/24 基线冲突，令牌化时以现有规范为准 |

---

## 3. 现状体检：已确认的问题清单

### 3.1 `UiTextEffect` 资源未定义（潜在缺陷，需先定性）

- `Themes\Theme.xaml:29` 的隐式 `TextBlock` 样式设置 `Effect` 为 `{DynamicResource UiTextEffect}`。
- 该键**在 `Theme.xaml` 与 `DarkTheme.xaml` 中均未声明**；grep 全仓仅 2 处命中：
  - `Theme.xaml:29`（引用）
  - `Desktop\DesktopAppearanceController.cs:66`（仅在桌面组件窗口的 `window.Resources` 内注入）
- 结论：主窗口与全部普通页面解析到空值，文字描边功能实际只对桌面组件生效。
- 处置：**这是既有行为，不一定算 bug**。P60 先写一个契约测试把「`Theme.xaml` 引用的每个 `DynamicResource` 键都有声明」固化下来，再决定是补默认值还是移除该 Setter。**在契约测试完成前不改行为**，避免无意中打开全应用文字描边。

### 3.2 令牌层缺口（视觉不一致的根因）

- 无间距令牌 → 页面各写 `Margin`（`24,20,24,28`、`20,0,140,0`…），留白节奏不可控。
- 无圆角令牌 → 6 种不同圆角并存（2/3/4/5/6/8）。
- 无字号令牌 → 112 处硬编码 `FontSize`，其中 16 处 `FontSize="11"` 的次要文字**本应**是 `CaptionTextStyle`（12px）。
- 无动效令牌 → 悬停/选中反馈不统一。

### 3.3 会议室看板脱离主题控制（本轮最高价值项）

- `Views\RoomBookingBoardControl.cs` 内 18 处硬编码十六进制颜色，直接以 `CreateFrozenBrush`/`CreateFrozenPen` 冻结为静态字段：
  - 网格：`#FFFFFF`、`#D9DEE5`、`#F0F2F5`
  - 他人占用：底 `#E4E7EA`、字 `#626A75`
  - 本人占用：底 `#DCEBFF`、字 `#174A8B`
  - 选中空闲：底 `#D8F0DF`、边 `#2E7D4F`、字 `#1F5B3A`
  - 选中冲突：底 `#F9E0E3`、边 `#C43D4B`、字 `#8F2231`
  - 手柄/拖拽预览：`#FFFFFF`、`#246BCE`、`#33246BCE`
- **后果**：① 深色主题下看板仍是白底；② 桌面组件的「独立主题/颜色/透明度」设置对看板完全无效；③ UI_DESIGN §2.3 的功能色（`#2E7D4F`/`#C43D4B`）在此处被复制成第二份来源，改色板必然漏改。
- 参考 DSH 的 `Tag`：用一个状态色派生填充与文字，改色只改一处。

### 3.4 其他既有违规（低风险，可一并清理）

| 位置 | 问题 | 依据 |
|------|------|------|
| `MainWindowViewModel.cs:19-30` | 助理导航用 `PackIconLucideKind.Sparkles`（星光图标） | UI_DESIGN §1.6 |
| `RegionSelectorWindow.xaml` | `#33000000`、`#11FFFFFF` 硬编码 | UI_DESIGN §10 |
| `Views\RecordView.xaml:12`、`ProjectView.xaml:12`、`StatisticsView.xaml:14` | 直接写 `FontSize="20" FontWeight="SemiBold"`，未用 `PageTitleStyle` | UI_DESIGN §10 |
| `MeetingDetailsWindow.xaml`、`MeetingExportWindow.xaml` | 按钮文字硬编码 `White`、标题 `FontSize="18"` 未用语义样式 | UI_DESIGN §10 |

---

## 4. 目标设计令牌体系

### 4.1 兼容性原则（硬约束）

1. **只新增，不改名，不删除**现有 30 个资源键 —— 全部页面用 `DynamicResource` 引用，改名会静默失效。
2. **令牌先声明、后替换**：先让令牌存在且被至少一处使用，再逐页把字面量替换为令牌；每一步都可 `eng\verify.cmd`。
3. **`DarkTheme.xaml` 只覆写颜色相关的键**；间距/圆角/字号/动效为跨主题常量，保持单一定义。
4. 间距/圆角/字号令牌用 `<sys:Double>`，与既有 `UiBodyFontSize` 一致，避免新机制。

### 4.2 新增令牌清单

**间距（4/8/12/16/24 基线，UI_DESIGN §2.2）**

| 键 | 值 | 用途 |
|----|----|------|
| `SpacingXs` | 4 | 图标与文字、紧凑内边距 |
| `SpacingSm` | 8 | 列表项内边距、按钮组间隔 |
| `SpacingMd` | 12 | 卡片内边距、表单行间隔 |
| `SpacingLg` | 16 | 区块间隔 |
| `SpacingXl` | 24 | 页面外边距、区块之间 |

**圆角（上限 8，UI_DESIGN §2.2）**

| 键 | 值 | 用途 |
|----|----|------|
| `RadiusSm` | 4 | 输入框、按钮（现为 8，见 §4.3 待决） |
| `RadiusMd` | 6 | 菜单、浮层、独立重复项 |
| `RadiusLg` | 8 | 抬高面板、弹窗 |

**字号（UI_DESIGN §2.1）**

| 键 | 值 | 对应现有样式 |
|----|----|--------------|
| `FontCaptionSize` | 12 | `CaptionTextStyle` |
| `FontCompactSize` | 13 | 紧凑表格 |
| `FontBodySize` | 14 | `UiBodyFontSize`（已存在，保留并作为正文字号引用点） |
| `FontPanelTitleSize` | 16 | `SectionTitleStyle` |
| `FontPageTitleSize` | 20 | `PageTitleStyle` |
| `FontClockSize` | 32 | 仅时钟可用的 28–36 区间 |

**控件尺寸**

| 键 | 值 | 说明 |
|----|----|------|
| `ControlHeightDefault` | 32 | 已存在的 `UiControlHeight`，保留 |
| `ControlHeightCompact` | 28 | 已存在的 `UiCompactControlHeight`，保留 |
| `ControlHeightPrimary` | 36 | UI_DESIGN §2.2「主要输入 36px」——**当前缺失** |
| `IconButtonSize` | 32 | `IconButtonStyle` 的 32×32 |
| `SidebarWidth` | 200 | `MainWindow.xaml` 当前字面量 |
| `BrandBarHeight` | 56 | 同上 |
| `NavigationItemHeight` | 38 | `NavigationItemStyle` 当前字面量 |

**动效（借鉴 A7）**

| 键 | 值 | 用途 |
|----|----|------|
| `MotionFast` | 120ms | 悬停/选中过渡 |
| `MotionBase` | 160ms | 浮层淡入、提示出现 |
| `EasingStandard` | `CubicEase` EaseInOut | 统一缓动 |

**新增语义画刷（颜色，需在 `DarkTheme.xaml` 同步覆写）**

| 键 | 浅色 | 深色 | 用途 |
|----|------|------|------|
| `ScrollbarThumbBrush` | `#D9DEE5` | `#383D45` | 滚动条 thumb（A5） |
| `ScrollbarThumbHoverBrush` | `#C4CBD4` | `#4A5058` | thumb 悬停 |
| `FocusRingBrush` | `#246BCE` | `#5794E6` | 键盘焦点环（当前各控件写死边框色） |
| `OverlayScrimBrush` | `#33000000` | `#66000000` | 遮罩，替换 `RegionSelectorWindow` 字面量 |
| `ElevationPanelBrush` | 描边+阴影 | 同结构换色 | 抬高面板（A3） |

### 4.3 需你裁决的两个取舍

| # | 取舍点 | 现状 | 选项 |
|---|--------|------|------|
| Q1 | 普通按钮圆角 | 模板写死 `CornerRadius="8"`（`Theme.xaml:64`） | **(a) 保持 8**（改动为 0，仅把它令牌化为 `RadiusLg`，不改变观感）；(b) 改为 `RadiusSm=4` 以严格符合 UI_DESIGN §2.2 ——会变动全部按钮观感 |
| Q2 | 会议室看板可见时段 | 08:00–24:00（`FirstVisibleCell=16`，576px） | **(a) 保持 08:00–24:00**；(b) 收窄为 08:00–20:00（工作时段，减少空行）；(c) 做成设置项 |

**默认建议：Q1 选 (a)（纯令牌化、零观感变动、零回归风险）；Q2 选 (a)（本轮只换皮不改信息架构，避免与用户既有肌肉记忆冲突）。** 两个问题都可以在 P60 之后再单独决策。

---

## 5. 会议室看板专项方案（核心）

### 5.1 目标

让 `RoomBookingBoardControl` 从「写死 18 个颜色、不受主题控制」变成「状态色 + 语义令牌驱动」，并保持**全部 16 个 `RoomBookingBoardTests` 通过**（这些测试验证的是几何与状态语义，本方案不改这些）。

### 5.2 结构改造（不改几何、不改命中测试）

现在：

```csharp
// RoomBookingBoardControl.cs —— 18 个 frozen 静态字段
private static readonly Brush OccupiedFillBrush = CreateFrozenBrush("#E4E7EA");
private static readonly Pen  GridHourPen        = CreateFrozenPen("#D9DEE5");
// ...
```

改为引入一个纯状态对象承载色板，由控件在 `OnRender` 时读取：

```csharp
// 新增：ViewModels/RoomBoardPalette.cs —— 纯数据，无 WPF 依赖，可单测
public sealed record RoomBoardPalette(
    Color GridSurface, Color GridHour, Color GridHalfHour,
    Color OccupiedFill, Color OccupiedText,
    Color OwnedFill, Color OwnedText,
    Color SelectedFreeFill, Color SelectedFreeBorder, Color SelectedFreeText,
    Color SelectedConflictFill, Color SelectedConflictBorder, Color SelectedConflictText,
    Color HandleFill, Color HandleBorder, Color PreviewFill, Color PreviewBorder);
```

- 控件通过 `TryFindResource` 解析语义令牌后构建 `RoomBoardPalette`；**解析失败时回落到现有字面量**，保证任何宿主（含测试宿主）行为不变。
- 冻结画笔缓存改为按 `RoomBoardPalette` 实例缓存（色板变则重建），避免每帧创建。
- `RoomBookingBoard`（`ViewModels`）保持不变；几何常量（`CellHeight=18`、`RoomWidth=125`、`FirstVisibleCell=16`）保持不变。

**验收**：`RoomBookingBoardTests` 16/16 仍通过（纯几何/状态回归）；新增 `RoomBoardPaletteTests` 覆盖「令牌缺失时回落到默认色板」「深色主题下返回深色色板」。

### 5.3 外观设置打通

- 桌面组件已通过 `DesktopAppearanceController.cs:43-73` 把外观写进窗口 `Resources` 并重绑 `SurfaceBrush`/`AccentBrush` 等。
- 只要看板改读语义键，**桌面组件内的看板会自动跟随组件的主题与颜色设置**——这是 §5.2 的免费收益。
- 需要补的只有：`DesktopAppearanceController` 在重绑时**同时提供**看板需要的派生色（占用底/选中底），或让看板用「状态色 + 透明度」自行派生（推荐后者，见 5.4）。

### 5.4 视觉编码改进（借鉴 A4，但服从 A8 的可读性下限）

在保持「同一状态语义」的前提下：

1. **状态色单源**：占用灰、本人蓝、空闲绿、冲突红各只保留**一个基准色**，其底色 = 基准色降饱和/加透明度，文字色 = 基准色加深。改色板时一处生效。
2. **文字对比度下限**：看板文字（当前 11px）与其背景的对比度**不低于 4.5:1**（远距离可读）。为此评估把看板字号从 11 提到 12（`FontCaptionSize`），行高 18 可容纳 12px。
3. **本人/他人占用的区分不能只靠颜色**（UI_DESIGN §2.3「状态不能只靠颜色表达」）：建议本人预约加左侧 3px 竖条（与导航选中项同一语法），他人预约保持纯色块。
4. **选中态已满足**（空心/实心 + 边框 + 粗体标签），保持不变。

### 5.5 不改的部分

`RoomBookingPicker.xaml/.xaml.cs` 的滚动同步、15 秒轮询、日期切换、`MeetingDetailsWindow` 的两按钮可见性规则（本人可删/他人可加）——全部保持。这些是行为，不是外观。

---

## 6. 分阶段交付与验收

每个切片遵循 `WORKLIST.md` 的 Red-Green-Refactor-Verify 约定；每个切片独立可验证、可回滚。

| 切片 | 内容 | 验收（可验证的成功标准） |
|------|------|--------------------------|
| **P60** 令牌基线 | 新增 §4.2 全部令牌（间距/圆角/字号/控件尺寸/动效/新增画刷），`Theme.xaml` + `DarkTheme.xaml` 同步；**不改任何页面引用**；先写「`Theme.xaml` 引用的 DynamicResource 键均有声明」契约测试 | `eng\verify.cmd` 退出 0；新增契约测试先失败后通过；现有 `UiDesignContractTests` 34 项全通过；**页面零改动**，观感不变 |
| **P61** 令牌接入主题控件 | `Theme.xaml` 内 24 个隐式样式 + 9 个命名样式改用令牌（含 `PrimaryButtonStyle`、`NavigationItemStyle`、`SegmentToggleStyle`）；解决 Q1 | `eng\verify.cmd` 退出 0；`HorizontalScrollBarKeepsLogicalLeftToRightDirection` 等模板断言仍通过；三档分辨率截图与改前逐像素对比，差异仅限预期项 |
| **P62** 会议室看板主题化 | `RoomBoardPalette` + `RoomBookingBoardControl` 改读令牌 + 回落；打通组件外观；落地 §5.4 的状态单源与对比度 | `RoomBookingBoardTests` 16/16；新增 `RoomBoardPaletteTests`；**深色主题下看板截图**（当前是白底，改后应为深色）；浅色截图无回归 |
| **P63** 页面字面量清理 | 替换 §3.4 的违规项：`PageTitleStyle`/`CaptionTextStyle` 归位、`Sparkles`→非星光图标、`RegionSelectorWindow` 用 `OverlayScrimBrush`、`MeetingDetails/Export` 用语义样式 | `eng\verify.cmd` 退出 0；`UiDesignContractTests` 全通过；新增「页面不出现硬编码 `FontSize="20"`+`SemiBold` 页面标题」契约测试 |
| **P64** 视觉验收与文档 | 三档分辨率 × 浅/深主题截图核验；更新 `UI_DESIGN.md`（把新令牌表写进 §2）；更新 `USER_GUIDE.md` 中受影响的界面描述 | 截图无截断/重叠/卡片套卡片；`eng\verify.cmd` 退出 0；文档与实际一致 |

### 6.1 验证手段（沿用现有基建，不新建）

1. **`eng\verify.cmd`** —— restore + format verify + Debug build + 全量测试（当前基线 **479/479**：Core 93、Infrastructure 104、Desktop 238、Server 44）。
2. **`WpfRuntimeHost`（`tests\CcCalendar.Desktop.Tests\Views\WpfRuntimeHost.cs`）** —— 已在用的 STA 运行时宿主，可加载真实窗口并截图。
3. **`UiDesignContractTests`（34 项）** —— 基于源码文本的契约断言，是本轮新增契约的自然落点。
4. **`RoomBookingBoardTests`（16 项）** —— 看板几何/状态回归护栏。
5. **人工视觉核验** —— UI_DESIGN §9 要求 1920×1080 与 1366×768；本轮**补充 980×640**（`MainWindow` 的 `MinWidth/MinHeight`）。

### 6.2 完成定义（沿用 DESIGN.md §11）

一个切片只有同时满足以下条件才算完成：验收标准已通过自动化测试或明确的人工验证；相关测试全部通过且无已知回归；未引入超出本切片的改动；用户可见行为已记录在设计或使用说明中；`WORKLIST.md` 已同步状态、验证证据、遗留问题和唯一下一步。

---

## 7. 风险与缓解

| 风险 | 影响 | 缓解 |
|------|------|------|
| 令牌化触及 24 个隐式样式，可能改变大量控件观感 | 面广、易回归 | P60 只加令牌不改引用；P61 才接入，且逐样式推进；每步截图对比 |
| `DynamicResource` 键改名导致静默失效 | 运行时无异常、界面回退系统默认样式，极难发现 | §4.1 只新增不改名；P60 的「引用键均有声明」契约测试兜底 |
| 看板色板改造影响桌面组件外观 | 桌面组件是最受用户关注的功能 | 令牌解析失败回落原色板；`RoomBookingBoardTests` 兜底；深色/浅色双截图 |
| 收紧字号/提高对比度改变用户既视感 | 用户可能不习惯 | Q2 默认保持信息架构不变；字号/对比度调整集中在 P62 且可单独回滚 |
| `UiTextEffect` 处置不当会全应用打开文字描边 | 视觉巨变 | P60 只加契约测试、**不改行为**；是否补默认值单独决策 |

---

## 8. 待裁决问题（阻塞开工）

| # | 问题 | 我的建议 |
|---|------|----------|
| Q1 | 普通按钮圆角保持 8px 还是改成规范里的 4px？ | 保持 8px（纯令牌化，零回归） |
| Q2 | 会议室看板可见时段保持 08:00–24:00 还是收窄？ | 保持 08:00–24:00（本轮只换皮） |
| Q3 | `UiTextEffect` 未定义：补默认值（全应用文字描边）还是移除该 Setter？ | 先加契约测试定性，再决定；倾向移除 Setter，把描边明确限定为桌面组件特性 |
| Q4 | 是否同意本轮切片顺序 P60→P64，且每片独立验收？ | 同意则按此更新 `WORKLIST.md` 并开工 P60 |

---

## 9. 本轮不处理但已记录的文档问题

体检时发现以下文档问题，**不属于 UI 优化范围**，仅记录，避免遗忘：

- `docs\DESIGN.md` 头部仍写「P0-P14 已完成…按 P15 逐步实现，最后更新 2026-08-17」，落后于 0.6.6/P40。
- `docs\SYNC_DESIGN.md` 头部停留在 P15-01…P15-07，落后于 P15-10 及 P16/P17/P23–P40。
- `docs\AUTO_UPDATE_DESIGN.md` 写「当前版本 0.4.5」且称「尚未接入静默安装」，而 0.6.1 已实现下载校验+自动安装重启。
- `docs\UPDATE_AND_COLOR_GUIDE.md` 写「当前发布版本 0.4.5」，且更新流程描述与已实现的自动安装不一致。
- `docs\USER_GUIDE.md` 标注适用 0.6.5，落后一个版本；`docs\USER_GUIDE - 副本.md` 是 0.5.2 期副本，且**在正文表格里公开了团队口令**。
- `docs\TEAM_MEMBER_GUIDE.md` 仍以 `cccalendar-0.4.5-...exe` 举例。
- `docs\CLOUD_OPS_GUIDE.md` 头部与客户端章节标注 0.5.2（服务端事实是最新的）。
- `WORKLIST.md` 的「当前检查点」混放了 2026-09-16、08-31、08-24/25 多个时期的状态。
