Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName Microsoft.VisualBasic
function Take-Screenshot { param([string]$Path) $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height; $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size); $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose() }
$PidOfApp = 31516
# Force cccalendar window to foreground and maximize
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32 {
  [DllImport("user32.dll")] public static extern IntPtr SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
  public const int SW_RESTORE=9, SW_MAXIMIZE=3;
}
"@
$p = [System.Diagnostics.Process]::GetProcessById($PidOfApp)
$hwnd = $p.MainWindowHandle
$fg = [Win32]::GetForegroundWindow()
$fgPid = 0
[void][Win32]::GetWindowThreadProcessId($fg, [ref]$fgPid)
Write-Output (("Current FG pid=" + $fgPid + " target pid=" + $PidOfApp + " HWND=" + $hwnd + " FG=" + $fg))
$tTarget = [Win32]::GetWindowThreadProcessId($hwnd, [ref]$null)
$tCurrent = [Win32]::GetWindowThreadProcessId($fg, [ref]$null)
if ($tCurrent -ne $tTarget) {
  [void][Win32]::AttachThreadInput($tCurrent, $tTarget, $true)
}
[void][Win32]::ShowWindow($hwnd, [Win32]::SW_RESTORE)
[void][Win32]::SetForegroundWindow($hwnd)
[void][Win32]::ShowWindow($hwnd, [Win32]::SW_MAXIMIZE)
if ($tCurrent -ne $tTarget) { [void][Win32]::AttachThreadInput($tCurrent, $tTarget, $false) }
Start-Sleep -Milliseconds 800
$fg2 = [Win32]::GetForegroundWindow()
$fg2Pid = 0
[void][Win32]::GetWindowThreadProcessId($fg2, [ref]$fg2Pid)
Write-Output (("After force: FG pid=" + $fg2Pid))

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("using System;")
[void]$sb.AppendLine("using System.Runtime.InteropServices;")
[void]$sb.AppendLine("public class MouseSimF {")
$q = [char]34
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern bool SetCursorPos(int x, int y);")
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);")
[void]$sb.AppendLine("  public const uint LEFTDOWN = 0x02, LEFTUP = 0x04;")
[void]$sb.AppendLine("  public static void ClickAt(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(200); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(200); }")
[void]$sb.AppendLine("  public static void DoubleClickAt(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(200); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(200); }")
[void]$sb.AppendLine("}")
Add-Type -TypeDefinition $sb.ToString() -ErrorAction SilentlyContinue

$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$wr = $root.Current.BoundingRectangle
Write-Output ("Maximized Window rect=" + $wr.ToString())
$ts0 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp0 = "D:/myProgram/cccalendar/screenshots/07_qa_maximized_state_" + $ts0 + ".png"
Take-Screenshot -Path $sp0
Write-Output (("SCT_MAX=" + $sp0))

# Re-find CalendarDay 2026/8/25 (future) DataItem
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$target=$null
foreach ($e in $all) {
  $n=$e.Current.Name
  if ($n -like "*CalendarDayViewModel*" -and $n -like "*Date = 2026/8/25*") { $target=$e; break }
}
if ($null -eq $target) { Write-Output "FAIL: Aug25 not found in tree"; exit 1 }
$r = $target.Current.BoundingRectangle
Write-Output (("Aug25 RECT=" + $r.ToString()))
$tx = [int]($r.Left + $r.Width/2)
$ty = [int]($r.Top + $r.Height/2)
Write-Output (("Aug25 UIA center=" + $tx + "," + $ty))
# click
[MouseSimF]::ClickAt($tx, $ty)
Start-Sleep -Milliseconds 700
$n2 = $target.Current.Name
$sel = if ($n2 -like "*IsSelected = True*") { $true } else { $false }
Write-Output (("After click IsSelected=" + $sel + " snippet=" + $n2.Substring(0, [Math]::Min(200, $n2.Length))))
$ts1 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp1 = "D:/myProgram/cccalendar/screenshots/07_qa_click_aug25_max_" + $ts1 + ".png"
Take-Screenshot -Path $sp1
Write-Output (("SCT_CLICK_MAX=" + $sp1))
# Double click
[MouseSimF]::DoubleClickAt($tx, $ty)
Start-Sleep -Milliseconds 1200
$ts2 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp2 = "D:/myProgram/cccalendar/screenshots/08_qa_dbclick_aug25_max_" + $ts2 + ".png"
Take-Screenshot -Path $sp2
Write-Output (("SCT_DBCLICK_MAX=" + $sp2))

# Check combo quick window
$cCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
$Combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $cCond)
if ($null -eq $Combo) {
  $pc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $and = New-Object System.Windows.Automation.AndCondition($pc, $cCond)
  $Combo = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $and)
}
if ($null -eq $Combo) {
  Write-Output "NO_QUICK_WINDOW: maximized + dbclick no combo"
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
    function DumpF { param($e,$d=0,$md=7) if ($null -eq $e) {return} $pad=""; for($i=0;$i -lt $d;$i++){$pad+="  "} $n=$e.Current.Name; if([string]::IsNullOrEmpty($n)){$n="(empty)"} $t=$e.Current.ControlType.ProgrammaticName -replace "^ControlType..",""; Write-Output ($pad + $t + " " + [char]34 + $n + [char]34); if ($d -ge $md){return} $ch = $e.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition); foreach($c in $ch){ DumpF $c ($d+1) $md } }
    DumpF $anc 0 7
    Write-Output "=== DUMP END ==="
  }
  $cancelCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "取消")
  $CancelBtn = if($null -ne $anc) { $anc.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cancelCond) } else { $null }
  if ($null -ne $CancelBtn) {
    $cbR = $CancelBtn.Current.BoundingRectangle
    [MouseSimF]::ClickAt([int]($cbR.Left + $cbR.Width/2), [int]($cbR.Top + $cbR.Height/2))
    Write-Output "Closed via Cancel"
  } else {
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    Write-Output "Closed via Esc"
  }
  Start-Sleep -Milliseconds 400
  $ts3 = Get-Date -Format "yyyyMMdd_HHmmss"
  $sp3 = "D:/myProgram/cccalendar/screenshots/09_qa_cancel_max_" + $ts3 + ".png"
  Take-Screenshot -Path $sp3
  Write-Output (("SCT_CANCEL_MAX=" + $sp3))
}