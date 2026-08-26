# 局域网部署与跨电脑验收

## 网络前提（先确认两台电脑互通）

跨电脑验收的前提是客户端网络能到达服务端 IP。客户端电脑先验证：

```powershell
Test-NetConnection 192.168.1.88 -Port 5080
```

`TcpTestSucceeded : True` 才继续配置登录。`False` 的常见原因：

- 两台电脑不在同一网段（`ipconfig` 的 IPv4 前三段不同，例如 `192.168.1.x` 与 `192.168.77.x`）且中间没有路由——连了手机热点、独立路由器都会这样；
- 公司 WiFi 开了客户端隔离（AP isolation），同网段也互不可达；
- 服务端防火墙规则未生效（用 `-Profile Any` 覆盖公用/专用网络配置文件）。

公司网络不可改时，用 Tailscale/ZeroTier 组虚拟局域网：两台电脑安装客户端并登录同一账号，各自获得 `100.x.x.x` 虚拟 IP（免费额度 3 用户 100 台，足够验收）。服务端照旧监听 `0.0.0.0:5080` 无需重启，客户端“服务端地址”改填服务端的虚拟 IP 即可；走纯出站连接，公司防火墙一般不拦截。这是“微信式”可达性——服务器人人可连，与客户端所在网络无关；正式做法见下文“生产环境”（云服务器 + 域名 + HTTPS）。

## 服务器电脑

在一台同网段、持续运行的 Windows 电脑上启动 Server：

```powershell
dotnet run --project src/CcCalendar.Server/CcCalendar.Server.csproj --urls http://0.0.0.0:5080
```

查看服务器局域网 IP：

```powershell
ipconfig
```

例如服务器 IP 是 `192.168.1.20`，放行 TCP 端口 5080（仅限局域网）：

```powershell
New-NetFirewallRule -DisplayName "cccalendar LAN 5080" -Direction Inbound -Protocol TCP -LocalPort 5080 -Action Allow -Profile Any
```

服务器电脑先验证：

```powershell
Invoke-WebRequest http://127.0.0.1:5080/health
```

## 局域网开发令牌登录（无 OIDC 时的跨电脑登录）

局域网验收不需要身份提供商：在服务器上启用开发令牌模式，
客户端用姓名登录即可。仅供内网测试，生产环境必须关闭并改用 OIDC。

服务器侧在 `appsettings.json`（或环境变量）配置：

```json
{
  "Authentication": {
    "DevToken": {
      "Enabled": true,
      "SigningKey": "至少 32 个字符的局域网签名密钥，两台电脑共用同一服务端即可",
      "WorkspaceId": "00000000-0000-0000-0000-000000000001"
    }
  }
}
```

- `WorkspaceId` 是团队工作区 GUID，同一个团队所有电脑填同一个值；
- 同一姓名总是派生出同一用户 ID，跨电脑重装后身份不丢；
- 登录即自动加入工作区（默认 Admin，可用 `Role` 调整为 Viewer/Member/Admin/Owner）；
- 令牌默认 8 小时有效（`AccessTokenLifetimeMinutes` 可调），过期后重新点“登录团队”即可；
- **公网部署必须配置共享口令** `SharedSecret`（至少 8 字符）：配置后登录必须携带同一口令，防止陌生人只凭姓名冒领令牌；口令配置在 `Authentication:DevToken:SharedSecret`，客户端在“团队连接”页的“开发令牌口令”输入框填写。未配置口令时登录不校验（仅限纯内网）。

## 初始化团队会议室（首次部署执行一次）

看板的房间列来自服务端房间目录，首次部署需要建房间。在服务器电脑上另开一个
PowerShell 窗口执行（把 `$base`/`$ws` 换成实际值）：

```powershell
$base = "http://127.0.0.1:5080"
$ws = "11111111-1111-1111-1111-111111111111"
# 配置了共享口令时登录请求需要带 sharedSecret 字段。
$login = Invoke-RestMethod -Method Post -Uri "$base/api/auth/dev-token" -ContentType "application/json" -Body (@{ name = "管理员"; sharedSecret = "<你的共享口令>" } | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
foreach ($room in "项目组二楼会议室", "生产组会议室", "技术中心二楼会议室", "聚英堂会议室", "院士办会议室", "研发中心三楼会议室", "财务部三楼会议室", "采购部会议室", "雅典学院", "蒙娜丽莎大厦") {
    $json = @{ name = $room; timeZoneId = "China Standard Time" } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri "$base/api/workspaces/$ws/rooms" -Headers $headers -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($json))
}
```

默认 file 存储会把房间落盘，只需建一次；重复执行会因同名房间报错，可忽略。

## 客户端电脑

在 cccalendar 设置的“团队连接”中二选一：

OIDC 模式（有公司身份提供商时）：

```text
服务端地址:        http://192.168.1.20:5080/
OIDC 授权地址:     由公司身份提供商提供
OIDC 令牌地址:     由公司身份提供商提供
客户端 ID:         公司注册的桌面客户端 ID
工作区 ID:         服务端工作区 GUID
```

开发令牌模式（局域网验收，OIDC 地址留空即可）：

