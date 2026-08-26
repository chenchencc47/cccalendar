# AI 助理 Worklist

> 续作入口：先读本文件和 [ASSISTANT_REVIEW_PLAN.md](ASSISTANT_REVIEW_PLAN.md)。每完成一个条目立即记录 Red/Green/Refactor/Verify；“思考”展示按用户决定保留。

## 当前检查点

- 阶段：C. UI 工作流与性能
- 当前条目：P22 团队登录恢复与本地日程补同步（实现及发布完成）
- 最近验证：启动恢复已保存开发令牌、过期令牌自动续签、会议室补同步使用稳定幂等键；完整回归 456/456；服务端 DevToken 有效期已调整为 7 天并重启验证通过。
- 注意：已有成员需重新登录一次，服务端才能保存其显示名；历史预约若没有成员显示名仍会显示 `未知成员`。

## A. 正确性与恢复能力

- [x] A-01 重复消息与历史读取
  - Red：把“允许时间戳不同的重复问题”和“恢复 ThinkingText”写成回归测试。
  - Green：移除跨历史永久去重，只保留短时间提交保护；加载历史保留时间戳重复消息及思考文本。
  - Refactor：只清理本条目引入的重复判断，不改会话标题规则。
  - Verify：`dotnet test tests/CcCalendar.Desktop.Tests/CcCalendar.Desktop.Tests.csproj --filter FullyQualifiedName~AssistantViewModelTests --no-restore`，20/20 通过。
- [x] A-02 日程查询准确性
  - Red：全天范围、按开始时间排序、返回地点、跨多日每日计划测试。
  - Green：修正 executor 和 `AiDraftService`。
  - Refactor：保持现有 SQLite 只读边界。
  - Verify：`SqliteReadOnlyAiToolExecutorTests` 2/2、`AiDraftServiceTests` 1/1 通过。
- [x] A-03 附件错误提示与历史原子写入
  - Red：运行时/单元测试覆盖 `InvalidDataException` 和中断写入。
  - Green：UI 捕获附件数据异常；历史采用临时文件替换并提供失败信号。
  - Refactor：不改变附件大小限制。
  - Verify：附件失败重试、历史恢复测试通过；历史写入改为临时文件替换；窗口捕获 `InvalidDataException`。

## B. 上下文与可解释性

- [x] B-01 会话上下文契约与 provider 接入
  - Red：新增会话轮次传递和最近 40 轮裁剪测试。
  - Green：`AiAssistantRequest` 携带会话轮次；四类 provider 流式/非流式请求加入历史消息。
  - Refactor：保持原有 provider 和工具协议，不引入新的上下文服务。
  - Verify：`AssistantViewModelTests` 21/21、`ReadOnlyAiToolCatalogTests` 4/4、`ProviderAiAssistantClientTests` 19/19。
- [x] B-02 provider/本地模式/附件出网状态提示
  - Green：上下文栏显示“仅本地/在线发送 · provider”和附件数量/总大小，不暴露完整端点。
  - Verify：Desktop 助理/UI 契约测试与 Debug build 通过。
- [x] B-03 工具来源、时间范围和冲突对象展示
  - Green：冲突面板增加候选日程标题、起止时间和时区摘要；保留冲突数量和选择按钮。
  - Verify：Desktop build 0 警告/0 错误。
- [x] B-04 思考文本保留、可折叠和持久化策略
  - 决策：按用户要求保留当前“思考”折叠展示，不替换为工具摘要。
  - Green：历史读取恢复 `ThinkingText`，现有折叠 UI 保持不变。
  - Verify：`ChatHistoryRestoresThinkingText` 通过。
- [x] C-03 提案/冲突/附件加载失败状态与无障碍
  - Red：补充冲突检测失败、提案确认失败、寻找空闲失败的回归测试，并检查图标操作的 AutomationProperties。
  - Green：冲突检测异常收敛到消息区且不丢失提案；确认失败保留提案；失败时附件仍保留；补齐复制、提案、冲突和工具栏操作名称。
  - Verify：`AssistantViewModelTests` 26/26；`UiDesignContractTests` 29/29。
