# 助理界面深度重构方案（P70/P71）：吸收 deepseek-harness 前端设计

> 创建：2026-09-18。本方案是 P60/P61 两轮"借鉴 harness"之后的第三轮。
> 本轮与前两轮的本质区别：**前两轮动的是令牌层（间距/圆角/阴影/动效），本轮动的是助理页面的组件形态与信息架构**——用户气泡、全宽助手消息、思考折叠行、token 统计胶囊、工具调用行、会话列表、Agent 本地执行。全部是"一眼可见"的变化。
>
> 源码参考路径：`D:\github_program\deepseek-harness`（下称 harness）。

---

## 0. 为什么上一轮"感觉没什么变化"

P61 检查点自己已复盘过：P60 有 5 片是肉眼不可见的重构。但从用户视角再深挖一层：

| 用户点名想要的东西 | harness 中的形态 | cccalendar 现状 | 差距性质 |
|---|---|---|---|
| 对话框（消息形态） | 用户右对齐气泡 + 助手全宽 Markdown | 头像+名称+底边框的"评论区"形态 | **组件形态不同** |
| 会话统计、token 用量 | composer 下方 StatsPills + 消息尾部 TurnUsagePanel | **完全没有**（数据层就没有 usage 事件） | **功能缺失** |
| 工具调用过程展示 | ToolRow（状态点+图标+摘要+展开） | 工具在流式循环内静默执行，UI 只见最终提案 | **信息不可见** |
| 子代理显示 | SubagentHeaderLineage 树 | 无子代理概念 | **功能缺失** |
| 字体、各种格式 | 14px/22px 正文、四级文字色、发丝描边 | P60/P61 已有令牌体系，数值接近 | 只需微调对齐 |

结论：**差距不在令牌数值，在于助理页目前是"表单式 AI 问答页"，而 harness 是"现代聊天客户端"**。本轮按 harness 的形态重造助理页。

---

## 1. 技术栈差异与移植策略

harness 前端：React + CSS Modules + micromark 自研 Markdown + shiki 高亮，monorepo（`packages/client/ui-theme`、`ui-chat`、`ui-tool`、`ui-subagent`、`ui-conversation`、`ui-sidebar` 等）。
cccalendar：WPF + CommunityToolkit.Mvvm + MdXaml + MahApps Lucide 图标。

**策略：按"语义等价"移植，不逐像素照搬。** 每个组件先抄结构与交互语义（这是感知的来源），颜色/圆角/字号走 cccalendar 已有的令牌体系（新增少量令牌），字体用 WPF 等价栈。绝不引入 WebView 去跑 React——那会引入整套 node 工具链，与桌面应用的测试体系（524 项）断裂。

### 1.1 harness 设计系统速查（移植依据）

**三层令牌**（`packages/client/ui-theme/src/styles/design-platform.css`）：
- `--dsw-static-*` 静态色板 → `--dsw-alias-*` 语义别名 → `--dsw-specific-*` 组件特定值。
- cccalendar 已有等价物：`Theme.xaml` 语义画刷 + `DarkTheme.xaml` 覆写，即"两层"，够用。

**关键数值**（来自 `MessageItem.module.css`、`StatsPills.module.css` 等）：

| 项 | 值 |
|---|---|
| 用户气泡 | 右对齐，max-width ≈70% 内容宽，圆角 **22px**，padding **10,16**，正文 **14px/22px** |
| 气泡底色 | 浅 `#EDF3FE`（deepseek-50）、深 `#2C2C2E`（neutral-bluish-850） |
| 助手消息 | 无气泡，全宽 Markdown，同字号 |
| 统计胶囊 | 圆角 24px（胶囊），文字用 tertiary 色，hover 用交互叠加 |
| 正文/次级字号 | 14px/22px 与 13px/18px 两档 |
| 文字四级 | primary / secondary / tertiary / caption（cccalendar 已有 Primary/Secondary，缺 tertiary/caption 拆分） |
| 动效 | 0.1/0.2/0.3s，`cubic-bezier(0.4,0,0.2,1)`（≈P61 已落的 Motion 令牌） |
| 字体栈 | UI: `Segoe UI, PingFang SC, Microsoft YaHei`；代码: `Consolas + PingFang SC`（Windows 等价） |
| 错误行 | 红点 8px + 红色 600 标题 + secondary 消息 + 代码字体错误码 |
| 文件卡 | 240px 宽，圆角 16px，28px 图标，名称 14px/500 + meta 12px |

