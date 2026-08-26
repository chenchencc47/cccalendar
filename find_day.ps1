Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName Microsoft.VisualBasic
$PidOfApp = 31516
$null = [Microsoft.VisualBasic.Interaction]::AppActivate($PidOfApp)
Start-Sleep -Milliseconds 200
$p = [System.Diagnostics.Process]::GetProcessById($PidOfApp)
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
foreach ($e in $all) {
  $n = $e.Current.Name
  if ($n -like "*CalendarDayViewModel*" -and $n -like "*Date = 2026/9/5*") {
    $ct = $e.Current.ControlType.ProgrammaticName
    $cls = $e.Current.ClassName
    $r = $e.Current.BoundingRectangle
    Write-Output ("CT=" + $ct + " CLS=" + $cls + " RECT=" + $r.ToString())
    Write-Output ("  NAME=" + $n.Substring(0, [Math]::Min(160, $n.Length)))
  }
}
Write-Output "done"