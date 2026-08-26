param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

if (-not $PidOfApp) { Write-Output "FAIL: no pid"; exit 1 }

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
$main = $null
foreach ($w in $wins) { if ($w.Current.Name -eq 'cccalendar') { $main = $w } }
if ($null -eq $main) { Write-Output "FAIL: main window not found"; exit 1 }

# Show/restore main window
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinR {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
$hwnd = [IntPtr]([int]($main.Current.NativeWindowHandle))
[void][WinR]::ShowWindow($hwnd, 9)
[void][WinR]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 800

# Find and click the 待办 nav item
$items = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$todoNav = $null
foreach ($i in $items) { if ($i.Current.Name -eq '待办' -and $i.Current.ControlType.ProgrammaticName -eq 'ControlType.ListItem') { $todoNav = $i; break } }
if ($null -eq $todoNav) {
  foreach ($i in $items) { if ($i.Current.Name -eq '待办') { $todoNav = $i; break } }
}
if ($null -eq $todoNav) { Write-Output "FAIL: 待办 nav not found"; exit 1 }

$rect = $todoNav.Current.BoundingRectangle
$cx = [int]($rect.Left + $rect.Width / 2)
$cy = [int]($rect.Top + $rect.Height / 2)
Write-Output ("NAV_TODO at " + $rect.Left + "," + $rect.Top + " size " + $rect.Width + "x" + $rect.Height)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RM8 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
"@
[RM8]::SetCursorPos($cx, $cy) | Out-Null
Start-Sleep -Milliseconds 200
[RM8]::mouse_event([RM8]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 50
[RM8]::mouse_event([RM8]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1200

function Take-Screenshot { param([string]$Path) $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height; $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size); $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose() }
$ts = Get-Date -Format "HHmmss"
$sp = "D:/myProgram/cccalendar/screenshots/qa12_todopage_$ts.png"
Take-Screenshot -Path $sp
Write-Output ("SCT=" + $sp)

# Dump todo page structure: quadrant titles and todo titles
$main2 = $null
$wins2 = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
foreach ($w in $wins2) { if ($w.Current.Name -eq 'cccalendar') { $main2 = $w } }
$els = $main2.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$quadrants = @('重要且紧急', '重要不紧急', '不重要但紧急', '不重要不紧急')
Write-Output "=== QUADRANT HEADERS FOUND ==="
foreach ($e in $els) {
  if ($quadrants -contains $e.Current.Name) { Write-Output ("QUAD: " + $e.Current.Name) }
}
Write-Output "=== TEXT ELEMENTS WITH TODO-LIKE NAMES ==="
$count = 0
foreach ($e in $els) {
  $n = $e.Current.Name
  if ($n -and $n.Length -gt 0 -and $n -notmatch '^(待办|四象限|看板|列表|新增待办)$') {
    if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Text') {
      Write-Output ("TXT: '" + $n + "' rect=" + [int]$e.Current.BoundingRectangle.Left + "," + [int]$e.Current.BoundingRectangle.Top)
      $count++
      if ($count -gt 40) { break }
    }
  }
}
