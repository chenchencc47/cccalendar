param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RM13 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class Win13 {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
public class EnumW13 {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder sb, int max);
  public static System.Collections.Generic.List<System.Tuple<IntPtr,string,bool>> Found = new System.Collections.Generic.List<System.Tuple<IntPtr,string,bool>>();
  public static void Run(int targetPid) {
    EnumWindows((h, l) => {
      uint p = 0;
      GetWindowThreadProcessId(h, out p);
      if (p == (uint)targetPid) {
        var sb = new System.Text.StringBuilder(256);
        GetWindowText(h, sb, 256);
        Found.Add(System.Tuple.Create(h, sb.ToString(), IsWindowVisible(h)));
      }
      return true;
    }, IntPtr.Zero);
  }
}
"@
function Get-MainWindow {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
  foreach ($w in $wins) { if ($w.Current.Name -eq 'cccalendar') { return $w } }
  return $null
}
function Click-At([int]$x, [int]$y) {
  [RM13]::SetCursorPos($x, $y) | Out-Null
  Start-Sleep -Milliseconds 300
  [RM13]::mouse_event([RM13]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 60
  [RM13]::mouse_event([RM13]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 600
}
if (-not $PidOfApp) { Write-Output "FAIL: no pid"; exit 1 }
$main = Get-MainWindow
if ($null -eq $main) { Write-Output "FAIL: main window not found"; exit 1 }
$mainHwnd = [IntPtr]([int]($main.Current.NativeWindowHandle))
[void][Win13]::ShowWindow($mainHwnd, 9)
[void][Win13]::SetForegroundWindow($mainHwnd)
Start-Sleep -Milliseconds 800

# ---- 点击快速新增按钮 ----
$btn = $main.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "快速新增")))
if ($null -eq $btn) {
  $els = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($e in $els) { if ($e.Current.Name -eq '快速新增' -and $e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button') { $btn = $e; break } }
}
if ($null -eq $btn) { Write-Output "FAIL: 快速新增 button not found"; exit 1 }
$br = $btn.Current.BoundingRectangle
Click-At ([int]($br.Left + $br.Width / 2)) ([int]($br.Top + $br.Height / 2))
Start-Sleep -Milliseconds 1500

# ---- Win32 定位快速新增窗口 ----
[EnumW13]::Run($PidOfApp)
$qaHwnd = [IntPtr]::Zero
foreach ($t in [EnumW13]::Found) {
  if ($t.Item2 -eq '快速新增' -and $t.Item3) { $qaHwnd = $t.Item1 }
}
if ($qaHwnd -eq [IntPtr]::Zero) { Write-Output "FAIL: quick add window not open"; exit 1 }
[void][Win13]::SetForegroundWindow($qaHwnd)
Start-Sleep -Milliseconds 600
$qa = [System.Windows.Automation.AutomationElement]::FromHandle($qaHwnd)

# ---- 定位会议室列头与 08:00 时刻度 ----
$els = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$roomHeader = $null
$label08 = $null
foreach ($e in $els) {
  $n = $e.Current.Name
  if ($n -eq '项目组二楼会议室' -and $null -eq $roomHeader) { $roomHeader = $e }
  if ($n -eq '08:00' -and $null -eq $label08) { $label08 = $e }
}
if ($null -eq $roomHeader -or $null -eq $label08) { Write-Output "FAIL: room header or 08:00 label not found"; exit 1 }
$hr = $roomHeader.Current.BoundingRectangle
$lr = $label08.Current.BoundingRectangle
Write-Output ("ROOM_HEADER rect=" + [int]$hr.Left + "," + [int]$hr.Top + " " + [int]$hr.Width + "x" + [int]$hr.Height)
Write-Output ("LABEL_08 rect=" + [int]$lr.Left + "," + [int]$lr.Top + " " + [int]$lr.Width + "x" + [int]$lr.Height)

# ---- 读取点击前开始/结束时间 ----
function Get-TimeComboValue($qaWin, [string]$name) {
  $el = $qaWin.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)))
  if ($null -eq $el) { return "(not found)" }
  try {
    $vp = $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    return $vp.Current.Value
  } catch {
    try {
      $sp = $el.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern)
      $sel = $sp.Current.GetSelection()
      if ($sel.Count -gt 0) { return $sel[0].Current.Name }
    } catch {}
    return "(no value)"
  }
}
$beforeStart = Get-TimeComboValue $qa "开始时间"
$beforeEnd = Get-TimeComboValue $qa "结束时间"
Write-Output ("BEFORE start=" + $beforeStart + " end=" + $beforeEnd)

# ---- 点击 8:00-8:30 单元格（第一列、08:00 刻度顶部附近）----
$cx = [int]($hr.Left + $hr.Width / 2)
$cy = [int]($lr.Top + 2)
Write-Output ("CLICK 8:00 cell at (" + $cx + "," + $cy + ")")
Click-At $cx $cy
Start-Sleep -Milliseconds 800

# ---- 读取点击后开始/结束时间并验证 ----
$afterStart = Get-TimeComboValue $qa "开始时间"
$afterEnd = Get-TimeComboValue $qa "结束时间"
Write-Output ("AFTER start=" + $afterStart + " end=" + $afterEnd)

# 单击切换 8:00-8:30 → 开始 08:00，结束 08:30
$okStart = ($afterStart -like '*08:00*')
$okEnd = ($afterEnd -like '*08:30*')
if ($okStart -and $okEnd) {
  Write-Output "BOARD_8AM_CLICK=PASS (8:00-10:00 区域可选中)"
} else {
  Write-Output "BOARD_8AM_CLICK=FAIL"
  exit 1
}

# ---- 关闭快速新增窗口 ----
$cancel = $qa.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "取消")))
if ($null -ne $cancel) {
  try {
    $ip = $cancel.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $ip.Invoke()
  } catch {}
}
Start-Sleep -Milliseconds 500
