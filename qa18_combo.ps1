param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class EW21 {
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
[EW21]::Run($PidOfApp, '快速新增')
if ([EW21]::Found -eq [IntPtr]::Zero) { Write-Output "FAIL: quick add not open"; exit 1 }
$qa = [System.Windows.Automation.AutomationElement]::FromHandle([EW21]::Found)
$els = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
foreach ($e in $els) {
  if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.ComboBox') {
    $name = $e.Current.Name
    $r = $e.Current.BoundingRectangle
    $expanded = '?'
    try {
      $ecp = $e.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
      $expanded = $ecp.Current.ExpandCollapseState
    } catch { $expanded = 'no-pattern' }
    Write-Output ("COMBO '" + $name + "' @" + [int]$r.Left + "," + [int]$r.Top + " expandState=" + $expanded)
  }
}
# 检查是否有 Popup 下拉列表元素（会议室列表）
foreach ($e in $els) {
  if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.List' -and $e.Current.Name -like '*会议室*') {
    Write-Output ("DROPDOWN LIST visible: '" + $e.Current.Name + "'")
  }
}
