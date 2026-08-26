param([int]$PidOfApp)
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class EW23 {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  public static List<long> Handles = new List<long>();
  public static List<string> Titles = new List<string>();
  public static void Run(int targetPid) {
    EnumWindows((h, l) => {
      uint p = 0;
      GetWindowThreadProcessId(h, out p);
      if (p == (uint)targetPid && IsWindowVisible(h)) {
        var sb = new StringBuilder(256);
        GetWindowText(h, sb, 256);
        Handles.Add(h.ToInt64());
        Titles.Add(sb.ToString());
      }
      return true;
    }, IntPtr.Zero);
  }
}
public class WI23 {
  [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
"@
Add-Type -AssemblyName System.Collections
[EW23]::Run($PidOfApp)
for ($i = 0; $i -lt [EW23]::Handles.Count; $i++) {
  $h = [IntPtr]::new([EW23]::Handles[$i])
  $title = [EW23]::Titles[$i]
  $enabled = [WI23]::IsWindowEnabled($h)
  $owner = [WI23]::GetWindow($h, 4)  # GW_OWNER
  $exStyle = [WI23]::GetWindowLong($h, -20)  # GWL_EXSTYLE
  $style = [WI23]::GetWindowLong($h, -16)  # GWL_STYLE
  Write-Output ("'" + $title + "' enabled=" + $enabled + " owner=" + $owner + " exStyle=0x" + $exStyle.ToString("X") + " style=0x" + $style.ToString("X"))
}
$fg = [WI23]::GetForegroundWindow()
Write-Output ("Foreground=" + $fg)
