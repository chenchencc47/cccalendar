Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName Microsoft.VisualBasic
function Take-Screenshot { param([string]$Path) $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height; $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size); $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose() }
$PidOfApp = 31516
$null = [Microsoft.VisualBasic.Interaction]::AppActivate($PidOfApp)
Start-Sleep -Milliseconds 300

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("using System;")
[void]$sb.AppendLine("using System.Runtime.InteropServices;")
[void]$sb.AppendLine("public class MouseSimE {")
$q = [char]34
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern bool SetCursorPos(int x, int y);")
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);")
[void]$sb.AppendLine("  public const uint LEFTDOWN = 0x02, LEFTUP = 0x04;")
[void]$sb.AppendLine("  public static void ClickAt(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(200); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(200); }")
[void]$sb.AppendLine("  public static void DoubleClickAt(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(200); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(200); }")
[void]$sb.AppendLine("}")
Add-Type -TypeDefinition $sb.ToString() -ErrorAction SilentlyContinue

# Target: 八月25 (Tuesday, future, well inside grid). Visual: x~582, y~654
$tx = 582
$ty = 654
Write-Output (("Click Aug25 center=" + $tx + "," + $ty))
[MouseSimE]::ClickAt($tx, $ty)
Start-Sleep -Milliseconds 700
$p = [System.Diagnostics.Process]::GetProcessById($PidOfApp)
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$sel=$false; $any=$null
foreach ($e in $all) {
  $n=$e.Current.Name
  if ($n -like "*CalendarDayViewModel*" -and $n -like "*IsSelected = True*") {
    $any = $n
    if ($n -like "*Date = 2026/8/25*") { $sel = $true }
  }
}
Write-Output (("Selected=" + $(if($null -ne $any){$any.Substring(0,[Math]::Min(200,$any.Length))} else {"NONE"})))
Write-Output (("Aug25 is selected=" + $sel))
$ts1 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp1 = "D:/myProgram/cccalendar/screenshots/07_qa_click_aug25_" + $ts1 + ".png"
Take-Screenshot -Path $sp1
Write-Output (("SCT_CLICK_AUG25=" + $sp1))

# If not selected yet, fallback: click 8/28 (Fri at x~880 y~654) to at least prove clicks work
if (-not $sel) {
  [MouseSimE]::ClickAt(880, 654)
  Start-Sleep -Milliseconds 700
  $all2 = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  $sel28=$false; $any2=$null
  foreach ($e in $all2) {
    $n=$e.Current.Name
    if ($n -like "*CalendarDayViewModel*" -and $n -like "*IsSelected = True*") {
      $any2=$n
      if ($n -like "*Date = 2026/8/28*") { $sel28=$true }
    }
  }
  Write-Output (("After click Aug28: Selected=" + $(if($null -ne $any2){$any2.Substring(0,[Math]::Min(200,$any2.Length))} else {"NONE"})))
  Write-Output (("Aug28 is selected=" + $sel28))
  # if Aug28 ok, use it for double click
  if ($sel28) {
    $tx = 880
    Write-Output "USE Aug28 for doubleclick target"
  } else {
    Write-Output "Fallback: use Aug25 for doubleclick anyway"
    $tx = 582
  }
}
$ts2 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp2 = "D:/myProgram/cccalendar/screenshots/07b_qa_click_fallback_" + $ts2 + ".png"
Take-Screenshot -Path $sp2
Write-Output (("SCT_FALLBACK=" + $sp2))

# Double-click current target
[MouseSimE]::DoubleClickAt($tx, $ty)
Start-Sleep -Milliseconds 1200
$ts3 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp3 = "D:/myProgram/cccalendar/screenshots/08_qa_dbclick_target_" + $ts3 + ".png"
Take-Screenshot -Path $sp3
Write-Output (("SCT_DBCLICK=" + $sp3))

# check quick window
$cCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
$Combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $cCond)
if ($null -eq $Combo) {
  $pc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $and = New-Object System.Windows.Automation.AndCondition($pc, $cCond)
  $Combo = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $and)
}
if ($null -eq $Combo) {
  Write-Output "NO_QUICK_WINDOW: after double click no combo"
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
    function DumpE { param($e,$d=0,$md=7) if ($null -eq $e) {return} $pad=""; for($i=0;$i -lt $d;$i++){$pad+="  "} $n=$e.Current.Name; if([string]::IsNullOrEmpty($n)){$n="(empty)"} $t=$e.Current.ControlType.ProgrammaticName -replace "^ControlType..",""; Write-Output ($pad + $t + " " + [char]34 + $n + [char]34); if ($d -ge $md){return} $ch = $e.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition); foreach($c in $ch){ DumpE $c ($d+1) $md } }
    DumpE $anc 0 7
    Write-Output "=== DUMP END ==="
  }
  $cancelCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "取消")
  $CancelBtn = if($null -ne $anc) { $anc.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cancelCond) } else { $null }
  if ($null -ne $CancelBtn) {
    $cbR = $CancelBtn.Current.BoundingRectangle
    [MouseSimE]::ClickAt([int]($cbR.Left + $cbR.Width/2), [int]($cbR.Top + $cbR.Height/2))
    Write-Output "Closed via Cancel"
  } else {
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    Write-Output "Closed via Esc"
  }
  Start-Sleep -Milliseconds 400
  $ts4 = Get-Date -Format "yyyyMMdd_HHmmss"
  $sp4 = "D:/myProgram/cccalendar/screenshots/09_qa_cancel_after_dbclick_" + $ts4 + ".png"
  Take-Screenshot -Path $sp4
  Write-Output (("SCT_CANCEL=" + $sp4))
}