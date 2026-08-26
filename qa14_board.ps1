param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class EW14 {
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
public class RM14 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class Win14 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
if (-not $PidOfApp) { Write-Output "FAIL: no pid"; exit 1 }

# ---- find quick add window by Win32 ----
[EW14]::Run($PidOfApp, '快速新增')
if ([EW14]::Found -eq [IntPtr]::Zero) { Write-Output "BOARD_TEST=FAIL (quick add window not open)"; exit 1 }
$qaHwnd = [EW14]::Found
[void][Win14]::SetForegroundWindow($qaHwnd)
Start-Sleep -Milliseconds 600
$qa = [System.Windows.Automation.AutomationElement]::FromHandle($qaHwnd)

# ---- locate 08:00 hour label ----
$els = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$label08 = $null
foreach ($e in $els) { if ($e.Current.Name -eq '08:00') { $label08 = $e; break } }
if ($null -eq $label08) { Write-Output "BOARD_TEST=FAIL (08:00 label not found)"; exit 1 }
$lr = $label08.Current.BoundingRectangle
$boardX = [int]($lr.Right) + 62   # 第一列会议室中心（列宽125）
Write-Output ("LABEL08 rect=" + [int]$lr.Left + "," + [int]$lr.Top + " -> board col1 center x=" + $boardX)

function Get-TimeTexts {
  $els2 = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  $times = @()
  foreach ($e in $els2) {
    $n = $e.Current.Name
    if ($n -match '^\d{2}:\d{2}$') { $times += $n }
  }
  return ($times -join ',')
}

# ---- click 8:00-8:30 cell in room column 1 ----
$cy = [int]($lr.Top + $lr.Height / 2)
[RM14]::SetCursorPos($boardX, $cy) | Out-Null
Start-Sleep -Milliseconds 300
[RM14]::mouse_event([RM14]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM14]::mouse_event([RM14]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1200
Write-Output ("CLICK_0800 times='" + (Get-TimeTexts) + "'")

# ---- drag from 8:00 to 9:30 (select 8:00-10:00) ----
$y930 = [int]($lr.Top + 3 * $lr.Height + $lr.Height / 2)  # 08:00 往下 3 个小时刻度
[RM14]::SetCursorPos($boardX, $cy) | Out-Null
Start-Sleep -Milliseconds 300
[RM14]::mouse_event([RM14]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 150
$steps = 6
for ($i = 1; $i -le $steps; $i++) {
  $ny = [int]($cy + ($y930 - $cy) * $i / $steps)
  [RM14]::SetCursorPos($boardX, $ny) | Out-Null
  Start-Sleep -Milliseconds 60
}
Start-Sleep -Milliseconds 200
[RM14]::mouse_event([RM14]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1500
Write-Output ("DRAG_0800_0930 times='" + (Get-TimeTexts) + "'")

# ---- screenshot for visual proof ----
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$ts = Get-Date -Format "HHmmss"
$sp = "D:/myProgram/cccalendar/screenshots/qa14_board_$ts.png"
$bmp.Save($sp, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output ("SCT=" + $sp)
