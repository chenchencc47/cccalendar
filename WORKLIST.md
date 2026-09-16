# cccalendar Worklist

> 本文件是跨会话续作的唯一进度入口。每完成一次 Red、Green、Refactor 或验证，都要立即更新对应条目和“当前检查点”。

## 当前检查点

- 2026-09-16：本轮 4 项 bug 修复（TDD 逐项 Red→Green→Verify），云端环境（阿里云 ECS 47.120.6.126:5080）已就绪：
  - B-01 快速新增导入会议室模糊匹配 ✅ 完成：`TencentMeetingInvitationParser.MatchRoom` 在原有两层（双向包含、整体单字符插入）之上新增第三层——房间名删一个字符的变体被包含在地点文本中即命中，覆盖"佛山西樵财务三楼会议室"→"财务部三楼会议室"。Red 1 失败 → Green Core 解析器测试 10/10。
  - B-02 日历会议显示位置错误（8 小时偏移）✅ 完成：根因——`ProviderAiAssistantClient.GetDateTimeOffset` 用 `DateTimeStyles.RoundtripKind` 把模型误标 Z 的本地墙钟时间按 UTC 解析，而系统提示只给无时区本地时间（本机 DB 中 3 条 TZ="Asia/Shanghai" 的"每日例会"存为 10:10 UTC=北京 18:10 为证据；QuickAdd/团队预约/云镜像路径时区均验证正确）。修复：新增 `GetZonedDateTimeOffset`——解析结果显式零偏移且 timeZoneId 偏移非零时按该时区墙钟时间重新锚定（须用 `GetUtcOffset(DateTimeOffset)` 重载，`GetUtcOffset(DateTime)` 对 Utc Kind 恒返回零）；`WriteAiToolCatalog` propose_timed_event 描述明确要求本地时间带偏移、禁止 Z/UTC。Red 1 失败 → Green Infrastructure AI 测试 21/21。
  - B-03 桌面日历不显示 ✅ 完成：根因——本机 `settings.json` `desktopWorkbenchAppearance.opacity=0.10730593607306027`（10.7% 近乎全透明），且设置页滑杆无整数吸附，拖动/滚轮产生任意小数漂移。修复：`AppearanceSettingsViewModel.OpacityPercent` 写入时 `Math.Round` 取整（Red 1 失败 → Green VM 测试 4/4）；`SettingsView.xaml` 滑杆加 `IsSnapToTickEnabled="True"`；本机 settings.json 该值重置为 0.95（应用未运行时直接改文件）。
  - B-04 快速新增待办无四象限选择 ✅ 完成：`QuickAddRequest` 增加 `TodoQuadrant? Quadrant` 可选参数；`QuickAddWindow` 待办类型显示"四象限"下拉（不指定/重要且紧急/重要不紧急/紧急不重要/不重要不紧急，第 0 项为不指定），`TryBuildRequests` Todo 分支携带选择，`CalendarDataService.QuickAddAsync` 经 `CreateTodo` 创建后应用 `MoveToQuadrant`（不指定时保持默认 IsImportant=false/IsUrgentOverride=null）。Red：Infrastructure CS1739 编译失败 + Desktop CS1061 编译失败 → Green：CalendarDataService 测试 15/15、QuickAddWindow 运行时测试 9/9。
  - 历史数据提示：本机 DB 中 3 条 TZ="Asia/Shanghai" 的"每日例会"（存为 10:10 UTC=北京 18:10）为 AI 误标 Z 产生的历史偏移数据，可选择删除或手动改期；新创建的日程已不受影响。
  - 完整回归：2026-09-16 `eng\verify.cmd` 通过，format check 通过、build 0 警告 0 错误、测试 479/479（Core 93、Infrastructure 104、Desktop 238、Server 44；基线 474 + 本轮新增 5）。
- 2026-09-16：版本 0.6.6 安装包构建并发布完成（含上述 4 项 bug 修复 + 本机 settings.json `apiBaseUrl` 前导空格清理）：`eng\publish.cmd` 完整回归 479/479、Release 发布与启动冒烟通过；首次构建时发现 `installer\cccalendar.iss` 的 AppVersion 独立于 csproj（曾产出误命名的 0.6.5 包，已删除重编），两处版本号已同步为 0.6.6。产物 `artifacts\installer\cccalendar-0.6.6-win-x64-setup.exe` 60,145,895 bytes，SHA-256 `5EC5032FB95EAD42622E7F2115BE9ABBE5DBE6E6F92C63209026F38452C2371B`；已按 OSS_RELEASE_GUIDE 上传安装包与根目录 `version.json`（先包后清单），公网校验通过：清单返回 0.6.6、SHA-256 一致、安装包 HEAD 200 Content-Length 60,145,895。其他电脑可通过“检查更新”或重新安装升级。
- 2026-09-16：**新增 P60 UI 优化阶段（方案已出，待裁决后开工）**。方案文档 [docs/UI_OPTIMIZATION_PLAN.md](docs/UI_OPTIMIZATION_PLAN.md)；本阶段借鉴 `D:\github_program\deepseek-harness\web-ui-extract` 的设计令牌纪律（三层令牌、抬高面描边即首层阴影、状态色单源派生、滚动条间接重绑、动效 120/160ms、容器驱动布局），明确拒绝其会话壳/气泡/大圆角/渐变/星光图标/阅读栏宽度轴。实测体检结论：`Themes\Theme.xaml` 只有 30 个资源键，**间距/圆角/字号/动效令牌全部缺失**（圆角 6 种字面量、硬编码字号 112 处/23 文件、硬编码颜色 33 处）；其中 `Views\RoomBookingBoardControl.cs` 独占 18 处硬编码颜色，导致**深色主题下会议室看板仍是白底、桌面组件的主题/颜色/透明度设置对看板完全无效**——这是本阶段最高价值项。另发现潜在缺陷：`Theme.xaml:29` 引用的 `{DynamicResource UiTextEffect}` 在全仓均未声明（仅 `DesktopAppearanceController.cs:66` 对桌面窗口注入），主窗口与普通页面解析为空。切片 P60-01…P60-06 见文件末尾 P60 章节；每片独立验收，P60-01 只加令牌不改页面引用以保证观感零变动。**当前无代码改动，未运行回归；开工前需先裁决 Q1（按钮圆角 8px vs 4px）、Q2（看板时段）、Q3（`UiTextEffect` 处置）。**
- 2026-08-31：新增 [VERSION_EVOLUTION_PLAN.md](docs/VERSION_EVOLUTION_PLAN.md)，整理前面指定的第 2、3、4、5、7 项版本跨越路线；会议室单字符模糊匹配已完成并通过完整回归，后续不再作为路线图待办。
- 2026-08-24：P21 会议室预约人展示客户端实现完成，`eng\\publish.cmd` 完成完整回归 454/454、Linux Server 自包含发布和 Windows 安装包构建；0.5.1 安装包已覆盖上传 OSS，公网清单/HEAD 校验通过。Linux Server 包已上传至 ECS，保留 `/root/cccalendar/data` 后替换并重启 `cccalendar`；本机和公网 `/health` 均返回 `status: ok`。
- 2026-08-24：P22 修复团队登录启动恢复（读取 Windows 凭据，开发令牌过期自动续签）与本地带会议室日程补同步（稳定幂等键）；完整回归 456/456。待重新生成并上传 0.5.1 安装包。
- 2026-08-24：P22 安装包重新生成并覆盖上传完成；`cccalendar-0.5.1-win-x64-setup.exe` 60,124,103 bytes，SHA-256 `8e1efd68510b832bc345718229a1571e6e205801906deef5999b486a73531cf9`；公网清单和安装包 HEAD/长度校验通过。
- 2026-08-24：P22 版本 0.5.2 发布完成；服务端 DevToken 有效期改为 7 天（10080 分钟），ECS `cccalendar` 重启成功，本机健康检查返回 `status: ok`。Windows 安装包 `artifacts\\installer\\cccalendar-0.5.2-win-x64-setup.exe` 60,106,331 bytes，SHA-256 `647D268D99A25DCEE2C35C55BFB107D604CEE7DA3751C72DF529AAC787A34039`；OSS 清单与安装包公网 HEAD/长度校验通过。
- 2026-08-24：诊断本机 0.5.2 启动无窗口：`calendar.db` 完整性为 `ok`，但 `__EFMigrationsLock` 残留迁移锁导致启动无限等待；清理过期锁后主窗口恢复。未删除日历数据；原 `calendar.db-shm`/`calendar.db-wal` 已保留为 `.stale-*` 备份。
- 2026-08-25：修复阿里云会议室目录异常：实际 `/root/cccalendar/data/rooms.json` 曾被旧部署数据覆盖为乱码房间和“云会议室 A/B/C”，与文档中的 10 间真实会议室不一致。已备份原文件为 `rooms.json.backup-20260825-0800`，恢复 10 间真实会议室并重启服务；公网 `/health` 与房间目录 API 均验证通过。

- 当前阶段：P18 会议邀请保留期与界面细节（进行中）
- 当前状态：会议邀请改为会后第 7 天仍保留、第 8 天首次加载时清理；快速新增邀请区改为多行输入框下置右对齐操作；会议室半小时格高度调整为 18px；天气日预报行增加高度避免温度截断；设置页加入完整 RGB 调色板、右上角更新图标和手动检查更新；助理回复增加可折叠思考块；全局按钮统一圆角交互样式；当前发布版本为 0.4.14。
- AI 助理专项续作清单见 [docs/ASSISTANT_WORKLIST.md](docs/ASSISTANT_WORKLIST.md)，方案见 [docs/ASSISTANT_REVIEW_PLAN.md](docs/ASSISTANT_REVIEW_PLAN.md)；保留“思考”折叠展示，当前执行 A-01 重复消息与历史读取。
- 自动更新：当前尚未接入客户端版本检查、签名下载与安装替换；后续单独按 HTTPS 清单 + 签名安装包（建议 Velopack/等价方案）实现。
- 自动更新方案已记录于 `docs/AUTO_UPDATE_DESIGN.md`；待单独迭代实现，不与本轮 UI 修复混在一起。
- 自动更新第一阶段已实现：启动后读取 `CCCALENDAR_UPDATE_MANIFEST_URL`，HTTPS 获取 `version.json`，仅提示高于当前版本的更新并打开下载地址；未配置地址时安全跳过。
- 更新地址策略已调整：生产地址写入 `App.xaml.cs` 的 `DefaultUpdateManifestUrl`，环境变量仅覆盖测试/灰度；已补充 OSS 控制台、RAM、ossutil 和上传验证步骤。
- OSS 发布 Bucket 已确认：`cccalendar-releases-01`，Region `cn-heyuan`，Endpoint `https://cccalendar-releases-01.oss-cn-heyuan.aliyuncs.com`；客户端清单地址已固化为 `/releases/version.json`。
- 2026-08-23：生成并上传 `0.3.6` 安装包（57.32 MB，SHA-256 `ECC34653489FEE7E5162A220830A66FACCC10C3C35738E0D074E7CBD01CAC2A9`）；公网清单返回 0.3.6，安装包 HEAD 返回 200。
- 2026-08-23：版本 0.3.7 增加设置页“检查更新”、启动自动检查提示、颜色点击调色板与自定义十六进制输入；生成并上传 `cccalendar-0.3.7-win-x64-setup.exe`（57.32 MB，SHA-256 `5F99135730E266AFC303FBAC7D4AC10A0E8BA80AA8B760DD687BD1D5AE5CF03A`）；公网清单返回 0.3.7，安装包 HEAD 返回 200。
- 2026-08-23：版本 0.3.8 将更新提示改为主窗口右上角下载图标（不再重复弹窗），调色板增加可见预览和 R/G/B 0–255 滑杆/数值输入；生成并上传 `cccalendar-0.3.8-win-x64-setup.exe`（57.31 MB，SHA-256 `79E6F5CFFDA1421039703DFF65530870A59DB8EA9A49D2377F649E7C2DE9DD1A`）；公网清单返回 0.3.8，安装包 HEAD 返回 200。
- 2026-08-23：版本 0.3.9 重做颜色选择器：完整色板 + “更多颜色...”独立 RGB 窗口，数字框加宽且支持 0–255；生成并上传 `cccalendar-0.3.9-win-x64-setup.exe`（57.31 MB，SHA-256 `6AFF5061764BFBDCC0B73AF9AE742508582334B65381049B42494DB61541C384`）；公网清单返回 0.3.9，安装包 HEAD 返回 200。
- 本轮追加：会议邀请输入框使用 `PreviewKeyDown` 拦截 Enter，Enter 与“导入”一致；会议室半小时格高度调整为 18px。
- 最近验证：2026-08-23，追加改动后 `eng\verify.cmd` 通过，完整回归 410/410（Core 88、Infrastructure 92、Desktop 188、Server 42）。
- 最近验证：2026-08-23，`eng\verify.cmd` 通过，完整回归 402/402（Core 88、Infrastructure 89、Desktop 183、Server 42），build 0 警告/0 错误；邀请保留期边界测试通过。
- 最近发布：`artifacts\installer\cccalendar-0.3.10-win-x64-setup.exe` 已生成并上传 OSS（60.12 MB，SHA-256 `B480FE0C8BBE329CB04A3C5413D87A763947C831079A1E3949EDC92BDE3612DC`）；公网清单返回 0.3.10，安装包 HEAD 返回 200。
- 最近修复：2026-08-23，定位并修复全局横向滚动条 `Track.IsDirectionReversed=True` 导致的初始右侧/左右反向问题；409 团队预约冲突改为可读提示；`eng\verify.cmd` 通过，Desktop 183 项。
- 最近发布：2026-08-23，包含上述修复的安装包重新生成成功，SHA-256 `F714DE2178D8CAE8FA1419BC9DF5ED4B671954D7EA5512D25AE8E9D0B54F23C8`。
- 2026-08-23：删除助理输入区“思考/深度搜索”开关，恢复原始 LLM 请求语义；新增 API 思考增量的可折叠回复区块；全局 Button/PrimaryButton/SegmentToggle 圆角统一为 8px；快速新增会议室看板半小时格从 20px 调整为 18px。`eng\verify.cmd` 通过，完整回归 411/411（Core 88、Infrastructure 93、Desktop 188、Server 42）；下一次构建递增至 0.3.11。
- 2026-08-23：修复调色板 Popup 数据上下文未继承导致色块空白；删除快速新增顶部“今天”按钮后的旧测试引用；恢复历史会话时过滤连续完全重复消息，并补充发送防重复与日期栏/颜色契约测试。
- 最近验证：2026-08-23，桌面测试 190/190；`eng\verify.cmd` 通过，完整回归 413/413（Core 88、Infrastructure 93、Desktop 190、Server 42），build 0 警告/0 错误；本轮未重新生成或上传安装包。
- 2026-08-23：版本 0.4.2 安装包已生成并上传 OSS；`version.json` 已切换到 0.4.2，公网清单与安装包 HEAD 校验通过。安装包大小 60,101,178 bytes，SHA-256 `32456437D9BDBE5BB3B740A8403EE8D0E6681588A68DFA15845F98027B1CCC8E`。
- 2026-08-23：版本 0.4.3 安装包已生成并上传 OSS；`version.json` 已切换到 0.4.3，公网清单与安装包 HEAD 校验通过。安装包大小 60,118,965 bytes，SHA-256 `77F9F55FB74A210601ED5943AACB85F32369921F7526FD9AE8AAE137A1DA800E`。
- 2026-08-23：版本 0.4.4 安装包已生成并上传 OSS；包含助理重复提问保护、历史重复清理、用户消息可选中复制及 AI Message 风格头像/复制操作。公网清单与安装包 HEAD 校验通过；安装包大小 60,098,581 bytes，SHA-256 `A0A6B619DB37752E288C3D0CD785853F8328806F2A937481C5E412A56ED31BC6`。
- 2026-08-23：版本 0.4.5 修复启动崩溃（只读用户消息 TextBox 改为 `Mode=OneWay`），重新生成并上传 OSS；公网清单、SHA-256 与安装包 HTTP 200 校验通过。安装包大小 60,123,673 bytes，SHA-256 `14D39B37E4D94771DAF379B82050B3F550EDF5B4A6D08091025EE3E162FE49F8`。
- 最近验证：2026-08-23，`eng\verify.cmd` 通过，完整回归 416/416（Core 88、Infrastructure 93、Desktop 193、Server 42），build 0 警告/0 错误；0.4.4 公网清单版本、SHA-256 和安装包 HTTP 200 均校验通过。
- 2026-08-23：修复设置页调色板色块空白：根因是颜色绑定位于 Button.Content 的 Border，在当前 WPF ContentPresenter/按钮模板下没有正确继承 `ColorChoiceViewModel`；改为外层颜色 Border + 内层透明按钮，确保每个色块直接绑定 `Color`。
- 最近验证：2026-08-23，`eng\verify.cmd` 通过，完整回归 414/414（Core 88、Infrastructure 93、Desktop 191、Server 42），build 0 警告/0 错误；本轮未重新生成或上传安装包。
- 2026-08-23：更新图标改用蓝色 `PrimaryButtonStyle`，保持新版本可用时清晰可见；快速新增窗口改为非模态 `Show()`，创建/取消使用显式 `Close()`，关闭后异步保存请求并防止重复打开。
- 最近验证：2026-08-23，`eng\verify.cmd` 通过，完整回归 414/414（Core 88、Infrastructure 93、Desktop 191、Server 42），build 0 警告/0 错误；本轮未重新生成安装包。
- 唯一下一步（2026-09-16 起）：**先裁决 Q1/Q2/Q3，再开工 P60-01 令牌基线**（新增设计令牌，不改页面引用，验收以「完整回归 479/479 + 新增契约测试通过 + 观感零变动」为准）；P15-09d 两地云端验收与生产 HTTPS/OIDC 为长期并行事项，见 P40/P47。本轮无阻塞项。

- 当前阶段：P16 快速新增/看板/桌面组件增强（P16-04~07 完成）
- 当前状态：P16 已发布，后续云端验收与生产 HTTPS/OIDC 仍是长期事项；本轮 P18 变更已在上方检查点记录。
- NEXT：两地电脑安装 0.4.14 跑 P15-09d 云端验收三场景（看板互见/同时段 409/重启持久化）；长期项为生产 HTTPS + OIDC。
- 最近验证：2026-08-23，版本 0.3.5 发布与 402 项完整回归通过；上一版 0.3.4 的历史记录保留在下方发布日志。
- 最近验证：2026-08-22，云服务器部署：阿里云 ECS（Ubuntu 22.04）systemd 部署 Server + 公网 `/health` ok + UTF-8 建房 + 公网验证无口令/错口令登录 401；Linux 自包含发布包 `artifacts\cccalendar-server-linux.zip`
- 最近验证：2026-08-21，版本 0.3.1 发布（局域网开发令牌登录 + 团队会议室看板接入 Server）；完整回归 377/377；安装包 SHA-256 `20B4CC2E8A4A8828F1B9622F149E2F15431D1DC01FDABD6304C249808E857060`
- 最近验证：2026-08-21，PostgreSQL 实机验收：创建 `cccalendar` 数据库/账号/btree_gist 扩展，真实 Server 启动 schema 初始化通过（5 表 + 幂等唯一键 + GiST 排斥约束）；新增 `PostgresBookingAcceptanceTests`（3 项，仅当设置 `ConnectionStrings__Postgres` 环境变量时执行实库验证），覆盖 `/health` storage=postgres、双客户端目录共享 + 同时段 409 + 幂等键重试、重启后变更/游标/预约持久化；验收后清空测试数据；`eng\verify.cmd` 通过，测试 342/342（Core 78、Desktop 157、Infrastructure 81、Server 26）
- 最近验证：2026-08-19，补丁 0.2.6：工具轮回传 400 自愈（真实 DeepSeek 全流程实测 200 未复现用户报错，判定为对端环境差异；增加 BadRequest 时降级最保守回传格式——去 reasoning_content/空 content——重试一次，流式+非流式两路）；AI 助手多会话（新建对话/删除/会话下拉切换、标题自动取首条用户消息、独立持久化 ai-chat-sessions.json、旧单会话文件自动迁移）；`eng\publish.cmd` 退出 0；测试 279/279（Core 68、Desktop 146、Infrastructure 65）；安装包 `artifacts\installer\cccalendar-0.2.6-win-x64-setup.exe`，SHA-256 `DE5A391F16C760AA7AB903B3F65A8F9173FBD7716187D2BECA7FD2189603EF24`
- 最近验证：2026-08-19，补丁 0.2.5：真正定位"日程不显示日期"根因——DatePicker 自定义模板的 PART_TextBox 用了普通 TextBox，而控件内部按 DatePickerTextBox 类型查找导致拿不到部件、日期文本永不写入；新增 STA 运行时测试（QuickAddWindowRuntimeTests，加载真实主题字典实例化窗口）先复现后修复（模板改 DatePickerTextBox + 派生样式）；`eng\publish.cmd` 退出 0；测试 277/277（Core 68、Desktop 145、Infrastructure 64）；安装包 `artifacts\installer\cccalendar-0.2.5-win-x64-setup.exe`，SHA-256 `5B3247C712EE0442DD1EFCBE3503A30B0C15458BFE1404434F6C757F3708ACFF`
- 最近验证：2026-08-19，补丁 0.2.4：快速新增默认类型固定为「日程」（根因：右上角按钮入口无 eventDate 时默认「待办」，日程字段整块隐藏，用户以为不显示日期）；会议室看板改 8:00–24:00 展示（FirstVisibleCell 偏移渲染+命中）、网格底色改纯白与已占用灰色拉开对比、新选区默认替换旧选区/按住 Ctrl 才累加；`eng\publish.cmd` 退出 0；测试 276/276；安装包 `artifacts\installer\cccalendar-0.2.4-win-x64-setup.exe`，SHA-256 `C0683C3CEA709381CA64C0942B25DCB591018B9AC6C877C165D36C6B0F78D5F0`
- 最近验证：2026-08-19，补丁 0.2.3：四象限/看板拖拽补 DragOver 修复（WPF 默认禁止效果导致 Drop 不触发）；天气窗口改为前 1 小时+现在+后 24 小时、格子透明命中、按下即捕获；AI 聊天历史 JSON 持久化 + 列表自动滚底；快速新增改左右布局 + GridSplitter 分栏可拖宽 + 时间小格高度减半（26→13）；`eng\publish.cmd` 退出 0；测试 276/276（Core 68、Desktop 144、Infrastructure 64）；安装包 `artifacts\installer\cccalendar-0.2.3-win-x64-setup.exe`，SHA-256 `998B08E91F5AB2DCA084EE8F1E37BEB8C033B8BB2B07936FDE81670F3A4D31E3`
- 最近验证：2026-08-19，补丁 0.2.2：DeepSeek 思考模式工具轮次回传 reasoning_content（实测定位 400 根因）；AI 输入框 Enter 发送/Shift+Enter 换行；待办四象限与看板移动持久化 + 卡片右键菜单；天气/看板滚轮横滚与天气拖拽平移；`eng\publish.cmd` 退出 0；测试 275/275（Core 68、Desktop 143、Infrastructure 64）；安装包 `artifacts\installer\cccalendar-0.2.2-win-x64-setup.exe`，SHA-256 `582CCBF7F439ADFABB43D8E92EBD2FA606D6A6C2D11E46A5C2529D6D27F438F8`
- 最近验证：2026-08-19，补丁 0.2.1：AI 上下文注入当前本地日期（修复"本周"被反问）；`eng\publish.cmd` 退出 0；测试 272/272（Core 68、Desktop 143、Infrastructure 61）；安装包 `artifacts\installer\cccalendar-0.2.1-win-x64-setup.exe`，SHA-256 `91131665762C50DD0CA8B05D421F43837549ED03DDEC5E8C647D1A1272D7E1C8`
- 最近验证：2026-08-19，`eng\publish.cmd` 退出 0；format check、build 0 警告/0 错误、测试 269/269（Core 66、Desktop 143、Infrastructure 60）、Release 自包含发布启动冒烟及安装器编译通过；安装包 `artifacts\installer\cccalendar-0.2.0-win-x64-setup.exe`，SHA-256 `0352FF3C4C6EE9B36EF89AAEEB112D06F060D8F16DE14BFA23B1B550454004C2`
- 最近验证：2026-08-19，`eng\publish.cmd` 退出 0；format check、build 0 警告/0 错误、测试 248/248（Core 66、Desktop 125、Infrastructure 57）、Release 自包含发布启动冒烟及安装器编译通过；安装包 `artifacts\installer\cccalendar-0.1.0-win-x64-setup.exe`，SHA-256 `823E97C23CE7D0AACBAB8BADBF4007C40A096727945AE49FAD48DDF1EFECCDC7`
- 最近验证：2026-08-17，`eng\publish.cmd` 退出 0，format check、build 0 警告/0 错误、测试 198/198、发布启动冒烟及安装器编译通过；隔离 UI 验收通过；SHA-256 `F625C077C564EC14D89AF7921523B4E60A901C50EA66FA8EB27EE0F2D800784F`
- 最近验证：2026-08-17，P7-40 provider 流式测试 12/12、Assistant/设置测试 19/19，format check 通过；真实分块时序测试通过
- 最近验证：2026-08-17，P7-39 Desktop AI/设置测试 18/18、配置/provider 测试 9/9，format check 通过
- 最近验证：2026-08-17，P7-38 Assistant 11/11、provider 6/6、Core AI 3/3；Desktop Debug build 0 警告、0 错误
- 最近验证：2026-08-17，`eng\publish.cmd` 退出 0，build 0 警告、0 错误，测试 182/182；发布启动冒烟和安装器编译通过，SHA-256 `7E7F70235892B1313E3B22FB3E328CBE3BCC7FEE3D193FBF14D7F7366476D3FA`
- 最近验证：2026-08-17，P7-36 AI/外观测试 6/6、Desktop Debug build 0 警告、0 错误；隔离进程强制终止并重启后恢复 42% 透明度和自定义 AI 端点
- 最近验证：2026-08-17，`eng\publish.cmd` 退出 0，build 0 警告、0 错误，测试 181/181；发布启动冒烟和安装器编译通过，SHA-256 `8AF70C01BBFCDBA44E3462140B4753050F23FB82A9B57026B1A7EA0100F7DD1D`
- 最近验证：2026-08-17，P7-34 助手相关测试 12/12，format check 通过，Debug build 0 警告、0 错误
- 最近验证：2026-08-17，P7-33 桌面状态测试 10/10、数据服务测试 3/3，format check 通过，Debug build 0 警告、0 错误
- 最近验证：2026-08-17，`eng\publish.cmd` 退出 0，build 0 警告、0 错误，测试 177/177；发布 EXE 与安装器均提取到新图标，SHA-256 `0AF9A043AA31FE6CC22D845B8D1D9AFFE662DEE1C102866BDF4F3BDDDD6E5914`
- 最近验证：2026-08-17，应用图标资源测试 1/1；PNG 透明角像素 alpha=0，16px/32px 放大检查可辨识日历轮廓和绿色勾选
- 最近验证：2026-08-17，`eng\publish.cmd` 退出 0，build 0 警告、0 错误，测试 176/176；自包含程序隐藏启动冒烟和安装器编译通过，SHA-256 `B58EB6DB48C99629059FD3DA99B901695550DF3461745E2DACB757D759C3F2DA`
- 最近验证：2026-08-17，P7-29 助手 ViewModel 测试 10/10；隔离实例通过文件选择器添加 `.md`、显示文件名及移除附件
- 最近验证：2026-08-17，P7-28 助手相关测试 7/7，Desktop Debug build 0 警告、0 错误；隔离实例截图确认 Markdown 标题与列表已渲染且不显示语法标记
- 最近验证：2026-08-17，P7-27 聚焦测试通过（桌面状态 6/6、数据服务 2/2）；隔离桌面实例完成全天日程创建、"全天"显示及确认删除后即时消失
- 最近验证：2026-08-17，`eng\publish.cmd` 退出 0，测试 168/168；日程窄宽换行、时间展示、当前起 24 小时天气及隐藏启动冒烟通过
- 最近验证：2026-08-17，`eng\publish.cmd` 退出 0，测试 166/166；Enter 搜索、自动展开、城市级过滤、行政区全称显示及隐藏启动冒烟通过
- 阻塞项：无
- 续作方式：先阅读 `docs/DESIGN.md`、`docs/SYNC_DESIGN.md` 和本文件，再只处理 `NEXT` 指向的条目；联网切片完成后立即更新本文件并继续下一项

