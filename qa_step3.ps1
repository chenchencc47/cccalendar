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

# Find quick window by searching for ComboBox ControlType descendant of process, then get its Window ancestor
$cCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
# Prefer to search the main window subtree first (WPF owned windows may not appear under Root)
$Combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $cCond)
if ($null -eq $Combo) {
  # try root subtree filtered by process id via AND condition with ProcessId
  $pc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $and = New-Object System.Windows.Automation.AndCondition($pc, $cCond)
  $Combo = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $and)
}
if ($null -eq $Combo) { Write-Output "FAIL no Combo in scope"; exit 1 }
$R = $Combo.Current.BoundingRectangle
Write-Output (("Combo rect=" + $R.ToString()))
$Cx = [int]($R.Right - 15); $Cy = [int]($R.Top + $R.Height/2)
[MouseSim]::Click($Cx, $Cy)
Start-Sleep -Milliseconds 600

$T1 = Get-Date -Format "yyyyMMdd_HHmmss"
$S1 = "D:/myProgram/cccalendar/screenshots/04_qa_combo_expanded_$T1.png"
Take-Screenshot -Path $S1
Write-Output (("SCREENSHOT_CLICK:" + $S1))

$Ecp = $null; $Ex=$false
try { $Ecp = $Combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern) } catch {}
if ($null -ne $Ecp) {
  $ST = $Ecp.Current.ExpandCollapseState
  Write-Output (("ClickState=" + $ST))
  if ($ST -eq [System.Windows.Automation.ExpandCollapseState]::Expanded) { $Ex = $true }
}

if (-not $Ex) {
  try { $Combo.SetFocus() } catch {}
  Start-Sleep -Milliseconds 200
  [System.Windows.Forms.SendKeys]::SendWait("%{DOWN}")
  Start-Sleep -Milliseconds 800
  $T2 = Get-Date -Format "yyyyMMdd_HHmmss"
  $S2 = "D:/myProgram/cccalendar/screenshots/04b_qa_combo_altdown_$T2.png"
  Take-Screenshot -Path $S2
  Write-Output (("SCREENSHOT_ALTDOWN:" + $S2))
  if ($null -ne $Ecp) {
    $ST2 = $Ecp.Current.ExpandCollapseState
    Write-Output (("AltState=" + $ST2))
    if ($ST2 -eq [System.Windows.Automation.ExpandCollapseState]::Expanded) { $Ex = $true }
  }
}

if ($Ex) {
  $iCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "日程")
  $pc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $and = New-Object System.Windows.Automation.AndCondition($pc, $iCond)
  $Item = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $and)
  if ($null -eq $Item) { $Item = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $iCond) }
  if ($null -ne $Item) {
    $IR = $Item.Current.BoundingRectangle
    $Ix = [int]($IR.Left + $IR.Width/2)
    $Iy = [int]($IR.Top + $IR.Height/2)
    Write-Output (("Item rect=" + $IR.ToString()))
    [MouseSim]::Click($Ix, $Iy)
  } else {
    Write-Output "Item not found; DOWN ENTER"
    [System.Windows.Forms.SendKeys]::SendWait("{DOWN}{ENTER}")
  }
  Start-Sleep -Milliseconds 700
  $T3 = Get-Date -Format "yyyyMMdd_HHmmss"
  $S3 = "D:/myProgram/cccalendar/screenshots/05_qa_schedule_selected_$T3.png"
  Take-Screenshot -Path $S3
  Write-Output (("SCREENSHOT_SCHEDULE:" + $S3))
} else {
  Write-Output "FAILED_EXPAND"
}