- [x] C-04 助理运行时截图与人工验收
  - Green：新增真实 WPF 运行时渲染测试，覆盖 1366×768 与 1920×1080，验证思考折叠、提案内容布局和视图不横向溢出。
  - Verify：`AssistantViewRuntimeTests` 2/2，两个尺寸均完成布局并成功渲染非空视图。

## 发布记录

- [x] 0.4.15：安装包 `artifacts\\installer\\cccalendar-0.4.15-win-x64-setup.exe`，60,143,195 bytes，SHA-256 `57799755D4AD9DF41922DE2B83BA6B0ECD51EF6C60FA8276561F1FD130F3A89E`；公网清单版本 `0.4.15`，安装包 HTTP HEAD `200`。
- [x] 0.5.1：修复 AI 确认后的主窗口/桌面组件刷新；补充刷新失败的准确提示；模型只返回文字时明确提示未创建；托盘和设置页增加鼠标穿透恢复说明。安装包 `artifacts\\installer\\cccalendar-0.5.1-win-x64-setup.exe`，60,138,462 bytes，SHA-256 `0EFB97AE9148546884CF40399585DF215446D79B151049397B3AFF025A3F439F`；公网清单版本 `0.5.1`，安装包 HTTP HEAD `200`。
- [x] 0.5.1 重新发布（会议室预约人展示）：安装包 `artifacts\\installer\\cccalendar-0.5.1-win-x64-setup.exe`，60,106,855 bytes，SHA-256 `29d1b15f074f49195aa849d605c4cf48ad5557f84e04e2a1804d3bc8f6e8d2af`；公网清单版本 `0.5.1`，安装包 HTTP HEAD `200` 且长度匹配。Linux Server 包 `artifacts\\cccalendar-server-linux.zip`（50,968,501 bytes）已上传并部署到阿里云 `/root/cccalendar`，服务重启及公网 `/health` 验证通过。
- [x] 0.5.1 追加 P22：安装包 `artifacts\\installer\\cccalendar-0.5.1-win-x64-setup.exe`，60,124,103 bytes，SHA-256 `8e1efd68510b832bc345718229a1571e6e205801906deef5999b486a73531cf9`；已覆盖上传 OSS，公网清单版本/哈希、安装包 HTTP HEAD `200` 和长度均校验通过。
- [x] 0.5.2 P22 发布：服务端 DevToken 有效期调整为 7 天（10080 分钟），ECS `cccalendar` 已重启且 `/health` 返回 `status: ok`；安装包 `artifacts\\installer\\cccalendar-0.5.2-win-x64-setup.exe`，60,106,331 bytes，SHA-256 `647D268D99A25DCEE2C35C55BFB107D604CEE7DA3751C72DF529AAC787A34039`；公网清单版本/哈希、安装包 HTTP HEAD `200` 和长度均校验通过。

## C. UI 工作流与性能

- [x] C-01 停止、重试、重新生成和空响应状态
  - Red：新增空响应和停止请求测试。
  - Green：加入 Stop/Retry 命令、取消令牌、空响应提示、取消/失败可重试提示；确认/撤销/空闲查找异常收敛到消息区。
  - Refactor：保持“思考”展示不变。
  - Verify：`AssistantViewModelTests` 23/23、Desktop UI 契约/Markdown 51/51；Debug build 0 警告/0 错误。
- [x] C-02 平滑自动滚动、事件解绑和长会话性能
  - Red：现有事件订阅在 DataContext 切换时无法解绑，自动滚动会强制拉回底部。
  - Green：仅接近底部时自动跟随；DataContext/Unloaded 时解除订阅。
  - Refactor：不改变像素滚轮策略。
  - Verify：Assistant Markdown 运行时测试通过。
- [ ] C-03 提案/冲突/附件加载失败状态与无障碍
- [ ] C-04 助理运行时截图与人工验收

## P23 本轮修复：重复会议、结构化邀请和看板同步

