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
$wr = $root.Current.BoundingRectangle
Write-Output ("Window: X=" + $wr.X + " Y=" + $wr.Y + " W=" + $wr.Width + " H=" + $wr.Height)

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("using System;")
[void]$sb.AppendLine("using System.Runtime.InteropServices;")
[void]$sb.AppendLine("public class MouseSimC {")
$q = [char]34
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern bool SetCursorPos(int x, int y);")
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);")
[void]$sb.AppendLine("  public const uint LEFTDOWN = 0x02, LEFTUP = 0x04, MOVE = 0x0001;")
[void]$sb.AppendLine("  public static void ClickAt(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(150); mouse_event(MOVE, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(50); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(100); }")
[void]$sb.AppendLine("  public static void DoubleClickAt(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(150); mouse_event(MOVE, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(50); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(100); }")
[void]$sb.AppendLine("}")
Add-Type -TypeDefinition $sb.ToString() -ErrorAction SilentlyContinue

# Target Aug30 DateText "30" at RECT=1147,697,15,19 -> center 1154,706
$tx = 1147 + 7
$ty = 697 + 9
Write-Output (("Click DateText center=" + $tx + "," + $ty))
[MouseSimC]::ClickAt($tx, $ty)
Start-Sleep -Milliseconds 600
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$target=$null; $sel=$false
foreach ($e in $all) {
  $n=$e.Current.Name
  if ($n -like "*CalendarDayViewModel*" -and $n -like "*Date = 2026/8/30*") {
    $target=$e
    if ($n -like "*IsSelected = True*") { $sel=$true }
    break
  }
}
Write-Output (("Aug30 selected after click=" + $sel))
if ($null -ne $target) { Write-Output (("snippet=" + $target.Current.Name.Substring(0, [Math]::Min(200, $target.Current.Name.Length)))) }
# Try SelectionItem pattern if not selected
if (-not $sel -and $null -ne $target) {
  try {
    $si = $target.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $si.Select()
    Start-Sleep -Milliseconds 500
    $n2 = $target.Current.Name
    $sel2 = if ($n2 -like "*IsSelected = True*") { $true } else { $false }
    Write-Output (("SelectionItemPattern.Select -> selected=" + $sel2))
  } catch { Write-Output (("SelectionItemPattern error=" + $_.Exception.Message)) }
}
$ts1 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp1 = "D:/myProgram/cccalendar/screenshots/07_qa_aug30_selected_v3_" + $ts1 + ".png"
Take-Screenshot -Path $sp1
Write-Output (("SCT_SELECTED_V3=" + $sp1))

# FINAL double click at same point
[MouseSimC]::DoubleClickAt($tx, $ty)
Start-Sleep -Milliseconds 1100
$ts2 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp2 = "D:/myProgram/cccalendar/screenshots/08_qa_dbclick_aug30_v3_" + $ts2 + ".png"
Take-Screenshot -Path $sp2
Write-Output (("SCT_DBCLICK_V3=" + $sp2))

$cCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
$Combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $cCond)
if ($null -eq $Combo) {
  $pc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $and = New-Object System.Windows.Automation.AndCondition($pc, $cCond)
  $Combo = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $and)
}
if ($null -eq $Combo) {
  Write-Output "NO_QUICK_WINDOW: v3 no combo"
} else {
  Write-Output "QUICK_WINDOW_OPENED: v3 yes"
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
    function DumpC2 { param($e,$d=0,$md=7) if ($null -eq $e) {return} $pad=""; for($i=0;$i -lt $d;$i++){$pad+="  "} $n=$e.Current.Name; if([string]::IsNullOrEmpty($n)){$n="(empty)"} $t=$e.Current.ControlType.ProgrammaticName -replace "^ControlType..",""; Write-Output ($pad + $t + " " + [char]34 + $n + [char]34); if ($d -ge $md){return} $ch = $e.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition); foreach($c in $ch){ DumpC2 $c ($d+1) $md } }
    DumpC2 $anc 0 7
    Write-Output "=== DUMP END ==="
  }
  $cancelCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "取消")
  $CancelBtn = if($null -ne $anc) { $anc.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cancelCond) } else { $null }
  if ($null -ne $CancelBtn) {
    $cbR = $CancelBtn.Current.BoundingRectangle
    [MouseSimC]::ClickAt([int]($cbR.Left + $cbR.Width/2), [int]($cbR.Top + $cbR.Height/2))
    Write-Output "Closed via Cancel"
  } else {
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    Write-Output "Closed via Esc"
  }
  Start-Sleep -Milliseconds 400
  $ts3 = Get-Date -Format "yyyyMMdd_HHmmss"
  $sp3 = "D:/myProgram/cccalendar/screenshots/09_qa_cancel_aug30_v3_" + $ts3 + ".png"
  Take-Screenshot -Path $sp3
  Write-Output (("SCT_CANCEL_V3=" + $sp3))
}