Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName WindowsBase
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RM3 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
}
"@

$PidOfApp = 28960

function Get-Day([string]$pattern) {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $PidOfApp)
  $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
  foreach ($w in $wins) {
    if ($w.Current.Name -eq '' -and $w.Current.BoundingRectangle.Width -gt 900) {
      $days = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
      foreach ($d in $days) { if ($d.Current.Name -like $pattern) { return $d } }
    }
  }
  return $null
}

# Verify what is under the point first
$pt = New-Object System.Windows.Point(1098, 302)
$under = [System.Windows.Automation.AutomationElement]::FromPoint($pt)
Write-Output ("Under point: " + $under.Current.ControlType.ProgrammaticName + " '" + $under.Current.Name + "'")

$d = Get-Day '*Date = 2026/8/21*'
if ($null -eq $d) { Write-Output "FAIL no day"; exit 1 }
Write-Output ("BEFORE: " + $d.Current.Name.Substring(0, [Math]::Min(105, $d.Current.Name.Length)))

# Single click
[RM3]::SetCursorPos(1098, 302) | Out-Null
Start-Sleep -Milliseconds 300
[RM3]::mouse_event([RM3]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 100
[RM3]::mouse_event([RM3]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1000

$d2 = Get-Day '*Date = 2026/8/21*'
Write-Output ("AFTER: " + $d2.Current.Name.Substring(0, [Math]::Min(105, $d2.Current.Name.Length)))
$sel = $d2.Current.Name -match 'Date = 2026/8/21[^}]*IsSelected = True'
Write-Output ("SELECTED=" + $sel)
