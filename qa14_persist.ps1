param([int]$PidOfApp, [string]$Title)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
function Get-MainWindow {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
  foreach ($w in $wins) { if ($w.Current.Name -eq 'cccalendar') { return $w } }
  return $null
}
if (-not $PidOfApp) { Write-Output "FAIL: no pid"; exit 1 }
$main = Get-MainWindow
if ($null -eq $main) { Write-Output "FAIL: main window not found"; exit 1 }

# ---- 前置主窗口，避免被置顶的桌面组件遮挡点击 ----
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RM10 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class Win10 {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
$mainHwnd = [IntPtr]([int]($main.Current.NativeWindowHandle))
[void][Win10]::ShowWindow($mainHwnd, 9)
[void][Win10]::SetForegroundWindow($mainHwnd)
Start-Sleep -Milliseconds 800

# ---- 导航到待办页 ----
$els0 = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$todoNav = $null
foreach ($i in $els0) { if ($i.Current.Name -eq '待办' -and $i.Current.ControlType.ProgrammaticName -eq 'ControlType.ListItem') { $todoNav = $i; break } }
if ($null -eq $todoNav) { foreach ($i in $els0) { if ($i.Current.Name -eq '待办') { $todoNav = $i; break } } }
if ($null -eq $todoNav) { Write-Output "FAIL: 待办 nav not found"; exit 1 }
$r = $todoNav.Current.BoundingRectangle
[RM10]::SetCursorPos([int]($r.Left + $r.Width / 2), [int]($r.Top + $r.Height / 2)) | Out-Null
Start-Sleep -Milliseconds 200
[RM10]::mouse_event([RM10]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 50
[RM10]::mouse_event([RM10]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1500

# ---- 定位卡片与象限表头 ----
$main = Get-MainWindow
$els = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$card = $null
$quads = @{}
$quadNames = @('重要且紧急', '重要不紧急', '不重要但紧急', '不重要不紧急')
foreach ($e in $els) {
  $n = $e.Current.Name
  if ($n -eq $Title) { $card = $e }
  if ($quadNames -contains $n) { $quads[$n] = $e.Current.BoundingRectangle }
}
if ($null -eq $card) { Write-Output "RESULT: card '$Title' not found"; exit 1 }
$r2 = $card.Current.BoundingRectangle
$cx2 = $r2.Left + $r2.Width / 2
$cy2 = $r2.Top + $r2.Height / 2
Write-Output ("CARD_AFTER_RESTART rect=" + [int]$r2.Left + "," + [int]$r2.Top)
$tl = $quads['重要且紧急']; $tr = $quads['重要不紧急']; $bl = $quads['不重要但紧急']; $br = $quads['不重要不紧急']
if ($null -eq $tl -or $null -eq $tr -or $null -eq $bl -or $null -eq $br) { Write-Output "RESULT: quadrant headers missing"; exit 1 }
$colLeft = ($tl.Left + $tr.Right) / 2
$rowTop = ($tl.Bottom + $bl.Top) / 2
$col = if ($cx2 -lt $colLeft) { 'left' } else { 'right' }
$row = if ($cy2 -lt $rowTop) { 'top' } else { 'bottom' }
$name = switch ("$col-$row") {
  'left-top' { '重要且紧急' }
  'right-top' { '重要不紧急' }
  'left-bottom' { '不重要但紧急' }
  'right-bottom' { '不重要不紧急' }
}
Write-Output ("RESULT: card is now in '" + $name + "' after restart")
if ($name -eq '重要且紧急') { Write-Output "PERSIST_TEST=PASS" } else { Write-Output "PERSIST_TEST=FAIL" }
