param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
foreach ($w in $wins) {
  Write-Output ("WINDOW name='" + $w.Current.Name + "' type=" + $w.Current.ControlType.ProgrammaticName + " rect=" + [int]$w.Current.BoundingRectangle.Left + "," + [int]$w.Current.BoundingRectangle.Top + " " + [int]$w.Current.BoundingRectangle.Width + "x" + [int]$w.Current.BoundingRectangle.Height)
}
$main = $null
foreach ($w in $wins) { if ($w.Current.Name -eq 'cccalendar') { $main = $w } }
if ($null -eq $main) { Write-Output "no main window"; exit 1 }
$els = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$seen = @{}
foreach ($e in $els) {
  $n = $e.Current.Name
  if ($n -and -not $seen.ContainsKey($n)) {
    $seen[$n] = $true
    Write-Output ("ELEM '" + $n + "' " + $e.Current.ControlType.ProgrammaticName)
  }
}
