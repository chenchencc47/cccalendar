param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class PX1 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class WinPX1 {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
function Get-MainWindow {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
  foreach ($w in $wins) { if ($w.Current.Name -eq 'cccalendar') { return $w } }
  return $null
}
$main = Get-MainWindow
if ($null -eq $main) { Write-Output "FAIL: main window not found"; exit 1 }
$mainHwnd = [IntPtr]([int]($main.Current.NativeWindowHandle))
[void][WinPX1]::ShowWindow($mainHwnd, 9)
[void][WinPX1]::SetForegroundWindow($mainHwnd)
Start-Sleep -Milliseconds 800

# 点击快速新增按钮
$btn = $main.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "快速新增")))
if ($null -eq $btn) {
  $els = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($e in $els) { if ($e.Current.Name -eq '快速新增' -and $e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button') { $btn = $e; break } }
}
if ($null -eq $btn) { Write-Output "FAIL: 快速新增 button not found"; exit 1 }
$br = $btn.Current.BoundingRectangle
[PX1]::SetCursorPos([int]($br.Left + $br.Width / 2), [int]($br.Top + $br.Height / 2)) | Out-Null
Start-Sleep -Milliseconds 300
[PX1]::mouse_event([PX1]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[PX1]::mouse_event([PX1]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 2000

# 截图并采样窗口顶部像素
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$sp = "D:/myProgram/cccalendar/screenshots/qa27_border_$((Get-Date -Format 'HHmmss')).png"
$bmp.Save($sp, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
Write-Output ("SCT=" + $sp)

# 找快速新增窗口
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
foreach ($w in $wins) {
  $r = $w.Current.BoundingRectangle
  if ($r.Width -gt 100) {
    Write-Output ("WIN '" + $w.Current.Name + "' rect=" + [int]$r.Left + "," + [int]$r.Top + " " + [int]$r.Width + "x" + [int]$r.Height)
    # 采样顶部边框像素行（y = Top, Top+1, Top+2 ... Top+6）与标题栏中部
    $cx = [int]($r.Left + $r.Width / 2)
    for ($dy = 0; $dy -le 6; $dy++) {
      $c = $bmp.GetPixel($cx, [int]$r.Top + $dy)
      Write-Output ("  pixel(x=" + $cx + ", y=" + ([int]$r.Top + $dy) + ") = R" + $c.R + " G" + $c.G + " B" + $c.B + " #" + $c.ToArgb().ToString("X8"))
    }
    # 标题栏中部（Top+15）
    $c2 = $bmp.GetPixel($cx, [int]$r.Top + 15)
    Write-Output ("  titlebar mid = R" + $c2.R + " G" + $c2.G + " B" + $c2.B + " #" + $c2.ToArgb().ToString("X8"))
  }
}
$bmp.Dispose()
