param([int]$X, [int]$Y)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName WindowsBase
$el = [System.Windows.Automation.AutomationElement]::FromPoint([System.Windows.Point]::new($X, $Y))
$depth = 0
while ($null -ne $el -and $depth -lt 12) {
  $r = $el.Current.BoundingRectangle
  Write-Output ("L" + $depth + " '" + $el.Current.Name + "' " + $el.Current.ControlType.ProgrammaticName + " rect=" + [int]$r.Left + "," + [int]$r.Top + " " + [int]$r.Width + "x" + [int]$r.Height)
  $el = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($el)
  $depth++
}
