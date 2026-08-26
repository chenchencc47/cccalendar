param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RM11 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class Win11 {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
public class EnumW11 {
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
function Get-Wins([int]$procId) {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $procId)
  return $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
}
if (-not $PidOfApp) { Write-Output "FAIL: no pid"; exit 1 }

# 最小化主窗口，确保工作台可见
$wins = Get-Wins $PidOfApp
foreach ($w in $wins) {
  if ($w.Current.Name -eq 'cccalendar') {
    $hwnd = [IntPtr]([int]($w.Current.NativeWindowHandle))
    [void][Win11]::ShowWindow($hwnd, 6)
  }
}
Start-Sleep -Milliseconds 800

# 找工作台窗口（无标题、宽>900）并前置
$wb = $null
$wins = Get-Wins $PidOfApp
foreach ($w in $wins) { if ($w.Current.Name -eq '' -and $w.Current.BoundingRectangle.Width -gt 900) { $wb = $w } }
if ($null -eq $wb) { Write-Output "FAIL: workbench not found"; exit 1 }
$wbHwnd = [IntPtr]([int]($wb.Current.NativeWindowHandle))
[void][Win11]::SetForegroundWindow($wbHwnd)
Start-Sleep -Milliseconds 600

# 找 8/21（非当天）格子
$days = $wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$d21 = $null
foreach ($d in $days) { if ($d.Current.Name -like '*Date = 2026/8/21*') { $d21 = $d } }
if ($null -eq $d21) { Write-Output "FAIL: Aug21 cell not found"; exit 1 }
$r = $d21.Current.BoundingRectangle
$cx = [int]($r.Left + $r.Width / 2)
$cy = [int]($r.Top + $r.Height / 2)
Write-Output ("Aug21 cell center (" + $cx + "," + $cy + ")")

# 双击（点格子中心）
[RM11]::SetCursorPos($cx, $cy) | Out-Null
Start-Sleep -Milliseconds 300
[RM11]::mouse_event([RM11]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM11]::mouse_event([RM11]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 100
[RM11]::mouse_event([RM11]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM11]::mouse_event([RM11]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 2500

# 用 Win32 EnumWindows 定位快速新增窗口（UIA RootElement 枚举不到 WPF 弹窗）
[EnumW11]::Run($PidOfApp)
$qaHwnd = [IntPtr]::Zero
foreach ($t in [EnumW11]::Found) {
  if ($t.Item2 -eq '快速新增' -and $t.Item3) { $qaHwnd = $t.Item1 }
}
$ts = Get-Date -Format "HHmmss"
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$sp = "D:/myProgram/cccalendar/screenshots/qa13_dbclick_$ts.png"
$bmp.Save($sp, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output ("SCT=" + $sp)

if ($qaHwnd -eq [IntPtr]::Zero) { Write-Output "DBCLICK_NON_TODAY=FAIL (quick add window not open)"; exit 1 }
Write-Output "QUICKADD_WINDOW found via Win32 EnumWindows"

# 通过 FromHandle 挂接 UIA 验证日期绑定
$qa = [System.Windows.Automation.AutomationElement]::FromHandle($qaHwnd)
$els = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$dateOk = $false
foreach ($e in $els) {
  $n = $e.Current.Name
  if ($n -like '*2026*8*21*' -or $n -like '*8月21日*') {
    Write-Output ("DATE_ELEM '" + $n + "'")
    $dateOk = $true
  }
}
if ($dateOk) { Write-Output "DBCLICK_NON_TODAY=PASS (quick add opened for 2026-08-21)" } else {
  foreach ($e in $els) { $n = $e.Current.Name; if ($n) { Write-Output ("ELEM '" + $n + "'") } }
  Write-Output "DBCLICK_NON_TODAY=PARTIAL (window open but date not verified)"
}

# 关闭快速新增窗口（按 ESC）
[System.Windows.Forms.SendKeys]::SendWait("{ESC}")
Start-Sleep -Milliseconds 500
