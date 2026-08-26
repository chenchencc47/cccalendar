param([int]$X, [int]$Y)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName WindowsBase
$pt = New-Object System.Windows.Point($X, $Y)
$e = [System.Windows.Automation.AutomationElement]::FromPoint($pt)
if ($null -eq $e) { Write-Output "FAIL: no element at point"; exit 1 }
Write-Output ("AT(" + $X + "," + $Y + "): type=" + $e.Current.ControlType.ProgrammaticName + " name='" + $e.Current.Name + "'")
$parent = $e
for ($i = 0; $i -lt 8; $i++) {
  $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
  $parent = $walker.GetParent($parent)
  if ($null -eq $parent) { break }
  Write-Output ("  parent" + $i + ": " + $parent.Current.ControlType.ProgrammaticName + " '" + $parent.Current.Name + "' @" + [int]$parent.Current.BoundingRectangle.Left + "," + [int]$parent.Current.BoundingRectangle.Top + " " + [int]$parent.Current.BoundingRectangle.Width + "x" + [int]$parent.Current.BoundingRectangle.Height)
}
