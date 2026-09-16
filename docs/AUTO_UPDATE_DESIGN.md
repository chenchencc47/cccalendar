# 自动更新接入方案

## 目标

让已安装的 Windows 客户端能够发现新版本、提示用户、下载更新并完成安装，同时保留失败恢复能力。更新由客户端主动检查，不由阿里云服务器强制推送到用户电脑。

## 推荐架构

```text
WPF 客户端
  └─ 启动后/每日一次请求 HTTPS version.json
       ├─ 当前版本 < latestVersion：显示更新提示
       ├─ 下载 signed installer/update bundle
       ├─ 校验 SHA-256 + Authenticode/签名
       └─ 退出旧进程，启动更新器，安装完成后重新启动

阿里云 OSS/CDN 或 HTTPS 静态站点
  ├─ version.json
  └─ cccalendar-<version>-win-x64-setup.exe 或 Velopack 包
```

## 分阶段实施

### 第一阶段：发布清单与检查

1. 每次发布递增 `0.3.x` 版本，并生成安装包。
2. 将安装包上传到阿里云 OSS，绑定 HTTPS 自定义域名或 CDN。
3. 发布同目录 `version.json`：

```json
{
  "version": "0.3.6",
  "url": "https://download.example.com/cccalendar-0.3.6-win-x64-setup.exe",
  "sha256": "...",
  "mandatory": false,
  "releaseNotes": "0.3.6 更新内容：\\n\\n- 修复提醒重复发送。\\n- 优化启动速度。",
  "releaseNotesUrl": "https://download.example.com/releases/0.3.6.html"
}
```

4. 客户端启动后延迟检查，之后每 24 小时检查一次；网络失败只记录日志，不影响本地日历使用。

### 第二阶段：安全下载与安装

- 只允许 HTTPS，禁止从当前会议室 API 的 5080 端口下载安装包。
- 下载到 `%TEMP%\\cccalendar-update\\`，使用临时文件，避免覆盖正在运行的安装包。
- 先校验清单中的 SHA-256，再校验 Windows Authenticode 签名；任一失败都删除临时文件并提示更新失败。
- 用户确认后退出旧程序，启动安装器静默升级；安装器完成后重新启动应用。
- Inno Setup 使用同一 `AppId`，保持原安装目录和用户数据不变。

### 第三阶段：失败恢复与灰度

- 更新前写入 pending 状态，启动成功后清除；连续启动失败时保留旧版本安装包并提示回滚。
- 当前产品不启用强制更新；`mandatory` 保持 `false`，客户端始终要求用户主动确认。
- 生产发布前检查版本号、安装包哈希、签名、HTTPS 证书和 OSS 对象权限。

## 技术选型

推荐使用 Velopack 管理增量包、安装替换、启动参数和回滚；如果暂时不引入第三方组件，也可以用现有 Inno Setup 实现最小版本检查器，但需要额外维护下载、签名验证、进程退出和失败恢复代码。不要只实现“下载 EXE 并直接运行”，那样无法可靠验证来源，也无法处理安装失败。

## 当前代码已实现的第一阶段

- `ApplicationUpdateClient` 只接受 HTTPS manifest 地址，解析版本、下载地址和 SHA-256，并只返回高于当前版本的清单。
- WPF 启动完成后异步检查，不阻塞本地数据库、天气和会议室功能。
- 发现新版本时先显示目标版本号和 `releaseNotes`；后台检查只显示更新入口，不下载、不安装。只有用户点击更新并确认后，才下载并校验 HTTPS 安装包。
- 生产构建使用 `App.xaml.cs` 的 `DefaultUpdateManifestUrl`，当前已固化为 `https://cccalendar-releases-01.oss-cn-heyuan.aliyuncs.com/releases/version.json`，普通用户无需设置任何环境变量；`CCCALENDAR_UPDATE_MANIFEST_URL` 仅作为测试、灰度和回滚覆盖项。

## 一键安装更新的后续设计

当前 0.3.7 将更新显示为主窗口右上角的下载图标，点击后仍打开 HTTPS 下载地址。要做到应用内直接安装，下一阶段应改为：

1. 客户端从 `version.json` 下载到 `%TEMP%\\cccalendar-update\\`，不覆盖正在运行的程序。
2. 下载完成后计算 SHA-256，并与清单完全比对；再校验安装包 Authenticode 签名。
3. 校验通过后启动 Inno Setup：`/VERYSILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS`，客户端随后退出。
4. 安装器使用现有固定 `AppId` 覆盖旧文件，保留用户数据目录；启动成功后清理临时文件。
5. 下载失败、哈希不一致、签名无效或安装器退出异常时删除临时文件，旧版本保持不动。

这套流程需要先为安装包配置稳定的代码签名证书，并增加下载进度、取消、失败恢复和启动回滚测试。未完成签名校验前，不应让客户端静默执行远程下载的 EXE。

## OSS 发布操作

### A. 阿里云控制台

