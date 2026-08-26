Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
function Take-Screenshot { param([string]$Path) $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height; $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size); $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose() }

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RealMouse {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
  public const uint MOUSEEVENTF_LEFTUP = 0x0004;
}
"@

$PidOfApp = 28960

function Find-QuickAddWindow {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
  foreach ($w in $wins) {
    if ($w.Current.Name -like '*快速新增*') { return $w }
  }
  return $null
}

# --- Test A: double-click non-today date (2026/8/21 Friday) in desktop workbench ---
# Cell center from UIA: Rect=1029,292,139,71 -> center (1098, 327)
$x = 1098; $y = 327
[RealMouse]::SetCursorPos($x, $y) | Out-Null
Start-Sleep -Milliseconds 250
[RealMouse]::mouse_event([RealMouse]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RealMouse]::mouse_event([RealMouse]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 90
[RealMouse]::mouse_event([RealMouse]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[RealMouse]::mouse_event([RealMouse]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1500

$ts = Get-Date -Format "HHmmss"
$sp = "D:/myProgram/cccalendar/screenshots/qa10_dbclick_0821_$ts.png"
Take-Screenshot -Path $sp
Write-Output ("SCT_DBCLICK=" + $sp)

$qa = Find-QuickAddWindow
if ($null -eq $qa) {
  Write-Output "TEST_A_FAIL: QuickAddWindow did NOT open after double-clicking 2026/8/21"
} else {
  Write-Output "TEST_A_PASS: QuickAddWindow OPENED after double-clicking non-today date"
  Write-Output ("QuickAdd Rect=" + $qa.Current.BoundingRectangle.ToString())
}
