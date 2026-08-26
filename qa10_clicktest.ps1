Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
function Take-Screenshot { param([string]$Path) $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height; $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size); $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose() }

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RealMouse {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
  public const uint MOUSEEVENTF_LEFTUP = 0x0004;
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(int x, int y);
}
"@

$PidOfApp = 28960

function Get-Aug21 {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
  foreach ($w in $wins) {
    if ($w.Current.Name -eq '' -and $w.Current.BoundingRectangle.Width -gt 900) {
      $days = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
      foreach ($d in $days) { if ($d.Current.Name -like '*Date = 2026/8/21*') { return $d } }
    }
  }
  return $null
}

function Find-AnyQuickAdd {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
  foreach ($w in $wins) { if ($w.Current.Name -like '*快速新增*' -or $w.Current.Name -like '*新增*') { return $w.Current.Name } }
  return $null
}

# Single click on the date-number area of 8/21 cell: rect 1029,292,139,71 -> top strip y=302, x=1098
$before = Get-Aug21
Write-Output ("BEFORE: " + $before.Current.Name.Substring(0, [Math]::Min(110, $before.Current.Name.Length)))

$hwndAt = [RealMouse]::WindowFromPoint(1098, 302)
Write-Output ("HWND at (1098,302): " + $hwndAt)

[RealMouse]::SetCursorPos(1098, 302) | Out-Null
Start-Sleep -Milliseconds 250
[RealMouse]::mouse_event([RealMouse]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 80
[RealMouse]::mouse_event([RealMouse]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 800

$after = Get-Aug21
Write-Output ("AFTER single click: " + $after.Current.Name.Substring(0, [Math]::Min(110, $after.Current.Name.Length)))
$sel = $after.Current.Name -like '*Date = 2026/8/21, IsCurrentMonth = True, IsToday = False, IsSelected = True*'
Write-Output ("SINGLE_CLICK_SELECTED=" + $sel)

# Now double-click (second click with ClickCount=2 timing)
[RealMouse]::mouse_event([RealMouse]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 70
[RealMouse]::mouse_event([RealMouse]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1500

$qa = Find-AnyQuickAdd
if ($null -eq $qa) { Write-Output "DBCLICK_QUICKADD=NOT_OPENED" } else { Write-Output ("DBCLICK_QUICKADD=OPENED name='" + $qa + "'") }

$ts = Get-Date -Format "HHmmss"
$sp = "D:/myProgram/cccalendar/screenshots/qa10_clicktest_$ts.png"
Take-Screenshot -Path $sp
Write-Output ("SCT=" + $sp)

# enumerate all top windows of the app to see if any new window appeared
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
foreach ($w in $wins) { Write-Output ("WIN: '" + $w.Current.Name + "' Rect=" + $w.Current.BoundingRectangle.ToString()) }
