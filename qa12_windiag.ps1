param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Diag2 {
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
Write-Output "=== ALL TOP-LEVEL UIA WINDOWS ==="
foreach ($w in $wins) {
  $r = $w.Current.BoundingRectangle
  $h = [IntPtr]([int]($w.Current.NativeWindowHandle))
  Write-Output ("WIN name='" + $w.Current.Name + "' type=" + $w.Current.ControlType.ProgrammaticName + " hwnd=" + $h + " rect=" + [int]$r.Left + "," + [int]$r.Top + " - " + [int]$r.Right + "," + [int]$r.Bottom + " visible=" + [Diag2]::IsWindowVisible($h))
}

# probe points across the main window area
Write-Output "=== WindowFromPoint PROBE ==="
$points = @(
  @(1300, 682),
  @(1300, 200),
  @(1300, 400),
  @(826, 448),
  @(700, 600),
  @(500, 700)
)
foreach ($p in $points) {
  $h = [Diag2]::WindowFromPoint($p[0], $p[1])
  $pid2 = 0
  [void][Diag2]::GetWindowThreadProcessId($h, [ref]$pid2)
  $sb = New-Object System.Text.StringBuilder 256
  [void][Diag2]::GetWindowText($h, $sb, 256)
  $rect = New-Object Diag2+RECT
  $ok = [Diag2]::GetWindowRect($h, [ref]$rect)
  Write-Output ("POINT(" + $p[0] + "," + $p[1] + ") -> hwnd=" + $h + " pid=" + $pid2 + " title='" + $sb.ToString() + "' rect=" + $rect.Left + "," + $rect.Top + " - " + $rect.Right + "," + $rect.Bottom + " getRectOk=" + $ok)
}