**数据结构依据**（StatsPills.tsx / TurnUsagePanel.tsx / ToolRow / SubagentHeaderLineage）：
- 会话累计：`{ uncachedInputTokens, cacheReadTokens, cacheWriteTokens, outputTokens }` + `{ turns, steps, llmMs, toolMs, ttftMs, decodeMs }`；
- 轮次用量：`{ totalTokens, outputTokens, uncachedInputTokens, cacheReadTokens, cacheWriteTokens, reasoningTokens, routes(provider/model) }`；
- 工具行：`{ variant, state: running/ok/error/stopped, title, summary, filePath, input, output }`；
- 子代理 lineage：`{ name/description, 运行状态, token 汇总, 时长, 嵌套层级 }`。

---

## 2. cccalendar 现状基线（改动起点）

- **视图**：`src/CcCalendar.Desktop/Views/AssistantView.xaml`（462 行）——ListBox 消息列表 + 头像行（AI/你）+ Expander 思考 + TextBox/MdXaml 双渲染 + 复制按钮 + 底部边框分隔；下方提案卡（PendingPreview）、变更卡（PendingChange）、冲突卡（PendingConflict）；底部输入条（附件/输入/发送/停止）；顶部命令栏（每日计划/周报/撤销/重试）+ 模型/权限条 + 会话 ComboBox。
- **ViewModel**：`ViewModels/AssistantViewModel.cs`（~1180 行）+ `AssistantMessageViewModel`（IsUser/Text/ThinkingText/CreatedAtUtc/IsMarkdown，无用量字段）。
- **数据层缺口（本轮关键）**：`Core/AI/AiAssistantRequest.cs` 的更新事件只有 `AiTextDelta / AiReasoningDelta / AiProposalUpdate` 三种——**没有 usage 事件、没有工具调用事件**。token 统计与工具行两类 UI 都必须先补数据层。
- **流式实现**：`Infrastructure/AI/ProviderAiAssistantClient.Streaming.cs` 四路（OpenAiResponses/Anthropic/OpenAiCompatible/Ollama），已有 `MaximumToolRounds` 工具循环，`ExecuteStreamingToolAsync` 内部执行工具——工具过程可在此发射事件。
- **主题**：`Themes/Theme.xaml`（93KB）已有间距/圆角（上限 16）/字号/动效/阴影/交互叠加令牌；`DarkTheme.xaml` 覆写。
- **测试基线**：524/524，`eng\verify.cmd` 退出 0。

---

## 3. 切片计划：P70（界面重构，7 片）

> 每片独立验收（TDD：能写测试的先 Red）、独立回归、可跨会话续作。顺序即依赖顺序。

### P70-01 消息形态重构（视觉冲击最大，先做）

把"评论区"改成"聊天客户端"：

- **用户消息**：右对齐气泡。新增 `BubbleBrush`（浅 `#EDF3FE` / 深 `#2C2C2E`）令牌；圆角 22（Q1 已裁决放开，新增 `RadiusBubble` 令牌，上限契约 16→24）；`MaxWidth` ≈ 70%；padding 10,16；**去掉用户头像与名称行**（Q2 已裁决，与 harness 完全一致）。
- **助手消息**：去掉头像+名称行，全宽 Markdown；复制按钮保留（常显小按钮即可）。
- **消息分隔**：去掉每条消息底部的 `BorderThickness="0,0,0,1"`，改为纯间距（16-24px）分组。
- 验收：`eng\verify.cmd` 全绿 + `eng\capture-ui.ps1` 浅/深截图对比 + 现有 AssistantView 运行时测试（消息模板相关断言同步更新）。

