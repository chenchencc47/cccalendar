Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName Microsoft.VisualBasic
$PidOfApp = 31516
$null = [Microsoft.VisualBasic.Interaction]::AppActivate($PidOfApp)
Start-Sleep -Milliseconds 200
$p = [System.Diagnostics.Process]::GetProcessById($PidOfApp)
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
Write-Output ("Window rect=" + $root.Current.BoundingRectangle.ToString())
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$target = $null
foreach ($e in $all) {
  $n = $e.Current.Name
  if ($n -like "*CalendarDayViewModel*" -and $n -like "*Date = 2026/8/30*") { $target = $e; break }
}
if ($null -eq $target) { Write-Output "Aug30 not found"; exit 1 }
function DumpC { param($ee,$d=0,$md=4) if ($null -eq $ee) {return} $pad=""; for($i=0;$i -lt $d;$i++){$pad+="  "} $nm=$ee.Current.Name; if([string]::IsNullOrEmpty($nm)){$nm="(empty)"} $t=$ee.Current.ControlType.ProgrammaticName -replace "^ControlType..",""; $cls=$ee.Current.ClassName; $r=$ee.Current.BoundingRectangle; Write-Output ($pad + $t + " CLS=" + $cls + " RECT=" + $r + " " + [char]34 + $nm.Substring(0, [Math]::Min(80, $nm.Length)) + [char]34); if ($d -ge $md){return} $ch = $ee.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition); foreach($c in $ch) { DumpC $c ($d+1) $md } }
DumpC $target 0 6