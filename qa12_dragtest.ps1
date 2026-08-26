param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RM9 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
public class WinR9 {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
function Take-Screenshot { param([string]$Path) $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height; $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size); $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose() }
function Get-MainWindow {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
  foreach ($w in $wins) { if ($w.Current.Name -eq 'cccalendar') { return $w } }
  return $null
}

if (-not $PidOfApp) { Write-Output "FAIL: no pid"; exit 1 }
$main = Get-MainWindow
if ($null -eq $main) { Write-Output "FAIL: main window not found"; exit 1 }
$hwnd = [IntPtr]([int]($main.Current.NativeWindowHandle))
[void][WinR9]::ShowWindow($hwnd, 9)
[void][WinR9]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 600

# ---- Step 0: navigate to 待办 page ----
$els0 = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$todoNav = $null
foreach ($i in $els0) { if ($i.Current.Name -eq '待办' -and $i.Current.ControlType.ProgrammaticName -eq 'ControlType.ListItem') { $todoNav = $i; break } }
if ($null -eq $todoNav) { foreach ($i in $els0) { if ($i.Current.Name -eq '待办') { $todoNav = $i; break } } }
if ($null -eq $todoNav) { Write-Output "FAIL: 待办 nav not found"; exit 1 }
$nr = $todoNav.Current.BoundingRectangle
[RM9]::SetCursorPos([int]($nr.Left + $nr.Width / 2), [int]($nr.Top + $nr.Height / 2)) | Out-Null
Start-Sleep -Milliseconds 200
[RM9]::mouse_event([RM9]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 50
[RM9]::mouse_event([RM9]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1200
$main = Get-MainWindow

# ---- Step 1: create a todo via the input box ----
$box = $main.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "新增待办")))
if ($null -eq $box) {
  # fall back: search by name
  $els = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($e in $els) { if ($e.Current.Name -eq '新增待办' -and $e.Current.ControlType.ProgrammaticName -eq 'ControlType.Edit') { $box = $e; break } }
}
if ($null -eq $box) { Write-Output "FAIL: input box not found"; exit 1 }
Write-Output "INPUT_BOX found"

$title = "QA拖拽验证" + (Get-Date -Format "HHmmss")
try {
  $vp = $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
  $vp.SetValue($title)
  Write-Output ("TYPED: " + $title)
} catch {
  Write-Output ("ValuePattern failed: " + $_.Exception.Message)
  exit 1
}

# focus and press Enter
$box.SetFocus()
Start-Sleep -Milliseconds 200
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 1500

# ---- Step 2: locate the new card and quadrant rects ----
$main = Get-MainWindow
$els = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$card = $null
$quads = @{}
$quadNames = @('重要且紧急', '重要不紧急', '不重要但紧急', '不重要不紧急')
foreach ($e in $els) {
  $n = $e.Current.Name
  if ($n -eq $title) { $card = $e }
  if ($quadNames -contains $n) { $quads[$n] = $e.Current.BoundingRectangle }
}
if ($null -eq $card) { Write-Output "FAIL: created card not found in quadrants"; exit 1 }
$cr = $card.Current.BoundingRectangle
Write-Output ("CARD rect=" + [int]$cr.Left + "," + [int]$cr.Top + " size " + [int]$cr.Width + "x" + [int]$cr.Height)
foreach ($k in $quads.Keys) { $r = $quads[$k]; Write-Output ("QUAD '" + $k + "' rect=" + [int]$r.Left + "," + [int]$r.Top + " size " + [int]$r.Width + "x" + [int]$r.Height) }

# The card should be in 不重要不紧急 (bottom-right). Target: 重要且紧急 (top-left).
$target = $quads['重要且紧急']
$sx = [int]($cr.Left + $cr.Width / 2)
$sy = [int]($cr.Top + $cr.Height / 2)
$tx = [int]($target.Left + $target.Width / 2)
$ty = [int]($target.Top + 140)

Write-Output ("DRAG from (" + $sx + "," + $sy + ") to (" + $tx + "," + $ty + ")")

# ---- Step 3: real mouse drag ----
[RM9]::SetCursorPos($sx, $sy) | Out-Null
Start-Sleep -Milliseconds 300
[RM9]::mouse_event([RM9]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 150
# move in steps so OLE drag sees WM_MOUSEMOVE
$steps = 12
for ($i = 1; $i -le $steps; $i++) {
  $nx = [int]($sx + ($tx - $sx) * $i / $steps)
  $ny = [int]($sy + ($ty - $sy) * $i / $steps)
  [RM9]::SetCursorPos($nx, $ny) | Out-Null
  Start-Sleep -Milliseconds 60
}
Start-Sleep -Milliseconds 250
[RM9]::mouse_event([RM9]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 2000

$ts = Get-Date -Format "HHmmss"
$sp = "D:/myProgram/cccalendar/screenshots/qa12_dragresult_$ts.png"
Take-Screenshot -Path $sp
Write-Output ("SCT=" + $sp)

# ---- Step 4: verify which quadrant contains the card now ----
# 表头只是 19px 高的文本，象限实际是 2x2 网格区域：按表头中心推导行列。
$main = Get-MainWindow
$els = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$card2 = $null
foreach ($e in $els) { if ($e.Current.Name -eq $title) { $card2 = $e; break } }
if ($null -eq $card2) { Write-Output "RESULT: card not found after drag"; exit 1 }
$r2 = $card2.Current.BoundingRectangle
$cx2 = $r2.Left + $r2.Width / 2
$cy2 = $r2.Top + $r2.Height / 2
Write-Output ("CARD_AFTER rect=" + [int]$r2.Left + "," + [int]$r2.Top)
$tl = $quads['重要且紧急']; $tr = $quads['重要不紧急']; $bl = $quads['不重要但紧急']; $br = $quads['不重要不紧急']
if ($null -eq $tl -or $null -eq $tr -or $null -eq $bl -or $null -eq $br) { Write-Output "RESULT: quadrant headers missing"; exit 1 }
$colLeft = ($tl.Left + $tr.Right) / 2
$rowTop = ($tl.Bottom + $bl.Top) / 2
$col = if ($cx2 -lt $colLeft) { 'left' } else { 'right' }
$row = if ($cy2 -lt $rowTop) { 'top' } else { 'bottom' }
$name = switch ("$col-$row") {
  'left-top' { '重要且紧急' }
  'right-top' { '重要不紧急' }
  'left-bottom' { '不重要但紧急' }
  'right-bottom' { '不重要不紧急' }
}
Write-Output ("RESULT: card is now in '" + $name + "' (col=" + $col + ", row=" + $row + ")")
if ($name -eq '重要且紧急') { Write-Output "DRAG_TEST=PASS" } else { Write-Output "DRAG_TEST=FAIL" }
