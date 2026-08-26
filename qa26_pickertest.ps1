param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class EW26 {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  public static IntPtr Found = IntPtr.Zero;
  public static void Run(int targetPid, string wantedTitle) {
    Found = IntPtr.Zero;
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
public class RM26 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class FG26 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
[EW26]::Run($PidOfApp, '快速新增')
if ([EW26]::Found -eq [IntPtr]::Zero) { Write-Output "FAIL: quick add not open"; exit 1 }
$qaHwnd = [EW26]::Found
[void][FG26]::SetForegroundWindow($qaHwnd)
Start-Sleep -Milliseconds 600
$qa = [System.Windows.Automation.AutomationElement]::FromHandle($qaHwnd)

# 先点标题栏激活
$qr = $qa.Current.BoundingRectangle
[RM26]::SetCursorPos([int]$qr.Left + 200, [int]$qr.Top + 12) | Out-Null
Start-Sleep -Milliseconds 250
[RM26]::mouse_event([RM26]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM26]::mouse_event([RM26]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 500

$els = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$today = $null; $dateText = $null; $prev = $null
foreach ($e in $els) {
  $n = $e.Current.Name
  if ($n -eq '返回今天' -and $null -eq $today) { $today = $e }
  if ($n -like '8月*日 *' -and $null -eq $dateText) { $dateText = $e }
  if ($n -eq '前一天' -and $null -eq $prev) { $prev = $e }
}
if ($null -eq $today -or $null -eq $dateText) { Write-Output "FAIL: picker buttons not found"; exit 1 }
Write-Output ("DATE_BEFORE: '" + $dateText.Current.Name + "'")

# 1) UIA Invoke 点击 返回今天
try {
  $ip = $today.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
  $ip.Invoke()
  Start-Sleep -Milliseconds 800
  Write-Output ("UIA_INVOKE_TODAY: date now '" + $dateText.Current.Name + "'")
} catch {
  Write-Output ("UIA_INVOKE_ERROR: " + $_.Exception.Message)
}

# 2) 鼠标点击 前一天 (840,55 区域附近: prev @902,55 32x32 中心 918,71)
$rp = $prev.Current.BoundingRectangle
$px = [int]($rp.Left + $rp.Width / 2)
$py = [int]($rp.Top + $rp.Height / 2)
[RM26]::SetCursorPos($px, $py) | Out-Null
Start-Sleep -Milliseconds 300
[RM26]::mouse_event([RM26]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM26]::mouse_event([RM26]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 800
Write-Output ("MOUSE_CLICK_PREV: date now '" + $dateText.Current.Name + "'")
