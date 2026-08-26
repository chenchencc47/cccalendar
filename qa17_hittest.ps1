param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class EW17 {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public static List<string> Infos = new List<string>();
  public static void Run(int targetPid) {
    EnumWindows((h, l) => {
      uint p = 0;
      GetWindowThreadProcessId(h, out p);
      if (p == (uint)targetPid && IsWindowVisible(h)) {
        var sb = new StringBuilder(256);
        GetWindowText(h, sb, 256);
        RECT r;
        GetWindowRect(h, out r);
        Infos.Add(h.ToInt64() + "|" + sb.ToString() + "|" + (r.R - r.L) + "x" + (r.B - r.T) + "@" + r.L + "," + r.T);
      }
      return true;
    }, IntPtr.Zero);
  }
}
public class HT17 {
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(int x, int y);
  [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
if (-not $PidOfApp) { Write-Output "FAIL: no pid"; exit 1 }
[EW17]::Run($PidOfApp)
foreach ($i in [EW17]::Infos) { Write-Output ("WIN " + $i) }

# 工作台 = 无标题且宽>900
$wbHwnd = [IntPtr]::Zero
foreach ($i in [EW17]::Infos) {
  $parts = $i -split '\|'
  if ($parts[1] -eq '' -and $parts[2] -match '^(\d+)x') {
    if ([int]$Matches[1] -gt 900) { $wbHwnd = [IntPtr]::new([int64]$parts[0]) }
  }
}
if ($wbHwnd -eq [IntPtr]::Zero) { Write-Output "FAIL: workbench not found"; exit 1 }
Write-Output ("WORKBENCH hwnd=" + $wbHwnd)
[void][HT17]::SetForegroundWindow($wbHwnd)
Start-Sleep -Milliseconds 800

# UIA 找 8/21 格子
$wb = [System.Windows.Automation.AutomationElement]::FromHandle($wbHwnd)
$days = $wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$d21 = $null
foreach ($d in $days) { if ($d.Current.Name -like '*Date = 2026/8/21*') { $d21 = $d } }
if ($null -eq $d21) { Write-Output "FAIL: Aug21 cell not found"; exit 1 }
$r = $d21.Current.BoundingRectangle
Write-Output ("AUG21 type=" + $d21.Current.ControlType.ProgrammaticName + " rect=" + [int]$r.Left + "," + [int]$r.Top + " " + [int]$r.Width + "x" + [int]$r.Height)
$cx = [int]($r.Left + $r.Width / 2)
$cy = [int]($r.Top + $r.Height / 2)

# Win32 命中测试
$h = [HT17]::WindowFromPoint($cx, $cy)
$p = 0
[void][HT17]::GetWindowThreadProcessId($h, [ref]$p)
$rootHwnd = [HT17]::GetAncestor($h, 2)
$pr = 0
[void][HT17]::GetWindowThreadProcessId($rootHwnd, [ref]$pr)
Write-Output ("WindowFromPoint(" + $cx + "," + $cy + ") hwnd=" + $h + " pid=" + $p + " | root=" + $rootHwnd + " pid=" + $pr + " | appPid=" + $PidOfApp)
$fg = [HT17]::GetForegroundWindow()
$pfg = 0
[void][HT17]::GetWindowThreadProcessId($fg, [ref]$pfg)
Write-Output ("Foreground hwnd=" + $fg + " pid=" + $pfg)
