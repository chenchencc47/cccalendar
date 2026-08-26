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

# Switch to 日历 nav (the one that opens calendar month view). Find the NavigationItemViewModel with Destination=Calendar in ListItems.
$liCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
$lis = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $liCond)
$calendarNav = $null
foreach ($li in $lis) {
  $n = $li.Current.Name
  if ($n -like "*Destination = Calendar*" -or $n -like "*Label = 日历*") { $calendarNav = $li; break }
}
if ($null -ne $calendarNav) {
  $r = $calendarNav.Current.BoundingRectangle
  $x = [int]($r.Left + $r.Width/2)
  $y = [int]($r.Top + $r.Height/2)
  [MouseSim]::Click($x, $y)
  Start-Sleep -Milliseconds 500
  Write-Output "Clicked 日历 nav"
}

$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$sp = "D:/myProgram/cccalendar/screenshots/07b_debug_calendar_after_switch_" + $ts + ".png"
Take-Screenshot -Path $sp
Write-Output (("SCT2:" + $sp))

# Now find the 42 CalendarDay cells; search for DateText inside ItemsControlItem (CalendarDayViewModel)
$all2 = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$c=0
foreach ($e in $all2) {
  $n = $e.Current.Name
  if ($n -like "*CalendarDayViewModel*" -and $n -like "*Date = *") {
    $ct = $e.Current.ControlType.ProgrammaticName
    $r = $e.Current.BoundingRectangle
    Write-Output ($ct + " RECT=" + $r + " | " + $n.Substring(0, [Math]::Min(160, $n.Length)))
    $c++
    if ($c -ge 50) { break }
  }
}
Write-Output (("CalendarDay matches=" + $c))
# Also find by ClassName ItemsControlItem
$clsCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ClassNameProperty, "ItemsControlItem")
$ics = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $clsCond)
Write-Output (("ItemsControlItem total=" + $ics.Count))
$c2=0
foreach ($e in $ics) {
  $n = $e.Current.Name
  if ($n -like "*Date = *") {
    $r = $e.Current.BoundingRectangle
    Write-Output ("IC RECT=" + $r + " | " + $n.Substring(0, [Math]::Min(150, $n.Length)))
    $c2++
    if ($c2 -ge 50) { break }
  }
}