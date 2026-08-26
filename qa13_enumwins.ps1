param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class EnumW {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rc);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [StructLayout(LayoutKind.Sequential)]
  public struct RECT { public int L, T, R, B; }
  public static List<string> Results = new List<string>();
  public static void Run(int targetPid) {
    EnumWindows((h, l) => {
      uint p = 0;
      GetWindowThreadProcessId(h, out p);
      if (p == (uint)targetPid) {
        var sb = new StringBuilder(256);
        GetWindowText(h, sb, 256);
        RECT rc;
        GetWindowRect(h, out rc);
        bool vis = IsWindowVisible(h);
        Results.Add("HWND=" + h + " vis=" + vis + " title='" + sb.ToString() + "' rect=" + rc.L + "," + rc.T + " " + (rc.R - rc.L) + "x" + (rc.B - rc.T));
      }
      return true;
    }, IntPtr.Zero);
  }
}
"@
[EnumW]::Run($PidOfApp)
foreach ($line in [EnumW]::Results) { Write-Output $line }
# 同时尝试 UIA 查找快速新增窗口
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
$qaUia = $null
foreach ($w in $wins) {
  Write-Output ("UIA-WINDOW '" + $w.Current.Name + "'")
  if ($w.Current.Name -eq '快速新增') { $qaUia = $w }
}
if ($null -ne $qaUia) { Write-Output "UIA_FOUND_QUICKADD=YES" } else { Write-Output "UIA_FOUND_QUICKADD=NO" }
