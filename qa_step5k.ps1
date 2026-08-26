Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName Microsoft.VisualBasic
function Take-Screenshot { param([string]$Path) $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height; $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size); $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose() }

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("using System;")
[void]$sb.AppendLine("using System.Runtime.InteropServices;")
[void]$sb.AppendLine("public class MouseSimG {")
$q = [char]34
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern bool SetCursorPos(int x, int y);")
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);")
[void]$sb.AppendLine("  public const uint LEFTDOWN = 0x02, LEFTUP = 0x04;")
[void]$sb.AppendLine("  public static void ClickAt(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(200); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(250); }")
[void]$sb.AppendLine("  public static void DoubleClickAt(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(200); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(250); }")
[void]$sb.AppendLine("}")
Add-Type -TypeDefinition $sb.ToString() -ErrorAction SilentlyContinue

$PidOfApp = 31516
$p = [System.Diagnostics.Process]::GetProcessById($PidOfApp)
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

# Find Aug25 DataItem first, then its Text child with Name="25" (DateText)
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$target=$null
foreach ($e in $all) {
  $n=$e.Current.Name
  if ($n -like "*CalendarDayViewModel*" -and $n -like "*Date = 2026/8/25*") { $target=$e; break }
}
if ($null -eq $target) { Write-Output "FAIL Aug25 DataItem not found"; exit 1 }
Write-Output (("Aug25 RECT=" + $target.Current.BoundingRectangle.ToString()))
# Search for child Text with Name="25" under it
$txtCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
$texts = $target.FindAll([System.Windows.Automation.TreeScope]::Descendants, $txtCond)
$dateText = $null
foreach ($t in $texts) {
  $tn = $t.Current.Name
  if ($tn -eq "25") { $dateText = $t; break }
}
if ($null -eq $dateText) {
  foreach ($t in $texts) { Write-Output (("  childtext=" + $t.Current.Name + " RECT=" + $t.Current.BoundingRectangle.ToString())) }
  Write-Output "FAIL DateText 25 not found"; exit 1
}
$dr = $dateText.Current.BoundingRectangle
Write-Output (("DateText RECT=" + $dr.ToString()))
if ($dr.IsEmpty) { Write-Output "DateText rect Empty"; exit 1 }
$tx = [int]($dr.Left + $dr.Width/2)
$ty = [int]($dr.Top + $dr.Height/2)
Write-Output (("Click on DateText center=" + $tx + "," + $ty))
[MouseSimG]::ClickAt($tx, $ty)
Start-Sleep -Milliseconds 700
$n2 = $target.Current.Name
$sel = if ($n2 -like "*IsSelected = True*") { $true } else { $false }
Write-Output (("After click: IsSelected=" + $sel))
$ts1 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp1 = "D:/myProgram/cccalendar/screenshots/07_qa_click_aug25_datetext_" + $ts1 + ".png"
Take-Screenshot -Path $sp1
Write-Output (("SCT_CLICK_DATETEXT=" + $sp1))

# Double click at same point
[MouseSimG]::DoubleClickAt($tx, $ty)
Start-Sleep -Milliseconds 1200
$ts2 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp2 = "D:/myProgram/cccalendar/screenshots/08_qa_dbclick_aug25_datetext_" + $ts2 + ".png"
Take-Screenshot -Path $sp2
Write-Output (("SCT_DBCLICK_DATETEXT=" + $sp2))

# Check quick-add window via ComboBox search
$cCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
$Combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $cCond)
if ($null -eq $Combo) {
  $pc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $and = New-Object System.Windows.Automation.AndCondition($pc, $cCond)
  $Combo = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $and)
}
if ($null -eq $Combo) {
  Write-Output "NO_QUICK_WINDOW: after dbclick datetext no combo"
} else {
  Write-Output "QUICK_WINDOW_OPENED: yes"
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
    function DumpG { param($e,$d=0,$md=7) if ($null -eq $e) {return} $pad=""; for($i=0;$i -lt $d;$i++){$pad+="  "} $n=$e.Current.Name; if([string]::IsNullOrEmpty($n)){$n="(empty)"} $t=$e.Current.ControlType.ProgrammaticName -replace "^ControlType..",""; Write-Output ($pad + $t + " " + [char]34 + $n + [char]34); if ($d -ge $md){return} $ch = $e.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition); foreach($c in $ch){ DumpG $c ($d+1) $md } }
    DumpG $anc 0 7
    Write-Output "=== DUMP END ==="
  }
  $cancelCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "取消")
  $CancelBtn = if($null -ne $anc) { $anc.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cancelCond) } else { $null }
  if ($null -ne $CancelBtn) {
    $cbR = $CancelBtn.Current.BoundingRectangle
    [MouseSimG]::ClickAt([int]($cbR.Left + $cbR.Width/2), [int]($cbR.Top + $cbR.Height/2))
    Write-Output "Closed via Cancel"
  } else {
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    Write-Output "Closed via Esc"
  }
  Start-Sleep -Milliseconds 400
  $ts3 = Get-Date -Format "yyyyMMdd_HHmmss"
  $sp3 = "D:/myProgram/cccalendar/screenshots/09_qa_cancel_datetext_" + $ts3 + ".png"
  Take-Screenshot -Path $sp3
  Write-Output (("SCT_CANCEL_DATETEXT=" + $sp3))
}