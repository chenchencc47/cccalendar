param([string]$Path)
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap $Path
# 板区域: image x 470-1100, y 118-545
# 扫描水平线: 对每个 y, 统计 x 480-1090 中非白像素数
Write-Output "--- 水平灰线 (非白像素>200 的行) ---"
for ($y = 118; $y -lt 545; $y++) {
  $cnt = 0
  for ($x = 480; $x -lt 1090; $x += 2) {
    $c = $bmp.GetPixel($x, $y)
    if (-not ($c.R -gt 245 -and $c.G -gt 245 -and $c.B -gt 245)) { $cnt++ }
  }
  if ($cnt -gt 200) { Write-Output ("y=" + $y + " count=" + $cnt) }
}
Write-Output "--- 垂直灰线 (非白像素>150 的列) ---"
for ($x = 470; $x -lt 1100; $x++) {
  $cnt = 0
  for ($y = 130; $y -lt 540; $y += 2) {
    $c = $bmp.GetPixel($x, $y)
    if (-not ($c.R -gt 245 -and $c.G -gt 245 -and $c.B -gt 245)) { $cnt++ }
  }
  if ($cnt -gt 150) { Write-Output ("x=" + $x + " count=" + $cnt) }
}
$bmp.Dispose()