## 本轮需求：会议室优先显示与桌面日历水平定位（2026-08-24）

- [x] 会议室看板按当天预约稳定排序：有会议的房间前移，保持原目录相对顺序；本地日程和云端团队预约统一处理。
  - Red：新增 `RoomBookingOrderingTests.MovesRoomsWithBookingsToFrontWhilePreservingCatalogOrder`，实现前编译失败（缺少排序策略）。
  - Green：新增 `RoomBookingOrdering`，并接入 `RoomBookingPicker.RenderBoard` 与 `CalendarViewModel.RebuildRoomColumns`。
  - Refactor：复用稳定排序策略，未做无关整理。
  - Verify：`dotnet test tests/CcCalendar.Desktop.Tests/CcCalendar.Desktop.Tests.csproj --filter "FullyQualifiedName~RoomBookingPickerRuntimeTests|FullyQualifiedName~RoomTimelineColumnsTests|FullyQualifiedName~RoomBookingOrderingTests" --no-restore`；本地/云端排序聚焦通过，桌面全量 207/207。
- [x] 桌面日历组件仅允许水平移动，纵向位置保持不变，缩放行为不变。
  - Red：新增 `DesktopCalendarLayoutStateTests.RememberHorizontalPositionPreservesVerticalPosition`，实现前编译失败（缺少水平位置 API）。
  - Green：增加 `RememberHorizontalPosition`，桌面日历拖动改为捕获鼠标仅更新 `Left`，并在位置变更时持久化 X。
  - Refactor：顶部居中定位在调整尺寸时只更新 Y，不重置用户已调整的 X。
  - Verify：`dotnet test tests/CcCalendar.Desktop.Tests/CcCalendar.Desktop.Tests.csproj --no-restore`；207/207 通过。

## 状态约定

- `[ ]` 未开始
- `[>]` 正在进行，同时只能有一个
- `[x]` 已完成并有验证证据
- `[!]` 阻塞，必须写明原因
- `[~]` 明确延期，不属于当前交付阶段

## TDD 循环记录规则

每个实现条目必须补充以下证据后才能完成：

```text
Red：测试名称、命令、预期失败原因
Green：最小实现说明、相关测试命令与结果
Refactor：仅记录实际发生的整理；没有则写“无”
Verify：完整验证命令、结果、必要的人工检查
```

## P0 - 需求与工程决策

## P17 - 会议室看板与会议邀请工作流

- [x] P17-01 横向拖动方向
  - Red：`HorizontalScrollTests.DraggingContentToTheRightMovesTheContentWithThePointer` 在缺少偏移计算契约时无法编译。
  - Green：新增 `CalculatePanOffset`，内容拖动使用直接操作方向，并跳过原生 `ScrollBar` 命中；Today 天气横向拖动提示同步更新。
  - Refactor：无。
  - Verify：Desktop 聚焦测试通过。
- [x] P17-02 邀请手动导入、原文留存与到期清理
  - Red：Core/Infrastructure 测试因 `CalendarEvent`、`QuickAddRequest` 缺少邀请字段失败；旧 UI 测试复现 TextChanged 自动填充。
  - Green：新增邀请原文和到期日字段、SQLite 幂等补列；输入框只粘贴，Enter/“导入”解析；保存请求携带原文，加载时在会议日期后第二天清理。
  - Refactor：无；保留现有标题附加会议号行为。
  - Verify：Core/Infrastructure/Desktop 聚焦测试通过。
- [x] P17-03 看板详情、日期栏与当天导出
  - Red：运行时测试验证非今天仍显示“今天”且无导出按钮。
  - Green：非今天折叠 Today 按钮；占用块支持双击事件、悬停补充会议号/链接；新增当天导出窗口，按会议分面板展示。
  - Refactor：详情与导出窗口复用同一会议号/链接提取逻辑。
  - Verify：RoomBookingPickerRuntimeTests 通过。
- [x] P17-04 复制模式
  - Red：无原始邀请导出/复制 API。
  - Green：每个会议提供“复制全部信息”“复制会议号和链接”，顶部提供“复制全部会议”；无原文时不生成空剪贴板内容。
  - Refactor：无。
  - Verify：MeetingExportWindowTests 通过。
- [x] P17-05 完整回归与视觉验收
  - Red：首次格式检查发现 `DatabaseInitializer.cs` 多余导入，清理后重跑。
  - Green：完整验证脚本通过；快速新增窗口真实启动并用 UIAutomation/屏幕截图检查按钮与日期栏布局。
  - Refactor：将邀请按钮改为固定宽度并排布局，修复窄表单下“粘贴会议邀请”截断。
  - Verify：`eng\verify.cmd` 399/399；截图 `screenshots\qa27_qa_001637.png`。

- [x] P0-01 固化产品需求基线
  - 验证：已写入 `docs/DESIGN.md` 第 1-7 节。
- [x] P0-02 建立逻辑架构、TDD 规则和完成定义
  - 验证：已写入 `docs/DESIGN.md` 第 8-11 节及本文件。
- [x] P0-03 收集并确认用户的其余开发规则与技术栈
  - 验收：记录语言、桌面框架、UI 约束、依赖策略、提交规则和目标 Windows 版本。
- [x] P0-04 决定是否初始化 Git 仓库及提交规范
  - 验收：仓库状态明确；若初始化，则首个提交范围只包含确认后的文档和脚手架。
- [x] P0-05 选择测试框架、数据库方案和 Windows 集成策略
  - 验收：形成简短 ADR，列出选择理由、替代项和验证原型。
- [x] P0-06 将完整范围拆成交付阶段并确认首阶段边界
  - 验收：每阶段有用户可观察结果、自动化验收范围和不包含项。

## P1 - 工程基础

- [x] P1-01 安装 .NET 10 SDK，并创建最小应用和测试工程
  - Red：不适用；此条只创建脚手架，不引入用户可观察行为。
  - Green：安装 .NET SDK 10.0.400；创建 Core、Infrastructure、Desktop 及对应测试项目。
  - Refactor：移除模板生成的空 `Class1` 和无断言 `UnitTest1` 文件。
  - Verify：`dotnet restore CcCalendar.sln`、`dotnet build CcCalendar.sln --no-restore --configuration Debug`、`dotnet test CcCalendar.sln --no-build --configuration Debug` 均退出 0；当前测试程序集没有测试用例。
- [x] P1-02 建立格式化、静态检查、真实单元测试和覆盖率命令
  - Red：`ApplicationIdentityTests.ProductNameIsCccalendar` 首次运行因 `ApplicationIdentity` 不存在而以 CS0103 失败。
  - Green：添加唯一产品名来源并用于 WPF 窗口标题；测试 1/1 通过。
  - Refactor：中央管理测试包版本；统一 LF、格式和静态分析；以 `eng\verify.cmd` 取代受 PowerShell 执行策略影响的脚本。
  - Verify：`eng\verify.cmd` 退出 0；format check 通过；6 项目 build 为 0 警告、0 错误；测试 1/1 通过并生成 Cobertura 覆盖率。
- [x] P1-03 建立本地配置、密钥存储接口和日志脱敏规则
  - Red：配置测试因模型/存储不存在而失败；脱敏测试因安全命名空间不存在而失败。
  - Green：实现默认设置、原子 JSON 保存、`ISecretStore` 和已知密钥替换；相关测试 4/4 通过，加上产品名共 5/5。
  - Refactor：将密钥与普通配置从接口上分离；补充 `docs/SECURITY.md` 明确允许和禁止记录的数据。
  - Verify：`eng\verify.cmd` 退出 0；6 项目 build 为 0 警告、0 错误；测试 5/5 通过并生成 Cobertura 覆盖率。
- [x] P1-04 建立 SQLite 迁移测试与临时数据库测试夹具
  - Red：迁移测试因 `TemporaryCalendarDatabase` 不存在而以 CS0246 失败。
  - Green：添加 EF Core SQLite 10.0.11、Bootstrap 迁移及禁用连接池的真实临时文件夹具；迁移测试通过。
  - Refactor：使用仓库本地 `dotnet-ef` 10.0.11；拒绝有高危传递依赖告警的 10.0.0，并统一生成文件格式。
  - Verify：`eng\verify.cmd` 退出 0；build 为 0 警告、0 错误；测试 6/6 通过；数据库文件在夹具释放后不存在。
- [x] P1-05 建立 Windows 发布包与干净环境启动验证
  - Red：首次发布在安装器阶段因 Inno Setup 位于当前用户目录而失败。
  - Green：发布脚本兼容系统级和当前用户 Inno Setup 路径；生成自包含 `win-x64` 应用并通过隐藏启动冒烟。
  - Refactor：发布前统一调用 `eng\verify.cmd`；安装器使用当前用户目录且不要求管理员权限。
  - Verify：`eng\publish.cmd` 退出 0；生成 `cccalendar-0.1.0-win-x64-setup.exe`；SHA-256 为 `F302A9E098ADE2194269B968A81E1214805B352C8B3FE3223913AFDAFABD9A6A`。

## P2 - 领域模型与本地数据

- [x] P2-01 项目、里程碑和参与人模型
  - Red：领域测试因 Projects 模型不存在而失败；持久化测试因 DbContext 没有 Projects 而失败；映射后因待生成迁移而失败。
  - Green：实现项目创建约束、里程碑完成、参与人去重、归档、EF 映射和 AddProjects 迁移；SQLite 往返通过。
  - Refactor：EF 兼容只使用私有构造/setter，映射保留在 Infrastructure；未提前加入任务进度或甘特图算法。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 18/18 通过。
- [x] P2-02 待办、子任务、状态与四象限规则
  - Red：领域测试因 Todos 模型不存在而失败；持久化测试因 DbContext 没有 Todos 而失败。
  - Green：实现收集箱/看板状态、四象限计算与覆盖、子任务进度、完成/重开、EF 映射和 AddTodos 迁移。
  - Refactor：项目删除采用待办外键置空，子任务级联删除；完成状态必须通过带时间戳的 `Complete`。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 25/25 通过。
- [x] P2-03 日程、全天/跨天日程与时间块
  - Red：Schedules 领域测试因模型不存在而失败；持久化测试因 DbContext 缺少日程/时间块集合而失败。
  - Green：实现定时、全天、跨天日程与待办时间块，保存 UTC/原始时区或日期区间，并添加 AddSchedules 迁移。
  - Refactor：全天结束日采用不含语义；项目删除时日程外键置空，待办删除时时间块级联删除。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 32/32 通过。
- [x] P2-04 重复规则、实例例外与节假日跳过规则
  - Red：规则、Ical.Net 展开及 RecurrenceSeries 持久化测试分别因实现不存在而失败。
  - Green：实现日/周/月/年与工作日规则、Ical.Net 5.2.3 展开、节假日/排除过滤、序列实体和 AddRecurrence 迁移。
  - Refactor：星期集合用 bit mask 持久化；单次修改采用排除原实例并另建普通日程；仅对 EF 生成迁移关闭 CA1861。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 40/40 通过。
- [x] P2-05 冲突检测和工作时间可用性
  - Red：Scheduling 测试因工作模板、时间区间和冲突检测不存在而失败。
  - Green：实现周一至周五 08:00-12:00/14:00-17:30 模板、忙碌扣除和半开区间冲突检测。
  - Refactor：本地工作时间与 UTC 日程区间使用不同值对象，避免时区语义混淆。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 43/43 通过。
- [x] P2-06 记录、附件、自动保存和历史版本
  - Red：Records 领域测试及 WorkRecords 持久化测试分别因模型/DbSet 不存在而失败。
  - Green：实现四类记录、追加式版本、历史恢复、附件相对路径校验、EF 映射和 AddWorkRecords 迁移。
  - Refactor：版本以递增序号确定当前正文，不依赖数据库集合顺序；附件数据库只保存元数据。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 48/48 通过。
- [x] P2-07 回收站、30 天保留和恢复
  - Red：回收站领域测试因策略/软删除不存在而失败；持久化测试在迁移缺失时失败。
  - Green：项目、待办、日程、记录实现软删除/恢复；全局查询过滤隐藏删除项；AddRecycleBin 迁移持久化删除时间。
  - Refactor：只对根对象软删除，子对象随根对象保留；清理资格统一由 30 天策略判断。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 51/51 通过。
- [x] P2-08 审计日志与撤销操作
  - Red：Auditing 领域测试和 AuditEntries 持久化测试分别因实现不存在而失败。
  - Green：实现无正文载荷的审计元数据、LIFO 单次撤销、EF 映射和 AddAuditEntries 迁移。
  - Refactor：撤销失败时保留栈顶以便重试；审计不复制密钥、记录正文或 AI 提示词。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 54/54 通过。
- [x] P2-09 本地备份、恢复及数据库格式版本
  - Red：备份恢复测试因 `SqliteBackupService` 和格式版本不存在而失败。
  - Green：实现 SQLite 在线备份、quick_check/格式版本校验、临时文件恢复替换和 DatabaseInitializer。
  - Refactor：数据库格式版本使用 `PRAGMA user_version`；逐记录同步版本语义延期到 P8 同步协议设计。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 55/55 通过；备份恢复后原项目存在。

## P3 - 主窗口工作流

- [x] P3-01 主导航和“今天”视图
  - Red：Desktop 测试因导航/今天 ViewModel 不存在而失败；首次可见启动因环境缺失 WINDIR 失败。
  - Green：实现九项导航、固定时钟今天页、Lucide 图标、紧凑双栏 UI，并在缺失时从 SystemRoot 补齐 WINDIR。
  - Refactor：UI 颜色/控件样式集中到 Theme.xaml；助理使用普通文本对话视觉，不采用机器人/星光/渐变。
  - Verify：`eng\verify.cmd` 退出 0；测试 57/57；build 0 警告、0 错误；PrintWindow 核验 1180x760 与 980x640 无截断或重叠。
- [x] P3-02 日历月/周/日程表视图
  - Red：Desktop 测试因 CalendarViewModel/模式不存在而失败。
  - Green：实现周一开头 42 格月历、7 天周历、日程表、前后翻页/今天定位和分段控件。
  - Refactor：日期计算独立于 XAML；子视图可见性改从 Window 主 DataContext 读取，修复今天页与日历页叠加。
  - Verify：`eng\verify.cmd` 退出 0；测试 60/60；build 0 警告、0 错误；PrintWindow 月历截图无重叠。
- [x] P3-03 项目概览、看板、里程碑和甘特图
  - Red：待办规划/依赖测试和 ProjectWorkspaceViewModel 测试分别因能力不存在而失败。
  - Green：实现规划区间、依赖约束与迁移，以及项目完成率、五列看板、里程碑和按日偏移的甘特条。
  - Refactor：项目页只使用领域对象生成读模型；看板与甘特布局数据不写进 XAML 计算。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 63/63 通过。
- [x] P3-04 待办四象限拖放、看板和列表
  - Red：TodoWorkspaceViewModel 测试因共享待办工作区不存在而失败。
  - Green：实现四象限、五列看板、列表、模式切换，以及象限/状态 WPF 拖放。
  - Refactor：三个视图由单一 allItems 刷新；完成列统一通过 TimeProvider 记录完成时间。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 65/65 通过。
- [x] P3-05 富文本记录、Markdown 快捷输入和全文搜索
  - Red：RecordWorkspaceViewModel 与 Markdown 格式测试因实现不存在而失败。
  - Green：实现记录搜索、Markdown 编辑工具栏/Ctrl 快捷键、选择记录和变化时追加版本。
  - Refactor：Markdown 是唯一正文格式，不维护第二份富文本载荷；搜索始终使用当前版本。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 68/68 通过。
- [x] P3-06 全局搜索、筛选和快速新增
  - Red：GlobalSearchViewModel 与 CalendarDataService 测试因实现不存在而失败。
  - Green：实现跨四类对象搜索/筛选、结果导航、本地数据库初始化/快照加载和四类快速新增。
  - Refactor：数据服务关闭 SQLite 池化以兼容备份恢复；新增后统一重载所有工作区。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 70/70 通过；UI Automation 打开并关闭快速新增窗口。
- [x] P3-07 统计视图和周报图表
  - Red：StatisticsViewModel 测试因统计实现不存在而失败。
  - Green：实现项目/待办/完成/逾期指标、近 7 天趋势、周报摘要和统计页面。
  - Refactor：专注时长不使用假数据，预留给 P7-01 完成后接入。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 71/71 通过。

## P4 - 桌面与快速面板

- [x] P4-01 系统托盘和右下角快速面板
  - Red：QuickPanelPositioner 测试因定位实现不存在而失败。
  - Green：实现托盘菜单/点击行为、右下角快速面板、时钟、迷你月历和显式退出。
  - Refactor：移除 WinForms 隐式 using 冲突；主窗口关闭只隐藏，托盘退出才 Shutdown。
  - Verify：`eng\verify.cmd` 退出 0；测试 72/72；UI Automation 关闭主窗后进程存活且主句柄为 0。
- [x] P4-02 组合式桌面工作台
  - Red：DesktopWorkbenchViewModel 测试因组合过滤实现不存在而失败。
  - Green：实现独立组合窗口、月历日期选择、所选日程/待办过滤和托盘显隐。
  - Refactor：工作台复用 CalendarViewModel 和同一 SQLite 快照，不复制日期算法。
  - Verify：`eng\verify.cmd` 退出 0；测试 73/73；PrintWindow 1000x520 截图无重叠。
- [x] P4-03 日历、日程和待办独立桌面组件
  - Red：DesktopComponentDefinition 测试因组件类型/尺寸定义不存在而失败。
  - Green：实现统一独立组件窗口、三种内容、独立尺寸/初始位置和托盘子菜单。
  - Refactor：三个组件复用 DesktopWorkbenchViewModel 与同一窗口模板。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 76/76 通过。
- [x] P4-04 本周/本月切换及独立尺寸记忆
  - Red：DesktopCalendarLayoutState 测试因布局设置、模式枚举和状态对象不存在而编译失败。
  - Green：实现两套尺寸状态、周/月桌面控件、动态行数，以及启动加载/退出保存；目标测试 1/1 通过。
  - Refactor：保留单一纯状态对象供两个窗口复用；配置由 App 统一加载/保存，避免窗口并发写设置文件。
  - Verify：`eng\verify.cmd` 退出 0；测试 77/77；UI Automation 验证组合窗口 1000x520 -> 1000x360 -> 1000x520。
- [x] P4-05 锁定、穿透、置底、置顶和贴边隐藏
  - Red：DesktopWindowBehaviorState 测试因行为状态、层级和贴边几何类型不存在而编译失败。
  - Green：实现四窗口独立行为状态、锁定/穿透/三层级/四边隐藏及托盘全局穿透恢复；目标测试 5/5 通过。
  - Refactor：统一由 DesktopWindowBehaviorController 施加窗口行为；独立组件只在首次显示时定位，临时隐藏后不再重置位置。
  - Verify：`eng\verify.cmd` 退出 0；测试 83/83；运行态验证锁定、穿透恢复、置顶、Progman 上层置底和四像素贴边露出。
- [x] P4-06 主题、透明度、字体、颜色和材质设置
  - Red：DesktopAppearanceSettingsState 测试因外观模型、目标隔离状态、材质和颜色角色不存在而编译失败。
  - Green（状态层）：实现四目标独立外观配置、数值边界、材质、颜色校验和浅深默认色板；目标测试 2/2 通过。
  - Green：实现完整设置页、四窗口即时预览、动态主题画刷、字体/密度和原生云母/亚克力材质；ViewModel 回调测试 1/1 通过。
  - Refactor：外观状态、窗口应用和 DWM 材质各自单一职责；统一动态资源并修复深色列表白底与无效缩放控件。
  - Verify：`eng\verify.cmd` 退出 0；测试 86/86；设置首屏/颜色区/深色工作台截图通过；DWM 属性亚克力=3、云母=2、纯色=1。
- [x] P4-07 可配置全局快捷键与冲突检测
  - Red：GlobalShortcutSettingsState 测试因结构化组合键、动作和冲突结果类型不存在而编译失败。
  - Green（状态层）：实现五动作结构化组合键、修饰键校验和应用内重复检测；目标测试 2/2 通过。
  - Green：实现 Win32 消息窗口注册、系统冲突回滚、五个运行时动作和快捷键设置页；回滚/格式测试 2/2 通过。
  - Refactor：快捷键状态与 Win32 注册职责分离；单一消息窗口在主窗口隐藏后继续接收，并统一随 App 释放。
  - Verify：`eng\verify.cmd` 退出 0；测试 90/90；隐藏主窗热键、五行系统冲突、重绑定触发和设置页截图通过。

## P5 - 提醒、日历数据与天气

- [x] P5-01 后台提醒调度和错过提醒补发
  - Red：Reminder 测试因提醒领域、待投递记录和错过判定不存在而编译失败。
  - Green（领域层）：实现提醒到期入队、错过宽限判定和重复入队保护；目标测试 2/2 通过。
  - Red（持久化层）：ReminderDispatchService 测试因 SQLite 调度服务和提醒 DbSet 不存在而编译失败。
  - Green：实现 UTC ticks 索引、事务投递、唯一去重、待投递持久化和应用生命周期后台扫描；持久化/调度测试 2/2 通过。
  - Refactor：P5-01 只负责生成持久化投递记录，通知样式、声音、稍后提醒和勿扰留给 P5-02 消费，不混入调度器。
  - Verify：`eng\verify.cmd` 退出 0；测试 95/95；真实 SQLite 跨触发点自动入队、重复扫描和应用迁移启动通过。
- [x] P5-02 系统通知、弹窗、声音、稍后提醒和勿扰
  - Red：DoNotDisturbPolicy 测试因通知设置和跨午夜/全屏抑制策略不存在而编译失败。
  - Green（策略层）：实现三通道/默认延后设置及跨午夜、全屏勿扰策略；目标测试 5/5 通过。
  - Red（交互状态）：稍后/确认集成测试因就绪查询、延后和确认持久化方法不存在而编译失败。
  - Green（交互状态）：实现投递延后、到点再现与确认移出；SQLite 集成测试 1/1 通过。
  - Red（通知协调）：ReminderNotificationCoordinator 测试因投递存储、通道和全屏检测边界不存在而编译失败。
  - Green（通知协调）：实现三通道开关、勿扰抑制和会话去重；协调器测试 1/1 通过。
  - Green：实现 Windows 托盘通知、右下角弹窗、系统提示音、5/10/30/60 分钟延后、确认、全屏检测和提醒设置页。
  - Refactor：投递存储、协调器和三个通道分层；投递消失时操作幂等，弹窗失败就地显示且不再导致驻留进程退出。
  - Verify：`eng\verify.cmd` 退出 0；测试 104/104；隔离库弹窗截图、延后写入、完成确认、进程存活和提醒设置页通过。
- [x] P5-03 中国农历、节气、节假日和调休数据
  - Red：LunarChineseCalendarService 测试因内部日历日模型和第三方适配服务不存在而编译失败。
  - Green（数据层）：接入 MIT lunar-csharp 1.6.8，映射农历、节气、节日、法定放假/调班、关联日期和 ISO 周数；目标测试 3/3 通过。
  - Green：农历短文本、节气/节日、“休/班”和周数已进入主月/周历、今天页、快速面板及两类桌面日历。
  - Refactor：第三方类型只存在于 Infrastructure 适配器；UI 依赖内部 IChineseCalendarService，并保持 7 列固定布局。
  - Verify：`eng\verify.cmd` 退出 0；测试 109/109；春节/立秋/国庆/调班数据及主月历、桌面、快速面板截图通过。
- [x] P5-04 ICS 导入导出及往返一致性
  - Red：`ExportThenImportPreservesSupportedCalendarSemantics` 与 `ImportAcceptsStandardTimedAndAllDayEvents` 已添加；基础设施测试项目因 `IcsCalendarFileService`、`IcsImportResult` 不存在而以 CS0246 失败。
  - Green（文件交换）：使用 Ical.Net 实现真实 SQLite 的 ICS 导入导出；标准字段覆盖定时、全天和重复，应用扩展字段保留原始时区与跳过节假日语义；目标测试 2/2、基础设施测试 24/24 通过。
  - Green：日历页提供可访问的导入/导出图标按钮、文件选择器、导入后数据重载及成功/失败反馈。
  - Refactor：数据库文件交换和 WPF 文件对话框保持分层；复用现有领域工厂、EF 映射和 Ical.Net，不新增平行日历模型。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 111/111；980x640 日历页截图无截断或重叠，UI Automation 可识别 ICS 操作。
- [~] P5-05 ICS URL 只读订阅和定时刷新
- [x] P5-06 自动定位、手动城市和天气预报
  - Red：`SearchAndForecastMapOpenMeteoResponses`、`RefreshFallsBackToSavedCityWhenAutomaticLocationFails`、`SelectingSearchResultUsesAndPersistsManualCity` 已添加；因天气适配器、领域模型、定位接口和 ViewModel 不存在而编译失败。
  - Green（服务与状态）：实现 Open-Meteo 城市搜索/当前/逐小时/7 日映射，以及自动定位失败回退、手动城市选择与设置回写；目标测试 3/3 通过。
  - Green：接入 Windows 定位、共享天气状态、今天页城市切换/12 小时/7 日预报和快速面板当前天气；持久化手动城市可在重启后恢复。
  - Refactor：定位、HTTP 映射和 WPF 状态分层；修复配置存在时 UI 线程同步等待异步读取导致的冷启动死锁，并让配置存储不捕获 UI 上下文。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 114/114；真实 Open-Meteo 上海预报加载成功，980x640 主窗口与 420x640 快速面板截图无截断或重叠。

## P6 - AI 助手

