# OSS 发布操作手册

本手册用于把 Windows 安装包发布到当前 OSS Bucket，并让客户端通过 `version.json` 发现新版本。

## 当前发布地址

- Bucket：`cccalendar-releases-01`
- Region：`cn-heyuan`
- Endpoint：`oss-cn-heyuan.aliyuncs.com`
- 公网清单：`https://cccalendar-releases-01.oss-cn-heyuan.aliyuncs.com/releases/version.json`
- 本机工具：`D:\ossutil\ossutil-2.3.0-windows-amd64\ossutil.exe`

## 发布流程

每次发布都要递增版本号，例如当前 `0.4.5` 后使用 `0.4.6`，不要覆盖旧版本目录。

1. 修改 `src/CcCalendar.Desktop/CcCalendar.Desktop.csproj` 的 `<Version>`。
2. 修改 `installer/cccalendar.iss` 的 `AppVersion`。
3. 在项目根目录构建安装包：

```powershell
cd D:\myProgram\cccalendar
cmd /c eng\publish.cmd
```

安装包位于：

```text
artifacts\installer\cccalendar-版本号-win-x64-setup.exe
```

## 生成 version.json

```powershell
$version = "0.4.5"
$installer = "artifacts\installer\cccalendar-$version-win-x64-setup.exe"
$bucket = "cccalendar-releases-01"
$region = "cn-heyuan"
$sha256 = (Get-FileHash $installer -Algorithm SHA256).Hash
$url = "https://$bucket.oss-$region.aliyuncs.com/releases/$version/cccalendar-$version-win-x64-setup.exe"

@{
  version = $version
  url = $url
  sha256 = $sha256
  mandatory = $false
} | ConvertTo-Json | Set-Content version.json -Encoding UTF8

Get-Content version.json
```

## 上传 OSS

如果 `ossutil` 没有加入 PATH，直接使用绝对路径：

```powershell
$ossutil = "D:\ossutil\ossutil-2.3.0-windows-amd64\ossutil.exe"
$version = "0.4.5"
$installer = "artifacts\installer\cccalendar-$version-win-x64-setup.exe"

& $ossutil cp -f $installer "oss://cccalendar-releases-01/releases/$version/cccalendar-$version-win-x64-setup.exe"
& $ossutil cp -f version.json "oss://cccalendar-releases-01/releases/version.json"
```

先上传安装包，再上传 `version.json`。这样客户端不会先看到一个尚未上传完成的安装包。

## 公网校验

```powershell
$manifest = Invoke-RestMethod "https://cccalendar-releases-01.oss-cn-heyuan.aliyuncs.com/releases/version.json"
$head = Invoke-WebRequest -Method Head $manifest.url
$localHash = (Get-FileHash $installer -Algorithm SHA256).Hash

if ($manifest.version -ne $version) { throw "公网版本不是 $version" }
if ($manifest.sha256 -ne $localHash) { throw "公网清单哈希与本地不一致" }
if ($head.StatusCode -ne 200) { throw "安装包 HTTP 状态不是 200" }

[pscustomobject]@{
  Version = $manifest.version
  Sha256Matches = ($manifest.sha256 -eq $localHash)
  HttpStatus = $head.StatusCode
  ContentLength = $head.Headers['Content-Length']
}
```

## 常见问题

- `ossutil` 找不到：使用上面的绝对路径，或把 `D:\ossutil\ossutil-2.3.0-windows-amd64` 加入当前用户 PATH 后重新打开 PowerShell。
- HTTP 403：检查 Bucket 是否公共读，以及 AccessKey 是否有对象写权限。
- HTTP 404：检查 Region、Bucket 名称和 `releases/<版本>/` 路径。
- 客户端没有发现更新：确认公网 `version.json` 的版本号高于客户端当前版本，且 `sha256` 非空。

## 安全边界

- AccessKey ID/Secret 只保存在本机 ossutil 配置，不写入仓库或脚本。
- 发布 Bucket 只开放公共读，写入权限仅授予 RAM 发布用户。
- 当前客户端使用 HTTPS 和 SHA-256 校验；静默安装、代码签名和回滚仍属于后续工作。
