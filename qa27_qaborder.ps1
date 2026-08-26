param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class PX2 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class WinPX2 {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
public class EnumW27 {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rc);
  [StructLayout(LayoutKind.Sequential)]
  public struct RECT { public int L, T, R, B; }
  public static System.Collections.Generic.List<System.Tuple<IntPtr,string>> Found = new System.Collections.Generic.List<System.Tuple<IntPtr,string>>();
  public static void Run(int targetPid) {
    EnumWindows((h, l) => {
      uint p = 0;
      GetWindowThreadProcessId(h, out p);
      if (p == (uint)targetPid && IsWindowVisible(h)) {
        var sb = new System.Text.StringBuilder(256);
        GetWindowText(h, sb, 256);
        Found.Add(System.Tuple.Create(h, sb.ToString()));
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
$main = Get-MainWindow
if ($null -eq $main) { Write-Output "FAIL: main window not found"; exit 1 }
$mainHwnd = [IntPtr]([int]($main.Current.NativeWindowHandle))
[void][WinPX2]::ShowWindow($mainHwnd, 9)
[void][WinPX2]::SetForegroundWindow($mainHwnd)
Start-Sleep -Milliseconds 1000

$btn = $main.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "快速新增")))
if ($null -eq $btn) {
  $els = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($e in $els) { if ($e.Current.Name -eq '快速新增' -and $e.Current.ControlType.ProgrammaticName -eq 'ControlType.Button') { $btn = $e; break } }
}
if ($null -eq $btn) { Write-Output "FAIL: 快速新增 button not found"; exit 1 }
$br = $btn.Current.BoundingRectangle
[PX2]::SetCursorPos([int]($br.Left + $br.Width / 2), [int]($br.Top + $br.Height / 2)) | Out-Null
Start-Sleep -Milliseconds 400
[PX2]::mouse_event([PX2]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 80
[PX2]::mouse_event([PX2]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 3000

[EnumW27]::Run($PidOfApp)
$qaRect = $null
$qaHwnd = [IntPtr]::Zero
foreach ($t in [EnumW27]::Found) {
  if ($t.Item2 -eq '快速新增') {
    $qaHwnd = $t.Item1
    $r = New-Object 'System.Drawing.Rectangle'
    $rc = New-Object EnumW27+RECT
    [EnumW27]::GetWindowRect($qaHwnd, [ref]$rc) | Out-Null
    $qaRect = @{ L = $rc.L; T = $rc.T; W = $rc.R - $rc.L; H = $rc.B - $rc.T }
  }
}
if ($null -eq $qaRect) { Write-Output "FAIL: 快速新增 window not open"; exit 1 }
Write-Output ("QA rect=" + $qaRect.L + "," + $qaRect.T + " " + $qaRect.W + "x" + $qaRect.H)

$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$cx = [int]($qaRect.L + $qaRect.W / 2)
for ($dy = 0; $dy -le 8; $dy++) {
  $c = $bmp.GetPixel($cx, [int]$qaRect.T + $dy)
  Write-Output ("  pixel(x=" + $cx + ", y=" + ([int]$qaRect.T + $dy) + ") = R" + $c.R + " G" + $c.G + " B" + $c.B + " #" + $c.ToArgb().ToString("X8"))
}
$ts = Get-Date -Format "HHmmss"
$sp = "D:/myProgram/cccalendar/screenshots/qa27_qa_$ts.png"
$bmp.Save($sp, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output ("SCT=" + $sp)

# 关闭窗口
[System.Windows.Forms.SendKeys]::SendWait("{ESC}")
Start-Sleep -Milliseconds 400