- [x] P6-01 模型提供商配置、Windows 凭据和连接测试
  - Red：`TestsOpenAiAndOllamaUsingTheirNativeDiscoveryEndpoints` 与 `SaveStoresApiKeyOnlyInSecretStoreAndConnectionTestReadsItBack` 已添加；因 AI 合约、连接测试器和设置 ViewModel 不存在而编译失败。
  - Green：实现 OpenAI 兼容/Ollama 配置、原生模型发现连接测试、Windows Credential Manager 密钥存储和掩码设置页；目标测试 2/2 通过，配置 JSON 仅保存端点与模型。
  - Refactor：提供商配置、HTTP 探测、密钥存储和 WPF 状态分层；既有 `ISecretStore` 作为唯一密钥边界，不把密钥加入设置模型。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 116/116；Windows 凭据唯一临时项写/读/删通过；980x640 AI 设置页截图无截断或重叠。
- [x] P6-02 受控只读查询工具和最小上下文构建
  - Red：`CatalogContainsOnlyReadToolsAndContextDoesNotEmbedApplicationData` 与 `QueryTodosReturnsOnlyMatchingProjectionWithoutUnrelatedRecordContent` 已添加；因只读目录、最小上下文和 SQLite 工具执行器不存在而编译失败。
  - Green：实现四个固定只读工具 schema、无应用快照的最小请求上下文，以及 SQLite `AsNoTracking` 查询投影、1-50 条上限和记录正文显式开关；目标测试 2/2 通过。
  - Refactor：工具目录、上下文和执行器分层；显式 switch 白名单拒绝未知工具，结果枚举使用稳定语义名，搜索统一数据库端不区分 ASCII 大小写。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 118/118；真实 SQLite 查询后实体数量不变且无关记录正文未进入结果。
- [x] P6-03 新建日程/待办/记录的结构化预览与确认
  - Red：`PreviewDoesNotWriteAndConfirmCreatesEachSupportedKindWithAudit` 已添加；因三种结构化草案、预览类型和确认服务不存在而编译失败。
  - Red（交互状态）：`PresentAndConfirmDraftUsesPendingPreviewExactlyOnce` 已添加；因 `AssistantViewModel` 不存在而以 CS0246 失败。
  - Green：实现定时/全天日程、待办和记录草案，内存预览队列，单次确认后的实体+审计原子写入，以及助理页确认/取消状态；目标测试 2/2 通过。
  - Refactor：模型草案、确认接口、SQLite 实现和 WPF 状态分层；确认失败恢复待处理草案，重复确认被拒绝，确认成功后统一重载应用数据。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 120/120；预览阶段数据库 0 写入，三类确认后各 1 实体且审计 3/3。
- [x] P6-04 修改、批量操作、删除确认和撤销
  - Red：`BatchPreviewConfirmAndUndoRestoresModifiedAndRecycledEntities` 已添加；因批量变更草案、预览、实体类型和确认服务不存在而编译失败。
  - Red（交互状态）：`ChangeSetRequiresConfirmationAndCanBeUndone` 已添加；因助理 ViewModel 缺少批量预览、确认和撤销入口而以 CS1739/CS1061 失败。
  - Green：实现待办状态修改、四类根实体批量软删除、变更预览、单次确认、逐项审计和整批 LIFO 撤销；助理页增加批量确认/取消与撤销入口；目标测试 2/2 通过。
  - Refactor：复用领域状态/回收站方法和 `UndoManager`；确认失败恢复草案，撤销失败保留栈顶，确认与撤销分别记录可撤销/不可撤销审计。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 122/122；三项预览零写入，确认后 3 条审计，撤销恢复全部实体并累计 6 条审计。
- [x] P6-05 冲突询问、空闲时间搜索和任务拆解
  - Red：`DetectsConflictsFindsWorkingHourSlotsAndConfirmsTaskDecomposition` 已添加；因规划服务、冲突询问、空闲槽和拆解预览类型不存在而编译失败。
  - Red（交互状态）：`ConflictingEventWaitsForChoiceBeforeCreationPreview` 已添加；因缺少规划接口和助理冲突状态入口而以 CS0246 失败。
  - Green：复用冲突检测和默认工作时段实现日程/时间块冲突询问、指定时区空闲槽、全天占用与任务拆解预览/确认/审计；助理页提供三选项和空闲槽选择；目标测试 2/2 通过。
  - Refactor：UTC 冲突与本地工作时段转换保持分层；新子任务显式标记 EF Added，规划失败恢复待确认拆解。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 124/124；09:30 候选命中 1 冲突，首个 1 小时空闲为 08:00-09:00且无午休槽，拆解确认追加 2 子任务与 1 审计。
- [x] P6-06 项目总结、每日计划和周报草稿
  - Red：`BuildsScopedProjectDailyAndWeeklyMarkdownDrafts` 已添加；因 `AiDraftService` 与 `AiTextDraft` 不存在而编译失败。
  - Red（交互状态）：`DailyAndWeeklyDraftCommandsAppendMarkdownMessages` 已添加；因缺少草稿接口和助理命令入口而以 CS0246 失败。
  - Green：实现只读项目总结、指定日期每日计划和指定周周报 Markdown 草稿，助理页提供每日计划/周报快捷命令及项目总结入口；目标测试 2/2 通过。
  - Refactor：所有时区过滤在读取后用明确时区完成；草稿只列事实标题和状态，不虚构进度或自动携带记录正文。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 126/126；目标项目草稿不含无关项目，日/周范围与助理 Markdown 消息通过。
- [x] P6-07 Ollama 本地模型与仅本地隐私模式
  - Red：`LocalOnlyModeAllowsLoopbackOllamaAndRejectsRemoteEndpoints` 与 `OllamaToolLoopUsesLocalEndpointAndReturnsOnlyToolProjection` 已添加；因仅本地设置、隐私策略和提供商助理客户端不存在而编译失败。
  - Red（交互状态）：`LocalOnlyModeSwitchesToLoopbackOllamaWithoutApiKey` 与 `SendAppendsUserAndAssistantMessagesAndClearsInput` 已添加；因设置/助理 ViewModel 缺少本地开关和发送入口而失败。
  - Green：实现仅本地模式强制 loopback Ollama、Ollama/OpenAI 兼容对话、最多 4 轮只读工具调用，以及助理输入/消息和设置开关；目标测试 4/4 通过。
  - Refactor：两类提供商共享工具 schema、消息循环和 SQLite 白名单执行器；隐私策略在凭据读取/HTTP 前执行，仅在线请求携带 Bearer key。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 130/130；真实 SQLite Ollama 假服务两轮请求只回送匹配投影；980x640 仅本地设置页隐藏密钥行，助理页无重叠。

## P7 - 工具中心

- [x] P7-01 倒计时、番茄钟及专注统计
  - Red：`CountdownPausesWithoutLosingTimeAndCompletesOnce`、`PomodoroCompletesFocusAndSwitchesToBreak` 与 `FocusSessionRoundTrips` 已添加；因计时工具、专注会话和 DbSet 不存在而失败。
  - Red（交互状态）：`CompletedPomodoroPersistsAndUpdatesTodayStatistics` 已添加；因专注存储接口和工具中心 ViewModel 不存在而以 CS0246 失败。
  - Green：实现可暂停/重置倒计时、25/5 番茄阶段、完成专注会话、SQLite 持久化与迁移、工具页今日统计和统计页专注分钟；目标测试 5/5 通过。
  - Refactor：计时状态机只依赖传入时间，Dispatcher 仅每秒 Tick；专注存储接口隔离 SQLite，工具页与统计页共享同一会话数据。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 134/134；AddFocusSessions 迁移升级现有隔离库成功，980x640 工具页截图无重叠。
- [x] P7-02 时间进度、日期计算和世界时钟
  - Red：`ProgressUsesLocalCalendarBoundaries` 与 `DateCalculationAndWorldClockAreDeterministic` 已添加；因时间进度、日期计算和世界时钟类型不存在而失败。
  - Green：实现日/周/月/年本地边界进度、DateOnly 日期差/加减、系统时区世界时钟，并接入工具页日期输入及上海/伦敦/纽约时钟；目标测试 2/2 通过。
  - Refactor：三类计算保持 Core 纯函数，工具 ViewModel 每秒只刷新读模型，不引入额外计时器或持久化。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 136/136；980x640 工具页下半区三栏无截断或重叠。
- [x] P7-03 整点报时和久坐提醒
  - Red：`ChimeAndSedentaryRemindersFireOnceAndRespectActivityReset` 已添加；因健康提醒设置、调度器和提醒类型不存在而失败。
  - Green：实现活动时段内整点一次性触发、15-240 分钟久坐周期、起身重置、后台 Dispatcher Host、托盘通知/系统音和工具页持久化设置；目标测试 1/1 通过。
  - Refactor：纯调度器与 Windows 通知 Host 分层，Host 不依赖工具页可见；提醒设置复用原子 JSON 存储。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 137/137；工具页最小窗口改为纵向滚动，提醒控件可完整访问。
- [x] P7-04 区域、窗口和全屏截图及标注
  - Red：`DragPointsNormalizeToPositivePixelRegion` 已添加；因 `CaptureRegion` 不存在而编译失败。
  - Green：实现主屏、可见窗口矩形和拖拽区域捕获，统一进入 InkCanvas 标注窗口，支持画笔、橡皮、清除、复制和 PNG 保存；目标测试 1/1 通过。
  - Refactor：像素捕获、区域选择和标注合成分层；窗口捕获改用屏幕矩形，修复 UI 线程对自身 `PrintWindow` 的同步死锁。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 138/138；运行态窗口捕获返回冻结 1180x900 BitmapSource。
- [x] P7-05 长截图、OCR、马赛克和截图贴屏
  - Red：`LayoutStitchesPagesUsingConfiguredOverlap` 已添加；因 `LongCaptureLayout` 不存在而编译失败。
  - Green：实现多帧窗口滚动捕获与固定重叠拼接、标注画布拖拽马赛克、Windows.Media.Ocr 识别/复制、无边框置顶截图贴屏；目标测试 1/1 通过。
  - Refactor：长图布局保持 Core 纯计算，拼接/像素化/OCR/贴屏分为独立适配器；OCR 不引入第三方网络服务。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 139/139；Windows OCR 对现有工具页 PNG 返回 241 字符。
- [x] P7-06 任意窗口置顶和图片贴屏
  - Red：`RefreshFiltersOwnAndUntitledWindowsAndToggleCallsBackend` 已添加；因窗口目标、原生后端接口和控制器不存在而失败。
  - Green：实现可替换的顶层窗口枚举/置顶后端、排除自身/无标题窗口的控制器、工具页窗口选择与置顶切换，以及从本地图片或截图创建无边框置顶贴屏窗；目标测试 1/1 通过。
  - Refactor：窗口枚举与 UI 状态分层，Win32 文本缓冲使用 char[] 并检查返回值；截图/图片贴屏复用同一窗口。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 140/140；假后端确认目标句柄按 true/false 顺序置顶与取消。
- [x] P7-07 文本、图片和文件剪贴板历史、搜索与收藏
  - Red：`TextImageAndFilesPersistSearchAndFavorite` 已添加；因剪贴板服务、实体、载荷和类型不存在而编译失败。
  - Green（持久化）：实现三类剪贴板元数据、大小写不敏感搜索、收藏优先排序和受管图片文件；目标测试 1/1 通过。
  - Red（工具中心）：`ClipboardSearchAndFavoriteRefreshTheVisibleHistory` 已添加；因服务边界和 ViewModel 剪贴板 API 不存在而以 CS0246 失败。
  - Green（工具中心）：实现可替换的存储边界、历史加载/搜索、选中状态和收藏刷新；目标测试 2/2 通过。
  - Red（自动采集）：`TextImageAndFilesAreRoutedToHistory` 已添加；因剪贴板读取边界、内容快照和协调器不存在而以 CS0246 失败。
  - Green（自动采集）：实现三类系统剪贴板快照路由、PNG 编码和 `WM_CLIPBOARDUPDATE` 驻留监听；目标测试 1/1 通过。
  - Green（界面）：工具中心实现固定高度历史列表、搜索、收藏和按文本/图片/文件类型写回系统剪贴板。
  - Refactor：领域服务边界、SQLite/受管文件存储、Windows 快照读取与 WPF 状态各自保持单一职责；复用仓库 UTC ticks 转换器。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 143/143；隔离实例通过 `WM_CLIPBOARDUPDATE` 捕获探针文本并显示，1180x760 截图无重叠或截断。
- [x] P7-08 日历日程录入与桌面驻留闭环
  - 验收：双击日期可指定日期与开始/结束时间；日历格显示最多三项及溢出数；新增后主窗口和桌面组件即时刷新；设置可让应用启动时只显示桌面组件与托盘。
  - Red：日历三项/溢出、指定本地日期时间和隐藏启动设置测试已添加；因目标 API 不存在而以 CS0117/CS0246/CS1061 失败。
  - Green（状态层）：实现日历按天事件投影/三项上限、本地日期时间请求和隐藏启动配置回调；目标测试 11/11 通过。
  - Green（交互层）：快速新增与日期双击共用日期/时间编辑窗口；主日历和三类桌面组件显示事件并由统一回调即时刷新；隐藏启动设置已接入应用生命周期。
  - Refactor：日期事件投影集中在 `CalendarViewModel`，所有窗口刷新统一回到 SQLite 快照；日程编辑只负责生成现有 `QuickAddRequest`，未新增重复持久化路径。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 147/147；隔离实例创建 09:00 日程后标题在主日历、桌面日期格和日程列表共出现 3 次；隐藏启动为主窗口 0、桌面窗口 1、进程存活；1180x760 截图无重叠。
- [x] P7-09 独立桌面组件、右键显隐与自由尺寸
  - 验收：桌面只保留日历窗口，日程和待办作为可选独立窗口；任一组件右键可勾选三者显隐；三个窗口可独立移动、调整宽高并跨重启记忆；背景透明度范围为 0–100%。
  - Red：0% 透明度、日历位置和日程/待办独立布局往返测试已更新/添加；因布局类型与位置 API 不存在而以 CS0117/CS0246/CS1061 失败。
  - Green（状态层）：实现 0–100% 透明度、日历位置和日程/待办独立宽高位置配置；目标测试 6/6 通过。
  - Red（显隐持久化）：设置往返测试加入三个组件显隐值；因 AppSettings 字段不存在而以 CS0117 失败。
  - Green（交互层）：组合工作台收敛为纯日历；日程/待办使用独立窗口和布局；组件右键及托盘共用显隐状态并跨重启保存；透明度滑块扩展为 0–100%。
  - Refactor：删除运行时重复的第四个日历窗口；三组件显隐统一经 `DesktopComponentVisibilityActions`，日程/待办复用 `DesktopPanelLayoutState`，旧配置字段保留以兼容已有设置文件。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 148/148；隔离实例仅有 3 个组件窗，尺寸 760x430/360x260/360x340 与位置均匹配配置；纯日历无日程/待办区；右键三项齐全；待办 0% 背景透明截图通过。
- [x] P7-10 日历顶部居中收起与显式宽高调整
  - 验收：日历默认固定屏幕顶部中央；右键可切换顶部自动隐藏，鼠标进入顶部中央感应条时展开；三个组件有可见缩放手柄和“设置宽高”数字入口。
  - Red：顶部居中位置/开关和宽高解析边界测试已添加；因目标布局与尺寸状态不存在而以 CS0246/CS1061 失败。
  - Green（状态层）：实现顶部中央锚定开关/位置计算和 220x160–1920x1200 数字宽高解析；目标测试 6/6 通过。
  - Green（交互层）：日历右键加入顶部固定/自动隐藏/设置宽高；三组件加入可见 ResizeGrip 和数字尺寸窗口；日历改宽后自动重新居中。
  - Refactor：复用既有 `DesktopWindowBehaviorController` 顶边隐藏能力；菜单统一在窗口预览阶段处理右键，避免日期按钮吞掉菜单；尺寸解析与窗口 UI 分离。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 153/153；760x430 日历顶部居中为 Left=580/Top=0，自动隐藏为 Top=-426，仅留 4px，鼠标移入后恢复 Top=0；数字尺寸窗口含宽高两项，截图可见右下角缩放标记。
- [x] P7-11 分钟级日程时间输入
  - 验收：日程开始和结束时间可选择任意分钟，包括 14:05、14:10；保存后保持所选本地分钟值。
  - Red：`TimeOptionsContainEveryMinuteOfTheDay` 已添加；因 `ScheduleTimeOption.CreateEveryMinute` 不存在而以 CS0117 失败。
  - Green：时间选项改为全天 1440 个一分钟粒度值，开始/结束下拉框支持键盘文本定位；状态层按所选 14:05–14:10 原值生成请求。
  - Refactor：分钟标签生成集中在现有 `ScheduleTimeOption`，未增加新的日程持久化路径。
  - Verify：`ScheduleEditorStateTests` 3/3 通过，包含分钟选项覆盖和 14:05–14:10 时区映射；最终 `eng\verify.cmd` 全量 159/159 通过。
- [x] P7-12 截图查看与标注缩放
  - 验收：截图标注和贴屏窗口均可缩放查看，缩放不改变导出原图像素尺寸。
  - Red：`ScreenshotZoomStateTests` 已添加；因缩放状态类型不存在而以 CS0246 失败。
  - Green：实现 25%–400%、25% 步进缩放状态；标注窗加入滑块及加减按钮，贴屏窗加入加减按钮和可滚动放大视图。
  - Refactor：两个窗口复用单一纯缩放状态；缩放施加在画布父容器，复制、保存、OCR 和贴屏仍从原尺寸标注画布渲染。
  - Verify：`ScreenshotZoomStateTests` 2/2 通过；Desktop 工程 WPF XAML 编译通过；最终 `eng\verify.cmd` 全量 159/159 通过。
- [x] P7-13 Anthropic Messages 与 OpenAI Responses API
  - 验收：AI 设置可选原有 OpenAI 兼容、OpenAI Responses、Anthropic 和 Ollama；各协议使用正确的端点、鉴权、请求与响应映射，并保持旧配置兼容。
  - Red：Responses/Anthropic 原生工具循环及四提供商连接测试已添加；因 `OpenAiResponses`、`Anthropic` 提供商不存在而以 CS0117 失败。
  - Red（设置可用性）：`ProviderSelectionAppliesUsableProtocolDefaults` 已添加；Anthropic 仍保留 OpenAI 端点，测试按预期失败。
  - Green：新增 OpenAI Responses 和 Anthropic 提供商；实现 Responses `output`/`function_call_output` 循环、Anthropic `tool_use`/`tool_result` 循环、各自鉴权与模型发现，并在设置页提供四协议选择。
  - Refactor：原有 Chat Completions/Ollama 路径保持独立；协议只共享受控只读工具执行与 HTTP 发送，Anthropic 密钥使用单独的 Windows 凭据标识。
  - Verify：`eng\verify.cmd` 退出 0；build 0 警告、0 错误；测试 159/159；新协议工具循环 2/2、四提供商连接测试 1/1、协议默认值测试 1/1 通过。
- [x] P7-14 全量验证与安装包重建
  - 验收：全部测试通过，自包含 win-x64 发布与隐藏启动冒烟通过，生成可分发安装包并记录 SHA-256。
  - Red：不适用；此条验证前三项功能并重建交付物。
  - Green：无产品代码；运行统一发布流水线生成最终自包含程序和 Inno Setup 安装包。
  - Refactor：无。
  - Verify：`eng\publish.cmd` 退出 0；测试 159/159；隐藏启动 2 秒未退出；安装包 SHA-256 为 `BF56BF626266D168E6296D908BA20A0ED4FCD263FA63F1864317EE5ADE94B02E`。
- [x] P7-15 桌面组件右键菜单崩溃修复
  - 验收：日历、日程、待办任一桌面组件右键可稳定打开菜单并切换三组件显隐，菜单分隔线不参与勾选状态刷新。
  - Red：`RefreshVisibilityChecksSkipsSeparators` 已添加；当前没有可跳过 `Separator` 的刷新入口，并结合系统事件日志确认原实现于菜单打开时抛 `InvalidCastException`。
  - Green：菜单勾选刷新改为只枚举 `MenuItem`，明确跳过 `Separator`，保留日历/日程/待办现有切换动作。
  - Refactor：将状态刷新提取为可直接验证的单一方法，不增加全局异常吞噬。
  - Verify：`DesktopComponentContextMenuTests` 1/1 通过；STA 测试同时覆盖分隔线与两种勾选状态；隔离实例右键打开菜单并切换“显示日程”后进程存活。
- [x] P7-16 日历日程强调显示与完整详情
  - 验收：月格日程以时间和标题的明确条目显示；点击条目或溢出提示可查看当天全部日程及完整时间；双击日期继续新增日程。
  - Red：`EventDetailsIncludeEveryEventWithFullLocalTimeRange` 已添加；因完整日程详情投影和查询入口不存在而以 CS0246/CS1061 失败。
  - Green：月格日程改为带强调色边、开始时间和加粗标题的可点击条目；“还有 N 项”同样可点击；新增当天完整详情窗，显示全部日程、全天/起止时间和新增入口。
  - Refactor：完整日期过滤与排序复用 `CalendarViewModel` 单一路径；主日历、桌面日历和备用日历组件共享同一详情投影，未增加数据库读取路径。
  - Verify：`CalendarViewModelTests` 6/6 通过；完整详情覆盖 4 项且正确映射 `全天`、`09:05–10:10`；隔离实例月格显示时间/标题条，点击后详情窗显示 `10:35–11:35` 完整时间。
- [x] P7-17 全量验证与安装包重建
  - 验收：自动化测试、桌面右键与日程详情运行验收、隐藏启动冒烟均通过，并生成新安装包与 SHA-256。
  - Red（运行验收）：隔离实例点击快速新增因当前时间包含秒、无法匹配整分钟选项而抛 `InvalidOperationException`；增加 `DefaultStartDropsSecondsToMatchMinuteOptions` 防回归测试。
  - Green：默认开始时间统一截断到可选整分钟，隔离实例成功新建日程；运行统一发布流水线重建最终安装包。
  - Refactor：整分钟归一化集中在 `ScheduleTimeOption`，不在窗口构造函数重复时间边界逻辑。
  - Verify：`eng\publish.cmd` 退出 0；build 0 警告、0 错误；测试 162/162；快速新增、右键菜单、组件切换、详情窗和隐藏启动冒烟通过；安装包 SHA-256 为 `2D5637CA0547A5D2F2A3470F6F4F95384FE66B1DBAD50E9146B9B1CFEC3B7354`。
- [x] P7-18 半小时下拉与任意分钟手输
  - 验收：开始/结束时间下拉只显示 00:00、00:30 等 48 个节点，同时接受手动输入 `14:05` 等任意合法 `HH:mm`。
  - Red：半小时选项、默认时间上取整和任意分钟文本解析测试已添加；因目标 API 不存在而以 CS0117 失败。
  - Green：时间下拉恢复为 48 个半小时节点并改为可编辑；创建时从文本严格解析 `HH:mm`，手输 `14:05` 不依赖下拉选中项。
  - Refactor：半小时生成、默认上取整和文本解析集中在 `ScheduleTimeOption`，窗口只组装请求。
  - Verify：`ScheduleEditorStateTests` 5/5 通过，覆盖半小时选项、14:05 手输、无效格式和本地时间映射。
- [x] P7-19 AI 服务端点粘贴入口
  - 验收：AI 设置的服务端点可通过 Ctrl+V、文本框右键和可见粘贴按钮填入剪贴板中的 base URL。
  - Red：用户运行态反馈从官方文档复制的 base URL 无明确可用粘贴路径；原设置行仅有裸文本框，无可见命令。
  - Green：服务端点文本框增加显式剪切/复制/粘贴/全选菜单及剪贴板图标按钮；按钮会用剪贴板中的去空白文本替换旧端点，避免拼接到原值前面。
  - Refactor：文本框菜单继续使用 WPF 标准 `ApplicationCommands`；按钮处理只负责一次替换，不缓存剪贴板内容。
  - Verify：运行态点击粘贴按钮后端点值精确为 `https://paste-acceptance.test/v1`，原剪贴板内容在验收后恢复；进程保持运行。
- [x] P7-20 当前位置名称与同名城市排序
  - 验收：自动定位显示城市、地区、国家，无法获得地址时至少显示坐标；城市搜索优先精确同名、城市级和高人口结果，佛山·广东优先于其他同名地点。
  - Red：`CurrentLocationDisplaysCityRegionAndCountry` 与 `SearchRanksExactCityLevelHighPopulationResultFirst` 已添加；当前仅显示名称且完全保留服务原始顺序，分别按预期失败。
  - Red（在线验收）：真实 Open-Meteo 中文查询“佛山”不返回广东佛山市；测试改为真实两段响应，要求在缺少城市级结果时补查“佛山市”并合并排序。
  - Red（定位验收）：Windows 在当前机器仅返回 `23.0250, 113.1460 · CN`，未提供 CivicAddress；新增坐标回退反向解析测试，原实现因把反向响应当作天气响应而失败。
  - Green：Windows 自动定位读取城市/省/国家并以经纬度兜底；坐标兜底通过免密反向地理编码补全城市/省/国家；天气标题使用完整位置名；Open-Meteo 候选扩至 100，在缺少城市级精确结果时补查“市”后缀，再按精确名称、城市级别和人口排序并取前 10。
  - Refactor：同名地点排序保留在基础设施响应映射层，ViewModel 只展示统一 `WeatherLocation.DisplayName`。
  - Verify：Open-Meteo 测试 3/3、Weather ViewModel 测试 3/3 通过；真实查询“佛山”首项为 `佛山市 · 广东 · 中国`，真实坐标反向解析为 `佛山市 · 广东省 · 中国`。
- [x] P7-21 全量验证与安装包重建
  - 验收：自动化、输入/粘贴/定位搜索运行验收和隐藏启动冒烟通过，生成新安装包及 SHA-256。
  - Red：不适用；此条负责对 P7-18 至 P7-20 做最终集成验收与交付物重建。
  - Green：运行统一发布流水线生成自包含 win-x64 程序和 Inno Setup 安装包。
  - Refactor：无。
  - Verify：`eng\publish.cmd` 退出 0；build 0 警告、0 错误；测试 166/166；运行态成功手输 `14:05`–`14:10` 新建日程，端点粘贴精确替换旧值，自动位置显示 `佛山市 · 广东省 · 中国`，“佛山”搜索首项为 `佛山市 · 广东 · 中国`；隐藏启动冒烟通过；安装包 SHA-256 为 `02D8AE29D69FB56FCB9415B3D5C135956395D9D6CA2D026234D5C5E0518AF859`。
- [x] P7-22 城市搜索结果清理与键盘交互
  - 验收：城市搜索不展示同名村镇；中国城市统一显示城市/省级行政区全称；搜索框按 Enter 可搜索且有结果时自动展开候选。
  - Red：`SearchKeepsCityLevelResultsAndNormalizesChineseAdministrativeNames` 失败，现有结果包含广东城市、云南四级行政区驻地和重庆普通聚居点共 3 项，且广东缺少“省”；`SelectingSearchResultUsesAndPersistsManualCity` 因 `IsSearchResultsOpen` 不存在而编译失败。
  - Green：Open-Meteo 结果只保留城市级行政地点；中国城市、省、自治区、特别行政区和直辖市补全法定后缀；搜索成功后 ViewModel 打开候选，选择后关闭；搜索输入框 Enter 绑定现有搜索命令。
  - Refactor：行政区规范化保留在天气基础设施映射层，UI 只绑定统一的 `WeatherLocation.DisplayName`；未建立额外城市字典或第二套搜索路径。
  - Verify：城市服务 3/3、Weather ViewModel 3/3 通过；隔离实例原生 Enter 搜索后下拉自动展开，只显示 `佛山市 · 广东省 · 中国`，完整文字未截断且进程保持运行。
