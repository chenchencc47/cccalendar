# 手动新增 Team 开发者操作手册

当前团队使用云端 Server 的 DevToken 模式。新增成员不需要创建阿里云 RAM 用户，也不需要修改 OSS；只需要使用新成员的姓名登录一次，服务端会按姓名稳定派生用户 ID 并加入当前工作区。

## 当前团队配置

- 服务端：`http://47.120.6.126:5080/`
- 工作区 ID：`11111111-1111-1111-1111-111111111111`
- 登录模式：开发令牌（OIDC 地址全部留空）
- 角色：当前 DevToken 配置的默认角色为 `Admin`；正式团队建议后续改为按成员分配 `Member` 或 `Viewer`。

不要把服务端口令写入公开文档、代码仓库或群公告。通过私下渠道把当前团队口令发给成员；如果口令泄露，按云服务器手册修改 `Authentication__DevToken__SharedSecret` 并通知全员重新登录。

## 给新成员的客户端步骤

1. 安装当前版本客户端，例如 `cccalendar-0.4.5-win-x64-setup.exe`。
2. 打开“设置”→“团队连接”。
3. 填写：

| 字段 | 填写内容 |
|---|---|
| 服务端地址 | `http://47.120.6.126:5080/` |
| OIDC 授权地址 | 留空 |
| OIDC 令牌地址 | 留空 |
| 客户端 ID | 留空 |
| 工作区 ID | 可留空，登录成功后自动回填；也可填当前工作区 ID |
| 开发令牌姓名 | 新成员真实姓名，前后不要加空格 |
| 开发令牌口令 | 当前团队共享口令，通过私下渠道获取 |

4. 点击“登录团队”。
5. 状态显示已登录后，进入“快速新增”查看会议室目录和团队预约。

同一个姓名在不同电脑登录，会得到同一个用户 ID。姓名一旦改写，哪怕只是多一个空格或使用不同字符，也会被视为另一个用户。

## 角色说明

- `Viewer`：查看会议室和预约。
- `Member`：在已有会议室中创建预约。
- `Admin`：管理会议室和团队数据。
- `Owner`：最高管理角色。

当前 DevToken 模式按服务端统一默认角色签发，不能在客户端输入框中单独选择角色。需要差异化角色时，应改用服务端 `Bootstrap:Memberships` 或正式 OIDC/成员管理接口。

## 管理员手动预置成员

如果需要在成员首次登录前预置角色，先用服务端生成稳定用户 ID。不要手工猜 GUID。可在开发机 PowerShell 执行：

```powershell
$name = "新成员姓名"
$namespace = [Guid]"8f1c3a52-9d47-4b6e-a3f2-5c9d0e7b1a44"
$bytes = $namespace.ToByteArray() + [Text.Encoding]::UTF8.GetBytes($name.Trim())
$hash = [Security.Cryptography.SHA256]::HashData($bytes)
$hash[6] = ($hash[6] -band 0x0f) -bor 0x50
$hash[8] = ($hash[8] -band 0x3f) -bor 0x80
$userId = [Guid]::new($hash[0..15])
$userId
```

然后在 Server 的配置中增加 Bootstrap 项：

```json
{
  "Bootstrap": {
    "Memberships": [
      {
        "WorkspaceId": "11111111-1111-1111-1111-111111111111",
        "UserId": "这里填上面计算的 userId",
        "Role": "Member"
      }
    ]
  }
}
```

生产服务器使用 systemd 时，建议通过独立配置文件或环境变量管理，不要直接改发布目录内的程序文件。修改后：

```bash
sudo systemctl daemon-reload
sudo systemctl restart cccalendar
curl http://127.0.0.1:5080/health
```

注意：当前 file 存储的成员实现是内存存储，服务重启后会由 DevToken 登录重新写入；PostgreSQL 模式才会持久化成员表。正式长期使用建议迁移 PostgreSQL + HTTPS + OIDC。

## 成员离职或姓名变更

当前没有独立的“禁用成员”界面。临时处理方式：

1. 更换团队共享口令；
2. 通知保留成员更新客户端口令并重新登录；
3. 正式环境改为 OIDC，并通过身份提供商禁用账号。

## 排障

- 登录 401：检查服务端地址、姓名是否有空格、共享口令是否正确。
- 连接超时：在客户端执行 `Invoke-RestMethod http://47.120.6.126:5080/health`。
- 登录成功但看不到会议室：确认工作区 ID一致，并重新打开快速新增。
- 令牌过期：再次点击“登录团队”，本地日程不会受影响。
