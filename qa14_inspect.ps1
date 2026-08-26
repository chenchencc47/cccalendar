param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RM12 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
"@
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
$main = $null
foreach ($w in $wins) { if ($w.Current.Name -eq 'cccalendar') { $main = $w } }
if ($null -eq $main) { Write-Output "FAIL: main window not found"; exit 1 }

$els0 = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$todoNav = $null
foreach ($i in $els0) { if ($i.Current.Name -eq '待办' -and $i.Current.ControlType.ProgrammaticName -eq 'ControlType.ListItem') { $todoNav = $i; break } }
if ($null -eq $todoNav) { foreach ($i in $els0) { if ($i.Current.Name -eq '待办') { $todoNav = $i; break } } }
if ($null -eq $todoNav) { Write-Output "FAIL: nav not found"; exit 1 }
$r = $todoNav.Current.BoundingRectangle
[RM12]::SetCursorPos([int]($r.Left + $r.Width / 2), [int]($r.Top + $r.Height / 2)) | Out-Null
Start-Sleep -Milliseconds 200
[RM12]::mouse_event([RM12]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 50
[RM12]::mouse_event([RM12]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1500

$main = $null
foreach ($w in $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)) { if ($w.Current.Name -eq 'cccalendar') { $main = $w } }
$els = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
foreach ($e in $els) {
  $n = $e.Current.Name
  $ct = $e.Current.ControlType.ProgrammaticName
  if ($n) { Write-Output ($ct + " | " + $n) }
}