- [x] P7-23 全量验证与安装包重建
  - 验收：完整测试、隐藏启动冒烟和安装器编译通过，生成新安装包及 SHA-256。
  - Red：不适用；此条负责 P7-22 的最终集成验证和交付物重建。
  - Green：运行统一发布流水线生成自包含 win-x64 程序和 Inno Setup 安装包。
  - Refactor：无。
  - Verify：`eng\publish.cmd` 退出 0；build 0 警告、0 错误；测试 166/166；隐藏启动冒烟和安装器编译通过；安装包 SHA-256 为 `EC28363A44CF37BF2A940B8E9F0DCBBB345D3F9A39C492A5B68B29014D809141`。
- [x] P7-24 日程窄宽换行与时间展示
  - 验收：桌面日程项显示本地起止时间或“全天”；组件缩窄后标题自动换行，不出现横向滚动条。
  - Red：`LoadBuildsAgendaRowsWithLocalTimeRanges` 已添加；因 `DesktopWorkbenchViewModel.SelectedEventRows` 不存在而以 CS1061 编译失败。
  - Green：新增只读日程显示投影，将定时日程映射为本地 `HH:mm–HH:mm`、全天日程映射为“全天”；日程列表禁用横向滚动并以可伸缩 TextBlock 自动换行。
  - Refactor：完成状态继续由独立待办领域承担，未给日历事件添加含义不清的完成字段或数据库迁移。
  - Verify：`DesktopWorkbenchViewModelTests` 2/2 通过；隔离桌面日程缩至 220px，运行态显示 `09:00–10:30`，长标题换成三行且 UI 树无水平滚动条。
- [x] P7-25 从当前时段开始的 24 小时天气
  - 验收：小时天气从当前整点开始，紧跟“现在”实况，再显示后续小时；不显示当前整点以前的数据，覆盖连续 24 个小时槽。
  - Red：`HourlyStartsAtCurrentHourAndIncludesCurrentConditions` 已添加；因小时显示项没有 `TimeLabel`，无法表达“现在”而以 CS1061 编译失败；原实现固定从服务返回的首项取 12 小时。
  - Green：按实况时间截取当前整点起的 24 个小时预报槽，在当前整点后插入“现在”实况卡片；小时 UI 改为显示明确的 `TimeLabel`。
  - Refactor：时间窗口只在现有天气 ViewModel 投影中计算，未修改天气 API、领域响应或增加第二次网络请求。
  - Verify：`WeatherViewModelTests` 4/4 通过；真实天气运行态顺序为 `12:00`、`现在`、`13:00`，UI Automation 共 25 项且首项不是 00:00，进程保持运行。
- [x] P7-26 全量验证与安装包重建
  - 验收：完整测试、日程和天气运行验收、隐藏启动冒烟及安装器编译通过，生成新安装包和 SHA-256。
  - Red：不适用；此条负责 P7-24、P7-25 的最终集成验证和交付物重建。
  - Green：运行统一发布流水线生成自包含 win-x64 程序和 Inno Setup 安装包。
  - Refactor：无。
  - Verify：`eng\publish.cmd` 退出 0；build 0 警告、0 错误；测试 168/168；220px 日程换行与时间显示、当前整点/现在/后续小时顺序、25 项天气窗口、隐藏启动冒烟及安装器编译通过；安装包 SHA-256 为 `401DFD7642BF2FD7E34D22BFAD15B89F958349A5C7514A74CD815C47D46B50B2`。
- [x] P7-27 全天日程与删除
  - 验收：新建日程可选择全天而不填写时间；主日历和桌面日历的当日详情均可确认删除，删除项进入回收站并从所有视图消失。
  - Red：`AllDayRequestDoesNotRequireTimes` 与 `QuickAddAllDayAndDeleteMovesEventOutOfActiveSnapshot` 已添加；分别因全天请求 API/日期字段和 `DeleteEventAsync` 不存在而以 CS1061/CS1729 失败。
  - Green（数据）：快速新增请求支持可选全天日期区间，编辑状态可生成单日全天请求；数据服务按日期创建全天事件，并通过既有回收站字段软删除日程。日程编辑 6/6、数据服务 2/2 通过。
  - Green（界面）：快速新增提供全天开关并在选中时禁用时间控件；主日历当日详情与独立日程组件提供确认删除入口，删除后统一刷新主窗口及桌面组件。
  - Refactor：复用已有软删除和全窗口刷新路径；未新增第二套删除或全天数据模型。
  - Verify：`ScheduleEditorStateTests` 6/6、`CalendarDataServiceTests` 2/2 通过；隔离 `CCCALENDAR_HOME` 的 UI Automation 验证全天日程无需时间即可创建、独立日程窗显示“全天”、确认删除后条目即时消失。
- [x] P7-28 AI Markdown 消息渲染
  - 验收：AI 输出按 Markdown 显示标题、粗体、列表和代码，不再原样显示 `**`；用户消息继续按纯文本显示，模型内容不作为可执行 HTML。
  - Red：新增 `AssistantMessagesUseMarkdownWhileUserMessagesRemainPlainText`，因 `AssistantMessageViewModel.IsMarkdown` 不存在而以 CS1061 编译失败。
  - Green：消息模型以派生属性区分用户纯文本和助手 Markdown；助手消息由 `MdXaml` 原生 WPF 文档控件渲染，不执行 HTML。聚焦测试 2/2 通过，Desktop Debug build 为 0 警告、0 错误。
  - Refactor：只在现有消息模板中按发送方分流渲染，未改变 AI 客户端协议或引入 HTML 转换层。
  - Verify：助手相关测试 7/7 通过，其中控件级测试确认标题、粗体、列表和代码正文存在且 `#`、`**`、反引号标记不残留；隔离实例截图确认每日计划标题和列表层级正常显示。
- [x] P7-29 AI 文本文件附件
  - 验收：AI 助手可附加 `.txt/.md/.csv/.json/.ics`，显示待发送文件并可移除；附件正文只进入下一次模型请求，受数量与大小限制，发送后清空。
  - Red：新增 `AttachedTextFileIsSentOnceAndShownByFileName` 与 `FailedSendKeepsAttachmentsForRetry`；因 `AssistantViewModel.AddAttachmentsAsync`/`Attachments` 不存在而以 CS1061 编译失败。
  - Green：支持读取 `.txt/.md/.csv/.json/.ics`，限制最多 5 个、单个 1 MB、合计 2 MB；附件可在发送前移除，正文只拼入模型请求，聊天消息仅显示文件名，成功清空、失败保留。`AssistantViewModelTests` 10/10 通过。
  - Refactor：无状态读取器集中处理文件边界，ViewModel 只维护本次待发送集合和模型请求组装；未新增自动写库路径。
  - Verify：隔离 `CCCALENDAR_HOME` 的 UI Automation 验证文件选择器添加 `.md`、待发送区显示文件名及移除按钮即时生效；成功/失败生命周期与限制由 10 项 ViewModel 测试覆盖。
- [x] P7-30 全量验证与安装包重建
  - 验收：完整测试、日程与 AI 运行验收、隐藏启动冒烟和安装器编译通过，生成新安装包及 SHA-256。
  - Red：不适用；此条负责 P7-27 至 P7-29 的最终集成验证和交付物重建。
  - Green：统一发布流水线生成自包含 win-x64 程序和 Inno Setup 安装包。
  - Refactor：无。
  - Verify：`eng\publish.cmd` 退出 0；format check 通过，build 0 警告、0 错误，测试 176/176；全天创建与删除、Markdown 文档渲染、附件选择与移除的隔离桌面验收通过；发布程序隐藏启动冒烟和安装器编译通过。安装包 SHA-256 为 `B58EB6DB48C99629059FD3DA99B901695550DF3461745E2DACB757D759C3F2DA`。
- [x] P7-31 应用图标与品牌资源
  - 验收：提供可编辑 SVG 母版、透明 PNG 预览和包含 16-256px 的 Windows ICO；主程序、窗口、任务栏、系统托盘和安装器统一使用新图标。
  - Red：新增 `EmbeddedIconSupportsTrayAndExplorerSizes`，因 `ApplicationIconLoader` 不存在而以 CS0103 编译失败。
  - Green：创建蓝色日历页与绿色勾选的 SVG 母版，生成透明 1024px PNG 和含 9 个尺寸的 ICO；图标嵌入桌面程序集并作为 Win32 应用图标，托盘和 Inno Setup 使用同一资源。聚焦测试 1/1 通过。
  - Refactor：保留 `eng\build-icon.ps1` 作为单一 SVG 到 PNG/ICO 生成入口，避免手工维护多份位图。
  - Verify：ICO 目录包含 16、20、24、32、40、48、64、128、256px；PNG 左上角 alpha=0；16px/32px 放大预览下日历轮廓与绿色勾选清晰可辨。
- [x] P7-32 图标验证与安装包重建
  - 验收：小尺寸像素与透明度检查、完整测试、发布启动冒烟和安装器编译通过，生成带新图标的安装包及 SHA-256。
  - Red：不适用；此条负责图标资源的最终集成验证与交付物重建。
  - Green：统一发布流水线生成带新 Win32 图标的自包含程序和 Inno Setup 安装包。
  - Refactor：无。
  - Verify：`eng\publish.cmd` 退出 0；format check 通过，build 0 警告、0 错误，测试 177/177；发布程序隐藏启动冒烟通过；从发布 EXE 与安装器提取的图标视觉一致。安装包 SHA-256 为 `0AF9A043AA31FE6CC22D845B8D1D9AFFE662DEE1C102866BDF4F3BDDDD6E5914`。
- [x] P7-33 桌面周视图与待办组件
  - 验收：周视图展示当天全部日程且不错误显示溢出数；独立待办展示无截止日期的活动待办，以方形复选框完成并即时消失；组件标题区可在未锁定时拖动窗口。
  - Red：`WeekViewShowsEveryEventWithoutOverflowCount` 失败，现有周视图只返回 4 条中的 3 条；`LoadShowsUndatedActiveTodosAndHidesCompletedTodos` 失败，无日期活动待办被过滤为空；`CompleteTodoPersistsCompletedState` 因 `CompleteTodoAsync` 不存在而以 CS1061 编译失败。
  - Green：周视图按当天实际日程数投影，月视图继续保留 3 条上限；无日期活动待办进入桌面收件箱；新增持久化完成服务并将独立待办改为方形复选框行，完成后统一刷新主窗口与桌面组件。桌面状态测试 10/10、数据服务测试 3/3 通过。
  - Refactor：拖动入口收敛到三个组件的透明标题区，列表与复选框不再触发整窗拖动；继续复用既有待办完成状态和全窗口刷新链路。
  - Verify：桌面状态测试 10/10、数据服务测试 3/3；`dotnet format ... --verify-no-changes` 通过，解决方案 Debug build 0 警告、0 错误；XAML 编译确认三个组件标题拖动事件与待办复选框事件接线有效。
- [x] P7-34 AI 中文排版
  - 验收：AI Markdown 使用清晰的中文 UI 字体；标题、正文、列表和代码层级紧凑，不再出现截图中的衬线中文和超大标题。
  - Red：`AssistantMarkdownUsesCompactChineseTypography` 先因 `AssistantMarkdownViewer` 不存在而以 CS0246 编译失败；加入最小控件后实际文档仍为 Calibri；修正根字体后再次失败，MdXaml 1.27.0 的一级标题仍为 42px。
  - Green：新增助手专用 Markdown 控件，正文使用 14px `Microsoft YaHei UI`；文档生成后将标题层级归一化为 20/18/16px 并收紧边距，保留 MdXaml 的 Markdown 解析与代码结构。控件级测试 2/2 通过。
  - Refactor：排版修正集中在唯一渲染控件中，未修改消息模型、AI 协议或 Markdown 安全边界。
  - Verify：助手相关测试 12/12；`dotnet format ... --verify-no-changes` 通过，解决方案 Debug build 0 警告、0 错误；控件级断言确认实际 FlowDocument 的中文字体与 20px 一级标题。
- [x] P7-35 全量验证与安装包重建
  - 验收：完整测试、发布启动冒烟和安装器编译通过，生成包含 P7-33 至 P7-34 的新安装包及 SHA-256。
  - Red：不适用；此条负责最终集成验收与交付物重建。
  - Green：统一发布流水线生成包含桌面周视图、待办组件和 AI 中文排版修正的自包含 win-x64 程序与 Inno Setup 安装包。
  - Refactor：无。
  - Verify：`eng\publish.cmd` 退出 0；format check 通过，build 0 警告、0 错误，测试 181/181；发布程序隐藏启动冒烟和安装器编译通过；安装包 SHA-256 为 `8AF70C01BBFCDBA44E3462140B4753050F23FB82A9B57026B1A7EA0100F7DD1D`。
- [x] P7-36 外观与 AI 设置即时持久化
  - 验收：调整日历/日程/待办透明度等外观后立即写入设置文件；AI 点击“保存设置”时等待端点、模型、提供商写盘完成；重启后恢复上述配置，API Key 仍不写入 JSON。
  - Red：`SaveWaitsForSettingsPersistenceBeforeReportingSuccess` 因现有 `Action<AiProviderSettings>` 不接受异步持久化所需的两个参数而以 CS1593 编译失败；首次隔离 UI 验收又复现外观同步保存捕获 WPF 上下文造成 `RangeValuePattern.SetValue` 超时，证明不能在 UI 线程等待会回到 UI 线程的 continuation。
  - Green：AI 设置回调改为可等待的异步持久化，只有 JSON 原子写盘完成后才显示“设置已保存”；外观变更在即时应用到对应窗口后同步更新目标设置并调用同一写盘路径；底层 await 不捕获 WPF 上下文，避免同步外观保存死锁。AI/外观聚焦测试 6/6 通过。
  - Refactor：退出保存和即时保存复用 `SaveAppSettingsAsync`；API Key 继续只经 `ISecretStore` 写入 Windows 凭据管理器。
  - Verify：Desktop Debug build 0 警告、0 错误；隔离 `CCCALENDAR_HOME` 中将透明度改为 42%、AI 端点改为 `https://persist.example/v1` 后直接强制终止进程，重启 UI 分别恢复 42 和该端点。
- [x] P7-37 全量验证与安装包重建
  - 验收：完整测试、发布启动冒烟和安装器编译通过，生成包含 P7-36 的新安装包及 SHA-256。
  - Red：不适用；此条负责最终集成验收与交付物重建。
  - Green：统一发布流水线生成包含外观与 AI 设置即时持久化修正的自包含 win-x64 程序和 Inno Setup 安装包。
  - Refactor：无。
  - Verify：`eng\publish.cmd` 退出 0；format check 通过，build 0 警告、0 错误，测试 182/182；发布程序隐藏启动冒烟和安装器编译通过；安装包 SHA-256 为 `7E7F70235892B1313E3B22FB3E328CBE3BCC7FEE3D193FBF14D7F7366476D3FA`。
- [x] P7-38 AI 只读/写入模式与确认提案
  - 验收：对话页可选择只读或写入模式；只读仅开放查询；写入可提出创建、待办状态和删除草案，但必须经过现有预览确认才写库；多项创建依次确认。
  - Red：`WriteModeQueuesModelCreationProposalsForConfirmation` 因 `AiAssistantMode`、`AssistantViewModel.Mode` 和提案客户端契约不存在而以 CS0103/CS0117/CS0246 编译失败。
  - Green：新增只读/写入模式选择；只读只发送查询工具，写入模式把模型的创建、待办状态及软删除工具调用解析为提案队列，并复用现有预览、确认、撤销链路逐项处理，模型调用阶段不直接写库。
  - Refactor：最小上下文构建器改为静态入口；统一以 `AiAssistantRequest`/`AiAssistantResult` 传递模式、回复与提案。
  - Verify：Assistant ViewModel 测试 11/11、provider 测试 6/6、Core AI 测试 3/3；Desktop Debug build 0 警告、0 错误；provider 回归验证只读请求不含 `propose_*`，写入工具调用后数据库记录数不变。
- [x] P7-39 多模型配置目录与对话切换
  - 验收：设置页可保存、查看和删除多个模型配置，并明确显示已配置状态；对话页可从已配置模型中切换，当前选择持久化；API Key 仍只存 Windows 凭据管理器。
  - Red：`SaveAddsNamedConfigurationToSharedCatalog` 与 `SelectingConfiguredModelChangesProviderUsedByConversation` 因 `AiModelConfiguration`、共享 `AiModelCatalog`、命名配置及目录保存契约不存在而以 CS0246/CS0117/CS1593 失败。
  - Green：新增可持久化的命名模型配置目录；设置页可新增、选择编辑和删除配置并显示模型名称；对话页新增模型下拉框；provider 每次请求读取共享目录中的当前模型，选择变化立即保存。旧单模型设置在升级时自动迁移为目录项。
  - Refactor：保留旧 `AiProvider` 字段和旧 ViewModel 构造入口用于兼容；共享 `AiModelCatalog` 统一设置页、对话页和 provider 的当前选择，不复制状态；API Key 继续只使用 Windows 凭据管理器。
  - Verify：Desktop AI/设置测试 18/18、配置/provider 测试 9/9，模型目录 JSON 重启往返、删除后选择更新、对话选择切换均通过；format check 通过。
- [x] P7-40 AI 真实流式输出
  - 验收：OpenAI Chat Completions、Responses、Anthropic 和 Ollama 均按各自流协议逐段更新同一条助手消息；流式工具调用仍遵守只读/确认边界。
  - Red：`StreamingDeltasAppendToOneAssistantMessage` 与 `OpenAiChatStreamsTextDeltasFromSse` 因 `AiAssistantUpdate`/`AiTextDelta`、流式客户端契约和可增量消息模型不存在而以 CS0246 失败。
  - Green：客户端新增增量更新契约，Assistant 在同一条可通知消息上追加文本；OpenAI Chat Completions 与 Responses、Anthropic Messages 使用 SSE，Ollama 使用 NDJSON，均开启网络流并逐事件回调；分片工具参数完整组装后才执行查询或生成确认提案。
  - Refactor：流式协议实现拆入 provider 的 partial 文件；旧 `SendAsync` 保留用于兼容，接口默认适配旧客户端；提案更新继续复用 P7-38 的队列和确认服务。
  - Verify：provider 流式/非流式测试 12/12、Assistant/设置测试 19/19，四种协议文本增量、OpenAI 分片工具参数及数据库零写入通过；门控网络流验证第一分块回调时请求尚未完成；format check 通过。
- [x] P7-41 全量验证与安装包重建
  - 验收：完整测试、隔离 UI 验收、发布启动冒烟和安装器编译通过，生成包含 P7-38 至 P7-40 的安装包及 SHA-256。
  - Red：不适用；此条负责最终集成验收与交付物重建。
  - Green：统一发布流水线重新生成包含只读/写入模式、多模型目录和四协议真实流式输出的 win-x64 自包含程序及 Inno Setup 安装包。
  - Refactor：封版复核补充“原样保存配置仍保持目录内选择引用”回归并修正 `AiModelCatalog.Upsert`；无其他整理。
  - Verify：`eng\publish.cmd` 退出 0；format check 通过，build 0 警告、0 错误，测试 198/198；发布程序隐藏启动冒烟和安装器编译通过；隔离 `CCCALENDAR_HOME` 实例通过 UI Automation 验证模型下拉、只读/写入模式、配置名称、已配置模型与删除控件，两张 1180×760 截图无重叠；安装包 59,842,568 字节，SHA-256 `F625C077C564EC14D89AF7921523B4E60A901C50EA66FA8EB27EE0F2D800784F`。
- [x] P7-42 主窗口视觉系统与重点工作区优化
  - 验收：基础 WPF 控件使用统一主题与完整交互状态；主搜索显示水印；今天页优先展示日程/待办；月历带选中日期详情；工具中心按四类工作区切换；项目/待办无卡片套卡片；AI 命令与模型权限上下文分层；最小和大屏布局无重叠。
  - Red：`SelectingDateRefreshesTheWorkspaceDetails` 先因 `SelectedDateText` 和 `SelectedEventDetails` 不存在而以 CS1061 失败；UI 设计契约随后分别因缺少九类控件样式、搜索水印、工具分类、日历详情、今天工作区优先级、去嵌套背景及 AI 双层工具栏而失败。
  - Green：扩展全局设计令牌与 TextBox、ComboBox、CheckBox、DatePicker、ProgressBar、ListBox、TabControl、DataGrid 等样式；主窗口收紧侧栏并增加搜索水印；今天页改为工作优先；日历单击同步 280px 详情面板；工具中心拆为四页签；规划工作区移除嵌套页面底色；AI 增加独立命令栏和模型/权限栏。
  - Refactor：页面标题、区块标题、辅助文字和图标按钮收敛到四个共享样式；复用现有 CalendarViewModel、工具命令和 AI 命令，不新增数据库路径或第二套业务状态；移除今天页没有真实数据来源的固定 0 统计。
  - Verify：`eng\publish.cmd` 退出 0；format check 通过，build 0 警告、0 错误，测试 206/206；Release 自包含程序隐藏启动冒烟和安装器编译通过；隔离 `CCCALENDAR_HOME` 截图覆盖今天、日历、工具、AI、设置和待办，980x640、1180x760、1366x768 以及 1920x1080 屏幕最大化均无文字截断、重叠或底部操作遮挡；安装包 59,848,586 字节，SHA-256 `E4211C33574B8C46801662FBF1330CEDDD7913DC1991774E9E816F5CA19C1264`。

- [x] P7-43 启动配置损坏恢复与异常退出
  - 验收：截断或非法 `settings.json` 不得让应用无窗口常驻；应用应保留损坏文件副本、回退默认设置，其他启动异常应记录并明确退出。
  - Red：隔离目录写入 `{bad` 后，现有 `OnStartup` 将 `JsonException` 交给已标记为 handled 的 WPF 异常处理器，进程持续运行但没有窗口；新增 `LoadWhenFileIsCorruptBacksItUpAndReturnsDefaults` 先因 `JsonException` 失败。
  - Green：设置存储捕获 JSON/数据格式错误，将原文件移动到 `settings.json.corrupt-*.json` 并返回默认设置；启动初始化增加顶层异常边界，启动阶段未完成时的未处理异常触发退出。
  - Refactor：无。
  - Verify：设置存储测试 4/4、基础设施测试 51/51、完整测试 207/207；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过；发布冒烟覆盖全新 `CCCALENDAR_HOME`。
- [x] P7-44 修复只读进度条跨电脑启动崩溃
  - 验收：在其他 Windows 电脑启动时，待办和工具中心的进度条不得尝试回写只读属性；无 WPF 绑定异常弹窗。
  - Red：截图中的异常为 `TodoItem.ProgressPercent` 只读属性被 `ProgressBar.Value` 默认双向绑定；新增 `ReadOnlyProgressValuesUseOneWayBindings` 先因待办页和工具中心缺少显式 `Mode=OneWay` 失败。
  - Green：为待办进度、日/周/月/年时间进度的 `ProgressBar.Value` 显式指定 `Mode=OneWay`。
  - Refactor：无。
  - Verify：回归测试 1/1；完整测试 208/208（Core 60、Desktop 97、Infrastructure 51）；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过；`eng\publish.cmd` 退出 0；全新 `CCCALENDAR_HOME` 发布程序启动 8 秒保持运行；安装器重新编译完成。

## P8 - 跨电脑同步（延期）

- [~] P8-01 同步协议、账户体系和端到端加密设计
- [~] P8-02 日历、项目、待办和记录增量同步
- [~] P8-03 冲突检测、保留副本和人工解决
- [~] P8-04 云剪贴板文本、图片和文件同步

## P9 - 日程交互与会议室视图

- [x] P9-01 修复 AI 日程查询 LINQ 翻译失败
  - 验收：AI 写入/查询日程时 `query_events` 不再因 `DateTimeOffset?` 比较无法翻译而失败。
  - Red：真实报错 `DbSet<CalendarEvent>().Where(...).Where(c => False || c.IsAllDay || c.EndAtUtc > @fromUtc)` 无法翻译。
  - Green：`SqliteReadOnlyAiToolExecutor` 先投影拉取再在内存做时间范围过滤。
  - Refactor：无。
  - Verify：Infrastructure 测试 57/57。
- [x] P9-02 日程地点（会议室）字段与更新方法
  - 验收：日程可记录会议室地点；标题、地点、定时/全天时间均可更新。
  - Red：`CalendarEvent.Location`、`UpdateDetails`、`RescheduleTimed/AllDay` 及迁移测试先失败。
  - Green：实体加 `Location`、三个更新方法；EF 配置与 `AddEventLocation` 迁移。
  - Refactor：无。
  - Verify：Core/Infrastructure 相关测试通过。
- [x] P9-03 数据服务日程更新与待办重开
  - 验收：`UpdateEventAsync` 持久化标题/地点/时间并可切换全天；`ReopenTodoAsync` 将已完成待办重开。
  - Red：`CalendarDataServiceTests` 新增 5 个用例先编译失败。
  - Green：实现两个服务方法，参数缺失抛 `ArgumentException` 包装。
  - Refactor：无。
  - Verify：`CalendarDataServiceTests` 6/6。
- [x] P9-04 快速新增窗口日期时间与会议室下拉
  - 验收：日程新增默认日期时间可见可选；可选会议室（可留空）。
  - Red：`UiDesignContractTests` 日期/时间/会议室绑定断言先失败。
  - Green：窗口实现 `INotifyPropertyChanged`；`MeetingRoomCatalog` 集中 5 个会议室选项。
  - Refactor：无。
  - Verify：Desktop 测试 121/121。
- [x] P9-05 日程查看、编辑与时间条预览
  - 验收：单击日程条打开当日详情（含会议室）；详情内双击条目或点编辑按钮进入编辑窗；编辑窗右侧显示当日时间条（08:00–22:00 半小时行），已预订色块标识、当前编辑时段描边高亮并随表单联动。
  - Red：`EventTimelinePreviewBuilderTests`、`EventEditStateTests`、`RoomTimelineLayoutTests` 与 UI 契约断言先失败。
  - Green：`EventEditWindow`（左表单右时间条）；`DayScheduleWindow` 加编辑/删除入口与地点行；`MainWindow` 串接 `UpdateEventAsync` 后刷新。
  - Refactor：无。
  - Verify：Desktop 测试 121/121（含 3 个预览构建用例）。
- [x] P9-06 会议室视图模式
  - 验收：日历页新增“会议室”模式：横轴为会议室、纵轴为时间（08:00–22:00），已预订色块可点击查看，按天前后翻页。
  - Red：`RoomTimelineColumnsTests` 3 个用例先失败。
  - Green：`CalendarViewMode.Room`；`CalendarViewModel.RoomColumns` 按会议室分列构建预订条；XAML 时间轴面板。
  - Refactor：无。
  - Verify：Desktop 测试 121/121。
