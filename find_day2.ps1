Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName Microsoft.VisualBasic
$PidOfApp = 31516
$null = [Microsoft.VisualBasic.Interaction]::AppActivate($PidOfApp)
Start-Sleep -Milliseconds 200
$p = [System.Diagnostics.Process]::GetProcessById($PidOfApp)
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$count = 0
foreach ($e in $all) {
  $n = $e.Current.Name
  if ($n -like "*CalendarDayViewModel*Date = *") {
    $ct = $e.Current.ControlType.ProgrammaticName
    Write-Output ($ct + " :: " + $n.Substring(0, [Math]::Min(120, $n.Length)))
    $count++
    if ($count -gt 50) { break }
  }
}