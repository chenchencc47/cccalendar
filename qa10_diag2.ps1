Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RM5 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
"@

$PidOfApp = 28960

function Get-Workbench {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
  foreach ($w in $wins) { if ($w.Current.Name -eq '' -and $w.Current.BoundingRectangle.Width -gt 900) { return $w } }
  return $null
}

function Click-At([int]$x, [int]$y) {
  [RM5]::SetCursorPos($x, $y) | Out-Null
  Start-Sleep -Milliseconds 250
  [RM5]::mouse_event([RM5]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 80
  [RM5]::mouse_event([RM5]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 900
}

$wb = Get-Workbench

# Test 1: click the previous-month nav button (find it first)
$btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$btns = $wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
$prevBtn = $null
foreach ($b in $btns) { if ($b.Current.Name -eq ([string][char]0x2039)) { $prevBtn = $b } }
if ($null -ne $prevBtn) {
  $r = $prevBtn.Current.BoundingRectangle
  $cx = [int]($r.Left + $r.Width / 2); $cy = [int]($r.Top + $r.Height / 2)
  Write-Output ("prev button at (" + $cx + "," + $cy + ")")
  $titleBefore = $null
  # find DisplayTitle text (contains 2026)
  $all = $wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($e in $all) { if ($e.Current.Name -like '*2026*8*') { $titleBefore = $e.Current.Name; break } }
  Write-Output ("Title before: " + $titleBefore)
  Click-At $cx $cy
  $all = $wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  $titleAfter = $null
  foreach ($e in $all) { if ($e.Current.Name -like '*2026*7*' -or $e.Current.Name -like '*2026*8*') { $titleAfter = $e.Current.Name; break } }
  Write-Output ("Title after prev click: " + $titleAfter)
  if ($titleBefore -ne $titleAfter) { Write-Output "NAV_BUTTON_WORKS" } else { Write-Output "NAV_BUTTON_BROKEN" }
  # navigate back
  $nextBtn = $null
  foreach ($b in $btns) { if ($b.Current.Name -eq ([string][char]0x203A)) { $nextBtn = $b } }
  if ($null -ne $nextBtn) {
    $r2 = $nextBtn.Current.BoundingRectangle
    Click-At ([int]($r2.Left + $r2.Width / 2)) ([int]($r2.Top + $r2.Height / 2))
  }
}

# Test 2: click day cell 8/2 at top-left area of grid (rect was 475ish? check current)
$all = $wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$day2 = $null
foreach ($e in $all) { if ($e.Current.Name -like '*Date = 2026/8/2,*') { $day2 = $e } }
if ($null -ne $day2) {
  $r = $day2.Current.BoundingRectangle
  $cx = [int]($r.Left + $r.Width / 2); $cy = [int]($r.Top + 10)
  Write-Output ("Aug2 cell click at (" + $cx + "," + $cy + ")")
  Click-At $cx $cy
  $all = $wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($e in $all) {
    if ($e.Current.Name -like '*Date = 2026/8/2,*') {
      $sel = $e.Current.Name -match 'IsSelected = True'
      Write-Output ("AUG2_SELECTED=" + $sel)
      break
    }
  }
}
