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
if (-not $PidOfApp -or -not $Title) { Write-Output "FAIL: usage: -PidOfApp <pid> -Title <todo title>"; exit 1 }
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RMQ {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class WinQ {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

# ---- navigate to 待办 page (must foreground the window first, or clicks go elsewhere) ----
$main = Get-MainWindow
if ($null -eq $main) { Write-Output "FAIL: main window not found"; exit 1 }
$hwnd = [IntPtr]([int]($main.Current.NativeWindowHandle))
[void][WinQ]::ShowWindow($hwnd, 9)
[void][WinQ]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 600
$els0 = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$todoNav = $null
foreach ($i in $els0) { if ($i.Current.Name -eq '待办' -and $i.Current.ControlType.ProgrammaticName -eq 'ControlType.ListItem') { $todoNav = $i; break } }
if ($null -eq $todoNav) { foreach ($i in $els0) { if ($i.Current.Name -eq '待办') { $todoNav = $i; break } } }
if ($null -eq $todoNav) { Write-Output "FAIL: 待办 nav not found"; exit 1 }
$nr = $todoNav.Current.BoundingRectangle
[RMQ]::SetCursorPos([int]($nr.Left + $nr.Width / 2), [int]($nr.Top + $nr.Height / 2)) | Out-Null
Start-Sleep -Milliseconds 300
[RMQ]::mouse_event([RMQ]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 50
[RMQ]::mouse_event([RMQ]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 2000

# ---- locate card and quadrant headers after restart ----
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
if ($null -eq $card) { Write-Output "PERSIST=FAIL (card not found after restart)"; exit 1 }
$r = $card.Current.BoundingRectangle
$cx = $r.Left + $r.Width / 2
$cy = $r.Top + $r.Height / 2
$tl = $quads['重要且紧急']; $tr = $quads['重要不紧急']; $bl = $quads['不重要但紧急']; $br = $quads['不重要不紧急']
if ($null -eq $tl -or $null -eq $tr -or $null -eq $bl -or $null -eq $br) { Write-Output "PERSIST=FAIL (quadrant headers missing)"; exit 1 }
$colLeft = ($tl.Left + $tr.Right) / 2
$rowTop = ($tl.Bottom + $bl.Top) / 2
$col = if ($cx -lt $colLeft) { 'left' } else { 'right' }
$row = if ($cy -lt $rowTop) { 'top' } else { 'bottom' }
$name = switch ("$col-$row") {
  'left-top' { '重要且紧急' }
  'right-top' { '重要不紧急' }
  'left-bottom' { '不重要但紧急' }
  'right-bottom' { '不重要不紧急' }
}
Write-Output ("CARD_AFTER_RESTART rect=" + [int]$r.Left + "," + [int]$r.Top + " quadrant='" + $name + "'")
if ($name -eq '重要且紧急') { Write-Output "PERSIST=PASS" } else { Write-Output "PERSIST=FAIL (expected 重要且紧急)" }