### P70-02 ReasoningRow：思考折叠行

替换现有 `Expander + SubtleSurface 块`为 harness 的"一行式"：

- 收起态：图标 + 标题（流式时"思考中…"带 sweep shimmer 动画；完成后"已深度思考（N 字 · M 秒）"）+ 摘要 ellipsis + chevron 旋转。
- 展开态：缩进的 tertiary 色文字块（13px）。
- shimmer 实现：WPF `LinearGradientBrush` + `DoubleAnimation`（P61 已有叠加层动画经验；`prefers-reduced-motion` 对应为系统"显示动画关闭"检测，可简化为常开）。
- 验收：运行时测试（折叠/展开状态、字数统计）+ 截图。

### P70-03 Token 用量数据层（纯 TDD，无 UI）

- **Core**：`AiAssistantUpdate` 新增子类型 `AiUsageUpdate(long PromptTokens, long CompletionTokens, long? CachedPromptTokens, long? CacheWriteTokens, long? ReasoningTokens, string? Model)`；`AiAssistantResult` 增加可选 `Usage` 汇总。
- **Infrastructure**（四路流式各自的捕获点）：
  - OpenAiCompatible/Responses：请求体加 `stream_options.include_usage=true`（DeepSeek 等支持；不支持的网关会忽略，需容错：末 chunk 无 usage 时不发事件）；末 chunk `usage` 字段解析。
  - Anthropic：`message_start` 的 `usage.input_tokens`（含 `cache_read_input_tokens/cache_creation_input_tokens`）+ `message_delta` 的 `output_tokens`。
  - Ollama：最终 `prompt_eval_count/eval_count`。
  - **每工具轮次各发一次**，UI 才能算"步数"。
- **Desktop**：`AssistantViewModel` 累计会话总量（turns/steps/tokens 四桶）；`AssistantMessageViewModel` 增加 `Usage`（该轮用量）与 `Elapsed`。
- 验收：Infrastructure 各 provider 的流式解析单测（伪造 SSE 行）+ VM 累计逻辑单测。**此片完成后 P70-04 才有数据。**

### P70-04 StatsPills：会话统计胶囊

- 位置：输入框正下方居中（harness 挂在 composer dock，视觉上属于输入区）。
- 两枚胶囊按钮（圆角=高度一半，即"胶囊"，超出 16 上限属"正圆/胶囊"豁免——见 Q1）：
  - **时钟 pill**：`N 轮 · M 步 · X tok/s` → 点击弹 Popup：LLM 时间 / 工具时间 / 首字延迟(TTFT) / 解码速度；
  - **数据库 pill**：`总 tokens · 缓存命中%` → 点击弹 Popup：缓存命中 / 未缓存输入 / 缓存读 / 缓存写 / 输出。
- WPF 实现：`Button`（胶囊样式复用 P61 叠加层）+ `Popup`（`StaysOpen=False`）+ 两列定义列表。
- 消息尾部同时加轻量 **TurnUsagePanel**：助手消息操作行里"⏱ 用时 12s · ∑ 3.2k tokens"，点击弹轮次详情（含 provider/model）。
- 验收：VM 统计属性绑定测试 + 运行时 Popup 开合测试 + 截图。

### P70-05 ToolRow：工具调用行

让 AI 的工具轮次从"黑盒"变"可见"（harness ToolRow 语义）：

- **数据层**：`AiAssistantUpdate` 新增 `AiToolCallUpdate(string ToolName, string? ArgsSummary, AiToolCallState State /*Running/Ok/Error*/, string? ResultSummary)`；`ExecuteStreamingToolAsync` 执行前后发射（工具循环在 `StreamOpenAiChatAsync` 等四处调用点统一包裹）。
- **UI**：消息流内插入工具行——状态点（8px，running 呼吸/ok 绿/error 红）+ 工具名中文映射（`propose_event`→"创建日程"等，取自工具目录）+ 参数摘要（一行 ellipsis）+ chevron 展开（参数 JSON + 结果摘要，等宽字体）。
- running 态复用 P70-02 的 sweep 动画。
- **提案卡片（PendingPreview/PendingChange）保持现有确认流不变**，仅把视觉风格对齐工具行语言（图标 + 标题 + 字段列表 + 主/次按钮）。
- 验收：伪造工具事件流 → 消息列表出现工具行且状态流转正确的运行时测试。

