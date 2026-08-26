param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Diag {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
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
if ($null -eq $main) { Write-Output "FAIL: no main window"; exit 1 }
$hwnd = [IntPtr]([int]($main.Current.NativeWindowHandle))
[void][Diag]::ShowWindow($hwnd, 9)
[void][Diag]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 600

# find the QA card
$els = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$card = $null
foreach ($e in $els) { if ($e.Current.Name -like 'QA拖拽验证*') { $card = $e } }
if ($null -eq $card) { Write-Output "FAIL: card not found"; exit 1 }
$cr = $card.Current.BoundingRectangle
$sx = [int]($cr.Left + $cr.Width / 2)
$sy = [int]($cr.Top + $cr.Height / 2)
Write-Output ("CARD at " + [int]$cr.Left + "," + [int]$cr.Top + " size " + [int]$cr.Width + "x" + [int]$cr.Height + " -> click point (" + $sx + "," + $sy + ")")

# what window is at that point?
$hAt = [Diag]::WindowFromPoint($sx, $sy)
$procId = 0
[void][Diag]::GetWindowThreadProcessId($hAt, [ref]$procId)
$rect = New-Object Diag+RECT
[void][Diag]::GetWindowRect($hAt, [ref]$rect)
Write-Output ("WINDOW_AT_POINT: hwnd=" + $hAt + " pid=" + $procId + " rect=" + $rect.Left + "," + $rect.Top + " - " + $rect.Right + "," + $rect.Bottom)
Write-Output ("MAIN_HWND=" + $hwnd + " MAIN_PID=" + $PidOfApp)
Write-Output ("FOREGROUND=" + [Diag]::GetForegroundWindow())

# single click on the card
[Diag]::SetCursorPos($sx, $sy) | Out-Null
Start-Sleep -Milliseconds 400
Write-Output ("FOREGROUND_AFTER_MOVE=" + [Diag]::GetForegroundWindow())
[Diag]::mouse_event([Diag]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 80
[Diag]::mouse_event([Diag]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 800
Write-Output "single click done"
