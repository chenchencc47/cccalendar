param([string]$Path)
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap $Path
# 图像坐标 = 屏幕坐标 - (370, 0)
# 板起点: screen(840,121) = image(470,121)
# 整点横线: image y = 121+26k; 半小时线: y=121+13+26k
# 会议室列边框: image x = 470+125k (595,720,845,970)
Write-Output "--- 垂直线检查 (image y=300, 板内部) ---"
foreach ($ix in @(590, 594, 595, 596, 600, 715, 719, 720, 721, 725)) {
  $c = $bmp.GetPixel($ix, 300)
  Write-Output ("x=" + $ix + ": #" + $c.R.ToString("X2") + $c.G.ToString("X2") + $c.B.ToString("X2"))
}
Write-Output "--- 水平线检查 (image x=600, 板内部) ---"
foreach ($iy in @(120, 121, 122, 133, 134, 135, 146, 147, 148, 159, 160)) {
  $c = $bmp.GetPixel(600, $iy)
  Write-Output ("y=" + $iy + ": #" + $c.R.ToString("X2") + $c.G.ToString("X2") + $c.B.ToString("X2"))
}
$bmp.Dispose()
