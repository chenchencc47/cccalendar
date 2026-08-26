param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class EW19 {
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
public class RM19 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class FG19 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
[EW19]::Run($PidOfApp, '快速新增')
if ([EW19]::Found -eq [IntPtr]::Zero) { Write-Output "FAIL: quick add not open"; exit 1 }
$qaHwnd = [EW19]::Found
[void][FG19]::SetForegroundWindow($qaHwnd)
Start-Sleep -Milliseconds 600
$qa = [System.Windows.Automation.AutomationElement]::FromHandle($qaHwnd)
$els = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)

$h08 = $null; $room1 = $null; $room5 = $null
foreach ($e in $els) {
  $n = $e.Current.Name
  if ($n -eq '08:00' -and $null -eq $h08) { $h08 = $e }
  if ($n -eq '项目组二楼会议室' -and $null -eq $room1) { $room1 = $e }
  if ($n -eq '院士办会议室' -and $null -eq $room5) { $room5 = $e }
}
if ($null -eq $h08 -or $null -eq $room1) { Write-Output "FAIL: labels not found"; exit 1 }
$r08 = $h08.Current.BoundingRectangle
$rr1 = $room1.Current.BoundingRectangle
$rr5 = $room5.Current.BoundingRectangle
$bx = [int]($rr1.Left + $rr1.Width / 2)
$y0800 = [int]($r08.Top + 6)          # 8:00-8:30 格中心
$y0900 = [int]($r08.Top + 2*13 + 6)   # 9:00-9:30 格中心
Write-Output ("board room1 center=(" + $bx + ") 8:00cell y=" + $y0800 + " 9:00cell y=" + $y0900)
Write-Output ("院士办 visible: left=" + [int]$rr5.Left + " right=" + [int]($rr5.Left + $rr5.Width))

function Get-FormValues {
  $out = @()
  $all = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($e in $all) {
    $r = $e.Current.BoundingRectangle
    if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Edit' -and $r.Left -lt 780) {
      try {
        $vp = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        $out += ("edit@" + [int]$r.Top + "='" + $vp.Current.Value + "'")
      } catch {}
    }
    if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.ComboBox' -and $r.Left -lt 780 -and $r.Top -gt 300 -and $r.Top -lt 360) {
      $kids = $e.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
      $txt = ''
      foreach ($k in $kids) { if ($k.Current.Name) { $txt = $k.Current.Name } }
      $out += ("roomCombo='" + $txt + "'")
    }
  }
  return $out -join ' '
}

Write-Output ("BEFORE: " + (Get-FormValues))

# 1) 单击 8:00-8:30（项目组二楼会议室）
[RM19]::SetCursorPos($bx, $y0800) | Out-Null
Start-Sleep -Milliseconds 300
[RM19]::mouse_event([RM19]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM19]::mouse_event([RM19]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 900
Write-Output ("AFTER_CLICK_0800_0830: " + (Get-FormValues))

# 2) 拖选 8:00 -> 9:30（起 8:00 格，止 9:00 格，含端点 = 3 格）
[RM19]::SetCursorPos($bx, $y0800) | Out-Null
Start-Sleep -Milliseconds 300
[RM19]::mouse_event([RM19]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 150
for ($i = 1; $i -le 10; $i++) {
  $ny = [int]($y0800 + ($y0900 - $y0800) * $i / 10)
  [RM19]::SetCursorPos($bx, $ny) | Out-Null
  Start-Sleep -Milliseconds 40
}
Start-Sleep -Milliseconds 200
[RM19]::mouse_event([RM19]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 900
Write-Output ("AFTER_DRAG_0800_0930: " + (Get-FormValues))
