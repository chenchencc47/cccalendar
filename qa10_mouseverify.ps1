Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RealMouse2 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
  public const uint MOUSEEVENTF_LEFTUP = 0x0004;
}
"@

$PidOfApp = 28960
$weekChar = [string][char]0x5468
$monthChar = [string][char]0x6708

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
$workbench = $null
foreach ($w in $wins) { if ($w.Current.Name -eq '' -and $w.Current.BoundingRectangle.Width -gt 900) { $workbench = $w } }
if ($null -eq $workbench) { Write-Output "FAIL: workbench not found"; exit 1 }

$btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$btns = $workbench.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
$weekBtn = $null
$monthBtn = $null
foreach ($b in $btns) {
  if ($b.Current.Name -eq $weekChar) { $weekBtn = $b }
  if ($b.Current.Name -eq $monthChar) { $monthBtn = $b }
}
if ($null -eq $weekBtn) { Write-Output "FAIL: week button not found"; exit 1 }

$r = $weekBtn.Current.BoundingRectangle
$cx = [int]($r.Left + $r.Width / 2)
$cy = [int]($r.Top + $r.Height / 2)
Write-Output ("week button center=(" + $cx + "," + $cy + ")")

[RealMouse2]::SetCursorPos($cx, $cy) | Out-Null
Start-Sleep -Milliseconds 250
[RealMouse2]::mouse_event([RealMouse2]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 80
[RealMouse2]::mouse_event([RealMouse2]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1500

$all = $workbench.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$dayCount = 0
foreach ($e in $all) { if ($e.Current.Name -like '*CalendarDayViewModel*') { $dayCount++ } }
Write-Output ("After week click: day cell count=" + $dayCount)
if ($dayCount -le 7) { Write-Output "MOUSE_EVENT_WORKS" } else { Write-Output "MOUSE_EVENT_BROKEN" }

if ($null -ne $monthBtn) {
  $r2 = $monthBtn.Current.BoundingRectangle
  $cx2 = [int]($r2.Left + $r2.Width / 2)
  $cy2 = [int]($r2.Top + $r2.Height / 2)
  [RealMouse2]::SetCursorPos($cx2, $cy2) | Out-Null
  Start-Sleep -Milliseconds 250
  [RealMouse2]::mouse_event([RealMouse2]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 80
  [RealMouse2]::mouse_event([RealMouse2]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 1000
  Write-Output "switched back to month"
}
