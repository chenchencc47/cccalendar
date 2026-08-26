# cccalendar 联网协作与会议室预约方案

> 状态：方案已确认，按 `WORKLIST.md` 的 P15 逐步实现
> 最后更新：2026-08-20

## 1. 目标与边界

cccalendar 保持本地优先：个人日程、待办、项目、记录和桌面组件即使没有网络也能继续使用。

联网能力先聚焦一个团队闭环：

1. 用户登录并加入一个团队工作区（Workspace）。
2. 客户端读取服务端维护的会议室目录和占用情况。
3. 用户在线预约、修改和取消会议室。
4. 服务端以事务方式判定冲突，其他客户端通过实时通知或增量同步看到变化。

第一阶段不做：客户端之间直连、共享 SQLite 文件、把所有个人数据一次性云同步、端到端加密附件同步和自建密码系统。

## 2. 网络通信模型

客户端不直接连接其他用户电脑，也不直接连接数据库。每个客户端都主动连接同一个 API 服务：

```text
WPF 客户端 A -- HTTPS/443 --> ASP.NET Core API -- PostgreSQL
WPF 客户端 B -- HTTPS/443 --> ASP.NET Core API
                         \-- SignalR/WebSocket 实时通知 --> A/B
```

- IP 或 DNS 找到服务端机器；端口定位服务进程；HTTP 是请求协议；HTTPS 是带 TLS 加密的 HTTP。
- 生产环境使用域名和 `https://...:443`，客户端通常只需要出站连接，不需要开放用户电脑的入站公网端口。
- 局域网原型可以使用 `http://192.168.x.x:5080`，但正式部署仍必须使用 HTTPS。
- SignalR 先通过 HTTP(S) 建立连接，条件允许时升级为 WebSocket；断线后使用游标增量同步补齐变化。

## 3. 部署模式

### 3.1 本地模式

无需登录，现有 SQLite 和桌面功能保持不变。没有团队工作区时，不显示远程预约状态。

### 3.2 局域网原型

在办公室服务器、NAS 或一台常开电脑运行 ASP.NET Core 服务，监听 `0.0.0.0:5080`，通过防火墙只允许内网访问。该模式用于验证通信链路，不作为公网生产方案。

### 3.3 正式联网

使用云服务器或公司服务器：反向代理监听 443，转发到 ASP.NET Core；数据放在 PostgreSQL。服务端只暴露 API 和 SignalR，不暴露数据库端口。

## 4. 身份、工作区与权限

本地模式不要求账号。团队模式要求身份，因为系统需要知道预约人、控制修改/取消权限、隔离不同团队并记录审计。

优先使用 OIDC/公司单点登录（例如 Microsoft Entra ID），其次使用邮箱验证码；不在 cccalendar 中自行实现密码存储。访问令牌短期有效，刷新令牌通过已有 `ISecretStore` 保存到 Windows 凭据管理器。

服务端核心对象：

```text
UserAccount       外部身份 subject、显示名
Workspace         团队或组织
Membership        用户在工作区中的 owner/admin/member/viewer 角色
Room              工作区下的会议室资源
RoomBooking       房间、预约人、标题、UTC 起止时间、状态和版本
AuditEntry        操作人、对象、动作、时间和结果，不保存敏感正文
```

会议室默认展示“已占用”和预约人；标题可按工作区策略限制为参与者或管理员可见。

## 5. 预约一致性

会议室是共享资源，服务端是最终权威。客户端看板的空闲判断只用于交互提示，不能作为最终保证。

创建预约时服务端在一个数据库事务中检查半开区间：

```text
同一 RoomId 且 NewStart < ExistingEnd 且 NewEnd > ExistingStart
=> 返回 HTTP 409 Conflict
```

请求应带幂等键，避免网络超时重试造成重复预约。编辑使用对象版本或 `If-Match`，版本不一致时返回冲突而不是覆盖别人的修改。

会议室预约默认要求在线确认。个人日程可以离线创建并留在本地；离线时不能把共享房间显示为已确认。

## 6. 客户端同步边界

保留现有本地 SQLite。新增同步组件只负责：

