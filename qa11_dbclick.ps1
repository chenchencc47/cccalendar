param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RM7 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class WinMin7 {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@
function Take-Screenshot { param([string]$Path) $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height; $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size); $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose() }

if (-not $PidOfApp) { $PidOfApp = 27808 }

function Get-Workbench {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
  foreach ($w in $wins) { if ($w.Current.Name -eq '' -and $w.Current.BoundingRectangle.Width -gt 900) { return $w } }
  return $null
}

# Minimize the main window (title cccalendar)
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
foreach ($w in $wins) {
  if ($w.Current.Name -eq 'cccalendar') {
    $hwnd = [IntPtr]([int]($w.Current.NativeWindowHandle))
    [void][WinMin7]::ShowWindow($hwnd, 6)
    Write-Output "Main window minimized"
  }
}
Start-Sleep -Milliseconds 800

$wb = Get-Workbench
if ($null -eq $wb) { Write-Output "FAIL: workbench not found"; exit 1 }

# Find Aug 21 day cell
$days = $wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$d21 = $null
foreach ($d in $days) { if ($d.Current.Name -like '*Date = 2026/8/21*') { $d21 = $d } }
if ($null -eq $d21) { Write-Output "FAIL: Aug21 not found"; exit 1 }
$r = $d21.Current.BoundingRectangle
$cx = [int]($r.Left + $r.Width / 2)
$cy = [int]($r.Top + 10)
Write-Output ("Aug21 cell at " + $r.ToString() + " -> click (" + $cx + "," + $cy + ")")

# Double click
[RM7]::SetCursorPos($cx, $cy) | Out-Null
Start-Sleep -Milliseconds 300
[RM7]::mouse_event([RM7]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM7]::mouse_event([RM7]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 90
[RM7]::mouse_event([RM7]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM7]::mouse_event([RM7]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 2000

$ts = Get-Date -Format "HHmmss"
$sp = "D:/myProgram/cccalendar/screenshots/qa11_dbclick0821_$ts.png"
Take-Screenshot -Path $sp
Write-Output ("SCT=" + $sp)

# Check quick add window
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
$qa = $null
foreach ($w in $wins) { if ($w.Current.Name -like '*2026*' -and $w.Current.Name -ne 'cccalendar') { $qa = $w; Write-Output ("Candidate window: '" + $w.Current.Name + "'") } }

# also check selection state
$wb2 = Get-Workbench
if ($null -ne $wb2) {
  $days2 = $wb2.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($d in $days2) {
    if ($d.Current.Name -like '*Date = 2026/8/21*') {
      $sel = $d.Current.Name -match 'IsSelected = True'
      Write-Output ("AUG21_SELECTED=" + $sel)
      break
    }
  }
}
