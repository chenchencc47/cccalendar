Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName Microsoft.VisualBasic
function Take-Screenshot { param([string]$Path) $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height; $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size); $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose() }
$PidOfApp = 31516
$null = [Microsoft.VisualBasic.Interaction]::AppActivate($PidOfApp)
Start-Sleep -Milliseconds 400
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$sp = "D:/myProgram/cccalendar/screenshots/07_debug_calendar_state_" + $ts + ".png"
Take-Screenshot -Path $sp
Write-Output (("SCT: " + $sp))
$p = [System.Diagnostics.Process]::GetProcessById($PidOfApp)
$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
# Search by DataItem control type (items control)
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::DataItem)
$all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
Write-Output (("DataItem count=" + $all.Count))
$c=0
foreach ($e in $all) {
  $n = $e.Current.Name
  if ($n -like "*CalendarDayViewModel*" -or $n -like "*Date = *") {
    Write-Output ($e.Current.ControlType.ProgrammaticName + " | RECT=" + $e.Current.BoundingRectangle + " | " + $n.Substring(0, [Math]::Min(200, $n.Length)))
    $c++
    if ($c -ge 50) { break }
  }
}
Write-Output ("Matches=$c")
# Also enumerate ItemsControlItem class items
$cond2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ClassNameProperty, "ItemsControlItem")
$all2 = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond2)
Write-Output (("ItemsControlItem count=" + $all2.Count))
$c2=0
foreach ($e in $all2) {
  $n = $e.Current.Name
  if ($n -like "*Date = *") {
    Write-Output ("IC | RECT=" + $e.Current.BoundingRectangle + " | " + $n.Substring(0, [Math]::Min(200, $n.Length)))
    $c2++
    if ($c2 -ge 50) { break }
  }
}