### P70-06 会话列表与三处收起能力（Q3 裁决后的扩展范围）

harness 是左侧会话栏；cccalendar 助理页当前是顶部 ComboBox（信息密度低、无预览）。改为：

- 助理页左侧会话列（宽 ~240px）：新对话按钮 + 会话条目（标题 + 相对时间"2 小时前" + hover 显示删除）+ 当前会话高亮（3px 标记条，与导航项同语法）。
- 条目点击切换会话（复用现有 `CurrentSession` 逻辑），空态显示"新对话"。
- **三处收起能力（Q3 裁决扩展，交互参照 harness 侧边栏 rail 折叠：宽度动画 + 图标条驻留）**：
  1. **主界面左侧导航栏**：收起成图标条（rail，仅图标驻留，hover 显示 tooltip），展开恢复完整标签；收起状态持久化到设置。
  2. **助理页会话栏**：收起成图标条（新建对话图标 + 会话图标），点击展开。
  3. **顶部搜索栏**：可整行收起（收起后留搜索图标按钮，点击展开）。
- 实现要点：收起/展开用宽度 `GridLength`/`Width` 动画（P61 已有动画基建）；三处状态各自持久化（设置键：`mainNavCollapsed`、`assistantSessionsCollapsed`、`topSearchCollapsed`）。
- 验收：切换/删除/新建的运行时测试 + 收起/展开状态持久化测试 + 截图（展开/收起各一张）。

### P70-07 Composer 精化与错误行

- **发送/停止合并**：单按钮状态切换（发送 ⇄ 停止，harness 同款），替代现在并排两个按钮。
- **顶栏收敛**：模型选择器 + 权限档移入输入框上缘一行（小号控件），顶部只留标题与"每日计划/周报/撤销/重试"；会话管理已移走（P70-06）。
- **错误行**：发送失败等错误从现在的隐式状态改为 harness 错误行形态（红点 + 红色标题 + secondary 消息 + 等宽错误码），插入消息流对应轮次尾部。
- **文件卡**：附件 chip 升级为 harness 文件卡形态（图标 28px + 名称 14px/500 + meta 12px，圆角 16，宽 240 定长）。
- 验收：运行时测试 + 截图（浅/深 × 3 档分辨率）。

---

## 4. 切片计划：P71（Agent 本地执行 + 子代理，独立阶段）

> 安全敏感、依赖 P70-05 工具行与审批 UI 基础，单独成阶段。Q4–Q6 已全部裁决（见 §5）。

### P71-01 权限三档 + 工具目录扩展

- `AiAssistantMode` 增加 `LocalAgent`：只读 → 写入（需确认）→ 本地代理（需确认 + 白名单）。**默认关，设置页显式开启（Q6 已裁决）**。
- 新增本地工具集（每工具独立白名单开关，设置页"AI 助手 → 本地代理"区）：
  - `read_file(path)`、`list_directory(path)`、`search_files(pattern, root)` —— 只读三件套，默认允许（可关）；
  - `write_file(path, content)`、`run_command(command, cwd)` —— 需审批；**审批粒度由用户自选（Q4 已裁决）**：审批弹层提供"允许一次 / 本次会话允许 / 总是允许 / 拒绝"，默认未配置时逐次审批。
- 工具执行结果走 P70-05 工具行展示。

### P71-02 工具审批 UI（内联）