```text
服务端地址:        http://192.168.1.20:5080/
开发令牌姓名:      张三
工作区 ID:         留空，登录成功后自动回填
```

也可以先从客户端电脑检查网络链路：

```powershell
Invoke-WebRequest http://192.168.1.20:5080/health
```

返回 `status: ok` 说明 IP、端口、防火墙和 HTTP 链路正常。返回 401 只会发生在需要登录的 API，不代表网络不通。

## 团队会议室看板

客户端登录团队连接后，“快速新增 → 会议室时间视图”自动切换为团队模式：

- 房间列来自服务端房间目录（在服务端工作区创建的会议室）；
- 灰色占用块来自全团队的在线预约（其他电脑提交的预约立即可见）；
- 在板上选中时段并保存日程时，同时向服务端提交在线预约；
  服务端冲突（如他人已约同一时段）会弹窗提示，本地日程仍会保存。

## 生产环境

`http://192.168.x.x:5080` 只适合局域网原型。正式环境应使用域名、HTTPS/443、反向代理和 PostgreSQL；客户端只连接 API，不访问服务器文件夹或数据库端口。OIDC 的 redirect URI、HTTPS 证书和反向代理配置必须与身份提供商登记值完全一致。

### 云服务器部署（异地办公）

两台电脑不在同一网络时（如异地办公），把 Server 部署到云服务器，所有客户端走公网连接——与“微信式”集中服务架构一致。

云服务器要求：2 核 2G 的 Linux（Ubuntu 22.04，**不要选 Windows**，2G 扛不住系统开销）；国内轻量应用服务器即可，纯 IP + 非标端口（5080）无需域名备案。

```powershell
# ① 开发机发布 Linux 版并打包（已随发布产出）
dotnet publish src/CcCalendar.Server/CcCalendar.Server.csproj -c Release -r linux-x64 --self-contained -o artifacts\publish\linux-x64-server
Compress-Archive -Path artifacts\publish\linux-x64-server\* -DestinationPath artifacts\cccalendar-server-linux.zip

# ② 上传（IP 换成控制台分配的公网 IP）
scp artifacts\cccalendar-server-linux.zip ubuntu@<服务器IP>:~/
```

```bash
# ③ 服务器上解压并注册 systemd 服务（口令必须设置，公网防止冒领令牌）
sudo apt update && sudo apt install -y unzip
mkdir ~/cccalendar && unzip ~/cccalendar-server-linux.zip -d ~/cccalendar
chmod +x ~/cccalendar/CcCalendar.Server
sudo tee /etc/systemd/system/cccalendar.service > /dev/null <<'EOF'
[Unit]
Description=cccalendar Server
After=network.target
[Service]
WorkingDirectory=/home/ubuntu/cccalendar
ExecStart=/home/ubuntu/cccalendar/CcCalendar.Server --urls http://0.0.0.0:5080
Environment=Authentication__DevToken__Enabled=true
Environment=Authentication__DevToken__SigningKey=<至少32位强密钥>
Environment=Authentication__DevToken__WorkspaceId=11111111-1111-1111-1111-111111111111
Environment=Authentication__DevToken__SharedSecret=<团队口令，至少8位>
Restart=always
User=ubuntu
[Install]
WantedBy=multi-user.target
EOF
sudo systemctl daemon-reload && sudo systemctl enable --now cccalendar
curl http://127.0.0.1:5080/health
```

```bash
# ④ 建房（Linux 终端 UTF-8，无中文乱码；口令模式登录需带 sharedSecret）
TOKEN=$(curl -s -X POST http://127.0.0.1:5080/api/auth/dev-token -H 'Content-Type: application/json' \
  -d '{"name":"admin","sharedSecret":"<团队口令>"}' | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)
for ROOM in 项目组二楼会议室 生产组会议室 技术中心二楼会议室 聚英堂会议室 院士办会议室 研发中心三楼会议室 财务部三楼会议室 采购部会议室 雅典学院 蒙娜丽莎大厦; do
  curl -s -X POST "http://127.0.0.1:5080/api/workspaces/11111111-1111-1111-1111-111111111111/rooms" \
    -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
    -d "{\"name\":\"$ROOM\",\"timeZoneId\":\"China Standard Time\"}"
done
```

⑤ 云控制台防火墙/安全组放行 TCP 5080。

⑥ 客户端“服务端地址”填 `http://<服务器公网IP>:5080/`，“开发令牌口令”填团队口令。

注意：HTTP 公网会明文传输姓名/口令/令牌，验收期可接受，长期使用需升级 HTTPS（域名 + 反向代理 + Let's Encrypt），并最终切换到 OIDC 登录后关闭 DevToken。

生产 Server 配置至少需要：

```json
{
  "Storage": {
    "Provider": "postgres",
    "ConnectionString": "Host=db.internal;Port=5432;Database=cccalendar;Username=cccalendar;Password=从部署密钥注入"
  },
  "Network": {
    "RequireHttps": true
  }
}
```

不要把 PostgreSQL 端口暴露给客户端电脑；只允许 Server 所在主机访问数据库。
