param([int]$Left, [int]$Top, [int]$W, [int]$H, [string]$Name)
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$bmp = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($Left, $Top, 0, 0, [System.Drawing.Size]::new($W, $H))
$sp = "D:/myProgram/cccalendar/screenshots/" + $Name + ".png"
$bmp.Save($sp, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output ("SCT=" + $sp)