- 工具行 running 态前插入审批态：工具名 + 参数摘要 + [允许一次] [本次会话允许] [总是允许] [拒绝]（四项均可选，Q4 裁决；"总是允许"写入白名单并可在设置页清除）。
- 实现：`AiToolCallUpdate` 增加 `PendingApproval` 状态；`ExecuteStreamingToolAsync` 对需审批工具先发 PendingApproval 事件并 `TaskCompletionSource` 等待 UI 裁决（带超时默认拒绝）；ViewModel 暴露审批命令。
- 审批等待期间用户可"停止"整轮请求（复用现有 StopCommand）。

### P71-03 子代理（spawn_subagent + Lineage 树）

- 工具目录增加 `spawn_subagent(description, task)`：以同一 provider/model 开子会话（独立 `AiAssistantRequest`，`subagentDepth+1`，上限 2 层），子代理可用除 spawn_subagent 外的当前档位工具。
- UI：消息流内子代理显示为**折叠卡**（头部：状态点 + 名称/描述 + token 汇总 + 时长；展开：子代理自己的消息流缩进渲染）；嵌套子代理用左侧竖线连接（harness lineage 树语义）。
- 数据：子代理轮次的 usage 并入会话总量（StatsPills 天然汇总），卡头单独显示子代理小计。
- 验收：伪造子代理事件流的运行时测试；真实 provider 下人工验收一次。

---

## 5. 决策点（已全部裁决，2026-09-18）

| # | 问题 | 裁决 |
|---|---|---|
| Q1 | 气泡/胶囊圆角 22/24px 超出 P61 的 16px 上限 | **放开**。圆角上限 16→24，新增 `RadiusBubble=22` 令牌；胶囊类（pill）按"高度一半"豁免。P61-02 的上限契约测试同步改为 24。 |
| Q2 | 助手消息是否保留小标识 | **不保留，与 harness 完全一致**：用户消息纯右对齐气泡，助手消息全宽无头像无名称。 |
| Q3 | 会话列表位置 | **助理页左侧栏（240px）**，且扩展范围：**三处收起能力**——① 主界面左侧导航栏收起成图标条（rail）；② 助理页会话栏收起成图标条；③ 顶部搜索栏可收起。交互参照 harness 侧边栏的折叠（宽度动画 + 图标条驻留）。 |
| Q4 | run_command 审批粒度 | **由用户自己选**：审批弹层提供"允许一次 / 本次会话允许 / 总是允许 / 拒绝"四项均可选；默认（未配置时）仍为逐次审批。 |
| Q5 | token 统计是否加成本估算 | **先只显示 tokens**，不做单价/金额。 |
| Q6 | P71 开关默认值 | **本地代理档默认关**，需在设置页显式开启。 |

---

## 6. 明确不吸收的内容（及理由）

- **React/CSS Modules/shiki 组件本身**：技术栈不同，语义等价移植即可；不引入 WebView。
- **IDE 型多面板 dock / 会话壳 / 右侧栏（终端、文件树）**：harness 是开发工具，cccalendar 是日历应用，助理是内嵌页不是主壳。
- **斜杠命令 / @文件提及**：harness 输入框的这两个功能依赖其会话运行时；cccalendar 助理的指令面（每日计划/周报）已由按钮承载，收益低。（若后续要加，属 P72+。）
- **Markdown 语法高亮**：MdXaml 无高亮；接 ColorCode/TextMate 是独立收益，不阻塞本轮，记录为 P72 候选。
- **compaction（上下文压缩标记行）/ 重试详情折叠**：依赖 harness 的会话压缩与重试运行时，cccalendar 的历史裁剪（isTrimmingHistory）用户无感，暂无对应语义。

## 7. 验收与回归纪律

- 每片完成即跑 `eng\verify.cmd`（format/build/全量测试），更新 WORKLIST.md 对应条目与检查点。
- 视觉片（P70-01/02/04/06/07）另跑 `eng\capture-ui.ps1 -Isolated` 浅/深双主题截图，存 `artifacts/ui-shots/`。
- 数据层片（P70-03/05、P71-01/02）严格 TDD：先写失败测试再实现。
- 版本号：P70 全部完成后递增次版本号（0.6.8 → 0.7.0，功能更新）。
