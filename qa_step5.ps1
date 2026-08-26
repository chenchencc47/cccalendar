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
[void]$sb.AppendLine("public class MouseSim {")
$q = [char]34
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern bool SetCursorPos(int x, int y);")
[void]$sb.AppendLine("  [DllImport("+$q+"user32.dll"+$q+", SetLastError=true)] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);")
[void]$sb.AppendLine("  public const uint LEFTDOWN = 0x02, LEFTUP = 0x04;")
[void]$sb.AppendLine("  public static void Click(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(30); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(30); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(30); }")
[void]$sb.AppendLine("  public static void DoubleClick(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(30); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(80); mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(20); mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(30); }")
[void]$sb.AppendLine("}")
Add-Type -TypeDefinition $sb.ToString() -ErrorAction SilentlyContinue

# Switch to 日历 page if not there (click ListItem with label 日历)
$navCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "日历")
$nav = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $navCond)
if ($null -ne $nav) {
  $nr = $nav.Current.BoundingRectangle
  $nx = [int]($nr.Left + $nr.Width/2)
  $ny = [int]($nr.Top + $nr.Height/2)
  [MouseSim]::Click($nx, $ny)
  Start-Sleep -Milliseconds 300
  Write-Output "Switched to 日历 tab"
}

# Click 今天 button to refresh then select next month date 2026/9/5 (future)
$todayBtnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "今天")
$todayBtn = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $todayBtnCond)
if ($null -ne $todayBtn) {
  $br = $todayBtn.Current.BoundingRectangle
  [MouseSim]::Click([int]($br.Left + $br.Width/2), [int]($br.Top + $br.Height/2))
  Start-Sleep -Milliseconds 400
}

# Find Day cell for Date = 2026/9/5 (Saturday, next month, in the grid)
# Search CalendarDayViewModel with Date containing "2026/9/5"
$itemCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Custom)
$allItems = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $itemCond)
$target = $null
foreach ($it in $allItems) {
  $nm = $it.Current.Name
  if ($nm -like "*Date = 2026/9/5*" -and $nm -like "*CalendarDayViewModel*") { $target = $it; break }
}
if ($null -eq $target) {
  Write-Output "FAIL target day not found by ControlType.Custom; try DataItem"
  $diCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::DataItem)
  $allDis = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $diCond)
  foreach ($it in $allDis) {
    $nm = $it.Current.Name
    if ($nm -like "*Date = 2026/9/5*" -and $nm -like "*CalendarDayViewModel*") { $target = $it; break }
  }
}
if ($null -eq $target) { Write-Output "FAIL target day still null"; exit 1 }
Write-Output (("Target Name=" + $target.Current.Name.Substring(0, [Math]::Min(200, $target.Current.Name.Length))))
$tr = $target.Current.BoundingRectangle
Write-Output (("Target rect=" + $tr.ToString()))
$tx = [int]($tr.Left + $tr.Width/2)
$ty = [int]($tr.Top + $tr.Height/2)
# Step 5a: single click to select
[MouseSim]::Click($tx, $ty)
Start-Sleep -Milliseconds 400
$ts1 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp1 = "D:/myProgram/cccalendar/screenshots/07_qa_click_future_day_" + $ts1 + ".png"
Take-Screenshot -Path $sp1
Write-Output (("SCREENSHOT_CLICK_DAY:" + $sp1))
# Verify selected
$sel = $false
$nm = $target.Current.Name
if ($nm -like "*IsSelected = True*") { $sel = $true }
Write-Output (("Selected=" + $sel))
# Step 5b: real DoubleClick
[MouseSim]::DoubleClick($tx, $ty)
Start-Sleep -Milliseconds 900

$ts2 = Get-Date -Format "yyyyMMdd_HHmmss"
$sp2 = "D:/myProgram/cccalendar/screenshots/08_qa_doubleclick_quickadd_" + $ts2 + ".png"
Take-Screenshot -Path $sp2
Write-Output (("SCREENSHOT_DOUBLECLICK:" + $sp2))

# Check whether quick-add window opened
$cCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
$Combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $cCond)
if ($null -eq $Combo) {
  $pc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $and = New-Object System.Windows.Automation.AndCondition($pc, $cCond)
  $Combo = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $and)
}
if ($null -eq $Combo) {
  Write-Output "NO_QUICK_WINDOW: double click did NOT open quick-add"
} else {
  Write-Output "QUICK_WINDOW_OPENED: yes"
  # Dump selected type: select combo text? Read Name of list item under it, or first child text
  $txtCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
  $allTxt = $Combo.FindAll([System.Windows.Automation.TreeScope]::Descendants, $txtCond)
  foreach ($t in $allTxt) { Write-Output (("  Combo child text=" + $t.Current.Name)) }
  # Inspect children of the quick-add window for date/time/room/recurrence
  # Find window parent of Combo by walking up
  $anc = $Combo
  while ($null -ne $anc -and $anc.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window) {
    try { $anc = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($anc) } catch { $anc = $null }
  }
  if ($null -eq $anc) {
    Write-Output "Window ancestor not found; fallback search Name=快速新增 under Subtree"
    $anc = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "快速新增")))
    if ($null -ne $anc -and $anc.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window) {
      Write-Output "Got non-window match; search siblings for window"
      $anc = $null
    }
  }
  if ($null -ne $anc) {
    Write-Output "=== Window contents summary ==="
    function Dump1 { param($e,$d=0,$md=6) if ($null -eq $e) {return} $pad = ""; for($i=0;$i -lt $d;$i++){$pad += "  "} $n=$e.Current.Name; if([string]::IsNullOrEmpty($n)){$n="(empty)"} $t=$e.Current.ControlType.ProgrammaticName -replace "^ControlType..",""; Write-Output ($pad + $t + " " + [char]34 + $n + [char]34); if ($d -ge $md){return} $ch = $e.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition); foreach($c in $ch){ Dump1 $c ($d+1) $md } }
    Dump1 $anc 0 7
  }
  # Then close via Cancel button or Esc
  $cancelCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "取消")
  $CancelBtn = $null
  if ($null -ne $anc) { $CancelBtn = $anc.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cancelCond) }
  if ($null -ne $CancelBtn) {
    $cbR = $CancelBtn.Current.BoundingRectangle
    [MouseSim]::Click([int]($cbR.Left + $cbR.Width/2), [int]($cbR.Top + $cbR.Height/2))
    Write-Output "Closed via Cancel button"
  } else {
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    Write-Output "Closed via Esc"
  }
  Start-Sleep -Milliseconds 400
  $ts3 = Get-Date -Format "yyyyMMdd_HHmmss"
  $sp3 = "D:/myProgram/cccalendar/screenshots/09_qa_after_cancel_" + $ts3 + ".png"
  Take-Screenshot -Path $sp3
  Write-Output (("SCREENSHOT_CANCELLED:" + $sp3))
}