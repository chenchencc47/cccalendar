# 只保留最近 N 个安装包，删除更旧的本地副本。
# 历史安装包已在 GitHub Releases 归档（里程碑版本），本地无需堆积。
param([int]$Keep = 3)

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$installerDir = Join-Path (Get-Location) 'artifacts\installer'
if (-not (Test-Path -LiteralPath $installerDir)) {
    Write-Host 'installer dir not found; nothing to prune.'
    exit 0
}

$all = Get-ChildItem -LiteralPath $installerDir -File -Filter 'cccalendar-*-win-x64-setup.exe' |
    Sort-Object LastWriteTime -Descending

$old = $all | Select-Object -Skip $Keep
foreach ($f in $old) {
    Remove-Item -LiteralPath $f.FullName -Force
    Write-Host ("pruned: " + $f.Name)
}

Write-Host ("installers kept: " + [Math]::Min($Keep, $all.Count) + " / " + $all.Count)
exit 0
