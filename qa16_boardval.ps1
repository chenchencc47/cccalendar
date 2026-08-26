param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class EW16 {
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
public class RM16 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class Win16 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
[EW16]::Run($PidOfApp, '快速新增')
if ([EW16]::Found -eq [IntPtr]::Zero) { Write-Output "FAIL: quick add not open"; exit 1 }
$qaHwnd = [EW16]::Found
[void][Win16]::SetForegroundWindow($qaHwnd)
Start-Sleep -Milliseconds 600
$qa = [System.Windows.Automation.AutomationElement]::FromHandle($qaHwnd)

function Get-FormValues {
  $els = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  $result = @()
  foreach ($e in $els) {
    if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.Edit') {
      $r = $e.Current.BoundingRectangle
      if ($r.Left -lt 780) {  # 左侧表单区域
        $val = ''
        try {
          $vp = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
          $val = $vp.Current.Value
        } catch { $val = '(no value pattern)' }
        $result += ("edit[" + [int]$r.Left + "," + [int]$r.Top + "]='" + $val + "'")
      }
    }
    if ($e.Current.Name -eq '会议室') {
      # 会议室组合框的选中值：找它下面的组合框文本
    }
  }
  # 会议室下拉显示值（ComboBox 选中项文本会出现在其子 Text）
  $comboVals = @()
  foreach ($e in $els) {
    $r = $e.Current.BoundingRectangle
    if ($e.Current.ControlType.ProgrammaticName -eq 'ControlType.ComboBox' -and $r.Left -lt 780) {
      $kids = $e.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
      $txt = ''
      foreach ($k in $kids) { if ($k.Current.Name) { $txt = $k.Current.Name } }
      $comboVals += ("combo[" + [int]$r.Left + "," + [int]$r.Top + "]='" + $txt + "'")
    }
  }
  return ($result -join ' ') + ' | ' + ($comboVals -join ' ')
}

Write-Output ("BEFORE: " + (Get-FormValues))

# ---- 单击 8:00-8:30 单元格（第一列 项目组二楼，y=128 即 8:00-8:30 中部）----
[RM16]::SetCursorPos(902, 128) | Out-Null
Start-Sleep -Milliseconds 300
[RM16]::mouse_event([RM16]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RM16]::mouse_event([RM16]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1000
Write-Output ("AFTER_CLICK_0800: " + (Get-FormValues))

# ---- 拖选 8:00 到 9:30（y 121 -> 199）----
[RM16]::SetCursorPos(902, 127) | Out-Null
Start-Sleep -Milliseconds 300
[RM16]::mouse_event([RM16]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 150
for ($i = 1; $i -le 8; $i++) {
  $ny = [int](127 + (205 - 127) * $i / 8)
  [RM16]::SetCursorPos(902, $ny) | Out-Null
  Start-Sleep -Milliseconds 50
}
Start-Sleep -Milliseconds 200
[RM16]::mouse_event([RM16]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1200
Write-Output ("AFTER_DRAG_0800_0930: " + (Get-FormValues))

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$ts = Get-Date -Format "HHmmss"
$sp = "D:/myProgram/cccalendar/screenshots/qa16_board_$ts.png"
$bmp.Save($sp, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output ("SCT=" + $sp)
