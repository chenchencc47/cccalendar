param([int]$X, [int]$Y)
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class WinPt {
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT pt);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [StructLayout(LayoutKind.Sequential)]
  public struct POINT { public int X; public int Y; }
}
"@
$pt = New-Object WinPt+POINT
$pt.X = $X; $pt.Y = $Y
$h = [WinPt]::WindowFromPoint($pt)
$sb = New-Object System.Text.StringBuilder 256
[void][WinPt]::GetWindowText($h, $sb, 256)
$title = $sb.ToString()
$sb2 = New-Object System.Text.StringBuilder 256
[void][WinPt]::GetClassName($h, $sb2, 256)
$class = $sb2.ToString()
$pid2 = 0
[void][WinPt]::GetWindowThreadProcessId($h, [ref]$pid2)
$proc = Get-Process -Id $pid2 -ErrorAction SilentlyContinue
Write-Output ("HIT hwnd=$h class='$class' title='$title' pid=$pid2 proc=$($proc.ProcessName)")
# walk up parents
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class WinPt2 {
  [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
}
"@
$cur = $h
for ($i = 0; $i -lt 6; $i++) {
  $parent = [WinPt2]::GetAncestor($cur, 2)  # GA_ROOT
  if ($parent -eq [IntPtr]::Zero -or $parent -eq $cur) { break }
  $sb3 = New-Object System.Text.StringBuilder 256
  [void][WinPt2]::GetWindowText($parent, $sb3, 256)
  $pid3 = 0
  [void][WinPt2]::GetWindowThreadProcessId($parent, [ref]$pid3)
  $proc3 = Get-Process -Id $pid3 -ErrorAction SilentlyContinue
  Write-Output ("ROOT hwnd=$parent title='$($sb3.ToString())' pid=$pid3 proc=$($proc3.ProcessName)")
  $cur = $parent
}
