# 云服务器与客户端操作手册（cccalendar 团队版）

> 本手册按当前实际环境编写：阿里云 ECS（Ubuntu 22.04，47.120.6.126）+ Windows 客户端（安装包版本以 OSS 清单 `version.json` 为准）。
> 按场景索引，找到对应章节复制命令执行即可。
> **注意：本文件包含口令等敏感信息，仅限团队内部使用，不要外传。**

## 1. 环境总览

### 云服务器（已部署完成）

| 项目 | 值 |
|------|-----|
| 云厂商/规格 | 阿里云 ECS 经济型 e（2 核 2G） |
| 系统 | Ubuntu 22.04（root 用户） |
| 公网 IP | `47.120.6.126`（服务地址 `http://47.120.6.126:5080/`） |
| 程序目录 | `/root/cccalendar`（数据目录 `/root/cccalendar/data`，file 存储；必须直接包含 `rooms.json` 和 `room-bookings.json`） |
| 守护方式 | systemd 服务 `cccalendar`（`Restart=always`，开机自启，崩溃自动拉起） |
| 安全组 | 已放行 SSH 22 / TCP 5080 |

### 当前配置的三个关键值（都在 systemd 环境变量里）

> **口令与密钥不再写入本文档。** 本仓库为公开仓库，把明文口令提交进去等于对全网公开。
> 实际值用下面这条命令从服务器 systemd 配置读取（只有登录服务器的人能看到）：
>
> ```bash
> grep -E 'SharedSecret|SigningKey' /etc/systemd/system/cccalendar.service
> ```

| 用途 | 取值方式 |
|------|----------|
| 团队工作区 ID | `11111111-1111-1111-1111-111111111111`（非机密，可明文） |
| 登录口令（所有人共用） | `Authentication__DevToken__SharedSecret`，见上方命令；经私密渠道单独发给成员 |
| 开发令牌有效期 | 7 天（10080 分钟） |
| 令牌签名密钥 | `Authentication__DevToken__SigningKey`，见上方命令（改了它 = 全员令牌失效，需重新登录） |

### 团队成员（DevToken 按姓名派生用户 ID，姓名必须一字不差）

正式成员：陈志坚、李方、刘昇海、潘豪勋、潘远观、谭骏楠、王昌侣、张华濠、周家丞
测试账号（勿删）：张三、李四

### 会议室（云端已建 10 间，2026-08-25 已核验恢复）

项目组二楼会议室、生产组会议室、技术中心二楼会议室、聚英堂会议室、院士办会议室、研发中心三楼会议室、财务部三楼会议室、采购部会议室、雅典学院、蒙娜丽莎大厦

---

## 2. 云服务器日常管理（在开发机上操作）

### 2.1 登录服务器

```powershell
ssh root@47.120.6.126
```

输入服务器密码（购买 ECS 时设置/重置的那个）。进入后命令提示符变为 `root@iZf8z8...:~#`。

### 2.2 查看服务状态（最常用）

```bash
systemctl status cccalendar --no-pager -l
```

- `Active: active (running)` + `since` 时间 = 正常；
- `Active: failed` / `inactive` = 服务停了，看 2.3 日志。

### 2.3 看运行日志（排障必用）

```bash
# 最近 50 行日志
journalctl -u cccalendar -n 50 --no-pager

# 实时滚动日志（Ctrl+C 退出）
journalctl -u cccalendar -f
```

每次客户端登录/建房/提交预约，日志都会有一行 `Request finished ... 200/409/401`，可据此确认请求到达。

### 2.4 启动 / 停止 / 重启服务

```bash
systemctl start cccalendar     # 启动
systemctl stop cccalendar      # 停止（客户端全部离线，本地日程不受影响）
systemctl restart cccalendar   # 重启（改配置后执行）
```

### 2.5 服务器本身重启后要做什么

什么都不用做。`systemctl enable` 已注册开机自启，重启 ECS 后服务自动起来。可验证：

```bash
systemctl status cccalendar --no-pager
curl http://127.0.0.1:5080/health
```

看到 `"status":"ok"` 即正常。服务器重启**不会**丢房间和预约（落盘在 `/root/cccalendar/data`）。

### 2.6 从开发机快速体检（不登录服务器）

```powershell
Invoke-RestMethod http://47.120.6.126:5080/health
```

返回 `status ok` 即服务+安全组+公网链路全部正常。

---

## 3. 云服务器运维场景

### 3.0 会议室预约人姓名显示更新

会议室面板的预约人姓名由 Server 预约查询返回。客户端更新后，如云端 Server 尚未更新，旧预约仍会显示“未知成员”。部署新 Server 后，新登录成员会把姓名写入成员目录，预约查询会返回 `OrganizerName`；历史成员需重新登录一次以补齐姓名。

