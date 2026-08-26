Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WM {
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
  public const uint WM_LBUTTONDOWN = 0x0201;
  public const uint WM_LBUTTONUP = 0x0202;
  public const int MK_LBUTTON = 0x0001;
  public static IntPtr MakeLParam(int x, int y) { return (IntPtr)(((y & 0xffff) << 16) | (x & 0xffff)); }
}
"@

$PidOfApp = 28960
$hwnd = [IntPtr]133728

function Get-Day([string]$pattern) {
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

# Convert screen (1098,302) to client coords of workbench
$pt = New-Object WM+POINT
$pt.X = 1098; $pt.Y = 302
[void][WM]::ScreenToClient($hwnd, [ref]$pt)
Write-Output ("Client coords: " + $pt.X + "," + $pt.Y)
$lp = [WM]::MakeLParam($pt.X, $pt.Y)

# Single click via SendMessage
$null = [WM]::SendMessage($hwnd, [WM]::WM_LBUTTONDOWN, [IntPtr][WM]::MK_LBUTTON, $lp)
Start-Sleep -Milliseconds 120
$null = [WM]::SendMessage($hwnd, [WM]::WM_LBUTTONUP, [IntPtr]0, $lp)
Start-Sleep -Milliseconds 1000

$d = Get-Day '*Date = 2026/8/21*'
$sel = $d.Current.Name -match 'Date = 2026/8/21[^}]*IsSelected = True'
Write-Output ("SENDMSG_SINGLE_SELECTED=" + $sel)

# Double click via SendMessage (down, up, dblclk, up)
$null = [WM]::SendMessage($hwnd, [WM]::WM_LBUTTONDOWN, [IntPtr][WM]::MK_LBUTTON, $lp)
Start-Sleep -Milliseconds 60
$null = [WM]::SendMessage($hwnd, [WM]::WM_LBUTTONUP, [IntPtr]0, $lp)
Start-Sleep -Milliseconds 60
$null = [WM]::SendMessage($hwnd, 0x0203, [IntPtr][WM]::MK_LBUTTON, $lp)
Start-Sleep -Milliseconds 60
$null = [WM]::SendMessage($hwnd, [WM]::WM_LBUTTONUP, [IntPtr]0, $lp)
Start-Sleep -Milliseconds 2000

# check for quick add window
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
$qa = $null
foreach ($w in $wins) { if ($w.Current.Name -like '*' + [char]0x5FEB + '*' ) { $qa = $w } }
Write-Output ("QUICKADD=" + $(if ($qa) { "OPENED" } else { "NOT_OPENED" }))
foreach ($w in $wins) { Write-Output ("WIN: '" + $w.Current.Name + "'") }