1. 本地事务成功后写入出站队列（Outbox）。
2. 后台任务上传可重试的命令并记录服务端确认。
3. 使用 `cursor` 拉取增量变更。
4. 收到 SignalR 通知后触发增量拉取，而不是信任通知正文。
5. 将在线、离线、提交中、冲突和权限拒绝状态提供给 UI。

第一条垂直切片只覆盖会议室预约；个人日程、待办、项目和记录的完整同步另行拆分，避免一次引入不可控的冲突范围。

## 7. 推荐 API

```text
GET    /api/workspaces
GET    /api/workspaces/{workspaceId}/rooms
GET    /api/workspaces/{workspaceId}/room-availability?from=&to=
POST   /api/workspaces/{workspaceId}/bookings
PATCH  /api/bookings/{bookingId}
DELETE /api/bookings/{bookingId}
GET    /api/sync?cursor=
WS     /hubs/sync
```

HTTP API 负责命令和查询；SignalR 只传对象 ID、版本和变更类型，客户端随后通过增量接口获取详细数据。

## 8. 实施顺序与完成定义

### 当前原型已实现

截至 2026-08-20，P15 原型已经具备以下可运行边界：

- Server 通过 HTTP API 提供房间目录、房间创建、预约、冲突 409、幂等键和游标增量查询。
- 生产 Server 可通过 `Storage:Provider=postgres` 使用 Npgsql/PostgreSQL；房间、预约、成员和同步游标均有数据库存储实现，预约唯一键和半开区间冲突由数据库约束保证。默认 `file` 仍是单实例/LAN 过渡实现。
- JWT Bearer/OIDC 配置、工作区成员角色授权和预约人 `sub` 校验已接入；WPF 设置页已提供 PKCE 回环登录入口，令牌只写 Windows 凭据管理器。
- `/hubs/sync` 使用 SignalR 按工作区广播变更 ID；客户端收到通知后通过 `/api/workspaces/{workspaceId}/sync?cursor=` 补齐，应用失败不会推进本地 cursor。
- 两个独立客户端的自动化验收已覆盖“创建后可见”和“同时间段返回 409”。
- 跨电脑局域网启动和防火墙步骤见 [LAN_DEPLOYMENT.md](LAN_DEPLOYMENT.md)；当前自动化测试使用同一测试主机，真实跨电脑验收需要在两台同网段电脑执行该步骤。
- PostgreSQL provider 已在本机 PostgreSQL 18.6 完成实机验收（2026-08-21）：schema 初始化（含 btree_gist 排斥约束）、双客户端同时段 409、幂等键重试和重启持久化全部通过；自动化验收见 `PostgresBookingAcceptanceTests`，执行方式见 [POSTGRES_ACCEPTANCE.md](POSTGRES_ACCEPTANCE.md)。
- 局域网开发令牌登录（2026-08-21）：`Authentication:DevToken` 启用后客户端可凭姓名登录（SHA256 派生确定性 userId + HS256 JWT），登录即加入工作区，无 OIDC 也能跨电脑协作；详见 [LAN_DEPLOYMENT.md](LAN_DEPLOYMENT.md)。
- WPF 会议室看板接入 Server（2026-08-21）：登录团队连接后“快速新增 → 会议室时间视图”房间列来自服务端目录、占用块来自团队在线预约，选中时段保存日程时同步提交在线预约；服务端冲突弹窗提示且本地日程保留。

P15-01：本地同步元数据、设备标识和 Outbox 表，包含迁移和重启往返测试。

P15-02：独立 `Room`/`RoomBooking` 领域模型，包含半开区间冲突测试。

P15-03：ASP.NET Core 局域网 API 最小闭环，包含创建、取消、409 冲突和幂等测试。

P15-04：WPF 客户端 HTTP 预约适配器与团队房间目录缓存。

P15-05：OIDC 登录、工作区成员权限和凭据生命周期。

P15-06：SignalR 通知、游标增量同步和断线恢复。

P15-07：双客户端验收：A 创建后 B 可见；B 同时预约同一时段得到 409；修改/取消可传播；无网络时个人日程仍可用。

每个切片遵循 Red-Green-Refactor-Verify，并在 `WORKLIST.md` 写入测试命令、结果和唯一下一步。