### 场景 A：升级 Server 版本（改了服务端代码后）

开发机 PowerShell（第 1 步、第 2 步，会提示输密码）：

```powershell
# ① 重新发布 Linux 包
cd D:\myProgram\cccalendar
Remove-Item -Recurse -Force artifacts\publish\linux-x64-server -ErrorAction SilentlyContinue
dotnet publish src\CcCalendar.Server\CcCalendar.Server.csproj -c Release -r linux-x64 --self-contained -o artifacts\publish\linux-x64-server
Remove-Item artifacts\cccalendar-server-linux.zip -ErrorAction SilentlyContinue
Compress-Archive -Path artifacts\publish\linux-x64-server\* -DestinationPath artifacts\cccalendar-server-linux.zip

# ② 上传
scp artifacts\cccalendar-server-linux.zip root@47.120.6.126:/root/
```

登录服务器执行（第 3 步，**保留 data 目录 = 保留全部房间和预约**）：

```bash
systemctl stop cccalendar
cp -r /root/cccalendar/data /root/cccalendar-data-keep
rm -rf /root/cccalendar && mkdir /root/cccalendar
unzip /root/cccalendar-server-linux.zip -d /root/cccalendar
chmod +x /root/cccalendar/CcCalendar.Server
cp -r /root/cccalendar-data-keep /root/cccalendar/data
rm -rf /root/cccalendar-data-keep
systemctl start cccalendar
curl http://127.0.0.1:5080/health
```

升级后请确认 `find /root/cccalendar/data -maxdepth 1 -type f` 能看到 `rooms.json` 与 `room-bookings.json`。不要把旧数据复制成 `/root/cccalendar/data/data`，否则服务会启动但读取空目录，表现为会议室和预约全部消失。

看到 `"status":"ok"` 升级完成。

### 场景 B：备份全部数据（房间 + 预约 + 成员）

服务器上执行：

```bash
systemctl stop cccalendar
tar czf /root/cccalendar-backup-$(date +%Y%m%d).tar.gz -C /root/cccalendar data
systemctl start cccalendar
ls -lh /root/cccalendar-backup-*.tar.gz
```

恢复备份：

```bash
systemctl stop cccalendar
rm -rf /root/cccalendar/data
tar xzf /root/cccalendar-backup-日期.tar.gz -C /root/cccalendar
systemctl start cccalendar
```

### 场景 C：新增会议室（不清数据、不影响现有预约）

开发机 PowerShell（远程 API 建房，自动 UTF-8 无乱码）：

```powershell
$base = "http://47.120.6.126:5080"
$ws = "11111111-1111-1111-1111-111111111111"
$login = Invoke-RestMethod -Method Post -Uri "$base/api/auth/dev-token" -ContentType "application/json" -Body ([System.Text.Encoding]::UTF8.GetBytes((@{ name = "admin"; sharedSecret = $teamSecret } | ConvertTo-Json)))
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
$json = @{ name = "新会议室名"; timeZoneId = "China Standard Time" } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$base/api/workspaces/$ws/rooms" -Headers $headers -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($json))
```

### 场景 D：查看全部会议室 / 某天全部预约

开发机 PowerShell：

```powershell
$base = "http://47.120.6.126:5080"
$ws = "11111111-1111-1111-1111-111111111111"
$login = Invoke-RestMethod -Method Post -Uri "$base/api/auth/dev-token" -ContentType "application/json" -Body ([System.Text.Encoding]::UTF8.GetBytes((@{ name = "admin"; sharedSecret = $teamSecret } | ConvertTo-Json)))
$headers = @{ Authorization = "Bearer $($login.accessToken)" }

# 全部房间
Invoke-RestMethod -Uri "$base/api/workspaces/$ws/rooms" -Headers $headers

# 2026-08-27 当天的全部预约（改日期即可）
$from = [DateTime]::Parse("2026-08-27 00:00:00").ToUniversalTime().ToString("o")
$to = [DateTime]::Parse("2026-08-28 00:00:00").ToUniversalTime().ToString("o")
Invoke-RestMethod -Uri "$base/api/workspaces/$ws/bookings?fromUtc=$from&toUtc=$to" -Headers $headers
```

预约记录里的 `startAtUtc/endAtUtc` 是 UTC，+8 即北京时间。

### 场景 E：清空团队数据重来（慎用！删掉全部房间、预约、成员）

服务器上执行：

```bash
systemctl stop cccalendar && rm -rf /root/cccalendar/data && systemctl start cccalendar
```

然后必须重新建房（见场景 C 循环建房），所有客户端重新点一次"登录团队"。
**注意：清 data 不会影响任何电脑上的本地日程/待办。**

### 场景 F：修改团队口令 / 签名密钥

