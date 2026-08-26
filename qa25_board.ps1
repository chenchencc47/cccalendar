param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class EW25 {
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
public class RM25 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class FG25 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
# 1) 双击 8/21 重新打开快速新增
$root = [System.Windows.Automation.AutomationElement]::RootElement
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class SW25 {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)))
foreach ($w in $wins) {
  if ($w.Current.Name -eq 'cccalendar') {
    $h = [IntPtr]([int]($w.Current.NativeWindowHandle))
    [void][SW25]::ShowWindow($h, 6)
  }
}
Start-Sleep -Milliseconds 600
$wb = $null
foreach ($w in $wins) { if ($w.Current.Name -eq '' -and $w.Current.BoundingRectangle.Width -gt 900) { $wb = $w } }
if ($null -eq $wb) { Write-Output "FAIL: workbench not found"; exit 1 }
$wbHwnd = [IntPtr]([int]($wb.Current.NativeWindowHandle))
[void][FG25]::SetForegroundWindow($wbHwnd)
Start-Sleep -Milliseconds 600
$days = $wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$d21 = $null
foreach ($d in $days) { if ($d.Current.Name -like '*Date = 2026/8/21*') { $d21 = $d } }
if ($null -eq $d21) { Write-Output "FAIL: Aug21 cell not found"; exit 1 }
$r = $d21.Current.BoundingRectangle
$cx = [int]($r.Left + $r.Width / 2)
$cy = [int]($r.Top + $r.Height / 2)
[RM25]::SetCursorPos($cx, $cy) | Out-Null
Start-Sleep -Milliseconds 300
[RM25]::mouse_event([RM25]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM25]::mouse_event([RM25]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 100
[RM25]::mouse_event([RM25]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM25]::mouse_event([RM25]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 2500

[EW25]::Run($PidOfApp, '快速新增')
if ([EW25]::Found -eq [IntPtr]::Zero) { Write-Output "FAIL: quick add did not open"; exit 1 }
$qaHwnd = [EW25]::Found
$qa = [System.Windows.Automation.AutomationElement]::FromHandle($qaHwnd)

# 2) 点标题栏激活窗口
[RM25]::SetCursorPos([int]($qa.Current.BoundingRectangle.Left + 200), [int]($qa.Current.BoundingRectangle.Top + 12)) | Out-Null
Start-Sleep -Milliseconds 300
[RM25]::mouse_event([RM25]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM25]::mouse_event([RM25]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 500

# 3) 找板坐标
$els = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$h08 = $null; $room1 = $null
foreach ($e in $els) {
  $n = $e.Current.Name
  if ($n -eq '08:00' -and $null -eq $h08) { $h08 = $e }
  if ($n -eq '项目组二楼会议室' -and $null -eq $room1) { $room1 = $e }
}
if ($null -eq $h08 -or $null -eq $room1) { Write-Output "FAIL: labels not found"; exit 1 }
$r08 = $h08.Current.BoundingRectangle
$rr1 = $room1.Current.BoundingRectangle
$bx = [int]($rr1.Left + $rr1.Width / 2)
$y0800 = [int]($r08.Top + 6)
$y0900 = [int]($r08.Top + 2*13 + 6)

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

# 4) 单击 8:00-8:30
[RM25]::SetCursorPos($bx, $y0800) | Out-Null
Start-Sleep -Milliseconds 300
[RM25]::mouse_event([RM25]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM25]::mouse_event([RM25]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 900
Write-Output ("AFTER_CLICK_0800_0830: " + (Get-FormValues))

# 5) 拖选 8:00 -> 9:30
[RM25]::SetCursorPos($bx, $y0800) | Out-Null
Start-Sleep -Milliseconds 300
[RM25]::mouse_event([RM25]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 150
for ($i = 1; $i -le 10; $i++) {
  $ny = [int]($y0800 + ($y0900 - $y0800) * $i / 10)
  [RM25]::SetCursorPos($bx, $ny) | Out-Null
  Start-Sleep -Milliseconds 40
}
Start-Sleep -Milliseconds 200
[RM25]::mouse_event([RM25]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 900
Write-Output ("AFTER_DRAG_0800_0930: " + (Get-FormValues))

# 6) 截图（只截看板区域）
$qr = $qa.Current.BoundingRectangle
$bmp = New-Object System.Drawing.Bitmap ([int]$qr.Width), ([int]$qr.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen([int]$qr.Left, [int]$qr.Top, 0, 0, [System.Drawing.Size]::new([int]$qr.Width, [int]$qr.Height))
$sp = "D:/myProgram/cccalendar/screenshots/qa25_board_after.png"
$bmp.Save($sp, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output ("SCT=" + $sp)
