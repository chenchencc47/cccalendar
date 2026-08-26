Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName Microsoft.VisualBasic
function Take-Screenshot { param([string]$Path) $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height; $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size); $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose() }

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("using System;")
[void]$sb.AppendLine("using System.Runtime.InteropServices;")
[void]$sb.AppendLine("public class MouseSimH {")
$q = [char]34
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern bool SetCursorPos(int x, int y);")
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);")
[void]$sb.AppendLine("  public const uint LEFTDOWN=0x02, LEFTUP=0x04, MOVE=0x0001;")
[void]$sb.AppendLine("  public static void ClickAtWithMove(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(250); mouse_event(MOVE, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(50); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(150); mouse_event(MOVE, 2, 1, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(150); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(300); }")
[void]$sb.AppendLine("  public static void DoubleClickAt(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(250); mouse_event(MOVE, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(50); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(80); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(300); }")
[void]$sb.AppendLine("}")
Add-Type -TypeDefinition $sb.ToString() -ErrorAction SilentlyContinue

$PidOfApp = 31516
$p = [System.Diagnostics.Process]::GetProcessById($PidOfApp)
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
# Use DateText center Aug 25 (future, date=2026/8/25): center 442,754
$tx = 442; $ty = 754
Write-Output (("Click with drag on Aug25 DateText=" + $tx + "," + $ty))
[MouseSimH]::ClickAtWithMove($tx, $ty)
Start-Sleep -Milliseconds 800
# Read state
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$target=$null; $sel=$false; $any=$null
foreach ($e in $all) {
  $n=$e.Current.Name
  if ($n -like "*CalendarDayViewModel*") {
    if ($n -like "*Date = 2026/8/25*") { $target=$e; if($n -like "*IsSelected = True*"){$sel=$true} }
    if ($n -like "*IsSelected = True*") { $any=$n }
  }
}
Write-Output (("Any selected=" + $(if($null -ne $any){$any.Substring(0,[Math]::Min(200,$any.Length))} else {"NONE"})))
Write-Output (("Aug25 selected=" + $sel))
$ts1 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp1 = "D:/myProgram/cccalendar/screenshots/07_qa_click_aug25_withdrag_" + $ts1 + ".png"
Take-Screenshot -Path $sp1
Write-Output (("SCT_DRAG=" + $sp1))
# Double click now
[MouseSimH]::DoubleClickAt($tx, $ty)
Start-Sleep -Milliseconds 1300
$ts2 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp2 = "D:/myProgram/cccalendar/screenshots/08_qa_dbclick_aug25_h_" + $ts2 + ".png"
Take-Screenshot -Path $sp2
Write-Output (("SCT_DBCLICK_H=" + $sp2))

$cCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
$Combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $cCond)
if ($null -eq $Combo) {
  $pc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $and = New-Object System.Windows.Automation.AndCondition($pc, $cCond)
  $Combo = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $and)
}
if ($null -eq $Combo) {
  Write-Output "NO_QUICK_WINDOW: h variant no combo"
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
    function DumpH { param($e,$d=0,$md=7) if ($null -eq $e) {return} $pad=""; for($i=0;$i -lt $d;$i++){$pad+="  "} $n=$e.Current.Name; if([string]::IsNullOrEmpty($n)){$n="(empty)"} $t=$e.Current.ControlType.ProgrammaticName -replace "^ControlType..",""; Write-Output ($pad + $t + " " + [char]34 + $n + [char]34); if ($d -ge $md){return} $ch = $e.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition); foreach($c in $ch){ DumpH $c ($d+1) $md } }
    DumpH $anc 0 7
    Write-Output "=== DUMP END ==="
  }
  $cancelCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "取消")
  $CancelBtn = if($null -ne $anc) { $anc.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cancelCond) } else { $null }
  if ($null -ne $CancelBtn) {
    $cbR = $CancelBtn.Current.BoundingRectangle
    [MouseSimH]::ClickAtWithMove([int]($cbR.Left + $cbR.Width/2), [int]($cbR.Top + $cbR.Height/2))
    Write-Output "Closed via Cancel"
  } else {
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    Write-Output "Closed via Esc"
  }
  Start-Sleep -Milliseconds 400
  $ts3 = Get-Date -Format "yyyyMMdd_HHmmss"
  $sp3 = "D:/myProgram/cccalendar/screenshots/09_qa_cancel_h_" + $ts3 + ".png"
  Take-Screenshot -Path $sp3
  Write-Output (("SCT_CANCEL_H=" + $sp3))
}