Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName Microsoft.VisualBasic
function Take-Screenshot { param([string]$Path) $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height; $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size); $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose() }
$PidOfApp = 31516
$null = [Microsoft.VisualBasic.Interaction]::AppActivate($PidOfApp)
Start-Sleep -Milliseconds 300
$p = [System.Diagnostics.Process]::GetProcessById($PidOfApp)
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("using System;")
[void]$sb.AppendLine("using System.Runtime.InteropServices;")
[void]$sb.AppendLine("public class MouseSimA {")
$q = [char]34
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern bool SetCursorPos(int x, int y);")
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);")
[void]$sb.AppendLine("  public const uint LEFTDOWN = 0x02, LEFTUP = 0x04;")
[void]$sb.AppendLine("  public static void Click(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(50); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(30); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(50); }")
[void]$sb.AppendLine("  public static void DoubleClick(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(50); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(50); }")
[void]$sb.AppendLine("}")
Add-Type -TypeDefinition $sb.ToString() -ErrorAction SilentlyContinue

# Target 2026/8/30 (Sunday) - row 5, well above weather panel overlap. Rect: 1133,685,89,90 -> center 1177, 730
$tx = 1133 + [int](89/2)
$ty = 685 + [int](90/2)
Write-Output (("Target Aug30 center=" + $tx + "," + $ty))
[MouseSimA]::Click($tx, $ty)
Start-Sleep -Milliseconds 600
# Verify selected via IsSelected check
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$target = $null; $sel = $false
foreach ($e in $all) {
  $n=$e.Current.Name
  if ($n -like "*CalendarDayViewModel*" -and $n -like "*Date = 2026/8/30*") {
    $target = $e
    if ($n -like "*IsSelected = True*") { $sel = $true }
    break
  }
}
Write-Output (("Aug30 selected=" + $sel))
if ($null -ne $target) { Write-Output (("Aug30 snippet=" + $target.Current.Name.Substring(0, [Math]::Min(180, $target.Current.Name.Length)))) }
$ts1 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp1 = "D:/myProgram/cccalendar/screenshots/07_qa_click_aug30_" + $ts1 + ".png"
Take-Screenshot -Path $sp1
Write-Output (("SCREENSHOT_CLICK_AUG30=" + $sp1))

# Double-click same center
[MouseSimA]::DoubleClick($tx, $ty)
Start-Sleep -Milliseconds 1000
$ts2 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp2 = "D:/myProgram/cccalendar/screenshots/08_qa_doubleclick_aug30_" + $ts2 + ".png"
Take-Screenshot -Path $sp2
Write-Output (("SCREENSHOT_DBCLICK_AUG30=" + $sp2))

# Check quick window
$cCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
$Combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $cCond)
if ($null -eq $Combo) {
  $pc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $and = New-Object System.Windows.Automation.AndCondition($pc, $cCond)
  $Combo = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $and)
}
if ($null -eq $Combo) {
  Write-Output "NO_QUICK_WINDOW: double click aug30 did NOT open quick-add"
} else {
  Write-Output "QUICK_WINDOW_OPENED: yes"
  # Combo text children
  $txtCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
  $allTxt = $Combo.FindAll([System.Windows.Automation.TreeScope]::Descendants, $txtCond)
  foreach ($t in $allTxt) { Write-Output (("  Combo text=" + $t.Current.Name)) }
  # walk to window parent
  $anc = $Combo
  while ($null -ne $anc -and $anc.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window) {
    try { $anc = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($anc) } catch { $anc = $null }
  }
  if ($null -eq $anc) {
    $anc = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "快速新增")))
    if ($null -ne $anc -and $anc.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window) { $anc = $null }
  }
  if ($null -ne $anc) {
    Write-Output "=== WINDOW DUMP START ==="
    function DumpA { param($e,$d=0,$md=7) if ($null -eq $e) {return} $pad = ""; for($i=0;$i -lt $d;$i++){$pad += "  "} $n=$e.Current.Name; if([string]::IsNullOrEmpty($n)){$n="(empty)"} $t=$e.Current.ControlType.ProgrammaticName -replace "^ControlType..",""; Write-Output ($pad + $t + " " + [char]34 + $n + [char]34); if ($d -ge $md){return} $ch = $e.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition); foreach($c in $ch){ DumpA $c ($d+1) $md } }
    DumpA $anc 0 7
    Write-Output "=== WINDOW DUMP END ==="
  }
  $cancelCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "取消")
  $CancelBtn = if($null -ne $anc) { $anc.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cancelCond) } else { $null }
  if ($null -ne $CancelBtn) {
    $cbR = $CancelBtn.Current.BoundingRectangle
    [MouseSimA]::Click([int]($cbR.Left + $cbR.Width/2), [int]($cbR.Top + $cbR.Height/2))
    Write-Output "Closed via Cancel"
  } else {
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    Write-Output "Closed via Esc"
  }
  Start-Sleep -Milliseconds 400
  $ts3 = Get-Date -Format "yyyyMMdd_HHmmss"
  $sp3 = "D:/myProgram/cccalendar/screenshots/09_qa_after_cancel_aug30_" + $ts3 + ".png"
  Take-Screenshot -Path $sp3
  Write-Output (("SCREENSHOT_AFTER_CANCEL_AUG30=" + $sp3))
}