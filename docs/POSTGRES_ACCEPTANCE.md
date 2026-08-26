# PostgreSQL 实机验收

2026-08-21 已在本机 PostgreSQL 18.6 完成验收：schema 初始化（含 btree_gist 排斥约束）、`/health` 返回 `storage: postgres`、双客户端同时段预约 409、幂等键重试和重启持久化全部通过。以下步骤供新环境重复验收使用。

1. 创建数据库和账号，并确保 Server 主机能访问 PostgreSQL 5432。应用账号不需要超级用户，但 `btree_gist` 扩展需由超级用户预先安装：

```powershell
psql -U postgres -d postgres -c "CREATE USER cccalendar WITH PASSWORD '<secret>';"
psql -U postgres -d postgres -c "CREATE DATABASE cccalendar OWNER cccalendar;"
psql -U postgres -d cccalendar -c "CREATE EXTENSION IF NOT EXISTS btree_gist WITH SCHEMA public;"
```

2. 设置连接串环境变量（不要写入仓库文件）：

```powershell
$env:ConnectionStrings__Postgres = "Host=127.0.0.1;Port=5432;Database=cccalendar;Username=cccalendar;Password=<secret>"
$env:Storage__Provider = "postgres"
$env:Network__RequireHttps = "false"
```

3. 启动 Server。启动时会执行 `PostgresSchema.Definition`，创建房间、预约、成员和同步游标表：

```powershell
dotnet run --project src/CcCalendar.Server/CcCalendar.Server.csproj --urls http://127.0.0.1:5080
```

4. 验收 `/health` 返回 `storage: postgres`。
5. 用两个客户端执行：创建房间、读取目录、同一时间段并发预约；第二个预约必须得到 `409 Conflict`。
6. 重启 Server，再用原 cursor 拉取 `/api/workspaces/{workspaceId}/sync?cursor=...`，确认变更仍存在。

步骤 4-6 已固化为自动化验收 `PostgresBookingAcceptanceTests`（设置 `ConnectionStrings__Postgres` 环境变量后运行；未设置时直接通过，不阻塞无数据库环境的常规回归）：

```powershell
$env:ConnectionStrings__Postgres = "Host=127.0.0.1;Port=5432;Database=cccalendar;Username=cccalendar;Password=<secret>"
dotnet test tests/CcCalendar.Server.Tests/CcCalendar.Server.Tests.csproj --filter "FullyQualifiedName~PostgresBookingAcceptanceTests"
```

验收数据使用随机工作区 ID，与真实数据互不冲突；如需清空残留测试数据，可在 psql 中 TRUNCATE 五张业务表（schema 保留）。

生产环境把 `Network__RequireHttps=true`，通过反向代理提供 HTTPS/443；不要把 PostgreSQL 端口暴露给客户端电脑。