1. 登录阿里云控制台，进入“对象存储 OSS”→“Bucket 列表”→“创建 Bucket”。建议创建专用发布 Bucket，例如 `cccalendar-releases-随机后缀`，不要和会议室 Server 数据混用。
2. 选择离用户较近的地域，存储类型选“标准”，冗余类型按默认即可；开启版本控制，便于误上传后恢复旧版。
3. 读写权限建议设置为“公共读、禁止公共写”。安装包本身不是秘密，安全性由 HTTPS、SHA-256 和后续代码签名保证；写入权限只授予 RAM 发布账号。
4. 先使用 OSS 提供的 HTTPS Bucket 地址即可，例如 `https://<bucket>.oss-<region>.aliyuncs.com/releases/version.json`。后续稳定后再绑定自定义下载域名和 HTTPS 证书。桌面客户端不需要 CORS 配置。
5. 进入 RAM 创建专用用户，例如 `cccalendar-release-uploader`，只授予该 Bucket 的对象读写权限。创建 AccessKey 后只在本机 `ossutil` 配置中使用，不要提交到代码仓库或发到聊天中。

### B. 安装 Windows 版 ossutil

你当前的错误说明 `ossutil.exe` 尚未安装或不在 PATH。请从阿里云官方文档下载 Windows amd64 版本并解压，例如放到 `C:\Tools\ossutil\ossutil.exe`：

<https://www.alibabacloud.com/help/en/oss/developer-reference/ossutil>

然后以当前用户加入 PATH，重新打开 PowerShell：

```powershell
$toolDir = "C:\Tools\ossutil"
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($userPath -notlike "*$toolDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$userPath;$toolDir", "User")
}
```

验证安装并配置：

```powershell
ossutil version
ossutil config
```

按提示填写 RAM 用户的 AccessKey ID、AccessKey Secret、Region 和 Endpoint。配置文件默认保存在当前用户目录，不会进入项目目录。然后验证权限：

```powershell
ossutil ls oss://<bucket>/
```

### C. 上传安装包和版本清单

```powershell
$version = "0.3.6"
$installer = "artifacts\\installer\\cccalendar-$version-win-x64-setup.exe"
$sha256 = (Get-FileHash $installer -Algorithm SHA256).Hash
$bucket = "<bucket>"
$region = "<region>"
$downloadUrl = "https://$bucket.oss-$region.aliyuncs.com/releases/$version/cccalendar-$version-win-x64-setup.exe"

@{
  version = $version
  url = $downloadUrl
  sha256 = $sha256
  mandatory = $false
} | ConvertTo-Json | Set-Content version.json -Encoding UTF8

ossutil cp $installer "oss://$bucket/releases/$version/cccalendar-$version-win-x64-setup.exe"
ossutil cp version.json "oss://$bucket/releases/version.json"
```

验证公网读取：

```powershell
Invoke-RestMethod "https://$bucket.oss-$region.aliyuncs.com/releases/version.json"
Invoke-WebRequest "https://$bucket.oss-$region.aliyuncs.com/releases/$version/cccalendar-$version-win-x64-setup.exe" -Method Head
```

如果返回 403，说明 Bucket 或对象不是公共读；如果返回 404，检查 Region、Bucket 名称和对象路径。

### D. 固化到客户端

把最终的 `version.json` HTTPS 地址写入 `src/CcCalendar.Desktop/App.xaml.cs` 的 `DefaultUpdateManifestUrl`，然后递增版本并重新生成安装包。之后普通用户安装即可自动检查，不需要设置环境变量。

### E. 我可以代你执行的范围

你完成 Bucket、RAM 用户和 `ossutil config` 后，我可以在本机执行构建、哈希计算、生成 `version.json`、上传和公网验证。当前机器还没有 `ossutil`，因此第一步必须先安装它并完成配置；不要把 AccessKey Secret 发送给我。

## 与当前项目的关系

- 当前客户端是 WPF，安装器是 Inno Setup，适合上述客户端主动检查模型。
- 阿里云会议室 Server 继续只负责会议室/预约 API；更新文件放在独立 HTTPS 下载域名或 OSS/CDN。
- 当前版本 **0.6.6**（本文档写于 0.4.5 时期，此处已按实际状态校正）。第一阶段的 manifest 检查、设置页手动检查、右上角更新图标、完整 RGB 调色板均已完成； .6.1 起已接入**下载后校验 SHA-256 → 自动关闭 → 安装 → 重启**的一键安装流程（见 `ASSISTANT_WORKLIST.md` 的 P35）。**仍未完成**：代码签名（Authenticode）校验、灰度发布与失败回滚。
- 由于 `0.3.5` 安装包发布时还没有更新检查代码，`0.3.5` 用户需要先手动安装一次 `0.3.6`；从 `0.3.6` 开始后续版本即可自动发现。
- OSS Bucket 已确定为 `cccalendar-releases-01`（华南 2 河源），本轮上传 `0.3.7` 安装包和 `version.json`。
