param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class EW20 {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  public static IntPtr Found = IntPtr.Zero;
  public static void Run(int targetPid, string wantedTitle) {
    EnumWindows((h, l) => {
      uint p = 0;
      GetWindowThreadProcessId(h, out p);
      if (p == (uint)targetPid && IsWindowVisible(h)) {
        var sb = new StringBuilder(256);
        GetWindowText(h, sb, 256);
        if (sb.ToString() == wantedTitle) { Found = h; }
      }
      return true;
    }, IntPtr.Zero);
  }
}
"@
[EW20]::Run($PidOfApp, '快速新增')
if ([EW20]::Found -eq [IntPtr]::Zero) { Write-Output "FAIL: quick add not open"; exit 1 }
$qa = [System.Windows.Automation.AutomationElement]::FromHandle([EW20]::Found)
$els = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
foreach ($e in $els) {
  $n = $e.Current.Name
  $r = $e.Current.BoundingRectangle
  $ct = $e.Current.ControlType.ProgrammaticName
  if ($n -or $r.Width -gt 0) {
    Write-Output ($ct + " '" + $n + "' @" + [int]$r.Left + "," + [int]$r.Top + " " + [int]$r.Width + "x" + [int]$r.Height)
  }
}