- [x] P9-07 待办输入即新增与点击完成/重开
  - 验收：待办页顶部输入回车即新增；点击条目在完成/重开间切换并持久化；拖拽分类不受影响。
  - Red：`TodoViewSupportsTypeToCreateAndClickToToggle` 契约断言先失败。
  - Green：`TodoView` 输入框 + 卡片 `MouseLeftButtonUp` 切换；`MainWindow` 落库（`QuickAddAsync`/`CompleteTodoAsync`/`ReopenTodoAsync`）后刷新。
  - Refactor：无。
  - Verify：Desktop 测试 121/121。

## P10 - 按周重复快速新增

- [x] P10-01 快速新增支持按周重复
  - 验收：日程新增窗口出现"重复"行（周一~周日勾选）；勾选周几则在所选日期所在周（周一起）为每个勾选日各创建一条日程（含会议室/时间）；不勾选则仅创建所选日期一条。全天日程同样支持。
  - Red：`WeeklyRecurrencePlannerTests` 3 用例（周一基周展开、空选择回退单日、去重）+ UI 契约 `QuickAddWindowOffersWeeklyRepeatSelection` 先失败。
  - Green：`WeeklyRecurrencePlanner.GetDatesInWeek` 纯函数；`QuickAddWindow.Request` 改为 `Requests` 列表按日期构建；`MainWindow` 两处消费点循环 `SaveRequestAsync`。
  - Refactor：顺带修复全天日程地点被丢弃的问题（`with { Location }` 对两分支统一生效）。
  - Verify：`eng\verify.cmd` 退出 0，测试 248/248。
- [x] P10-02 重新发布与安装包
  - 验收：`eng\publish.cmd` 退出 0；安装包 `artifacts\installer\cccalendar-0.1.0-win-x64-setup.exe` 生成。
  - Verify：Release 自包含发布冒烟通过；SHA-256 `823E97C23CE7D0AACBAB8BADBF4007C40A096727945AE49FAD48DDF1EFECCDC7`。

## P11 - 会议室预订板与 AI 接口修复

- [x] P11-01 主题修复：ComboBox 模板补 ToggleButton + PART_EditableTextBox
  - 验收：鼠标点击可打开下拉框（此前模板缺 ToggleButton 导致命中测试失败）；可编辑下拉支持文本输入。
  - Verify：`UiDesignContractTests` ComboBox 模板契约通过；全量 Desktop 测试通过。
- [x] P11-02 AI 400 修复：错误体回显 + reasoning_content 剥离
  - 验收：接口报错时助手显示具体错误信息（HTTP 状态 + error.message），不再是裸 400；推理模型（DeepSeek reasoner/v4-flash）工具循环回传 assistant 消息前剥离 `reasoning_content`，消除 DeepSeek 400。
  - Red：`ChatToolLoopStripsReasoningContentBeforeEchoingAssistantMessage` 先失败。
  - Green：`CreateSanitizedAssistantMessage` 剥离字段；`EnsureSuccessWithApiErrorAsync`/`ExtractApiErrorMessage` 回显错误体；连接测试同样回显。
  - Verify：Infrastructure 测试 60/60；真实 DeepSeek `deepseek-v4-flash` 接口实测返回 200。
- [x] P11-03 会议室预订板核心逻辑（TDD）
  - 验收：半小时粒度选择（14:00-15:00 / 14:30-15:30）、跨会议室矩形批量选择、占用判定与绿（选中空闲）/红（选中冲突）/灰（未选占用）三态。
  - Red：`RoomBookingBoardTests` 先失败；Green：`RoomBookingBoard` 纯逻辑；Verify：全绿。
- [x] P11-04 RoomBookingPicker 控件
  - 验收：顶部「今天 + ‹ › + `8月19日 周三 (今天)`（蓝色高亮）」日期栏；左侧 24 小时刻度（小时间隔）；首行五会议室列头；自绘板支持点选/拖选/上下边缘半小时调整；色块内标注「会议室 空闲/已占用」；选中块四角白色圆形调节点。
  - Green：`RoomBookingBoardControl`（FrameworkElement 自绘 + 鼠标交互）+ `RoomBookingPicker`（UserControl 组合）。
- [x] P11-05 集成到快速新增 + 日程编辑窗口
  - 验收：两个窗口右侧/下方内嵌会议室时间视图区；选中色块回填日期/会议室/起止时间；切换日期同步表单；全天勾选时禁用时间区。
  - Green：`EventEditWindow` 右栏替换时间条为 Picker；`QuickAddWindow` 内嵌 Picker（外层行改 `*` 修复布局溢出）+ `PickerPicked`/`PickerDateSwitched` 回填；`MainWindow` 两处构造传入 `GetEventsOnDate` 加载器。
  - Verify：UI 契约 `EventEditWindowShowsRoomBookingPickerBesideForm`/`QuickAddWindowEmbedsRoomBookingPicker` 通过（替换旧「当日时间条」契约）。
- [x] P11-06 版本 0.2.0 发布与安装包
  - 验收：`eng\publish.cmd` 退出 0；`artifacts\installer\cccalendar-0.2.0-win-x64-setup.exe` 生成。
  - Verify：build 0 警告/0 错误；测试 269/269（Core 66、Desktop 143、Infrastructure 60）；SHA-256 `0352FF3C4C6EE9B36EF89AAEEB112D06F060D8F16DE14BFA23B1B550454004C2`。

## P14 - 桌面交互与会议室看板修复批次

- [x] P14-01 会议室看板列宽与半小时网格线
  - 验收：每列宽度缩小 1/6（150→125），院士办会议室列完整可见（横向滚动兜底）；半小时浅色分隔线可见。
  - Green：`RoomBookingBoardControl.RoomWidth=125`；网格绘制增加 `GridHalfHourPen` :30 分界线；`RoomBookingPicker` 头/体双层 ScrollViewer 同步横向滚动。
- [x] P14-02 快速新增重复设置重构
  - 验收：左面板「重复」为下拉框（默认不重复/每周重复）；选每周后显示周一~周日勾选 + 重复日期范围（默认今天起 90 天，可改起止）；范围内每个勾选周几各创建一条日程。
  - Red：`WeeklyRecurrencePlannerTests` 日期范围展开用例 + `UiDesignContractTests` 重复控件契约先失败。
  - Green：`WeeklyRecurrencePlanner` 增加 From/To 范围展开；`QuickAddWindow` RepeatModeBox/RepeatPanel/RepeatStartPicker/RepeatEndPicker + 手动编辑保护。
- [x] P14-03 双击非当天日期无法新增修复
  - 验收：桌面工作台双击任意日期（含非当天）打开快速新增并预填该日期。
  - 真因：单击冒泡到窗口级 `MouseLeftButtonDown` 触发 `DragMove`，模态移动循环吞掉双击第二次按下。修复：`DayMouseLeftButtonDown` 中 `e.Handled = true` 拦截冒泡。
- [x] P14-04 主面板今日日程/今日待办数据拉通
  - 验收：主面板「今天」页展示当日日程与待办（此前仅静态占位）。
  - Green：`TodayViewModel` 装载 + `MainWindow.ReloadAsync` 接线；Red/Green 测试覆盖当日过滤与完成态。
- [x] P14-05 待办四象限拖动修复
  - 验收：四象限视图卡片可拖动换象限，且重启后保持（SQLite 持久化 IsImportant/IsUrgentOverride）。
  - 真因有两层：(1) 看板外层 ScrollViewer 未随四象限折叠，整层覆盖吞掉鼠标事件——折叠样式挂到外层 ScrollViewer；(2) 拖动启动挂在卡片 MouseMove，快速移动时收不到事件——移到根元素 `PreviewMouseMove` + 拖动源跟踪。
  - Red：`TodoViewRuntimeTests`（真实 STA 实例化 + 命中测试验证卡片中心不被滚动器遮挡）+ `UiDesignContractTests.TodoDragStartIsWiredAtViewRootNotOnCards`。
  - 基础设施：`WpfRuntimeHost` 共享 STA 线程与 Application 实例，解决并行 UI 测试"同 AppDomain 多 Application"冲突。
- [x] P14-06 桌面日程/待办组件右键新增
  - 验收：桌面日程、桌面待办组件右键菜单提供「新增日程」「新增待办」入口，预选对应类型打开快速新增。
  - Green：`DesktopComponentContextMenu` 扩展 + `App.CreateTodoFromDesktopAsync` 接线。
- [x] P14-07 看板 8:00-10:00 不可选中修复
  - 验收：会议室看板 8:00-10:00 区域可点选/拖选，网格线从顶边开始完整渲染。
  - 真因：`RoomBookingBoardControl` 在 BodyScroll 中垂直居中（内容高 416 < 视口高 ~561），整体下移 72.5px，视觉刻度与命中区域错位。修复：XAML 加 `VerticalAlignment="Top"`。
  - Red：`RoomBookingPickerRuntimeTests.BoardIsLaidOutBelowHeaderAndHitTestsToItself` / `BoardRendersHourGridLinesStartingAtTopEdge`（真实布局断言板顶 = 表头底 + 间距，命中测试命中板自身）。
- [x] P14-08 UI 自动化实测（PowerShell + UIAutomation + Win32）
  - 双击非当天（8/21）：快速新增打开且日期绑定正确（`8月21日 周五`）——注意 WPF 弹窗不出现在 UIA RootElement 子级，需 Win32 `EnumWindows` 定位再 `FromHandle` 挂接。
  - 四象限拖动：新建待办从右下拖到左上「重要且紧急」，`DRAG_TEST=PASS`。
  - 拖动持久化：重启应用后卡片仍在「重要且紧急」，`PERSIST_TEST=PASS`。
  - 看板 8:00 点击：点击第一列 8:00-8:30 单元格后开始/结束时间从 11:30-12:30 变为 08:00-08:30，`BOARD_8AM_CLICK=PASS`。
  - 脚本：`qa13_dbclick.ps1`、`qa12_dragtest.ps1`、`qa14_persist.ps1`、`qa15_board8am.ps1`。
- [x] P14-09 清理诊断代码
  - 删除无断言的 `RoomBookingPickerLayoutDiagnostics.cs`（正式布局测试已覆盖）。
  - Verify：build 0 警告/0 错误；测试 288/288（Core 68、Desktop 155、Infrastructure 65）。
- [x] P14-10 窗口标题栏/边框颜色调查与撤销
  - 现象：主窗口与快速新增顶部边框/标题栏变粉色。实测像素 `#F9EFF8`；根因是 Windows 11 按系统强调色（本机 `0xFFE89AB0` 粉）给 DWM 标题栏/边框染色，并非应用代码问题。用户认可根因后决定不改系统行为。
  - 曾实现 `WindowChromeColorizer`（DwmSetWindowAttribute 固定 DWMWA_BORDER_COLOR/DWMWA_CAPTION_COLOR 为主题 AccentBrush）并发布 0.2.7，按用户要求整体撤销：删除类与测试、移除 6 个窗口接线与主题切换联动、版本回退 0.2.6、删除 0.2.7 安装包。
  - 结论：窗口标题栏/边框颜色跟随系统强调色为 Windows 默认行为，如需恢复蓝色请在系统设置 → 个性化 → 颜色中调整强调色。
- [x] P14-12 版本 0.2.8 发布（P14 全部修复，不含已撤销的颜色固定）
  - Verify：`eng\publish.cmd` 退出 0（含全量测试 + 冒烟启动）。
  - 安装包：`artifacts\installer\cccalendar-0.2.8-win-x64-setup.exe`，SHA-256 `CD1C05F669F9B3C6FCF7C68942A65D18A520CB2713BA97C4F7AFB415E50EF779`。

## P15 - 联网协作与团队会议室预约

- [x] P15-01 本地同步元数据、设备标识和 Outbox 表
  - 验收：同步元数据保存设备 ID、拉取游标和更新时间；Outbox 保存实体、操作、JSON 载荷、重试次数和确认时间；关闭并重新打开 DbContext 后数据保持。
  - Red：`SyncMetadataTests`、`SyncPersistenceTests` 先因 `CcCalendar.Core.Sync` 和 DbSet 不存在而失败；迁移前基础设施测试因 pending model changes 失败。
  - Green：新增 `SyncMetadata`、`SyncOutboxMessage`、`SyncOperationKind`，EF 配置和 `AddSyncMetadata` 迁移；Core 2/2、Infrastructure 1/1 通过。
  - Refactor：无。
  - Verify：迁移往返测试通过；下一步进入独立会议室资源模型。
- [x] P15-02 独立 Room/RoomBooking 领域模型与冲突测试
  - 验收：会议室拥有稳定 ID、名称、工作区归属和时区；预约使用 UTC 起止时间；同一房间半开区间重叠时报告冲突，不同房间或相邻区间不冲突。
  - Red：先补 Core 领域测试，再实现最小模型和冲突判定。
  - Green：新增 `Room`、`RoomBooking` 和 `RoomBookingConflictDetector`，UTC 归一化和半开区间冲突测试通过。
  - Refactor：复用现有半开区间约定；无额外抽象。
  - Verify：`dotnet test CcCalendar.sln --no-restore --configuration Debug` 通过，Core 73、Infrastructure 66、Desktop 155，共 294 项。
- [x] P15-03 ASP.NET Core 局域网 API 最小闭环
  - 验收：创建、取消预约；冲突返回 409；重复请求使用幂等键只产生一条预约。
- Red：`BookingApiTests` 先因 Server 入口和路由不存在而失败。
  - Green：新增独立 `CcCalendar.Server`、内存房间预约存储、创建/删除路由和幂等键处理；API 测试 3/3 通过。
  - Refactor：存储通过 `IRoomBookingStore` 隔离，后续可替换 PostgreSQL；补充并发幂等键保护。
  - Verify：完整解决方案测试通过，Core 73、Infrastructure 66、Desktop 155、Server 3，共 297 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。
- [x] P15-04 WPF 客户端 HTTP 预约适配器与团队房间目录缓存
  - 验收：HTTP 客户端通过相对 API 路由读取房间目录、发送 `Idempotency-Key` 创建预约、传播 409，并缓存同一 Workspace 的房间目录直到强制刷新。
  - Red：`RoomBookingApiClientTests` 先因客户端适配器和目录缓存不存在而失败。
  - Green：新增 Core 远程请求/结果契约、Infrastructure `RoomBookingApiClient` 和 `RoomCatalogCache`；测试 4/4 通过；服务端补充房间目录 GET 路由。
  - Refactor：网络代码只位于 Infrastructure，Core 依赖窄接口；无 UI 改造。
  - Verify：完整解决方案测试通过，Core 73、Infrastructure 70、Desktop 155、Server 4，共 302 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。
- [x] P15-05 OIDC 登录、工作区成员权限和凭据生命周期
  - 当前子切片：Server JWT Bearer/OIDC 配置边界、客户端令牌注入、工作区成员存储与角色授权、服务端房间目录与预约持久化、PKCE 协议、token 生命周期、回环回调和 WPF 团队连接设置已完成；P15-05 收尾，下一步进入 SignalR/增量同步。
  - Red：`BookingApiTests.AnonymousRequestIsRejected`、`BookingForAnotherOrganizerIsForbidden` 先因接口未要求认证而失败。
  - Green：生产 Server 配置 `Authentication:Authority`/`Authentication:Audience` 和 JWT Bearer；预约、取消、房间目录要求认证，并校验令牌 `sub` 与预约人一致；测试使用独立 fake scheme。
  - Refactor：测试认证只存在于 Server.Tests，不进入生产程序集。
  - Red（成员授权）：新增 `SignedInUserWithoutMembershipCannotReadRooms`、`ViewerCanReadRoomsButCannotCreateBooking`、`AdminCanCreateRoom`、`MembershipDoesNotCrossWorkspaceBoundary`，先因成员存储和授权服务不存在而编译失败。
  - Green（成员授权）：新增 `IWorkspaceMembershipStore`/线程安全内存实现、`WorkspaceAuthorizationService`、房间目录存储；房间目录按工作区检查 `CanReadRooms`，预约检查 `CanCreateBooking`，创建房间检查 `CanManageRooms`。
  - Refactor（成员授权）：房间目录读取通过锁定快照避免并发枚举；生产代码不包含测试认证 handler。
  - Verify（成员授权）：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 78、Infrastructure 71、Desktop 155、Server 10，共 314 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。
  - Red（持久化）：新增 `FileRoomStoreTests.RoomCatalogSurvivesStoreRecreation` 和 `BookingAndIdempotencySurviveStoreRecreation`，先因文件存储和稳定 ID 重建入口不存在而编译失败。
  - Green（持久化）：新增目录/预约 JSON 原子文件存储，生产默认使用 `Storage:DataDirectory`，领域模型增加 `Restore` 以保留资源 ID；测试工厂显式覆盖为内存存储。
  - Refactor（持久化）：目录和预约存储继续依赖窄接口，原子写入逻辑集中到 `AtomicJsonFile`；该实现作为单实例/LAN 过渡存储，后续可替换 PostgreSQL。
  - Verify（持久化）：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 78、Infrastructure 71、Desktop 155、Server 12，共 316 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。
  - Red（PKCE/token）：新增 `OidcPkceTests.AuthorizationRequestUsesS256AndCarriesState`、`TokenClientExchangesAuthorizationCode`、`AccessTokenProviderRefreshesExpiredTokenAndKeepsRefreshToken`，先因 OIDC 契约、PKCE 工厂和 token 客户端不存在而编译失败。
  - Green（PKCE/token）：新增 OIDC 客户端选项、S256 PKCE 授权 URL、授权码/refresh token HTTP 客户端、凭据存储和可替换 `IAccessTokenProvider`。
  - Refactor（PKCE/token）：令牌只通过 `ISecretStore` 序列化保存，access token 配置与普通 `AppSettings` 分离；refresh 响应缺少新 refresh token 时保留旧值。
  - Verify（PKCE/token）：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 78、Infrastructure 74、Desktop 155、Server 12，共 319 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。
  - Red（回环回调）：新增 `OidcLoginFlowTests.CallbackWithMatchingStateReturnsAuthorizationCode`、`CallbackWithDifferentStateIsRejected`、`ProviderErrorIsSurfacedBeforeTokenExchange` 和真实 TCP `LoopbackReceiverAcceptsOnlyItsCallbackPath`，先因回调解析/监听器不存在而编译失败。
  - Green（回环回调）：新增 state/code/error 校验、`LoopbackOidcCallbackReceiver`、系统浏览器启动器和 `OidcLoginClient`；动态 loopback redirect URI 通过 PKCE 授权请求传给身份提供商。
  - Refactor（回环回调）：浏览器启动通过 `IOidcBrowserLauncher` 隔离，登录客户端不直接依赖 WPF；回调响应使用实际 UTF-8 字节长度。
  - Verify（回环回调）：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 78、Infrastructure 78、Desktop 155、Server 12，共 323 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。
  - Red（WPF 接入）：新增 `TeamConnectionViewModelTests.LoginPersistsTokenAndMarksConnectionAuthenticated`、`LogoutDeletesPersistedToken`，先因团队连接设置模型和 ViewModel 不存在而编译失败。
  - Green（WPF 接入）：新增 `TeamConnectionSettings`、`TeamConnectionViewModel` 和设置页“团队连接”标签；登录/退出命令接入 `OidcLoginClient` 与 `OidcTokenStore`，配置变化回写 `AppSettings`。
  - Refactor（WPF 接入）：ViewModel 通过登录委托隔离浏览器/网络，测试不启动真实浏览器；普通设置只保存端点和客户端 ID，不保存 token。
  - Refactor（错误处理）：登录取消、无效配置和网络异常统一转换为 ViewModel 状态文本，避免 WPF 未处理异常。
  - Verify（WPF 接入）：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 78、Infrastructure 78、Desktop 157、Server 12，共 325 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。
- [x] P15-06 SignalR 通知、游标增量同步和断线恢复
  - 当前子切片：SignalR 鉴权、工作区分组、变更通知客户端、服务端游标增量 API 和断线同步协调器已完成；P15-06 收尾，下一步双客户端验收。
  - Red：`BookingApiTests.AnonymousSignalRNegotiationIsRejected` 先因 `/hubs/sync` 未映射而得到 404，而不是预期的 401。
  - Green：新增 `[Authorize] SyncHub`、`JoinWorkspace` 成员校验、`SignalRChangeNotifier`、Core `SyncChange` 契约和自动重连 `SyncHubClient`；房间/预约创建成功后广播 ID、操作和版本。
  - Refactor：SignalR 只发送变更通知，不发送完整预约正文；客户端通过 `IAccessTokenProvider` 注入 Bearer，服务端测试用 NoOp notifier 隔离网络。
  - Verify：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 78、Infrastructure 78、Desktop 157、Server 13，共 326 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。
  - Red（游标增量）：新增 `SyncCursorReturnsRoomCreationOnce` 和 `SyncApiClientTests.ReadsChangesAfterCursorWithBearerToken`，先因 `/sync` 路由、游标存储和客户端接口不存在而失败。
  - Green（游标增量）：新增按工作区递增的 `InMemorySyncChangeStore`、`GET /api/workspaces/{workspaceId}/sync?cursor=`、Core `SyncPage`/`ISyncChangeClient` 和 Infrastructure `SyncApiClient`。
  - Refactor（游标增量）：服务端先记录变更再广播；客户端对负 cursor 归一化为 0，并复用 Bearer token 注入。
  - Verify（游标增量）：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 78、Infrastructure 79、Desktop 157、Server 14，共 328 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。
  - Red（断线恢复）：新增 `SyncCoordinatorTests.AppliesPageBeforeAdvancingCursor`、`FailedApplyDoesNotAdvanceCursorAndCanRetry`，先因 cursor 存储接口和同步协调器不存在而编译失败。
  - Green（断线恢复）：新增 `ISyncCursorStore`、`SyncConnectionState` 和 `SyncCoordinator`；同步串行化，应用全部变更后才保存 cursor，异常进入 `Faulted`。
  - Refactor（断线恢复）：协调器不绑定 SQLite/SignalR，cursor 存储和变更应用均通过窄接口注入；释放内部信号量。
  - Verify（断线恢复）：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 78、Infrastructure 81、Desktop 157、Server 14，共 330 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。
- [x] P15-07 双客户端联网验收与发布配置
  - 当前子切片：双客户端 HTTP 验收、部署配置、PostgreSQL provider/schema、Owner bootstrap 和 PostgreSQL 实机验收均已完成。
  - Red：新增 `TwoClientsShareRoomChangesAndServerConflictDecision`，在实现前没有覆盖两个独立客户端共享目录和服务端冲突判定的验收。
  - Green：两个独立 `HttpClient` 连接同一 `TestServer`；A 创建房间后 B 可读取，A 创建预约后 B 同时预约得到 HTTP 409。
  - Refactor：补充 Server 默认 `appsettings.json`（监听 5080、Storage 数据目录、OIDC 配置占位），并在 `docs/SYNC_DESIGN.md` 标注 JSON 仅为 LAN/单实例过渡存储。
  - Verify：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 78、Infrastructure 81、Desktop 157、Server 23，共 339 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。真实 PostgreSQL/HTTPS 和身份提供商验收仍待完成。
  - Red（LAN 可诊断）：新增 `HealthEndpointIsReachableWithoutAuthentication`，在实现前没有一个不依赖登录即可区分“网络不通”和“API 未认证”的探针。
  - Green（LAN 可诊断）：新增匿名 `GET /health`，返回服务名和 `status=ok`；新增 [LAN_DEPLOYMENT.md](docs/LAN_DEPLOYMENT.md)，说明 `0.0.0.0:5080`、局域网 IP、防火墙和客户端配置。
  - Refactor（LAN 可诊断）：将局域网原型与生产 HTTPS/PostgreSQL 边界写入文档，避免把同机 TestServer 验收误认为跨电脑验收。
  - Verify（LAN 可诊断）：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 78、Infrastructure 81、Desktop 157、Server 16，共 332 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。
  - Red（部署配置）：新增 `DeploymentOptionsTests`，先因生产部署配置模型不存在而编译失败。
  - Green（部署配置）：新增 `ServerDeploymentOptions`，集中解析 `Storage:Provider`、连接串、数据目录和 `Network:RequireHttps`；非法 provider、PostgreSQL 缺连接串、尚未注册 PostgreSQL provider 均在启动时快速失败；可配置 HTTPS 重定向。
  - Refactor（部署配置）：LAN 默认保持 `file`，生产配置不会静默回退到 JSON；`appsettings.json` 显式声明 provider 和 HTTPS 开关。
  - Verify（部署配置）：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 78、Infrastructure 81、Desktop 157、Server 19，共 335 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。
  - Red（PostgreSQL schema）：新增 `PostgresSchemaTests`，先因 PostgreSQL schema 契约不存在而失败。
  - Green（PostgreSQL schema）：新增 Npgsql 10.0.1、`PostgresSchema`；数据库级唯一幂等约束、GiST 半开区间排斥约束、成员表和 Workspace 变更游标表已固化。
  - Refactor（PostgreSQL provider）：新增 `PostgresDatabase`、房间目录/预约/成员/同步游标存储；`Storage:Provider=postgres` 时按配置注册，缺连接串或数据库不可用不会静默回退 file。
  - Red/Green（Owner bootstrap）：新增 `WorkspaceMembershipBootstrapperTests`；服务启动按 `Bootstrap:Memberships` 写入 Workspace 成员，非法身份配置快速失败。
  - Verify：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 78、Infrastructure 81、Desktop 157、Server 23，共 339 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。当前机器无 Docker、psql 或 PostgreSQL 服务，尚未完成真实 PostgreSQL 迁移/并发验收。
  - Red（PostgreSQL 实机验收）：新增 `PostgresBookingAcceptanceTests`（3 项，仅当设置 `ConnectionStrings__Postgres` 环境变量时执行实库验证，否则通过以不阻塞无库回归），在真实 PostgreSQL 环境前无法验证 schema 初始化、排斥约束和重启持久化。
  - Green（PostgreSQL 实机验收）：本机安装 PostgreSQL 18.6；创建 `cccalendar` 账号/数据库并预装 btree_gist 扩展；真实 Server 以 `Storage__Provider=postgres` 启动，schema 初始化成功（rooms、room_bookings、workspace_memberships、workspace_sync_versions、sync_changes 5 表 + 幂等唯一键 + GiST 半开区间排斥约束），`/health` 返回 `storage: postgres`。
  - 验收（PostgreSQL 实机验收）：3/3 通过——双客户端目录共享（A 建房 B 可见）、同时段预约第二个 409、同幂等键重试返回原预约；销毁 Server 实例后用同一连接串重建，原 cursor 之前变更仍存在、已消费 cursor 不重复下发、原预约依然生效（同时段再约 409）。验收后 TRUNCATE 全部测试数据，数据库回到空表状态。
  - Verify：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 78、Infrastructure 81、Desktop 157、Server 26，共 342 项；`dotnet format CcCalendar.sln --no-restore --verify-no-changes` 通过。无 PostgreSQL 环境时验收测试自动跳过（直接通过），不破坏常规回归。
