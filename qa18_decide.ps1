param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class EW24 {
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
public class RM24 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class FG24 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
[EW24]::Run($PidOfApp, '快速新增')
if ([EW24]::Found -eq [IntPtr]::Zero) { Write-Output "FAIL: quick add not open"; exit 1 }
$qaHwnd = [EW24]::Found
[void][FG24]::SetForegroundWindow($qaHwnd)
Start-Sleep -Milliseconds 600
$qa = [System.Windows.Automation.AutomationElement]::FromHandle($qaHwnd)

# 1) UIA 编程式展开 开始时间
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '开始时间')
$combo = $qa.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
if ($null -eq $combo) { Write-Output "FAIL: combo not found"; exit 1 }
try {
  $ecp = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
  $ecp.Expand()
  Start-Sleep -Milliseconds 500
  $state = $ecp.Current.ExpandCollapseState
  Write-Output ("UIA_EXPAND_RESULT: " + $state)
  if ($state -eq 'Expanded') {
    $ecp.Collapse()
    Start-Sleep -Milliseconds 300
    Write-Output ("UIA_COLLAPSE_RESULT: " + $ecp.Current.ExpandCollapseState)
  }
} catch {
  Write-Output ("UIA_EXPAND_ERROR: " + $_.Exception.Message)
}

# 2) 鼠标点击 取消 按钮 (1358,696 72x32) 中心
Start-Sleep -Milliseconds 500
[RM24]::SetCursorPos(1394, 712) | Out-Null
Start-Sleep -Milliseconds 300
[RM24]::mouse_event([RM24]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM24]::mouse_event([RM24]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1500

# 3) 检查窗口是否还在
[EW24]::Found = [IntPtr]::Zero
[EW24]::Run($PidOfApp, '快速新增')
if ([EW24]::Found -eq [IntPtr]::Zero) {
  Write-Output "MOUSE_CLICK_CANCEL=PASS (dialog closed by mouse click)"
} else {
  Write-Output "MOUSE_CLICK_CANCEL=FAIL (dialog still open, mouse input dead)"
}
