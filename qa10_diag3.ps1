Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RM6 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
"@

$PidOfApp = 28960

function Get-Workbench {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
  foreach ($w in $wins) { if ($w.Current.Name -eq '' -and $w.Current.BoundingRectangle.Width -gt 900) { return $w } }
  return $null
}

function Click-At([int]$x, [int]$y) {
  [RM6]::SetCursorPos($x, $y) | Out-Null
  Start-Sleep -Milliseconds 250
  [RM6]::mouse_event([RM6]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 80
  [RM6]::mouse_event([RM6]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 1000
}

$wb = Get-Workbench

# Switch back to month view: click the month toggle
$toggles = $wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
$monthBtn = $null
foreach ($b in $toggles) { if ($b.Current.Name -eq ([string][char]0x6708)) { $monthBtn = $b } }
if ($null -ne $monthBtn) {
  $r = $monthBtn.Current.BoundingRectangle
  Click-At ([int]($r.Left + $r.Width / 2)) ([int]($r.Top + $r.Height / 2))
  $all = $wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  $cnt = 0
  foreach ($e in $all) { if ($e.Current.Name -like '*CalendarDayViewModel*') { $cnt++ } }
  Write-Output ("Day count after month switch: " + $cnt)
}

# Now click Aug 2 day cell
$all = $wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$day2 = $null
foreach ($e in $all) { if ($e.Current.Name -like '*Date = 2026/8/2,*') { $day2 = $e } }
if ($null -eq $day2) { Write-Output "FAIL: Aug2 not found"; exit 1 }
$r = $day2.Current.BoundingRectangle
$cx = [int]($r.Left + $r.Width / 2); $cy = [int]($r.Top + 10)
Write-Output ("Aug2 cell at " + $r.ToString() + " clicking (" + $cx + "," + $cy + ")")
Click-At $cx $cy

$all = $wb.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
foreach ($e in $all) {
  if ($e.Current.Name -like '*Date = 2026/8/2,*') {
    $sel = $e.Current.Name -match 'IsSelected = True'
    Write-Output ("AUG2_SELECTED=" + $sel)
    break
  }
}