- [x] P15-08 局域网开发令牌登录（方案 A：无 OIDC 也能跨电脑登录）
  - Red：新增 `DevTokenOptionsTests`（6 项：默认关闭、启用必填签名密钥/工作区、密钥≥32 字符、与 OIDC Authority 互斥、角色与有效期解析）和 `DevTokenAuthTests`（4 项端到端：未启用时 404、真实 JwtBearer 下签发的 Bearer 令牌可建房/预约/匿名 401、同名同 ID 不同名不同 ID、空白姓名 400）。
  - Green：新增 `DevTokenOptions`（Authentication:DevToken 配置节，默认关闭）、`DevTokenIssuer`（SHA256 派生确定性 userId + HS256 JWT，sub/name 声明）；Program.cs 在 JwtBearer 挂对称密钥 TokenValidationParameters 并关闭 MapInboundClaims（保留 sub），启用时映射 `POST /api/auth/dev-token`，登录即 Upsert 工作区成员（默认 Admin，可配角色）。
  - Red/Green（客户端）：新增 `DevTokenLoginClient`（Infrastructure，POST api/auth/dev-token 返回 DevTokenLoginResult）、`DevTokenLoginClientTests`（2 项）和 `DevTokenTeamConnectionViewModelTests`（4 项）；`TeamConnectionSettings` 增加 `DevTokenName`；`TeamConnectionViewModel` 在 OIDC 地址为空时走开发令牌登录（校验姓名/服务端地址、令牌存 "dev.tokens"、自动回填工作区 ID）；设置页新增“开发令牌姓名”输入；App.xaml.cs 接线 `DevTokenLoginClient`。
  - Verify：`eng\verify.cmd` 通过（含 P15-09 后续切片的完整回归 377/377）；Server DevToken 10/10、Desktop DevToken/TeamConnection 8/8 通过；部署说明已写入 [LAN_DEPLOYMENT.md](docs/LAN_DEPLOYMENT.md)。
- [ ] P15-09 客户端会议室看板接入 Server（进行中）
  - 目标：RoomBookingPicker 从 Server 拉房间目录和当日占用、提交在线预约，跨电脑互相可见。
  - 子切片 a) 完成：Server 预约区间查询端点 `GET /api/workspaces/{id}/bookings?fromUtc=&toUtc=`（只返回与区间相交的预约）+ 客户端 `IRoomBookingClient.GetBookingsAsync` + `RoomBookingApiClient` 查询实现。
  - 子切片 b) 完成：`TeamRoomBoardService`（Infrastructure）——按本地日期换算 UTC 区间拉取房间目录与远程预约；提交在线预约（未配置/房间不在目录返回 false 静默跳过，服务端 409 抛 HttpRequestException 由 UI 提示）；`TeamRoomBoardServiceTests` 4 项覆盖。
  - 子切片 c) 完成：WPF 看板接线——`TeamRoomBoardSnapshot`/`TeamRoomBoardMapper`（RoomId→房间名映射，停用房间过滤）、`RoomBookingPicker` 团队加载器（日期切换重拉、世代号防陈旧回写、加载失败保持本地视图、`RoomsChanged` 事件）、`QuickAddWindow` 房间下拉跟随团队目录、`TeamRoomBookingSubmissionResolver` 从 QuickAddRequest 解析可提交预约、`MainWindow` 保存日程后同步提交在线预约（冲突弹窗且本地日程保留）、`StoredTokenAccessTokenProvider`（凭据存储直读令牌）与 App.xaml.cs 接线（HttpClient 按服务端地址缓存）。
  - Verify（c 切片）：`eng\verify.cmd` 通过，构建 0 警告/0 错误；Core 80、Infrastructure 88、Desktop 170、Server 39，共 377 项；`dotnet format` verify 通过。
  - 待办 d) 实机跨电脑验收：两台同网段电脑按 [LAN_DEPLOYMENT.md](docs/LAN_DEPLOYMENT.md) 部署——服务器启用 DevToken + PostgreSQL/file 存储，两台客户端分别用姓名登录同一工作区，验证 A 电脑提交预约后 B 电脑看板可见、同时段互约 409、重启后仍可见。
  - 进展 d)（2026-08-22，改走云服务器）：因两地办公不同网段（LAN 直连不通），购买阿里云 ECS e-c1m1.large（2C2G Ubuntu 22.04，公网 47.120.6.126）；`cccalendar-server-linux.zip`（linux-x64 自包含）上传部署，systemd 守护（`/etc/systemd/system/cccalendar.service`，`Restart=always`，DevToken 启用 + SigningKey + WorkspaceId + SharedSecret 环境变量），安全组放行 SSH 22/TCP 5080；公网 `/health` ok；UTF-8 建房 3 间（云会议室A/B/C，乱码数据已清空 `data` 目录重建）；公网验证无口令/错口令登录均 401。**剩余**：两地客户端装 0.3.3，服务端地址 `http://47.120.6.126:5080/` + 团队共享口令（见 docs/CLOUD_OPS_GUIDE.md §1 取值方式）登录（A=张三/B=李四），跑互见/冲突/持久化三场景。
- [x] P15-10 公网 DevToken 安全加固（共享口令）
  - 背景：上公网后“只凭姓名发令牌”会被陌生人冒领，登录端点增加共享口令防线。
  - Red：Server 测试覆盖配置了 SharedSecret 后无口令/错口令登录 401、对口令登录成功；客户端测试覆盖登录请求携带口令字段。
  - Green：`DevTokenOptions.SharedSecret`（≥8 字符校验，配置后登录必带口令）；`DevTokenLoginRequest` 增加 SharedSecret；客户端设置页“开发令牌口令”输入框，登录请求自动携带（未填不发送）。不配置则不校验（内网向后兼容）。
  - 部署：云服务器 systemd 已配置 `Authentication__DevToken__SharedSecret`。
  - Verify：公网实测无口令/错口令 401 ✓（2026-08-22）；含本切片完整回归见 0.3.2/0.3.3 发布记录。
- [x] P16-01 本地数据与团队连接隔离验证
  - 用户诉求：连接团队后原有本地日程不能丢。
  - 验证结论：本地 SQLite 位于 `%LocalAppData%\cccalendar\data\calendar.db`（ApplicationPaths，用户目录而非安装目录），安装包升级覆盖安装不删数据；团队登录/登出只写凭据管理器令牌（OidcTokenStore），TeamRoomBoardService 只拉远程目录/提交远程预约，全程不触碰本地库。无代码改动。
- [x] P16-02 快速新增时间联动（开始时间驱动结束时间）
  - 验收：开始时间选定后（如 14:00），结束时间自动更新为开始后一小时（15:00）；随后仍可手动改结束时间；跨午夜钳制到 23:30。
  - Red：`QuickAddWindowRuntimeTests.SelectingStartTimeMovesEndTimeToOneHourLater` 先因无联动失败（改前 9:00→10:00 不变；实际用例默认 10:00/11:00 起）。
  - Green：`SelectedStartTime` setter 中把 `SelectedEndTime` 同步为 Start+1h（TimeOptions 精确查找；wrap 跨天钳 23:30）。看板拖选路径（PickerPicked 先设 Start 后设 End）最终值不受影响。
  - Refactor：无。
- [x] P16-03 腾讯会议邀请解析与快速新增表单增强
  - 验收：快速新增支持一键解析腾讯会议邀请——主题→标题框（placeholder“输入主题”）、会议号→新增“会议号”输入框、会议时间→日期+半小时对齐的起止时间（14:10-15:10→14:00-15:00）、与会地点→模糊匹配会议室（“佛山西樵研发中心三楼会议室”命中“研发中心三楼会议室”，取最长命中）；保存时会议号附加到标题（`主题（腾讯会议 749-5361-1348）`）；非邀请文本提示且不改字段。
  - Red：`TencentMeetingInvitationParserTests`（7 项：完整解析/无地点解析/非邀请拒绝/包含式模糊匹配/多命中取最长/无命中回空/半小时四舍五入）和 `QuickAddWindowRuntimeTests.ApplyMeetingInvitationFillsFormFieldsAndMatchesRoom`、`ApplyMeetingInvitationReturnsFalseForNonInvitationText` 先因解析器与 ApplyMeetingInvitation 不存在而失败。
  - Green：Core 新增 `TencentMeetingInvitationParser`（会议主题/会议号/会议时间/入会链接/与会地点 5 组正则 + MatchRoom 模糊匹配 + RoundToNearestHalfHour）；QuickAddWindow 新增“粘贴会议邀请”按钮（读剪贴板）、`ApplyMeetingInvitation(text)`（切日程类型、解除全天、填充全部字段）、MeetingNumberBox 输入框、标题 placeholder 样式（VisualBrush 空文本触发）；TryBuildRequests 会议号并入标题。
  - Refactor：本地会议室目录增加“研发中心三楼会议室”（用户实际房间名，模糊匹配目标）；MeetingRoomCatalogTests 同步更新。
  - Verify：`eng\publish.cmd` 退出 0，构建 0 警告/0 错误；Core 87、Infrastructure 88、Desktop 174、Server 42，共 391 项；`dotnet format` verify 通过；版本 0.3.3 发布，安装包 `artifacts\installer\cccalendar-0.3.3-win-x64-setup.exe`，SHA-256 `4263B003B3AF9C2A24319C320014EB35FE829A1AFFE11E488A04C9DE15B41E6C`，发布启动冒烟通过。
- [x] P16-04 快速新增表单重构（字段标签 + 邀请输入框行）
  - 验收：每个输入字段（类型/会议邀请/主题/会议室/会议号/日期/时间）上方有左对齐小字灰色标签；“会议邀请”为独立多行输入框（右端“粘贴会议邀请”按钮）：按钮点击读剪贴板填入输入框，也支持直接在输入框手动粘贴/输入邀请内容——TextChanged 实时解析，识别成功即填充下方字段，非邀请文本不改任何字段；窗口高度 760→840 适配。
  - Red：`QuickAddWindowRuntimeTests.TypingInvitationTextIntoBoxFillsFormFields`（手动输入触发填充 + 非邀请文本不覆盖）。
  - Green：QuickAddWindow.xaml 左列改 StackPanel 布局 + MeetingInvitationBox（AcceptsReturn/Wrap/TextChanged）；code-behind `PasteMeetingClick`（剪贴板→输入框→TextChanged 解析）、`MeetingInvitationTextChanged`（TryParse 成功调 ApplyMeetingInvitation）；移除标题 placeholder（标签已表达）。
- [x] P16-05 桌面组件日程“编辑”误入快速新增（缺陷修复）
  - 现象：桌面组件（工作台/日程组件）打开日程详情点“编辑”，弹出的是快速新增（空表单），而不是编辑窗预填原内容。
  - 根因：`DesktopComponentWindow.EventDetailsClick` 与 `DesktopWorkbenchWindow.EventDetailsClick` 均忽略 `DayScheduleWindow.EditedEventId`，把“编辑”当“新增”路由（主窗口 `ShowScheduleDetails` 是对的）。
  - 修复：两窗口新增 `scheduleEditRequested: Func<Guid, Window, Task>` 构造参数，`EventDetailsClick` 优先处理 `EditedEventId`→弹出 `EventEditWindow`（预填标题/会议室/时间，保存覆盖原日程）；App.xaml.cs 接线 `EditScheduleFromDesktopAsync`→`MainWindow.OpenEventEditAsync`。
  - 说明：模态对话框路由与主窗口同模式，改动小；以桌面组件双路径人工验证（无既有桌面窗口自动化测试基建）。
- [x] P16-06 会议室目录更新（本地 + 云端）
  - 验收：本地 `MeetingRoomCatalog.Rooms` 更新为 9 个真实会议室：项目组二楼会议室、生产组会议室、技术中心二楼会议室、聚英堂会议室、院士办会议室、研发中心三楼会议室、财务部三楼会议室、采购部会议室、雅典学院（原“生产会议室”→“生产组会议室”，新增财务部三楼/采购部/雅典学院）。
  - 云端：已完成重建（2026-08-22 用户清 `data` 目录后，开发机远程 UTF-8 API 建房 9 间，全部 active、中文正常）。
  - Red/Green：MeetingRoomCatalogTests 同步更新目录断言。
- [x] P16-07 看板占用块悬停详情
  - 验收：会议室看板悬停灰色占用块 200ms 显示 ToolTip：会议主题（本地日程标题含腾讯会议号时一并显示）+ 会议室 + 起止时间（如 14:00–15:00）；悬停空闲区域无提示。
  - Red：`RoomBookingPickerRuntimeTests.HoveringOccupiedBlockShowsBookingDetailsToolTip`（STA 实例化 Picker + 本地事件渲染，悬停命中块/空闲格断言 ToolTip）。
  - Green：`RoomOccupiedBlock` 增加 `Start/End`（本地时区 TimeOnly）；`RoomBookingBoardControl.UpdateHoverToolTip`（命中房间列×时间格的占用块→设置 ToolTip，OnMouseMove 拖拽外调用）；Picker 渲染时从日程/团队预约换算本地时间；XAML 设 `ToolTipService.InitialShowDelay=200/ShowDuration=15000`。
- [x] P16-08 云端运维手册文档
  - 产出：[docs/CLOUD_OPS_GUIDE.md](docs/CLOUD_OPS_GUIDE.md)——按当前实际环境（阿里云 47.120.6.126、9 会议室、11 成员）编写的场景化操作手册：环境总览、服务器日常管理（状态/日志/启停/自启验证/远程体检）、运维场景（升级保留数据、备份恢复、新增会议室、查看全部预约、清空重来、改口令）、客户端操作（新电脑安装登录、能力说明、令牌过期、换机迁移、卸载说明）、故障排查对照表、安全边界。
  - API 依据：`GET/POST /api/workspaces/{ws}/rooms`、`GET /api/workspaces/{ws}/bookings?fromUtc=&toUtc=`（Program.cs 已核对）。

## 会话日志

