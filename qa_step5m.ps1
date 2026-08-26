Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName Microsoft.VisualBasic
function Take-Screenshot { param([string]$Path) $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height; $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size); $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose() }

$PidOfApp = 31516
$p = [System.Diagnostics.Process]::GetProcessById($PidOfApp)
$hwnd = $p.MainWindowHandle

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinMsg {
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(int x, int y);
  public const uint WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP=0x0202, WM_LBUTTONDBLCLK=0x0203;
  public const int MK_LBUTTON = 0x0001;
  public static IntPtr MakeLParam(int low, int high) { return (IntPtr)((high << 16) | (low & 0xffff)); }
  public static int ToLParam(int x, int y) { return ((y & 0xffff) << 16) | (x & 0xffff); }
}
"@
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)

# Target: Aug 26 DateText = "26" (future, Wed; in maximized layout it is at center of column 4 row 5, clearly in window). Find its exact UIA rect.
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$target=$null; $dateText=$null
foreach ($e in $all) {
  $n=$e.Current.Name
  if ($n -like "*CalendarDayViewModel*" -and $n -like "*Date = 2026/8/26*") { $target=$e; }
  if ($e.Current.ControlType -eq [System.Windows.Automation.ControlType]::Text -and $e.Current.Name -eq "26") {
    # parent check? not perfect but ok
    $r = $e.Current.BoundingRectangle
    if (-not $r.IsEmpty) { $dateText = $e }
  }
}
if ($null -eq $target) { Write-Output "FAIL Aug26 DataItem"; exit 1 }
# if dateText not found via name, search under target
if ($null -eq $dateText) {
  $tCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
  $subs = $target.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tCond)
  foreach ($s in $subs) { if ($s.Current.Name -eq "26") { $dateText = $s; break } }
}
if ($null -eq $dateText) { Write-Output "FAIL Aug26 dateText"; exit 1 }
$dr = $dateText.Current.BoundingRectangle
Write-Output (("Aug26 DateText screen RECT=" + $dr.ToString()))
$sx = [int]($dr.Left + $dr.Width/2)
$sy = [int]($dr.Top + $dr.Height/2)
Write-Output (("Screen point=" + $sx + "," + $sy))
$null = [WinMsg]::SetCursorPos($sx, $sy)
Start-Sleep -Milliseconds 200
# Find actual HWND at this point
$cellHwnd = [WinMsg]::WindowFromPoint($sx, $sy)
Write-Output (("WindowFromPoint HWND=" + $cellHwnd + " main=" + $hwnd))
# Convert screen to client for that hwnd
$pt = New-Object WinMsg+POINT
$pt.X = $sx; $pt.Y = $sy
[void][WinMsg]::ScreenToClient($cellHwnd, [ref]$pt)
Write-Output (("Client coords=" + $pt.X + "," + $pt.Y))
$lp = [WinMsg]::ToLParam($pt.X, $pt.Y)
# single click
$null = [WinMsg]::SendMessage($cellHwnd, [WinMsg]::WM_LBUTTONDOWN, [IntPtr][WinMsg]::MK_LBUTTON, [IntPtr]$lp)
Start-Sleep -Milliseconds 100
$null = [WinMsg]::SendMessage($cellHwnd, [WinMsg]::WM_LBUTTONUP, [IntPtr]0, [IntPtr]$lp)
Start-Sleep -Milliseconds 800
$n2 = $target.Current.Name
$sel = if ($n2 -like "*IsSelected = True*") { $true } else { $false }
Write-Output (("After SendMessage click Aug26: IsSelected=" + $sel))
Write-Output (("snippet=" + $n2.Substring(0, [Math]::Min(200, $n2.Length))))
$ts1 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp1 = "D:/myProgram/cccalendar/screenshots/07_qa_smsg_aug26_click_" + $ts1 + ".png"
Take-Screenshot -Path $sp1
Write-Output (("SCT_SMSG_CLICK=" + $sp1))

