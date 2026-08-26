param([int]$PidOfApp)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinK {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
$qa = $null
foreach ($w in $wins) {
  Write-Output ("WINDOW '" + $w.Current.Name + "' rect=" + [int]$w.Current.BoundingRectangle.Left + "," + [int]$w.Current.BoundingRectangle.Top + " " + [int]$w.Current.BoundingRectangle.Width + "x" + [int]$w.Current.BoundingRectangle.Height)
  if ($w.Current.Name -eq '快速新增') { $qa = $w }
}
if ($null -eq $qa) { Write-Output "QUICKADD=NOT_FOUND"; exit 1 }
$hwnd = [IntPtr]([int]($qa.Current.NativeWindowHandle))
[void][WinK]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 400

# 读取快速新增窗口内的日期文本（DatePicker 文本框内容 2026/8/21）
$els = $qa.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
$found = $false
foreach ($e in $els) {
  $n = $e.Current.Name
  if ($n -like '*2026*8*21*' -or $n -like '*2026/8/21*') {
    Write-Output ("DATE_TEXT='" + $n + "' type=" + $e.Current.ControlType.ProgrammaticName)
    $found = $true
  }
}
# 也打印所有含 2026 的元素帮助诊断
foreach ($e in $els) {
  $n = $e.Current.Name
  if ($n -like '*2026*') { Write-Output ("ELEM2026 '" + $n + "' " + $e.Current.ControlType.ProgrammaticName) }
}
if ($found) { Write-Output "QUICKADD_DATE=2026-08-21 PASS" } else { Write-Output "QUICKADD_DATE=NOT_FOUND" }
