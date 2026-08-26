param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class EW18 {
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
[EW18]::Run($PidOfApp, '快速新增')
if ([EW18]::Found -eq [IntPtr]::Zero) { Write-Output "FAIL: quick add not open"; exit 1 }
$qa = [System.Windows.Automation.AutomationElement]::FromHandle([EW18]::Found)
$els = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$dateFound = $false
foreach ($e in $els) {
  $n = $e.Current.Name
  if ($n -like '*2026*8*21*') { Write-Output ("DATE_ELEM: " + $n); $dateFound = $true }
  if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Edit') {
    try {
      $vp = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
      $r = $e.Current.BoundingRectangle
      if ($r.Left -lt 780) { Write-Output ("EDIT[" + [int]$r.Left + "," + [int]$r.Top + "]='" + $vp.Current.Value + "'") }
    } catch {}
  }
}
if ($dateFound) { Write-Output "RESULT=PASS date is 2026-08-21" } else { Write-Output "RESULT=FAIL date not found" }