- [x] P23-01 `propose_timed_event` 增加 `location` / `meetingNumber`，确认写入 `CalendarEvent.Location` 与标准腾讯会议邀请文本；标题保持会议主题。
- [x] P23-02 对模型返回的链接/会议号标题后缀做客户端清理，并在写入提示中明确字段职责。
- [x] P23-03 同一轮连续定时日程合并成批量预览，一次确认批量创建；待办、记录及单条提案流程保持原有行为。
- [x] P23-04 AI 确认后的本地带地点日程触发团队会议室预约同步，沿用稳定幂等键，避免重复占用；已打开的看板会主动刷新。
- [x] P23-05 回归验证：`dotnet test CcCalendar.sln --no-restore`，Core 91、Infrastructure 100、Desktop 226、Server 42 全部通过。

## P24 团队预约身份与导入时间精度

- [x] P24-01 覆盖旧用户 ID、启动恢复令牌 subject 和 14:10 邀请导入的回归测试。
- [x] P24-02 开发令牌登录始终同步服务端返回的 workspace/user ID；启动恢复时从 JWT `sub` 修复旧用户 ID；权限失败不再阻断云端看板展示。
- [x] P24-03 腾讯会议导入保留原始分钟，并动态加入分钟级时间选项，18:10–19:10 不再变成 18:00–19:00。
- [x] P24-04 云端核验：登录和房间目录接口正常，当前查询日期没有预约记录；完整回归 Core 91、Infrastructure 100、Desktop 228、Server 42 通过。
- [x] P24-05 会议室看板增加 15 秒低频轮询，跨电脑新增预约后当前打开的面板自动刷新。

## P25 预约人员与失败反馈

- [x] P25-01 覆盖旧预约无姓名时当前开发令牌姓名回退，以及在线预约返回 `false` 的提示。
- [x] P25-02 服务端开发令牌登录按姓名 Upsert 成员目录；客户端对当前用户旧预约回退显示姓名，双击/悬停均可见“会议人员”。
- [x] P25-03 快速新增不再静默忽略预约未提交，提示重新登录刷新身份；服务端临时预约实测返回 `OrganizerName=周家丞`。

## P26 跨电脑会议详情与会议室归属

- [x] P26-01 预约数据契约扩展：云端保存并返回完整会议邀请文本，兼容旧 JSON/旧数据库记录。
- [x] P26-02 看板视觉归属：当前登录用户预约显示蓝色，其他用户预约显示灰色；预约人 ID/姓名随快照传递。
- [x] P26-03 他人会议导入：双击详情提供“添加到我的日程”，只创建本地日程，不重新占用云端会议室；邀请、地点和分钟级时间保留。
- [x] P26-04 验证与发布：新增映射/提交回归测试；完整回归 465/465；0.5.4 安装包 60,135,659 bytes、SHA-256 `516F1C329001ECC330405479C7937DE557DDDFE5955503651009C8F70CB2464F` 已上传 OSS；Server 发布完成，阿里云 `/health` 返回 ok。

## P27 云端预约删除权限与房间目录恢复

- [x] P27-01 服务端按预约创建人校验 DELETE 权限，创建人 204、他人 403、不存在 404；新增 2 个 API 回归测试。
- [x] P27-02 客户端蓝色自建预约显示删除按钮，删除成功关闭详情并刷新会议室看板；灰色他人预约不可删除。
- [x] P27-03 修复阿里云工作区房间目录，恢复正式 10 间会议室并从旧备份恢复已有预约；服务重启和公网接口实测通过。
- [x] P27-04 构建发布 0.5.5 安装包并上传 OSS：完整回归 467/467；安装包 60,154,886 bytes，SHA-256 `D5744B8B6493969B1D65A12281EA5CD79B2D3FC7E52CFA7BE914ED0521838BA7`；公网清单和安装包 HTTP 200、长度/哈希校验通过。

## 发布记录

