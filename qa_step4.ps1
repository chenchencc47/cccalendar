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

# Step 4: Send Esc to close quick-add window (focus it first by clicking on main window area - actually just activate the app and send Esc)
# First focus: click on main window title area
$wr = $root.Current.BoundingRectangle
$tx = [int]($wr.Left + 60)
$ty = [int]($wr.Top + 12)
[MouseSim]::Click($tx, $ty)
Start-Sleep -Milliseconds 200
[System.Windows.Forms.SendKeys]::SendWait("{ESC}")
Start-Sleep -Milliseconds 500
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$sp = "D:/myProgram/cccalendar/screenshots/06_qa_after_esc_" + $ts + ".png"
Take-Screenshot -Path $sp
Write-Output (("SCREENSHOT_AFTER_ESC:" + $sp))

# Verify no quick add ComboBox visible now
$cCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
$Combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $cCond)
if ($null -eq $Combo) { Write-Output "OK: Quick window closed (no ComboBox)" } else { Write-Output (("WARN: Combo still present rect=" + $Combo.Current.BoundingRectangle.ToString())) }