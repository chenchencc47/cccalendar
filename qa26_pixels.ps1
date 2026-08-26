param([string]$Path, [int]$L, [int]$T, [int]$W, [int]$H)
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap $Path
$gray = 0; $white = 0; $other = 0
$samples = @()
for ($x = $L; $x -lt ($L + $W); $x += 3) {
  for ($y = $T; $y -lt ($T + $H); $y += 3) {
    $c = $bmp.GetPixel($x, $y)
    if ($c.R -gt 245 -and $c.G -gt 245 -and $c.B -gt 245) { $white++ }
    elseif ($c.R -ge 200 -and $c.G -ge 200 -and $c.B -ge 200) { $gray++ }
    else { $other++; if ($samples.Count -lt 8) { $samples += ("(" + $x + "," + $y + ")=#" + $c.R.ToString("X2") + $c.G.ToString("X2") + $c.B.ToString("X2")) } }
  }
}
Write-Output ("white=" + $white + " gray=" + $gray + " other=" + $other)
if ($samples.Count -gt 0) { Write-Output ("non-bg samples: " + ($samples -join ' ')) }
# 检查会议室列分隔线位置 x=965 (板内 y=200)
foreach ($x in @(940, 960, 964, 965, 966, 970)) {
  $c = $bmp.GetPixel($x, 250)
  Write-Output ("col x=" + $x + " y=250: #" + $c.R.ToString("X2") + $c.G.ToString("X2") + $c.B.ToString("X2"))
}
# 检查整点横线 y=147 (9:00) 在板内 x=902
foreach ($y in @(140, 145, 147, 148, 150)) {
  $c = $bmp.GetPixel(902, $y)
  Write-Output ("row y=" + $y + " x=902: #" + $c.R.ToString("X2") + $c.G.ToString("X2") + $c.B.ToString("X2"))
}
$bmp.Dispose()