# Double click via messages
$null = [WinMsg]::SendMessage($cellHwnd, [WinMsg]::WM_LBUTTONDBLCLK, [IntPtr][WinMsg]::MK_LBUTTON, [IntPtr]$lp)
Start-Sleep -Milliseconds 100
$null = [WinMsg]::SendMessage($cellHwnd, [WinMsg]::WM_LBUTTONUP, [IntPtr]0, [IntPtr]$lp)
Start-Sleep -Milliseconds 1200
$ts2 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp2 = "D:/myProgram/cccalendar/screenshots/08_qa_smsg_aug26_dbclick_" + $ts2 + ".png"
Take-Screenshot -Path $sp2
Write-Output (("SCT_SMSG_DBCLICK=" + $sp2))

# Check quick-add window
$cCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
$Combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $cCond)
if ($null -eq $Combo) {
  $pc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $and = New-Object System.Windows.Automation.AndCondition($pc, $cCond)
  $Combo = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $and)
}
if ($null -eq $Combo) {
  Write-Output "NO_QUICK_WINDOW: SendMessage dbclick no combo"
} else {
  Write-Output "QUICK_WINDOW_OPENED: yes"
  $txtCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
  $allTxt = $Combo.FindAll([System.Windows.Automation.TreeScope]::Descendants, $txtCond)
  foreach ($t in $allTxt) { Write-Output (("  Combo text=" + $t.Current.Name)) }
  $anc = $Combo
  while ($null -ne $anc -and $anc.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window) { try { $anc = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($anc) } catch { $anc = $null } }
  if ($null -eq $anc) {
    $anc = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "快速新增")))
    if ($null -ne $anc -and $anc.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window) { $anc = $null }
  }
  if ($null -ne $anc) {
    Write-Output "=== DUMP START ==="
    function Dmp { param($e,$d=0,$md=7) if ($null -eq $e) {return} $pad=""; for($i=0;$i -lt $d;$i++){$pad+="  "} $n=$e.Current.Name; if([string]::IsNullOrEmpty($n)){$n="(empty)"} $t=$e.Current.ControlType.ProgrammaticName -replace "^ControlType..",""; Write-Output ($pad + $t + " " + [char]34 + $n + [char]34); if ($d -ge $md){return} $ch = $e.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition); foreach($c in $ch){ Dmp $c ($d+1) $md } }
    Dmp $anc 0 7
    Write-Output "=== DUMP END ==="
    # Close via Cancel or Esc
    $cancelCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "取消")
    $CancelBtn = $anc.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cancelCond)
    if ($null -ne $CancelBtn) {
      $cbR = $CancelBtn.Current.BoundingRectangle
      $cx = [int]($cbR.Left + $cbR.Width/2)
      $cy = [int]($cbR.Top + $cbR.Height/2)
      $null = [WinMsg]::SetCursorPos($cx,$cy)
      Start-Sleep -Milliseconds 100
      $cHwnd = [WinMsg]::WindowFromPoint($cx,$cy)
      $cpt = New-Object WinMsg+POINT
      $cpt.X=$cx; $cpt.Y=$cy
      [void][WinMsg]::ScreenToClient($cHwnd, [ref]$cpt)
      $clp = [WinMsg]::ToLParam($cpt.X, $cpt.Y)
      $null = [WinMsg]::SendMessage($cHwnd, [WinMsg]::WM_LBUTTONDOWN, [IntPtr][WinMsg]::MK_LBUTTON, [IntPtr]$clp)
      Start-Sleep -Milliseconds 50
      $null = [WinMsg]::SendMessage($cHwnd, [WinMsg]::WM_LBUTTONUP, [IntPtr]0, [IntPtr]$clp)
      Write-Output "Closed via Cancel"
    } else {
      [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
      Write-Output "Closed via Esc"
    }
    Start-Sleep -Milliseconds 400
    $ts3 = Get-Date -Format "yyyyMMdd_HHmmss"
    $sp3 = "D:/myProgram/cccalendar/screenshots/09_qa_cancel_smsg_" + $ts3 + ".png"
    Take-Screenshot -Path $sp3
    Write-Output (("SCT_CANCEL_SMSG=" + $sp3))
  }
}