- [x] 0.5.3：包含团队预约身份校正、预约失败可见化、预约人员姓名回退、分钟级邀请导入和会议室轮询刷新。安装包 `artifacts\\installer\\cccalendar-0.5.3-win-x64-setup.exe`，60,155,293 bytes，SHA-256 `19A056AA3CCBF4E6FC557477142F4360B50CF8F6173D86CBEFDF8B5528FC4B74`；公网清单版本、哈希、HTTP 200 和 Content-Length 均校验通过。
- [x] 0.5.5：预约删除权限按创建人校验，删除广播同步，详情窗口仅允许删除蓝色自建预约；阿里云恢复正式 10 间会议室并重启服务。安装包 `artifacts\\installer\\cccalendar-0.5.5-win-x64-setup.exe`，60,154,886 bytes，SHA-256 `D5744B8B6493969B1D65A12281EA5CD79B2D3FC7E52CFA7BE914ED0521838BA7`；公网清单版本/哈希、安装包 HTTP HEAD 200 且长度匹配。
- [x] 0.5.6：重新生成并上传 Windows 安装包；完整回归 467/467，安装包 `artifacts\\installer\\cccalendar-0.5.6-win-x64-setup.exe` 60,154,880 bytes，SHA-256 `653AA29CAE8B50EED130103489679C3956EFD017052C7BDB682143068795F0B4`；公网清单与安装包 HTTP HEAD 200 且长度/哈希匹配。

## P29 团队登录状态持久化

- [x] 团队连接字段变更立即保存到本地设置，避免升级/异常退出导致自动连接所需配置丢失。
- [x] 启动仍优先读取未过期令牌；开发令牌过期时使用已保存姓名和口令静默重新登录；桌面测试 232/232。

## P30 旧预约邀请文本补齐与自动连接

- [x] 增加创建人授权的预约邀请原文更新接口；客户端自动将本地日程原文补齐到云端旧预约。
- [x] 发布 0.5.7 Windows 安装包和 Server：完整回归 467/467；安装包 60,145,984 bytes，SHA-256 `5830446DDEBE0C62C1E0C0BC13F30621BC75EAA92BE8F69AE8E24661C9D438F0`；公网清单/安装包 HTTP 200、长度和哈希一致；Server `/health` 与邀请补齐接口实测通过，正式会议室和预约数据已恢复。

## P31 登录后看板刷新与云端目录失败隔离

- [x] 团队连接状态变化主动刷新已打开看板，确保手动登录后使用最新工作区和用户身份。
- [x] 云端加载失败保留最后成功快照并记录诊断，不再静默显示旧占位目录。
- [x] 发布 0.5.8 Windows 安装包并完成验证：完整回归 467/467；安装包 60,145,279 bytes，SHA-256 `D785EDDA20290C9CC9BF614E2D9975F940618E27DCA93B53868CB8457BE3FF89`；公网清单/安装包 HTTP 200、长度和哈希一致；云端 10 间房、5 条预约。

## P32 在线预约冲突可解释性

- [x] 409 冲突提示补充云端冲突房间、分钟级时间和预约人；自建冲突提示删除入口，避免误认为没有预约。
- [x] 完整回归 467/467；待下一版安装包验收。

## P33 本地保存重复提交修复

- [x] 确认 `201 + 409` 根因：本地保存触发全局刷新同步一次，快速新增显式提交再次发送同一预约。
- [x] 改为先提交云端再保存本地，避免重复提交；完整回归 467/467。
- [ ] 下一版安装包验收单次创建只生成一条云端预约。

## P34 删除云端预约同步移除本地镜像

- [x] 修复删除自己创建的云端预约后本地镜像仍显示在会议室看板、并可能被后台同步重新创建的问题。
- [x] 删除成功后按房间、标题、日期和分钟级起止时间删除对应本地日程，再刷新看板。
- [x] 新增匹配回归测试；桌面测试 233/233 通过。
- [x] 发布 0.5.9 Windows 安装包并上传 OSS；完整回归 468/468；安装包 `artifacts\\installer\\cccalendar-0.5.9-win-x64-setup.exe`，60,130,937 bytes，SHA-256 `81409611D0367591D30AB05F5346095EAAA456B166D2B9FEBD6349E2DAD992F6`；公网清单和安装包 HTTP 200、长度/哈希一致。