| 日期 | 变更 | 验证 | 下一步 |
| --- | --- | --- | --- |
| 2026-08-16 | 建立需求方案与 TDD worklist | 人工核对需求；必需文档 2/2 存在；进行中任务 1 项；仓库未初始化 Git | 等待用户补充开发规则并完成 P0-03 |
| 2026-08-16 | 确定 .NET 10/WPF 技术栈、UI 规范和交付阶段，初始化 Git | ADR 与 UI 文档存在；Git 分支为 `main`；检测到本机没有 .NET SDK | 安装 SDK 并完成 P1-01 |
| 2026-08-16 | 安装 .NET 10.0.400，创建 3 个生产项目和 3 个测试项目 | restore/build/test 命令退出 0；build 为 0 警告、0 错误；当前无测试用例 | 完成 P1-02 测试与静态检查基线 |
| 2026-08-16 | 建立中央包版本、静态分析、格式检查、覆盖率和首个 TDD 产品名行为 | `eng\verify.cmd` 退出 0；测试 1/1；build 0 警告、0 错误 | 完成 P1-03 配置与敏感信息边界 |
| 2026-08-16 | 实现本地 JSON 设置、密钥存储接口和日志脱敏规则 | `eng\verify.cmd` 退出 0；测试 5/5；build 0 警告、0 错误 | 完成 P1-04 SQLite 迁移与测试夹具 |
| 2026-08-16 | 建立 EF Core 10.0.11、Bootstrap 迁移和真实临时 SQLite 夹具 | `eng\verify.cmd` 退出 0；测试 6/6；无 NuGet 安全告警 | 完成 P1-05 发布与启动验证 |
| 2026-08-16 | 建立自包含发布、启动冒烟和 Inno Setup 安装器 | `eng\publish.cmd` 退出 0；安装器 SHA-256 已记录 | 开始 P2-01 项目领域模型 |
| 2026-08-16 | 实现项目、里程碑、参与人及 SQLite 持久化 | `eng\verify.cmd` 退出 0；测试 18/18；build 0 警告、0 错误 | 开始 P2-02 待办领域模型 |
| 2026-08-16 | 实现待办状态、四象限、子任务进度及 SQLite 持久化 | `eng\verify.cmd` 退出 0；测试 25/25；build 0 警告、0 错误 | 开始 P2-03 日程与时间块 |
| 2026-08-16 | 实现定时/全天/跨天日程、时间块及 SQLite 持久化 | `eng\verify.cmd` 退出 0；测试 32/32；build 0 警告、0 错误 | 开始 P2-04 重复规则 |
| 2026-08-16 | 实现重复规则、Ical.Net 展开、例外/节假日过滤及持久化 | `eng\verify.cmd` 退出 0；测试 40/40；build 0 警告、0 错误 | 开始 P2-05 冲突与可用时间 |
| 2026-08-16 | 实现默认工作模板、空闲区间计算和冲突检测 | `eng\verify.cmd` 退出 0；测试 43/43；build 0 警告、0 错误 | 开始 P2-06 记录与版本 |
| 2026-08-16 | 实现项目记录、追加版本、附件元数据及持久化 | `eng\verify.cmd` 退出 0；测试 48/48；build 0 警告、0 错误 | 开始 P2-07 回收站 |
| 2026-08-16 | 实现根对象软删除、查询过滤、恢复和 30 天保留策略 | `eng\verify.cmd` 退出 0；测试 51/51；build 0 警告、0 错误 | 开始 P2-08 审计与撤销 |
| 2026-08-16 | 实现审计元数据持久化和会话内 LIFO 撤销 | `eng\verify.cmd` 退出 0；测试 54/54；build 0 警告、0 错误 | 开始 P2-09 备份恢复 |
| 2026-08-16 | 实现 SQLite 一致性备份、完整性/版本校验和恢复 | `eng\verify.cmd` 退出 0；测试 55/55；build 0 警告、0 错误 | 开始 P3-01 主导航与今天视图 |
| 2026-08-16 | 实现主导航、今天双栏页面和克制型 UI 主题 | `eng\verify.cmd` 退出 0；测试 57/57；两档窗口截图通过 | 开始 P3-02 日历视图 |
| 2026-08-16 | 实现月/周/日程表日历并修复子视图叠加 | `eng\verify.cmd` 退出 0；测试 60/60；月历截图通过 | 开始 P3-03 项目工作区 |
| 2026-08-16 | 实现项目概览、看板、里程碑、甘特条和待办依赖 | `eng\verify.cmd` 退出 0；测试 63/63；build 0 警告、0 错误 | 开始 P3-04 待办工作区 |
| 2026-08-16 | 实现待办四象限/看板/列表共享状态与拖放 | `eng\verify.cmd` 退出 0；测试 65/65；build 0 警告、0 错误 | 开始 P3-05 记录与搜索 |
| 2026-08-16 | 实现记录搜索、Markdown 编辑工具和版本保存 | `eng\verify.cmd` 退出 0；测试 68/68；build 0 警告、0 错误 | 开始 P3-06 全局搜索与快速新增 |
| 2026-08-16 | 实现全局搜索、真实 SQLite 加载和四类快速新增 | `eng\verify.cmd` 退出 0；测试 70/70；快速新增运行验收通过 | 开始 P3-07 统计与周报 |
| 2026-08-16 | 实现统计指标、7 天趋势和周报摘要 | `eng\verify.cmd` 退出 0；测试 71/71；build 0 警告、0 错误 | 开始 P4-01 托盘与快速面板 |
| 2026-08-16 | 实现托盘、快速面板和关闭驻留 | `eng\verify.cmd` 退出 0；测试 72/72；关闭主窗驻留验收通过 | 开始 P4-02 组合桌面工作台 |
| 2026-08-16 | 实现组合桌面月历/日程/待办窗口与托盘显隐 | `eng\verify.cmd` 退出 0；测试 73/73；工作台截图通过 | 开始 P4-03 独立桌面组件 |
| 2026-08-16 | 实现三种独立桌面组件与托盘子菜单 | `eng\verify.cmd` 退出 0；测试 76/76；build 0 警告、0 错误 | 开始 P4-04 周/月与尺寸记忆 |
| 2026-08-16 | 实现桌面周/月切换与两套尺寸持久化 | `eng\verify.cmd` 退出 0；测试 77/77；运行态尺寸切换通过 | 开始 P4-05 桌面窗口行为 |
| 2026-08-16 | 实现四桌面窗口锁定、穿透、层级与贴边隐藏 | `eng\verify.cmd` 退出 0；测试 83/83；Win32 运行态验收通过 | 开始 P4-06 桌面外观设置 |
| 2026-08-16 | 实现四桌面窗口独立外观、细分颜色与 DWM 材质 | `eng\verify.cmd` 退出 0；测试 86/86；三张视觉截图与 DWM 属性通过 | 开始 P4-07 全局快捷键 |
| 2026-08-16 | 实现五个可配置全局快捷键与两层冲突检测 | `eng\verify.cmd` 退出 0；测试 90/90；系统热键与重绑定运行态通过 | 开始 P5-01 后台提醒调度 |
| 2026-08-16 | 实现持久化提醒投递、错过补发与后台周期扫描 | `eng\verify.cmd` 退出 0；测试 95/95；SQLite 时间推进通过 | 开始 P5-02 通知与勿扰 |
| 2026-08-16 | 实现三通道提醒、延后/完成和跨午夜/全屏勿扰 | `eng\verify.cmd` 退出 0；测试 104/104；隔离运行与两张视觉截图通过 | 开始 P5-03 中国日历数据 |
| 2026-08-16 | 接入农历/节气/节假日/调班并展示到全部日历 | `eng\verify.cmd` 退出 0；测试 109/109；三类日历视觉验收通过 | 开始 P5-04 ICS 文件交换 |
| 2026-08-16 | 实现 ICS 导入导出、标准文件导入与重复日程往返一致性 | `eng\verify.cmd` 退出 0；测试 111/111；最小窗口日历页截图通过 | 跳过延期 P5-05，开始 P5-06 天气与城市定位 |
| 2026-08-16 | 实现 Windows 自动定位、手动城市及 Open-Meteo 当前/小时/7 日预报 | `eng\verify.cmd` 退出 0；测试 114/114；真实天气主窗口和快速面板截图通过 | 开始 P6-01 模型配置、凭据与连接测试 |
| 2026-08-16 | 实现 OpenAI 兼容/Ollama 配置、Windows 凭据存储与连接测试 | `eng\verify.cmd` 退出 0；测试 116/116；凭据临时往返和 AI 设置页截图通过 | 开始 P6-02 受控只读查询与最小上下文 |
| 2026-08-16 | 实现四类受控只读 AI 查询工具和无数据库快照的最小上下文 | `eng\verify.cmd` 退出 0；测试 118/118；SQLite 查询投影与无关正文隔离通过 | 开始 P6-03 新建预览与确认 |
| 2026-08-16 | 实现 AI 三类结构化创建草案、预览、单次确认与审计 | `eng\verify.cmd` 退出 0；测试 120/120；预览零写入和三类确认通过 | 开始 P6-04 修改、批量、删除确认与撤销 |
| 2026-08-16 | 实现 AI 待办修改、批量软删除、逐项审计与整批撤销 | `eng\verify.cmd` 退出 0；测试 122/122；三项批量确认和恢复通过 | 开始 P6-05 冲突、空闲时间与任务拆解 |
| 2026-08-16 | 实现 AI 冲突询问、工作时段空闲槽和任务拆解确认 | `eng\verify.cmd` 退出 0；测试 124/124；冲突三选项、08:00 首槽和拆解审计通过 | 开始 P6-06 总结、每日计划与周报草稿 |
| 2026-08-16 | 实现范围受控的项目总结、每日计划和周报 Markdown 草稿 | `eng\verify.cmd` 退出 0；测试 126/126；三类草稿范围隔离与助理命令通过 | 开始 P6-07 Ollama 与仅本地隐私模式 |
| 2026-08-16 | 实现 Ollama/OpenAI 对话、只读工具循环和仅本地隐私模式 | `eng\verify.cmd` 退出 0；测试 130/130；本地工具循环与两张 AI 视觉截图通过 | 开始 P7-01 倒计时、番茄钟与专注统计 |
| 2026-08-16 | 实现倒计时、番茄钟、专注会话迁移与统计展示 | `eng\verify.cmd` 退出 0；测试 134/134；迁移启动和工具页截图通过 | 开始 P7-02 时间进度、日期计算与世界时钟 |
| 2026-08-16 | 实现时间进度、日期计算和上海/伦敦/纽约世界时钟 | `eng\verify.cmd` 退出 0；测试 136/136；三栏时间工具页截图通过 | 开始 P7-03 整点报时与久坐提醒 |
| 2026-08-16 | 实现整点报时、久坐提醒、托盘/声音通知与设置 | `eng\verify.cmd` 退出 0；测试 137/137；工具页滚动布局通过 | 开始 P7-04 截图与标注 |
| 2026-08-16 | 实现区域/窗口/全屏截图与 InkCanvas 标注、复制、保存 | `eng\verify.cmd` 退出 0；测试 138/138；窗口位图运行验收通过 | 开始 P7-05 长截图、OCR、马赛克与贴屏 |
| 2026-08-16 | 实现长截图拼接、拖拽马赛克、Windows OCR 与截图贴屏 | `eng\verify.cmd` 退出 0；测试 139/139；OCR 运行验收返回 241 字符 | 开始 P7-06 任意窗口置顶与图片贴屏 |
| 2026-08-16 | 实现外部窗口枚举/置顶切换与本地图片贴屏 | `eng\verify.cmd` 退出 0；测试 140/140；窗口筛选与置顶切换通过 | 开始 P7-07 剪贴板历史、搜索与收藏 |
| 2026-08-16 | 实现本地剪贴板历史、自动采集、搜索、收藏和三类载荷写回 | `eng\verify.cmd` 退出 0；测试 143/143；隔离运行捕获和工具页截图通过 | 非延期范围全部完成；P8 保持延期 |
| 2026-08-16 | 补齐指定日期日程、日历格事件、跨窗口刷新和隐藏启动 | `eng\verify.cmd` 退出 0；测试 147/147；日程三处即时刷新和隐藏启动运行验收通过 | 非延期范围全部完成；P8 保持延期 |
| 2026-08-17 | 拆分日历/日程/待办三组件，增加右键显隐、独立布局记忆和 0–100% 透明度 | `eng\verify.cmd` 退出 0；测试 148/148；三窗口桌面截图与菜单验收通过 | 非延期范围全部完成；P8 保持延期 |
| 2026-08-17 | 增加日历顶部居中/自动隐藏、4px 感应展开和显式宽高控件 | `eng\verify.cmd` 退出 0；测试 153/153；顶部收起/展开与缩放标记截图通过 | 非延期范围全部完成；P8 保持延期 |
| 2026-08-17 | 增加分钟级日程时间、截图/贴屏缩放、Anthropic Messages 与 OpenAI Responses API | `eng\publish.cmd` 退出 0；测试 159/159；隐藏启动冒烟和安装包生成通过 | 非延期范围全部完成；P8 保持延期 |
| 2026-08-17 | 修复桌面组件右键崩溃，增强月格日程条并增加当日完整详情窗 | `eng\publish.cmd` 退出 0；测试 162/162；隔离实例右键切换、快速新增和日程详情验收通过 | 非延期范围全部完成；P8 保持延期 |
| 2026-08-17 | 恢复半小时下拉并支持分钟手输，补充 AI 端点粘贴，完善当前位置名称和佛山搜索排序 | `eng\publish.cmd` 退出 0；测试 166/166；四项隔离运行验收与隐藏启动冒烟通过；安装包 SHA-256 已记录 | 非延期范围全部完成；P8 保持延期 |
| 2026-08-17 | 城市搜索过滤同名村镇，统一中国行政区全称，增加 Enter 搜索和结果自动展开 | `eng\publish.cmd` 退出 0；测试 166/166；隔离实例 Enter 搜索、自动展开和完整显示验收通过；安装包 SHA-256 已记录 | 非延期范围全部完成；P8 保持延期 |
| 2026-08-17 | 日程组件增加本地时间和窄宽自动换行；小时天气改为当前整点、现在实况和未来 24 小时槽 | `eng\publish.cmd` 退出 0；测试 168/168；日程和天气隔离运行截图、隐藏启动冒烟通过；安装包 SHA-256 已记录 | 非延期范围全部完成；P8 保持延期 |
| 2026-08-17 | AI 助手增加只读/写入确认模式、多模型配置与对话切换、四协议真实流式输出 | `eng\publish.cmd` 退出 0；测试 198/198；隔离 UI、隐藏启动冒烟和安装器编译通过；SHA-256 已记录 | 非延期范围全部完成；P8 保持延期 |
| 2026-08-17 | 统一 WPF 控件主题并重排今天、日历、工具、规划与 AI 重点工作区 | `eng\publish.cmd` 退出 0；测试 206/206；四档窗口/屏幕截图无重叠；新安装包与 SHA-256 已记录 | 非延期范围全部完成；P8 保持延期 |
| 2026-08-19 | 修复 AI 查询 LINQ 报错；日程支持会议室地点、查看/编辑与时间条预览；新增会议室视图；待办输入即新增、点击完成/重开 | `eng\verify.cmd` 退出 0；测试 244/244（Core 66、Desktop 121、Infrastructure 57）；format check、build 0 警告/0 错误 | P9 全部完成；等待用户后续会议室增减或新反馈 |
| 2026-08-19 | 快速新增支持按周重复（周一~周日勾选，按所选周展开多条创建）；修复全天日程地点丢失 | `eng\publish.cmd` 退出 0；测试 248/248（Core 66、Desktop 125、Infrastructure 57）；新安装包 SHA-256 `823E97C2…CCDC7` | P10 全部完成 |
| 2026-08-19 | 修复 ComboBox 下拉与 AI 400（错误体回显 + DeepSeek reasoning_content 剥离）；新增会议室预订板（24h 刻度/半小时点选拖选/绿红灰三态/四角调节点）并集成到快速新增与日程编辑；配置 DeepSeek `deepseek-v4-flash` 凭据 | `eng\publish.cmd` 退出 0；测试 269/269（Core 66、Desktop 143、Infrastructure 60）；真实 DeepSeek 接口 200；安装包 0.2.0 SHA-256 `0352FF3C…04C2` | P11 全部完成 |
| 2026-08-19 | 补丁 0.2.1：AI 系统指令注入当前本地日期+周几+「相对日期直接推算不反问」规则（`MinimalAiContextBuilder` + `ProviderAiAssistantClient` TimeProvider），修复"本周一到周四"被反问 | `eng\publish.cmd` 退出 0；测试 272/272（Core 68、Desktop 143、Infrastructure 61）；安装包 0.2.1 SHA-256 `91131665…7E1C8` | P11 收尾 |
| 2026-08-19 | 补丁 0.2.2：实测定位 DeepSeek 思考模式 400 根因（带 tool_calls 的 assistant 消息必须回传 reasoning_content），流式收集/非流式保留两路修复 + 撤销 0.2.0 错误剥离；AI 输入框 Enter 发送、Shift+Enter 换行；待办四象限/看板移动接入持久化（`MoveTodoToQuadrantAsync`/`MoveTodoToStatusAsync`）+ 卡片右键菜单快速移动；新增 `HorizontalScroll` 附加行为（看板/天气滚轮横滚、天气按住拖拽右拉=往后看） | `eng\publish.cmd` 退出 0；测试 275/275（Core 68、Desktop 143、Infrastructure 64）；真实 DeepSeek 接口 4 并行 tool_calls 回传 200；安装包 0.2.2 SHA-256 `582CCBF7…438F8` | P11 修复批次 |
| 2026-08-19 | 补丁 0.2.3：四象限/看板 Drop 目标补 `DragOver` 设 Move 效果（根因：WPF 默认"禁止"光标使 Drop 从不触发）；天气改为前 1 小时+现在+后 24 小时窗口（26 格，过滤已过去时段）、格子加透明背景保证命中、拖拽改按下即 CaptureMouse；新增 `AiChatHistoryStore` JSON 持久化（最近 200 条，重启恢复）+ 消息列表自动滚底；快速新增改左右布局（左表单/中 GridSplitter 可拖宽/右会议室看板）+ 半小时格高度 26→13 | `eng\publish.cmd` 退出 0；测试 276/276（Core 68、Desktop 144、Infrastructure 64）；安装包 0.2.3 SHA-256 `998B08E9…4D31E3` | P12 交互修复批次 |
| 2026-08-19 | 补丁 0.2.4：快速新增默认类型固定「日程」修复"不显示日期"（右上角按钮入口默认待办导致字段隐藏）；看板 `FirstVisibleCell=16` 只渲染/命中 8:00–24:00 且刻度同步 8–23 点、网格底 #FFFFFF、无 Ctrl 新选区替换旧选区/Ctrl 累加 | `eng\publish.cmd` 退出 0；测试 276/276（Core 68、Desktop 144、Infrastructure 64）；安装包 0.2.4 SHA-256 `C0683C3C…8D5F0` | P12 交互修复批次 |
| 2026-08-19 | 补丁 0.2.5：STA 运行时测试真实复现"日程类型日期不显示"——根因是 DatePicker 模板 PART_TextBox 用普通 TextBox，控件内部 `as DatePickerTextBox` 得 null；修复为 DatePickerTextBox + BasedOn TextBox 派生样式；InternalsVisibleTo 开放测试程序集 | `eng\publish.cmd` 退出 0；测试 277/277（Core 68、Desktop 145、Infrastructure 64）；安装包 0.2.5 SHA-256 `5B3247C7…8ACFF` | P12 交互修复批次 |
| 2026-08-19 | 补丁 0.2.6：工具轮 400 自愈重试（真实 key+真实管线复现失败——读/写模式、4 并行工具、多轮回传全 200，判定用户另一台机器为环境差异；`PostToolRoundAsync`/`PostStreamToolRoundAsync` 在工具轮回传遇 400 时自动降级去 reasoning_content/content 重试一次）；AI 助手多会话：`AssistantChatSession` + 会话存储 `ai-chat-sessions.json`（旧文件自动迁移）、新建/删除/切换命令、标题自动派生、AssistantView 会话栏 UI | `eng\publish.cmd` 退出 0；测试 279/279（Core 68、Desktop 146、Infrastructure 65）；安装包 0.2.6 SHA-256 `DE5A391F…3EF24` | P13 AI 会话与兼容性批次 |
| 2026-08-20 | P14 桌面交互与看板修复：看板列宽 125+半小时网格线；重复下拉（默认不重复）+周几勾选+日期范围；双击非当天（DragMove 吞双击修复）；今日日程/待办拉通；四象限拖动（外层 ScrollViewer 折叠 + 根级 PreviewMouseMove）；桌面组件右键新增；看板 8:00-10:00 不可选（垂直居中错位修复）；`WpfRuntimeHost` STA 测试基建；UI 自动化实测 4 项全 PASS | build 0 警告/0 错误；测试 288/288（Core 68、Desktop 155、Infrastructure 65）；UI 实测：双击非当天/四象限拖动/拖动持久化/看板 8:00 点击均 PASS | P14 全部完成 |
| 2026-08-20 | 窗口标题栏/边框粉色调查：确认根因为 Windows 11 系统强调色染色（非应用代码问题）；曾按用户要求实现颜色固定并发布 0.2.7，用户认可根因后整体撤销，删除 0.2.7 安装包 | 测试 288/288（撤销后回归） | 保持系统默认行为 |
| 2026-08-20 | 版本 0.2.8 发布：P14 全部修复（看板列宽/网格线/8:00-10:00 可选、重复下拉+周几+日期范围、双击任意日期新增、今日日程/待办拉通、四象限拖动+持久化、桌面组件右键新增），不含已撤销的颜色固定 | `eng\publish.cmd` 退出 0；安装包 0.2.8 SHA-256 `CD1C05F6…0EF779` | 0.2.8 发布完成 |
| 2026-08-20 | 确认联网协作方案：本地 SQLite + HTTPS API + SignalR，团队模式使用 OIDC，会议室预约由服务端事务判定冲突；新增 `docs/SYNC_DESIGN.md` 并将 M6 转为 P15 实施 | 文档核对完成；未改运行代码 | 完成 P15-01 本地同步元数据 |
| 2026-08-20 | P15-01/02：新增同步元数据与 Outbox 迁移；新增 Room/RoomBooking 领域模型和半开区间冲突检测 | Core/Infrastructure 测试通过；完整回归 294/294 | 完成 P15-03 局域网 API |
| 2026-08-20 | P15-03/04：新增独立 ASP.NET Core Server、预约 201/409/幂等 API、房间目录路由、HttpClient 适配器和目录缓存 | 完整回归 302/302；format verify 通过 | 开始 P15-05 OIDC 登录与权限 |
| 2026-08-20 | P15-05 首个身份边界：Server 使用 JWT Bearer/OIDC 配置，预约接口要求认证并校验 `sub` 与预约人一致；Core 固化 Viewer/Member/Admin/Owner 权限规则 | Server 6/6；完整回归 309/309；format verify 通过 | 接入工作区成员存储与角色授权 |
| 2026-08-20 | P15-05 客户端令牌注入：`RoomBookingApiClient` 通过可替换 `IAccessTokenProvider` 为目录、预约和取消请求添加 Bearer，不把令牌写入配置 | HTTP 客户端 5/5；`eng\verify.cmd` 通过；完整回归 310/310 | 接入工作区成员存储与角色授权 |
| 2026-08-20 | P15-05 工作区成员授权：新增线程安全成员存储和授权服务；Viewer 只读房间目录，Member 可预约，Admin/Owner 可创建房间；无成员和跨工作区访问返回 403 | `eng\verify.cmd` 通过；完整回归 314/314；format verify 通过 | 服务端房间目录与预约持久化 |
| 2026-08-20 | P15-05 服务端持久化：新增房间/预约 JSON 原子文件存储与重启往返测试，生产默认按 `Storage:DataDirectory` 保存，测试继续使用内存替身 | `eng\verify.cmd` 通过；完整回归 316/316；format verify 通过 | WPF OIDC/PKCE 登录和凭据生命周期 |
| 2026-08-20 | P15-05 PKCE/token 生命周期：新增 S256 授权 URL、授权码交换、refresh token 交换和凭据存储驱动的 access token provider | `eng\verify.cmd` 通过；完整回归 319/319；format verify 通过 | WPF 回环回调、浏览器登录接入 |
| 2026-08-20 | P15-05 回环登录：新增 TCP loopback callback、state/code/error 校验、系统浏览器启动器和 `OidcLoginClient`，不把回调 code 写入日志 | `eng\verify.cmd` 通过；完整回归 323/323；format verify 通过 | 将 OIDC 登录入口接入 WPF 设置/工作区状态 |
| 2026-08-20 | P15-05 WPF 接入：新增团队连接设置模型、登录/退出 ViewModel 和设置页入口；登录成功 token 写入凭据存储，普通 JSON 配置不含 token | `eng\verify.cmd` 通过；完整回归 325/325；format verify 通过 | P15-06 SignalR 通知、游标增量同步和断线恢复 |
| 2026-08-20 | P15-06 SignalR 通知：新增鉴权 SyncHub、按工作区分组、Core 变更契约、服务端 notifier 和自动重连客户端；匿名 negotiate 返回 401 | `eng\verify.cmd` 通过；完整回归 326/326；format verify 通过 | P15-06 游标增量 API、断线补齐和客户端状态 |
| 2026-08-20 | P15-06 游标增量：新增按工作区递增变更存储、`GET /api/workspaces/{workspaceId}/sync?cursor=` 和 `SyncApiClient`，SignalR 通知后可按 cursor 补齐 | `eng\verify.cmd` 通过；完整回归 328/328；format verify 通过 | 断线恢复后台任务与客户端同步状态 |
| 2026-08-20 | P15-06 断线恢复：新增串行 `SyncCoordinator`、cursor 存储接口和 `Faulted`/`Online` 状态；应用失败不推进 cursor，重试可继续 | `eng\verify.cmd` 通过；完整回归 330/330；format verify 通过 | P15-07 双客户端联网验收与发布配置 |
| 2026-08-20 | P15-07 双客户端联网验收：两个独立客户端共享房间目录；A 创建预约后 B 对同一时段预约返回 409；补充 Server 默认监听和存储配置 | `eng\verify.cmd` 通过；完整回归 331/331；format verify 通过 | P15-07 PostgreSQL/HTTPS 部署适配和真实 OIDC 验收 |
| 2026-08-20 | P15-07 收尾验证：团队登录 ViewModel 对取消、无效配置和网络异常显示状态而不冒泡；清单顶部检查点同步到生产部署适配 | `eng\verify.cmd` 通过；完整回归 331/331；format verify 通过 | P15-07 PostgreSQL/HTTPS 部署适配和真实 OIDC 验收 |
| 2026-08-20 | P15-07 LAN 诊断：新增匿名 `/health` 和 [LAN_DEPLOYMENT.md](docs/LAN_DEPLOYMENT.md)，明确当前双客户端测试是同机测试，真实跨电脑需按局域网步骤验收 | `eng\verify.cmd` 通过；完整回归 332/332；format verify 通过 | P15-07 PostgreSQL/HTTPS 部署适配和真实 OIDC 验收 |
| 2026-08-20 | P15-07 部署配置边界：集中 storage provider/连接串/HTTPS 开关，生产选择 postgres 时不允许缺连接串或静默回退 JSON | `eng\verify.cmd` 通过；完整回归 335/335；format verify 通过 | PostgreSQL 事务存储（房间、预约、成员和变更游标） |
| 2026-08-20 | P15-07 PostgreSQL provider：新增 Npgsql schema、数据库级冲突约束、房间/预约/成员/同步游标存储和 Owner bootstrap；未检测到本机 PostgreSQL，因此保留实机验收待办 | `eng\verify.cmd` 通过；完整回归 339/339；format verify 通过 | PostgreSQL 实机迁移/并发验收（需 Docker 或 PostgreSQL 环境） |
| 2026-08-21 | P15-07 PostgreSQL 实机验收：本机 PostgreSQL 18.6 上创建 cccalendar 数据库/账号/btree_gist，真实 Server postgres 模式 schema 初始化通过；新增 `PostgresBookingAcceptanceTests` 实库验收（health/双客户端 409/幂等重试/重启持久化），验收通过后清空测试数据 | 实机验收 3/3；`eng\verify.cmd` 通过；完整回归 342/342；format verify 通过 | 跨电脑真实局域网验收（两台同网段电脑按 LAN_DEPLOYMENT.md）；生产 HTTPS/OIDC 部署方案 |
| 2026-08-21 | 版本 0.3.0 发布：首个含 P15 团队协作的安装包（团队连接设置页、PKCE 登录、令牌只存 Windows 凭据管理器、会议室在线预约客户端栈） | `eng\publish.cmd` 退出 0；完整回归 342/342；发布启动冒烟通过；安装包 0.3.0 SHA-256 `33480510CC38D1814386B795DEA39149D355BC3C51A68535F47AC0E975D07A7D` | 跨电脑真实局域网验收；生产 HTTPS/OIDC 部署方案 |
| 2026-08-21 | P15-09 a/b：Server 预约区间查询端点 + 客户端 `GetBookingsAsync` + `TeamRoomBoardService`（按天拉取房间目录/远程预约、提交在线预约）；P15-09 c：WPF 看板接线（`RoomBookingPicker` 团队加载器/世代号防陈旧/失败保持本地、`QuickAddWindow` 房间下拉跟随团队目录、`MainWindow` 保存日程后同步提交在线预约并冲突弹窗、`StoredTokenAccessTokenProvider`、App 接线）；修复 `TeamRoomBoardService` 残留 `gate` 引用与测试字段名 | `eng\verify.cmd` 通过（format verify、build 0 警告/0 错误）；完整回归 377/377（Core 80、Infrastructure 88、Desktop 170、Server 39） | P15-09d 实机跨电脑验收（两台电脑按 LAN_DEPLOYMENT.md：DevToken 登录同一工作区，看板互见 + 409 + 重启持久化） |
| 2026-08-21 | 版本 0.3.1 发布：含局域网开发令牌登录（无 OIDC 跨电脑登录）与团队会议室看板接入 Server（房间列/占用块/在线提交） | `eng\publish.cmd` 退出 0；发布启动冒烟通过；安装包 0.3.1 SHA-256 `20B4CC2E8A4A8828F1B9622F149E2F15431D1DC01FDABD6304C249808E857060` | P15-09d 实机跨电脑验收；生产 HTTPS/OIDC 部署方案 |
| 2026-08-21 | P15-09d 实机验收尝试（A=192.168.1.88 有线，B=WiFi 192.168.77.x）：两台电脑不同网段互不可达，局域网方案受阻；用户决策改为云服务器部署（异地办公）；期间发现 PowerShell 5.1 建房中文变问号，已改用 UTF-8 字节发送并在文档修正 | B 端 `Test-NetConnection` 失败（网络不可达）；服务器代码无改动 | 购买 2C2G Linux 轻量云服务器（Ubuntu 22.04），按 LAN_DEPLOYMENT.md 云端部署章节执行 |
| 2026-08-21 | P15-10 DevToken 共享口令加固（公网防冒领）：Server `DevTokenOptions.SharedSecret`（≥8 字符，空则不校验）+ 端点 401 校验；Client `DevTokenLoginClient` 带 sharedSecret（null 不发送字段）、`TeamConnectionSettings.DevTokenSharedSecret`、设置页"开发令牌口令"输入框；验证 linux-x64 自包含发布可用并产出 `artifacts\cccalendar-server-linux.zip`；LAN_DEPLOYMENT.md 新增云服务器部署章节（systemd + 安全组 + UTF-8 建房） | `eng\verify.cmd` 通过；完整回归 381/381（Server 42 含口令 3 项、Desktop DevToken 7/7）；linux-x64 publish 退出 0 | 版本 0.3.2 发布后：购买云服务器按文档部署，两地客户端口令登录完成 P15-09d 云端验收 |
| 2026-08-21 | 版本 0.3.2 发布：DevToken 共享口令（公网部署安全带）+ 云服务器部署支持（linux-x64 自包含发布产物 `artifacts\cccalendar-server-linux.zip`） | `eng\publish.cmd` 退出 0；发布启动冒烟通过；安装包 0.3.2 SHA-256 `E54BB92CBC43E5A3E60A2D13E11C8AF0C02E475EBF172F88D3B2CAD28AEAEBEE` | 用户购买 2C2G Ubuntu 云服务器后按 [LAN_DEPLOYMENT.md](docs/LAN_DEPLOYMENT.md) 云端部署章节执行，两地客户端口令登录完成 P15-09d；长期需 HTTPS + OIDC |
| 2026-08-22 | 云服务器部署完成：阿里云 ECS e-c1m1.large（2C2G Ubuntu 22.04，47.120.6.126）；SSH 密钥改密码登录、上传 `cccalendar-server-linux.zip`、systemd 守护 + 安全组放行 5080；修复两处部署问题（安全组未放行 5080、早前建房编码乱码清 `data` 目录重建）；从开发机公网建房（云会议室A/B/C）并验证口令防线（无口令/错口令 401） | 公网 `Test-NetConnection 5080` 通；`/health` ok；房间目录 3 间中文正常；冒充登录被拒 | P15-09d 两地客户端实测（0.3.3 + 张三/李四 + 口令登录跑三场景） |
| 2026-08-22 | P16-01/02/03：本地数据与团队连接隔离验证（AppData 库 + 令牌分离，无代码改动）；快速新增开始时间联动结束+1h；腾讯会议邀请一键解析（`TencentMeetingInvitationParser`：主题/会议号/时间/地点 5 组正则 + MatchRoom 模糊匹配 + 半小时对齐）+ "粘贴会议邀请"按钮 + 会议号输入框 + 标题 placeholder"输入主题" + 保存时会议号并入标题；本地目录增"研发中心三楼会议室" | `eng\publish.cmd` 退出 0；完整回归 391/391（Core 87、Infrastructure 88、Desktop 174、Server 42）；format verify 通过；发布启动冒烟通过 | P15-09d 云端实机验收（用户两地装 0.3.3 实测）；验收后收 P15-09 进入生产 HTTPS/OIDC |
| 2026-08-22 | 版本 0.3.3 发布：快速新增时间联动 + 腾讯会议邀请解析填充 + 会议号输入框 + 会议室模糊匹配 | `eng\publish.cmd` 退出 0；安装包 0.3.3 SHA-256 `4263B003B3AF9C2A24319C320014EB35FE829A1AFFE11E488A04C9DE15B41E6C` | 两地客户端 0.3.3 云端验收 |
| 2026-08-22 | P16-04~07：快速新增表单重构（字段上方小标签 + 会议邀请输入框行，按钮粘贴/手动输入均实时解析）；桌面组件日程“编辑”误入快速新增缺陷修复（DesktopComponentWindow/DesktopWorkbenchWindow 忽略 EditedEventId，新增 scheduleEditRequested 路由到 EventEditWindow 预填覆盖）；本地会议室目录更新为 9 个真实会议室；看板占用块悬停 ToolTip 显示主题/会议室/起止时间（RoomOccupiedBlock 加时间字段 + UpdateHoverToolTip）；LAN_DEPLOYMENT.md 建房命令同步 9 会议室 + 口令字段 | 完整回归 393/393（Core 87、Server 42、Desktop 176、Infrastructure 88）；format verify 通过；发布启动冒烟通过 | 用户服务器清 data 后远程重建 9 会议室；两地 0.3.4 云端验收 |
| 2026-08-22 | 版本 0.3.4 发布：快速新增表单重构 + 桌面组件编辑修复 + 9 会议室目录 + 看板悬停详情 | `eng\publish.cmd` 退出 0；安装包 0.3.4 SHA-256 `AB8CA4C6E3BB57B8844993AD32CFC73AA7429DF3BFABDCA22859C968E2F43574` | 云端房间重建 + P15-09d 验收 |
| 2026-08-23 | 手动检查更新增加最新/可用/失败反馈；助理输入区改为圆角输入容器并增加“思考/深度搜索”操作行；接入 supplied Material Symbols 下载图标资源 | `eng\verify.cmd` 退出 0；完整回归 410/410（Core 88、Infrastructure 92、Desktop 188、Server 42）；build 0 警告、0 错误；0.3.10 已上传 OSS，公网清单与安装包 HEAD 均通过 | 进入用户安装验收 |
| 2026-08-23 | 版本 0.4.1 安装包构建并上传 OSS：统一客户端/安装器版本号，发布 Windows x64 自包含安装包；新增 OSS 发布手册与 Team 开发者手册 | `eng\publish.cmd` 退出 0；完整回归 411/411（Core 88、Infrastructure 93、Desktop 188、Server 42）；build 0 警告、0 错误；SHA-256 `96539CA0AE9461658D49DA81FA06F33D2B9AA3A35BD2A04133A8CF76EDF3FE25`；公网清单返回 0.4.1，安装包 HEAD 200 | 新成员按 [TEAM_MEMBER_GUIDE.md](docs/TEAM_MEMBER_GUIDE.md) 登录验收 |
| 2026-08-23 | 修复项目/记录页面按钮无响应：项目/记录新增按钮接入快速新增窗口；记录“保存版本”新增数据库持久化版本写入；助理历史去重补充时间戳窗口；提醒链路核查完成 | `eng\verify.cmd` 退出 0；完整回归 419/419（Core 88、Infrastructure 94、Desktop 195、Server 42）；build 0 警告、0 错误 | 天气预报仍以 Open-Meteo WMO 代码为来源；提醒通道已运行但普通日程尚无创建提醒入口 |
| 2026-08-23 | 版本 0.4.6 发布：包含项目/记录按钮与记录版本持久化修复、助理历史去重修复 | `eng\publish.cmd` 退出 0；完整回归 419/419；安装器编译 0 警告、0 错误；SHA-256 `A6097D2558FA8F38F90B9F3CBE7A1A48BE5B0D61A7D061437F6D0535FC43260C`；OSS 清单和安装包公网 HTTP 200 | 等待用户安装验收 |
| 2026-08-23 | 修复助理重复用户消息与跳跃滚动；项目支持删除并移入回收站；快速新增的会议邀请仅日程显示；新增项目/记录操作回归测试 | `eng\verify.cmd` 退出 0；完整回归 422/422（Core 88、Infrastructure 95、Desktop 197、Server 42）；build 0 警告、0 错误 | 待用户验收记录列表刷新与助理拖动体验 |
| 2026-08-23 | 记录左侧列表增加标题、版本号显示，保存版本和新增记录有明确可见反馈 | `eng\verify.cmd` 退出 0；完整回归 422/422；build 0 警告、0 错误 | 待用户验收 |
| 2026-08-23 | 版本 0.4.7 发布：包含助理去重与平滑滚动、项目删除、记录列表版本反馈、快速新增类型条件显示 | `eng\publish.cmd` 退出 0；完整回归 422/422；安装器编译 0 警告、0 错误；SHA-256 `57DDBC94A3D503C8F5B1FBEB9CD79D4B2606624F85DBB644D4701E1AE6541E4A`；OSS 清单与安装包公网 HTTP 200 | 等待用户安装验收 |
| 2026-08-23 | 修复助理滚轮灵敏度；记录页增加“搜索记录”标签与保存状态反馈（已保存/内容未变化/保存失败），保存接口返回变更结果 | `eng\verify.cmd` 退出 0；完整回归 422/422；build 0 警告、0 错误 | 待用户验收滚轮与记录保存反馈 |
| 2026-08-23 | 版本 0.4.8 发布：包含助理滚轮、记录保存状态和搜索框标注修复 | `eng\publish.cmd` 退出 0；完整回归 422/422；安装器编译 0 警告、0 错误；SHA-256 `13B642DDAD209872E983024BEBF356519A8F294698E2A2ED36A74CD82B0A3C53`；OSS 清单与安装包公网 HTTP 200 | 等待用户安装验收 |
| 2026-08-23 | 新增面向最终用户的中文使用手册，覆盖安装、日历、快速新增、会议室、项目、记录、AI、天气、提醒、更新和故障排查 | 文档已写入 [docs/USER_GUIDE.md](docs/USER_GUIDE.md) | 供最终用户查看 |
| 2026-08-23 | 工具中心支持自定义倒计时、番茄钟专注/休息时长；倒计时和专注完成增加托盘通知与系统提示音；默认稍后提醒改为可输入分钟数；用户手册补充工具和提醒说明 | `eng\verify.cmd` 退出 0；完整回归 425/425（Core 88、Infrastructure 95、Desktop 200、Server 42）；build 0 警告、0 错误 | 待用户验收 |
| 2026-08-23 | 版本 0.4.9 发布：包含自定义计时、计时完成通知和可输入稍后提醒时长 | `eng\publish.cmd` 退出 0；完整回归 425/425；安装器编译 0 警告、0 错误；SHA-256 `6662E333AAA7A5292A1C83EFEA762A73370F98C5490EA1073C5218B6887CD937`；OSS 清单与安装包公网 HTTP 200 | 等待用户安装验收 |
| 2026-08-23 | 会议日程增加独立的可选提前提醒：快速新增/编辑可关闭或设置 1–1440 分钟；持久化 `ReminderLeadMinutes`，创建/修改/删除日程时同步创建或清理提醒；明确“默认稍后提醒”仅用于弹窗内点击稍后后的延后时间 | `eng\verify.cmd` 退出 0；完整回归 429/429；新增会议提醒创建、关闭清理、窗口交互与 UI 合约测试通过 | 待用户验收会议提醒交互；发布版本待用户确认 |
| 2026-08-23 | 版本 0.4.10 发布：递增桌面项目、安装器和用户手册版本标记，重新生成 Windows x64 自包含安装包并上传 OSS | `eng\publish.cmd` 退出 0；完整回归 425/425；安装器编译 0 警告、0 错误；SHA-256 `B9F52B092AC0CEE6BD90F034333DA5265C0A53C33AFBC37657F14CE14E68BD16`；更新清单与安装包公网 HTTP 200，安装包 60,115,729 bytes | 等待用户安装验收 |
| 2026-08-23 | 版本 0.4.11 发布：递增桌面项目、安装器和用户手册版本标记，重新生成 Windows x64 自包含安装包并上传 OSS | `eng\publish.cmd` 退出 0；完整回归 429/429；安装器编译 0 警告、0 错误；SHA-256 `96F04BDF6A52505C174D96012794E424C41D694E49C375FFD1D9953BD0B0E042`；安装包 60,124,652 bytes；公网清单与安装包 HTTP 200 | 等待用户安装验收 |
| 2026-08-23 | 修复同一提醒同时显示应用内弹窗和旧式 Windows 通知的问题；弹窗优先，Windows 通知改为备用通道，提示音保持独立 | `eng\verify.cmd` 退出 0；完整回归 430/430；新增双通道互斥回归测试通过 | 下个版本发布时包含 |
| 2026-08-23 | 新增本地与云端会议室“蒙娜丽莎大厦”，云端工作区会议室目录达到 10 间；发布 0.4.12 Windows x64 安装包并上传 OSS | `eng\publish.cmd` 退出 0；完整回归 430/430；安装器编译成功；SHA-256 `907A2178DF67D7CCFCAEA3FC6F4E3E680D55D6B14779BFF0592AF6A61C0BECC8`；安装包 60,105,577 bytes | 等待用户安装验收 |
| 2026-08-24 | 版本 0.4.13 发布：包含会议室有预约优先排序和桌面日历仅水平移动调整；重新生成 Windows x64 自包含安装包并上传 OSS | `eng\publish.cmd` 退出 0；完整回归 435/435；安装器编译成功；SHA-256 `F8CB99EDDE46EFDE3C08B67C6548DED2BDD14E86A6CB26F5E5848B9AAD7814F6`；安装包 60,109,222 bytes；公网清单版本 0.4.13、SHA-256 匹配、安装包 HTTP 200 | 等待用户安装验收 |
| 2026-08-24 | 版本 0.4.14 发布：包含腾讯会议中文格式邀请解析和桌面组件“鼠标穿透”右键切换；重新生成 Windows x64 自包含安装包并上传 OSS | `eng\publish.cmd` 重跑成功；完整回归 438/438；安装器编译成功；SHA-256 `3E208D3D891EE20B760F8C3826E5C66CD43856F3E048143BA305834463000D10`；安装包 60,107,677 bytes；公网清单版本 0.4.14、SHA-256 匹配、安装包 HTTP 200 | 等待用户安装验收 |
| 2026-08-24 | 腾讯会议邀请兼容“会议名称/中文年月日/会议地点/链接附会议号”格式；桌面组件右键菜单新增“鼠标穿透”切换 | Red：新增解析器、快速新增运行时和右键菜单测试先失败；Green：扩展解析正则并接入三个桌面组件的现有鼠标穿透控制；Verify：`eng\verify.cmd` 通过，Core 90/90、Desktop 209/209、Infrastructure 97/97、Server 42/42，0 警告/0 错误 | 待用户验收 |