服务器上编辑服务配置：

```bash
nano /etc/systemd/system/cccalendar.service
# 修改 Environment=Authentication__DevToken__SharedSecret=新口令
# 保存退出：Ctrl+O 回车，Ctrl+X
systemctl daemon-reload && systemctl restart cccalendar
```

改完通知全员：设置 → 团队连接 → "开发令牌口令"改成新口令 → 重新点"登录团队"。

---

## 4. 客户端电脑操作（每台同事电脑）

### 4.1 新电脑安装与登录（发给同事的说明）

1. 拿到安装包 `cccalendar-<版本号>-win-x64-setup.exe`（约 60MB，版本号见公网清单），双击安装；
2. 打开 cccalendar → 设置 → 团队连接，填写：

| 字段 | 填什么 |
|------|--------|
| 服务端地址 | `http://47.120.6.126:5080/` |
| OIDC 各项 | **全部留空** |
| 开发令牌姓名 | 自己的真实姓名（如 `周家丞`，**一字不差**） |
| 开发令牌口令 | 团队共享口令（见 §1 的取值方式，经私密渠道获取） |

3. 点"登录团队"，状态显示"已登录"即成功（工作区 ID 自动回填，不用手填）。

安装包获取：开发机 `D:\myProgram\cccalendar\artifacts\installer\` 目录，微信/共享盘传给同事即可。

### 4.2 登录后能用什么

- **快速新增 → 会议室时间视图**：房间列自动变成云端 10 间真实会议室；灰色占用块 = 全团队在线预约，**鼠标悬停显示会议主题、会议室、起止时间**；
- **保存带会议室的日程**时自动向云端提交预约；他人已约同时段会弹"在线预约冲突"，本地日程仍保存；
- **粘贴会议邀请**：快速新增左上"会议邀请"框，点"粘贴会议邀请"按钮或直接粘入原文；确认内容后点击"导入"或按 Enter，才会填充主题/会议号/时间，并把"与会地点"模糊匹配到会议室（如"佛山西樵研发中心三楼会议室"→"研发中心三楼会议室"）；
- 本地日程/待办/看板与团队数据完全独立，断网离线一切照常。

### 4.3 令牌过期（7 天后）

过期后团队功能停更；客户端下次启动会使用已保存姓名和团队口令自动重新登录，必要时也可点"登录团队"手动重登。本地数据不受任何影响。

### 4.4 换电脑 / 重装系统迁移

1. 新电脑装同版本客户端并登录；
2. 本地日程数据在旧电脑的这个文件夹（可选迁移）：

```text
C:\Users\<用户名>\AppData\Local\cccalendar\
```

把整个文件夹拷到新电脑相同位置即可带走全部本地日程/待办。团队预约不用迁——都在云端。

### 4.5 卸载/覆盖安装

卸载和升级安装都**不会**删本地数据（数据在 AppData，不在安装目录）。彻底清除才需要手动删上面的文件夹。

---

## 5. 故障排查对照表

| 症状 | 先查什么 | 处理 |
|------|---------|------|
| 登录报"连接尝试失败/超时" | 开发机 `Invoke-RestMethod http://47.120.6.126:5080/health` | 通→客户端网络问题；不通→查服务器 2.2 和安全组 5080 |
| 登录报 401 | 口令是否输错、姓名是否带空格 | 按 4.1 核对口令（见 §1 取值方式） |
| 看板房间列还是本地默认 | 是否已登录团队（设置页状态） | 重新登录；登录后看板约 1 秒刷新 |
| 看板占用块不出现 | 另一台电脑是否真保存了带会议室的日程 | 用场景 D 从开发机查当天预约确认 |
| 弹"在线预约冲突" | 该时段该会议室是否已被占用 | 悬停灰色块看是谁约的，换时段或换会议室 |
| 提示"开发令牌登录失败"但网络正常 | 令牌是否过期（7 天） | 客户端启动时会使用已保存姓名和团队口令自动重新登录；如仍失败再手动点"登录团队" |
| 服务挂了 | 服务器 2.2 看状态 | systemd 会自动拉起；反复挂看 2.3 日志贴给开发 |

---

## 6. 安全提醒（现状与边界）

- 现状是 **HTTP 明文 + 姓名口令登录**，适用于团队验收期/内网级信任环境；
- 团队共享口令只发给团队成员，经私密渠道传递，**不要写进公开仓库、文档或群公告**；泄露后立即按场景 F 更换；
- 长期正式使用前需升级：域名 + HTTPS（Let's Encrypt）+ OIDC 正式登录，届时关闭 DevToken（已在 WORKLIST 排期）；
- 服务器 SSH 密码不要与团队口令相同；安全组只保留 22/5080 两个端口。
