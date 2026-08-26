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
}
"@

$PidOfApp = 28960

function Get-DayState([string]$pattern) {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
  foreach ($w in $wins) {
    if ($w.Current.Name -eq '' -and $w.Current.BoundingRectangle.Width -gt 900) {
      $days = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
      foreach ($d in $days) { if ($d.Current.Name -like $pattern) { return $d } }
    }
  }
  return $null
}

function Find-QuickAdd {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
  foreach ($w in $wins) { if ($w.Current.Name -like '*快速新增*') { return $w } }
  return $null
}

# Verify current position of 8/21 cell
$d21 = Get-DayState '*Date = 2026/8/21*'
$r = $d21.Current.BoundingRectangle
$cx = [int]($r.Left + $r.Width / 2)
$cy = [int]($r.Top + 10)
Write-Output ("Aug21 cell rect=" + $r.ToString() + " click point=(" + $cx + "," + $cy + ")")

# Double click on the date-number strip of the 8/21 cell
[RealMouse]::SetCursorPos($cx, $cy) | Out-Null
Start-Sleep -Milliseconds 300
[RealMouse]::mouse_event([RealMouse]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RealMouse]::mouse_event([RealMouse]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 80
[RealMouse]::mouse_event([RealMouse]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RealMouse]::mouse_event([RealMouse]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1800

$ts = Get-Date -Format "HHmmss"
$sp = "D:/myProgram/cccalendar/screenshots/qa10_dbclick0821_v2_$ts.png"
Take-Screenshot -Path $sp
Write-Output ("SCT=" + $sp)

$qa = Find-QuickAdd
if ($null -eq $qa) {
  Write-Output "TEST_A_FAIL: QuickAddWindow NOT opened after double-clicking 2026/8/21"
  $d21b = Get-DayState '*Date = 2026/8/21*'
  Write-Output ("Aug21 state now: " + $d21b.Current.Name.Substring(0, [Math]::Min(110, $d21b.Current.Name.Length)))
} else {
  Write-Output "TEST_A_PASS: QuickAddWindow OPENED after double-clicking non-today 2026/8/21"
  Write-Output ("QuickAdd rect=" + $qa.Current.BoundingRectangle.ToString())
}