## P19 - AI 助理体检后续（见 docs/ASSISTANT_WORKLIST.md）

## P60 - UI 优化（借鉴 deepseek-harness Web UI 设计语言）

> 方案文档：[docs/UI_OPTIMIZATION_PLAN.md](docs/UI_OPTIMIZATION_PLAN.md)（编写于 2026-09-16，待用户确认 Q1–Q4 后开工）
> 参考实现：`D:\github_program\deepseek-harness\web-ui-extract`
> 基线：2026-09-16 完整回归 479/479（Core 93、Infrastructure 104、Desktop 238、Server 44）

体检结论（已实测，作为本阶段的动机与验收对照）：

- `Themes\Theme.xaml` 仅声明 30 个资源键（16 画刷 + 9 命名样式 + 3 个 `sys:Double` + 1 转换器）；**间距、圆角、字号、动效令牌为 0 个**。
- 圆角靠字面量：`2`×3、`3`×3、`4`×8、`5`×1、`6`×4、`8`×4。
- 硬编码字号 112 处、分布于 23 个文件；硬编码十六进制颜色 33 处，其中 `Views\RoomBookingBoardControl.cs` 独占 18 处。
- `Theme.xaml:29` 引用 `{DynamicResource UiTextEffect}`，该键在 `Theme.xaml`/`DarkTheme.xaml` 中均未声明，仅 `Desktop\DesktopAppearanceController.cs:66` 对桌面窗口注入 → 主窗口与普通页面解析为空（潜在缺陷，待 Q3 裁决）。

- [ ] P60-01 令牌基线：在 `Theme.xaml` 新增全部令牌（间距 `SpacingXs/Sm/Md/Lg/Xl`、圆角 `RadiusSm/Md/Lg`、字号 `FontCaptionSize/FontCompactSize/FontPanelTitleSize/FontPageTitleSize/FontClockSize`、控件尺寸 `ControlHeightPrimary/IconButtonSize/SidebarWidth/BrandBarHeight/NavigationItemHeight`、动效 `MotionFast/MotionBase/EasingStandard`、新增语义画刷 `ScrollbarThumbBrush/ScrollbarThumbHoverBrush/FocusRingBrush/OverlayScrimBrush/ElevationPanelBrush`），`DarkTheme.xaml` 同步覆写颜色键。**本切片不改任何页面引用**，观感必须与改动前一致。
  - Red：新增契约测试 `ThemeDeclaresEveryReferencedDynamicResourceKey`（解析 `Theme.xaml` 中所有 `{DynamicResource X}` 引用，断言每个键都在 `Theme.xaml` 或 `DarkTheme.xaml` 中有声明）——预期先因 `UiTextEffect` 未声明而失败，据此把该缺陷固化为可见事实。
  - Green：补齐令牌声明；按 Q3 裁决处置 `UiTextEffect`（补默认值或移除该 Setter）。
  - Refactor：令牌分组加注释，与 `docs/UI_DESIGN.md` §2 的章节一一对应；不引入 `<Color>` 资源或新的令牌机制（沿用现有 `<sys:Double>` 与 `SolidColorBrush`）。
  - Verify：`eng\verify.cmd` 退出 0；新增契约测试通过；`UiDesignContractTests` 34 项全通过；`[x]` 完成后把新令牌表写入 `docs/UI_DESIGN.md` §2。
  - 约束：**只新增、不改名、不删除**现有 30 个资源键（页面全部用 `DynamicResource` 引用，改名会静默回退系统默认样式）。

- [ ] P60-02 主题控件接入令牌：`Theme.xaml` 内 24 个隐式样式与 9 个命名样式（含 `PrimaryButtonStyle`、`IconButtonStyle`、`NavigationItemStyle`、`SegmentToggleStyle`）由字面量改用令牌；按 Q1 裁决处理普通按钮圆角。
  - Red：新增契约测试断言 `Theme.xaml` 中不再出现非令牌的 `CornerRadius="N"` 与样式级 `FontSize="N"` 字面量（模板内的结构性尺寸除外）。
  - Green：逐样式替换为 `{DynamicResource ...}`/`{StaticResource ...}` 令牌引用。
  - Refactor：`CaptionTextStyle`/`SectionTitleStyle`/`PageTitleStyle` 改为引用字号令牌，消除同义尺寸的第二处定义。
  - Verify：`eng\verify.cmd` 退出 0；`HorizontalScrollBarKeepsLogicalLeftToRightDirection`、`ComboBoxTemplateSupportsMouseToggleAndEditableInput` 等模板断言仍通过；三档分辨率截图与 P60-01 后逐像素对比，差异仅限预期项。

- [ ] P60-03 会议室看板主题化（本阶段最高价值）：新增 `ViewModels\RoomBoardPalette.cs`（纯数据、无 WPF 依赖、可单测），`Views\RoomBookingBoardControl.cs` 的 18 处硬编码颜色改为「按语义令牌构建色板 + 解析失败回落原字面量」；冻结画笔缓存改为按色板实例缓存。
  - Red：新增 `RoomBoardPaletteTests`——覆盖「令牌缺失时回落到当前默认色板」「深色主题下返回深色色板」；实现前因 `RoomBoardPalette` 不存在而编译失败。
  - Green：控件经 `TryFindResource` 解析语义令牌构建色板；几何常量（`CellHeight=18`、`RoomWidth=125`、`FirstVisibleCell=16`）与命中测试**保持不变**。
  - Refactor：状态色单源——占用灰/本人蓝/空闲绿/冲突红各只保留一个基准色，由其派生底/边/字，消除功能色在 `Theme.xaml` 与控件内的两份来源。
  - Verify：`RoomBookingBoardTests` 16/16 仍通过（几何与状态语义无回归）；**深色主题下看板截图**（当前为白底，改后应为深色）与浅色截图各一张；`eng\verify.cmd` 退出 0。
  - 附带收益：桌面组件已由 `DesktopAppearanceController.cs:43-73` 重绑窗口级语义键，看板改读语义键后**自动跟随组件的主题与颜色设置**。
  - 变量：按 Q2 裁决决定可见时段是否仍为 08:00–24:00。

- [ ] P60-04 看板可读性与状态表达：看板文字与背景对比度不低于 4.5:1；评估看板字号由 11px 提到 `FontCaptionSize`（12px，行高 18px 可容纳）；本人预约增加左侧 3px 竖条（与导航选中项同一语法），使「本人/他人」不只靠颜色区分（`UI_DESIGN.md` §2.3）。
  - Red：新增契约测试断言看板文字色与其背景色的对比度满足阈值；断言本人占用块存在独立于颜色的视觉标记。
  - Green：按上表调整色板派生与绘制。
  - Refactor：为深色主题单独校验对比度，不用同一组派生系数（深色底需更高亮度差）。
  - Verify：浅色/深色各一张看板截图人工核验；`eng\verify.cmd` 退出 0。

- [ ] P60-05 页面字面量清理：`RecordView.xaml:12`、`ProjectView.xaml:12`、`StatisticsView.xaml:14` 的 `FontSize="20" FontWeight="SemiBold"` 改用 `PageTitleStyle`；`CalendarView.xaml`/`RoomBookingPicker.xaml` 等处 `FontSize="11"` 次要文字改用 `CaptionTextStyle`；`MainWindowViewModel.cs:19-30` 助理导航 `PackIconLucideKind.Sparkles` 换为非星光图标（`UI_DESIGN.md` §1.6）；`RegionSelectorWindow.xaml` 的 `#33000000`/`#11FFFFFF` 改用 `OverlayScrimBrush`；`MeetingDetailsWindow.xaml`/`MeetingExportWindow.xaml` 的硬编码 `White` 与 `FontSize="18"` 改用语义样式。
  - Red：新增契约测试断言页面不出现硬编码页面标题字号，且助理导航不使用 `Sparkles`。
  - Green：逐处替换为语义样式/令牌。
  - Refactor：只替换等价观感的写法，不顺手重排布局。
  - Verify：`eng\verify.cmd` 退出 0；`UiDesignContractTests` 全通过；受影响的会议详情/导出窗口截图核验。

- [ ] P60-06 视觉验收与文档同步：按 `UI_DESIGN.md` §9 核验 **1920×1080、1366×768**，本轮补充 **980×640**（`MainWindow` 的 `MinWidth/MinHeight`）；桌面组件另验浅色/深色/复杂壁纸背景；检查无文字截断、控件位移、重叠、卡片套卡片、装饰渐变；键盘焦点/悬停/禁用/错误/选中状态完整。
  - Verify：`eng\verify.cmd` 退出 0；截图集归档到 `artifacts\`；`docs\UI_DESIGN.md` §2 令牌表与实现一致；`docs\USER_GUIDE.md` 中受影响的界面描述已更新；本文件状态、验证证据与唯一下一步已同步。

- 未决问题（开工前需用户裁决，详见方案文档 §4.3 与 §8）：
  - Q1 普通按钮圆角保持 8px（纯令牌化，零回归）还是改为 `RadiusSm=4` 以严格符合 `UI_DESIGN.md` §2.2（会变动全部按钮观感）？
  - Q2 会议室看板可见时段保持 08:00–24:00，还是收窄为工作时段？
  - Q3 `UiTextEffect` 未定义：补默认值（全应用打开文字描边）还是移除该 Setter（把描边明确限定为桌面组件特性）？
  - 本阶段**不改**：业务逻辑、HTTP 契约、`RoomBookingBoard` 几何与命中测试、28 个 XAML 页面的整体重写、技术栈。

## P40 - 版本跨越方案（聚焦 2、3、4、5、7）

- [x] P40-01 方案文档：新增 [docs/VERSION_EVOLUTION_PLAN.md](docs/VERSION_EVOLUTION_PLAN.md)，记录增量同步与 cursor、云端一致性、安全身份、AI 助手、工程质量/可观测性/发布体系的目标、分阶段交付和验收门槛。
- [x] P40-02 会议室名称识别：地点名与配置房间名允许恰好一个字符插入/缺失；新增回归测试，`dotnet test CcCalendar.sln --no-restore` 完整回归 474/474 通过。
- [ ] P41 增量同步与离线恢复：补充 cursor 分页/过期快照、Outbox 持久化重试和断网恢复验收。
- [ ] P44 云端一致性：补充对象版本冲突、幂等重放、事务回滚、迁移和备份恢复验收。
- [ ] P47 生产安全：域名 HTTPS/OIDC、角色矩阵、令牌撤销和关闭生产 DevToken。
- [ ] P50 AI 可靠性：固定评测集、Provider 错误恢复、工具调用追踪和数据范围校验。
- [ ] P53 发布体系：CI 闸门、结构化日志/指标、安装包签名、灰度发布和回滚演练。

## P23 - AI 重复会议创建与会议室同步

- [x] P23-01 提案字段结构化：`propose_timed_event` 增加 `location`、`meetingNumber`；AI 草案与确认写入分别保存地点和腾讯会议号，标题不再承载链接/会议号。
- [x] P23-02 标题清理与提示约束：系统指令明确标题/地点/会议号职责；客户端对模型返回的链接、会议号后缀做保守清理。
- [x] P23-03 批量确认：同一轮连续多个定时日程合并为一个预览，一次确认后逐项写入；单条提案和待办/记录流程保持兼容。
- [x] P23-04 云端会议室同步：AI 确认后的本地带地点日程在数据刷新后按日期提交团队预约，使用 `calendar-event-{eventId}` 幂等键；已打开的会议室看板也会主动重新加载。
- [x] P23-05 TDD 验证：新增 provider 字段解析、标题清理、确认写入地点/会议号、批量确认回归测试；完整回归 Core 91、Infrastructure 100、Desktop 226、Server 42 全部通过。

## P24 - 团队预约身份与邀请分钟精度

- [x] P24-01 Red：补充开发令牌旧 `CurrentUserId`、启动恢复令牌 subject 和会议邀请 14:10 精确导入测试。
- [x] P24-02 Green：登录成功始终以服务端返回的 workspace/user ID 校正本地配置；启动恢复从开发令牌 `sub` 修复旧用户 ID；预约同步遇到身份权限错误时不阻断看板加载。
- [x] P24-03 Green：导入腾讯会议时间不再四舍五入到半小时，动态加入 18:10 等分钟级时间选项，保存请求保留原始时间。
- [x] P24-04 Verify：云端实测登录与会议室目录正常，但 2026-08-25 当前没有预约记录；完整回归 Core 91、Infrastructure 100、Desktop 228、Server 42 全部通过。
- [x] P24-05 Green：会议室看板增加 15 秒低频云端轮询，另一台电脑创建预约后无需手动切换日期即可刷新当前面板。

## P25 - 预约人员回退与提交失败可见化

- [x] P25-01 Red：补充旧预约缺少 `OrganizerName` 时使用当前开发令牌姓名的映射测试，以及预约服务返回 `false` 时的用户提示测试。
- [x] P25-02 Green：服务端登录已按开发令牌姓名写入成员目录；客户端对当前用户的旧预约提供姓名回退，双击/悬停继续显示“会议人员”。
- [x] P25-03 Green：快速新增不再静默吞掉 `TryCreateBookingAsync=false`，会提示用户重新登录并刷新身份；服务端 201/预约查询已用临时预约实测，`OrganizerName=周家丞`。
- [x] P25-04 发布：版本 `0.5.3` 安装包已生成并上传 OSS；完整回归 91/100/230/42，公网清单、安装包 HTTP 200、SHA-256 和长度校验一致。

## P26 - 跨电脑会议详情、预约人颜色与导入

- [x] P26-01 Red：补充团队看板映射携带预约人 ID、邀请文本和当前用户归属测试；补充预约提交携带邀请文本测试。
- [x] P26-02 Green：RoomBooking/预约 API/文件与 PostgreSQL 存储增加可选 `MeetingInvitationText`，旧数据可继续读取；云端详情在其他电脑显示完整邀请内容。
- [x] P26-03 Green：看板预约块增加预约人 ID/归属，当前用户预约使用蓝色，他人预约使用灰色；悬停和双击详情继续显示会议人员。
- [x] P26-04 Green：他人会议详情增加“添加到我的日程”，只写本地日程并保留地点、时间、邀请文本；本地同步按同一房间/标题/时间识别云端预约，避免重复提交。
- [x] P26-05 Verify/发布：完整回归 465/465（Core 91、Infrastructure 100、Desktop 232、Server 42）；版本 0.5.4 安装包 `artifacts\\installer\\cccalendar-0.5.4-win-x64-setup.exe` 60,135,659 bytes，SHA-256 `516F1C329001ECC330405479C7937DE557DDDFE5955503651009C8F70CB2464F`，已上传 OSS，公网清单与 HEAD 校验通过；Linux Server 已部署阿里云并保留 `/root/cccalendar/data`，公网 `/health` 返回 ok。

## P27 - 云端预约删除权限与房间目录恢复

- [x] P27-01 Red/Green：预约存储增加按 ID 查询；DELETE 仅允许 OrganizerId 与 JWT `sub` 一致的创建人，其他成员返回 403，不存在返回 404。
- [x] P27-02 Green：删除成功广播 `room_booking/deleted` SyncChange；客户端详情仅对蓝色自建预约显示“删除我的预约”，删除后刷新看板；灰色他人预约不显示删除入口。
- [x] P27-03 云端修复：恢复工作区正式 10 间会议室，并从旧备份恢复 `room-bookings.json`（保留已有预约）；阿里云服务重启后 `/health`、房间目录、预约查询和创建人删除实测通过。
- [x] P27-04 发布：完整回归 467/467（Core 91、Infrastructure 100、Desktop 232、Server 44）；0.5.5 安装包 `artifacts\\installer\\cccalendar-0.5.5-win-x64-setup.exe` 60,154,886 bytes，SHA-256 `D5744B8B6493969B1D65A12281EA5CD79B2D3FC7E52CFA7BE914ED0521838BA7`；OSS 清单与安装包公网 HTTP 200、长度和哈希一致。

## P28 - 0.5.6 安装包发布

- [x] 版本号递增至 0.5.6；`eng\\publish.cmd` 完成全量回归 467/467、发布构建和 Inno Setup 安装器编译。
- [x] 安装包 `artifacts\\installer\\cccalendar-0.5.6-win-x64-setup.exe` 60,154,880 bytes，SHA-256 `653AA29CAE8B50EED130103489679C3956EFD017052C7BDB682143068795F0B4`；已上传 OSS，公网清单和安装包 HTTP 200、长度/哈希一致。

## P29 - 团队登录配置即时持久化

- [x] 团队连接设置变更后立即写入本地 `settings.json`；避免安装器重启或异常退出发生在 OnExit 保存前时丢失姓名、口令和服务地址。
- [x] 桌面回归测试通过 232/232；启动恢复仍使用 Windows 凭据管理器中的令牌，令牌过期时自动静默续签。

## P30 - 旧预约邀请文本补齐与自动连接发布

- [x] 云端预约增加创建人授权的邀请原文更新接口；客户端同步自己创建且云端缺少原文的预约时自动补齐。
- [x] 发布 0.5.7 Windows 安装包和 Server：完整回归 467/467；安装包 `artifacts\\installer\\cccalendar-0.5.7-win-x64-setup.exe` 60,145,984 bytes，SHA-256 `5830446DDEBE0C62C1E0C0BC13F30621BC75EAA92BE8F69AE8E24661C9D438F0`；OSS 清单/安装包公网 HTTP 200、长度和哈希一致；Server `/health` 正常，PATCH 创建人授权接口实测 204，部署后恢复 10 间房和 5 条预约。

## P31 - 登录后看板刷新与云端目录失败隔离

- [x] 团队登录/恢复/退出触发 `ConnectionChanged`，已打开的会议室看板主动刷新；开发令牌在工作区和用户 ID 更新完成后再触发刷新。
- [x] 看板云端加载失败不再静默伪装为云端目录，保留最后一次成功云端快照并记录诊断信息。
- [x] 发布 0.5.8 Windows 安装包并验证：完整回归 467/467；安装包 `artifacts\\installer\\cccalendar-0.5.8-win-x64-setup.exe` 60,145,279 bytes，SHA-256 `D785EDDA20290C9CC9BF614E2D9975F940618E27DCA93B53868CB8457BE3FF89`；OSS 清单/安装包 HTTP 200、长度和哈希一致；公网云端目录 10 间、预约 5 条。

## P32 - 在线预约冲突可解释性

- [x] 冲突提示查询当前日期云端快照，显示实际冲突会议室、分钟级起止时间和预约人；当前账号自建预约提示可在看板删除。
- [x] 保留原有本地日程保存行为；完整回归 467/467。
- [ ] 下一版安装包发布后验证用户界面中的冲突详情。

## P33 - 修复本地保存导致的重复云端提交

- [x] 根因确认：`SaveRequestAsync` 的全局刷新会先同步本地会议室事件，随后快速新增流程再次显式提交同一预约，造成服务端 `201` 后 `409`；另一台电脑看到的是第一次成功预约。
- [x] 调整快速新增顺序为先提交云端、再保存本地，避免同一条日程重复 POST；完整回归 467/467。
- [ ] 下一版安装包发布后验证单次创建只产生一个云端预约。

## P34 - 删除云端预约同步移除本地镜像

- [x] 修复删除自己创建的云端预约后本地镜像日程仍被看板渲染、并可能被后台同步重新提交的问题。
- [x] 删除成功后按房间、标题、日期和分钟级起止时间匹配并移除本地镜像，再刷新桌面和会议室看板。
- [x] 新增匹配回归测试；桌面测试 233/233 通过。
- [x] 发布 0.5.9 Windows 安装包并上传 OSS；完整回归 468/468；安装包 `artifacts\\installer\\cccalendar-0.5.9-win-x64-setup.exe`，60,130,937 bytes，SHA-256 `81409611D0367591D30AB05F5346095EAAA456B166D2B9FEBD6349E2DAD992F6`；公网清单和安装包 HTTP 200、长度/哈希一致。

- [x] P19-A01 重复消息与历史读取：允许时间戳不同的重复问题，恢复历史思考文本；`AssistantViewModelTests` 20/20 通过。
- [x] P19-A02 日程查询准确性：全天日期范围、时间排序、地点投影、跨多日每日计划；Infrastructure 聚焦测试 3/3 通过。
- [x] P19-A03 附件错误提示与聊天历史原子写入：附件数据异常在 UI 内提示，历史改为临时文件替换。
- [x] P19-B01 会话上下文契约与 provider 接入：最近 40 轮传入四类 provider；Desktop/Core/Infrastructure 聚焦测试通过。
- [x] P19-C01 停止、重试、空响应和提案失败状态：新增停止/重试/空响应状态，确认、撤销和空闲查找异常收敛到消息区；Desktop 助理测试 23/23。
- [x] P19-C02 自动滚动与事件解绑：仅接近底部时跟随，DataContext/卸载解除订阅。
- [x] P19-B02 provider/本地模式/附件出网状态提示：上下文栏显示本地/在线、provider、附件数量和总大小。
- [x] P19-B03 工具来源、时间范围和冲突对象展示：冲突面板显示候选日程标题、时间和时区；保留“思考”展示。
- [x] P19-B04 思考文本保留、可折叠和持久化策略：按用户决定保留折叠思考展示，历史读取恢复 ThinkingText。
- [x] P19-C03 提案/冲突/附件加载失败状态：冲突检测、提案确认、寻找空闲和附件失败均在消息区收敛；失败不丢失待确认提案或附件；补齐助理操作无障碍名称。
- [x] P19-C04 助理运行时验收：新增 WPF 渲染测试，覆盖 1366×768 与 1920×1080，思考折叠、提案区域和消息布局通过。
- [x] P19 发布回归与安装包发布：版本 0.4.15 完整验证 449/449，安装器编译成功并上传 OSS；公网清单版本 0.4.15、SHA-256 匹配、安装包 HTTP 200。
- [x] P20 会议日程同步与鼠标穿透恢复：AI 确认写入后刷新主窗口及桌面组件；模型未返回工具提案时明确提示未创建；托盘增加醒目的恢复鼠标操作入口，设置页说明默认快捷键 `Ctrl+Alt+Shift+P`；版本 0.5.1 已发布，公网清单和安装包 HTTP 200。
- [x] P21 会议室面板双击详情显示预约人：预约 API 增加 `OrganizerName`，客户端看板块、悬停提示和双击详情展示“会议人员”；完整回归 454/454。待按 CLOUD_OPS_GUIDE.md 部署云端 Server 后，公网预约查询即可返回姓名。
