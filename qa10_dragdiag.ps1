Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RM4 {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
  public const uint MOVE = 0x0001;
}
public class WinRect4 {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$hwnd = [IntPtr]133728

function Get-Rect {
  $r = New-Object WinRect4+RECT
  [void][WinRect4]::GetWindowRect($hwnd, [ref]$r)
  return "$($r.Left),$($r.Top)"
}

$before = Get-Rect
Write-Output ("Window pos BEFORE: " + $before)

# Press on day cell 8/21 (1098,302), drag to (1198,302), release
[RM4]::SetCursorPos(1098, 302) | Out-Null
Start-Sleep -Milliseconds 300
[RM4]::mouse_event([RM4]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 150
[RM4]::SetCursorPos(1198, 302) | Out-Null
Start-Sleep -Milliseconds 200
[RM4]::SetCursorPos(1298, 302) | Out-Null
Start-Sleep -Milliseconds 200
[RM4]::mouse_event([RM4]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 800

$after = Get-Rect
Write-Output ("Window pos AFTER drag from day cell: " + $after)
if ($before -ne $after) { Write-Output "DRAGMOVE_FIRED: window moved => event bubbled to Window" } else { Write-Output "DRAGMOVE_NOT_FIRED: window did NOT move" }

# Also test drag from the header area (title text at ~700,30)
[RM4]::SetCursorPos(700, 30) | Out-Null
Start-Sleep -Milliseconds 300
[RM4]::mouse_event([RM4]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 150
[RM4]::SetCursorPos(800, 30) | Out-Null
Start-Sleep -Milliseconds 200
[RM4]::mouse_event([RM4]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 800
$after2 = Get-Rect
Write-Output ("Window pos AFTER drag from header: " + $after2)
if ($after -ne $after2) { Write-Output "HEADER_DRAGMOVE_FIRED" } else { Write-Output "HEADER_DRAGMOVE_NOT_FIRED" }
