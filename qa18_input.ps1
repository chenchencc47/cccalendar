param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class EW22 {
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
public class RM22 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class FG22 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
[EW22]::Run($PidOfApp, '快速新增')
if ([EW22]::Found -eq [IntPtr]::Zero) { Write-Output "FAIL: quick add not open"; exit 1 }
$qaHwnd = [EW22]::Found
[void][FG22]::SetForegroundWindow($qaHwnd)
Start-Sleep -Milliseconds 600

# 点击 开始时间 下拉框 (402,271 176x36) 中心偏左
[RM22]::SetCursorPos(420, 289) | Out-Null
Start-Sleep -Milliseconds 300
[RM22]::mouse_event([RM22]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM22]::mouse_event([RM22]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 800

$qa = [System.Windows.Automation.AutomationElement]::FromHandle($qaHwnd)
$els = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
foreach ($e in $els) {
  if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.ComboBox' -and $e.Current.Name -eq '开始时间') {
    try {
      $ecp = $e.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
      Write-Output ("开始时间 combo expandState=" + $ecp.Current.ExpandCollapseState)
    } catch { Write-Output "no expand pattern" }
  }
}
