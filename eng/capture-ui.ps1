<#
.SYNOPSIS
    Capture per-page screenshots of the cccalendar main window for UI review.

.DESCRIPTION
    Launches a real app instance (optionally with an isolated data directory so the
    machine's own data is untouched), switches pages through the left navigation via
    UI Automation, and writes one PNG per page into artifacts/ui-shots/.

    The repository had no screenshot tool at all (its .gitignore excludes /qa*.ps1),
    while UI_DESIGN.md section 9 requires accepting the UI at 1920x1080 and 1366x768,
    so this is the reusable entry point.

    NOTE: keep this file ASCII-only. Windows PowerShell 5.1 reads .ps1 as ANSI unless a
    BOM is present, so non-ASCII literals here corrupt the parser.

.PARAMETER Mode
    Theme written into the isolated settings.json: Light / Dark / System.

.PARAMETER Size
    Window size as WxH, for example 1366x768.

.PARAMETER Pages
    Navigation labels to capture. Defaults to all nine, in navigation order.

.PARAMETER Isolated
    Use artifacts/ui-shots/home as the data root (CCCALENDAR_HOME).

.EXAMPLE
    powershell -File eng\capture-ui.ps1 -Mode Dark -Size 1366x768 -Isolated
#>
[CmdletBinding()]
param(
    [ValidateSet('Light', 'Dark', 'System')]
    [string]$Mode = 'Light',

    [string]$Size = '',

    [object[]]$Pages = @(),

    [string]$OutputDirectory = '',

    [switch]$Isolated,

    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

<#
.SYNOPSIS
    Crop the unpainted (pure black) band PrintWindow leaves on the right/bottom edge.
#>
function RemoveUnpaintedEdge {
    param([System.Drawing.Bitmap]$Bitmap)

    $midY = [int]($Bitmap.Height / 2)
    $midX = [int]($Bitmap.Width / 2)

    $right = $Bitmap.Width - 1
    while ($right -gt 0) {
        $c = $Bitmap.GetPixel($right, $midY)
        if (-not ($c.R -eq 0 -and $c.G -eq 0 -and $c.B -eq 0)) { break }
        $right--
    }

    $bottom = $Bitmap.Height - 1
    while ($bottom -gt 0) {
        $c = $Bitmap.GetPixel($midX, $bottom)
        if (-not ($c.R -eq 0 -and $c.G -eq 0 -and $c.B -eq 0)) { break }
        $bottom--
    }

    $targetWidth = [Math]::Max(1, $right + 1)
    $targetHeight = [Math]::Max(1, $bottom + 1)
    if ($targetWidth -eq $Bitmap.Width -and $targetHeight -eq $Bitmap.Height) {
        return $Bitmap.Clone()
    }

    $rect = New-Object System.Drawing.Rectangle 0, 0, $targetWidth, $targetHeight
    return $Bitmap.Clone($rect, $Bitmap.PixelFormat)
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

# --- Win32 interop ------------------------------------------------------------
if (-not ('UiShot.Native' -as [type])) {
    Add-Type -Namespace UiShot -Name Native -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)]
public struct RECT { public int Left, Top, Right, Bottom; }

[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
[DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hwnd, int x, int y, int w, int h, bool repaint);
'@
}

# Navigation labels are passed on the command line (non-ASCII lives only here, never in this file).
$navLabels = @(
    ([char]0x4ECA + [char]0x5929),   # today
    ([char]0x65E5 + [char]0x5386),   # calendar
    ([char]0x9879 + [char]0x76EE),   # projects
    ([char]0x5F85 + [char]0x529E),   # todos
    ([char]0x8BB0 + [char]0x5F55),   # records
    ([char]0x52A9 + [char]0x7406),   # assistant
    ([char]0x7EDF + [char]0x8BA1),   # statistics
    ([char]0x5DE5 + [char]0x5177),   # tools
    ([char]0x8BBE + [char]0x7F6E)    # settings
)
$navSlugs = @('today', 'calendar', 'projects', 'todos', 'records', 'assistant', 'statistics', 'tools', 'settings')

# Normalise: PowerShell may pass a single space-joined string when an array is given
# positionally, so flatten and split before comparing labels.
$requested = @()
foreach ($entry in $Pages) {
    foreach ($piece in ([string]$entry -split '\s+')) {
        if (-not [string]::IsNullOrWhiteSpace($piece)) { $requested += $piece }
    }
}
if ($requested.Count -eq 0) { $requested = $navLabels }

$repoRoot = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $repoRoot 'src\CcCalendar.Desktop\bin\Debug\net10.0-windows10.0.22621.0\cccalendar.exe'

if (-not (Test-Path $exePath)) {
    throw "Executable not found: $exePath`nBuild first: dotnet build CcCalendar.sln -c Debug"
}

if (-not $SkipBuild) {
    Write-Host 'Building so screenshots reflect current source...' -ForegroundColor Cyan
    & dotnet build (Join-Path $repoRoot 'CcCalendar.sln') --no-restore -v:q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\ui-shots'
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

# --- Isolated data root -------------------------------------------------------
$childEnvironment = @{}
if ($Isolated) {
    $dataRoot = Join-Path $OutputDirectory 'home'
    New-Item -ItemType Directory -Force -Path (Join-Path $dataRoot 'data') | Out-Null
    $settingsPath = Join-Path $dataRoot 'settings.json'
    $settings = @{}
    if (Test-Path $settingsPath) {
        $existing = Get-Content $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($property in $existing.PSObject.Properties) {
            $settings[$property.Name] = $property.Value
        }
    }
    # The app reads the app-level key "theme" (lower-case); "themePreference" is legacy
    # and ignored. Set both so the tool keeps working if either name wins later.
    $settings['theme'] = $Mode
    $settings['themePreference'] = $Mode
    $settings | ConvertTo-Json -Depth 8 | Set-Content $settingsPath -Encoding UTF8
    $childEnvironment['CCCALENDAR_HOME'] = $dataRoot
    Write-Host "Isolated data root: $dataRoot (theme $Mode)" -ForegroundColor DarkGray
}

# --- Launch ------------------------------------------------------------------
Write-Host 'Launching app...' -ForegroundColor Cyan
$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $exePath
$startInfo.UseShellExecute = $false
foreach ($kv in $childEnvironment.GetEnumerator()) {
    $startInfo.Environment[$kv.Key] = $kv.Value
}
$process = [System.Diagnostics.Process]::Start($startInfo)

try {
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 400
        $process.Refresh()
        if ($process.HasExited) { throw "App exited with code $($process.ExitCode)." }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
    }
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Timed out waiting for the main window.' }

    $sizeTag = 'default'
    if ($Size -match '^(\d+)\s*[xX]\s*(\d+)$') {
        $width = [int]$Matches[1]
        $height = [int]$Matches[2]
        $sizeTag = "$($width)x$($height)"
        [UiShot.Native]::MoveWindow($process.MainWindowHandle, 40, 40, $width, $height, $true) | Out-Null
        Write-Host "Window size set to $sizeTag" -ForegroundColor DarkGray
    }

    [UiShot.Native]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
    Start-Sleep -Seconds 3

$root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    if ($null -eq $root) { throw 'Could not reach the main window through UI Automation.' }

    
    $listItemCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)
    $navItems = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $listItemCondition)
    Write-Host "UI Automation found $($navItems.Count) list items." -ForegroundColor DarkGray
if ($VerbosePreference -ne 'SilentlyContinue') {
    $names = @()
    foreach ($item in $navItems) { $names += $item.Current.Name }
    Write-Host ("  names: " + ($names -join ' | ')) -ForegroundColor DarkGray
    Write-Host ("  requested: " + ($requested -join ' | ')) -ForegroundColor DarkGray
}

    $modeTag = $Mode.ToLowerInvariant()
    $captured = 0

    foreach ($page in $requested) {
        $target = $null
        foreach ($item in $navItems) {
            if ($item.Current.Name -eq $page) { $target = $item; break }
        }

        if ($null -eq $target) {
            Write-Warning "Navigation item not found, skipping: $page"
            continue
        }

        $slug = "page$captured"
        for ($i = 0; $i -lt $navLabels.Count; $i++) {
            if ([string]::Equals($navLabels[$i], $page, [StringComparison]::Ordinal)) {
                $slug = $navSlugs[$i]
                break
            }
        }

        $selection = $null
        if ($target.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selection)) {
            $selection.Select()
        }
        else {
            Write-Warning "Item has no SelectionItemPattern, skipping: $page"
            continue
        }

        Start-Sleep -Milliseconds 1300
        [UiShot.Native]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
        Start-Sleep -Milliseconds 250

        $rect = New-Object UiShot.Native+RECT
        [UiShot.Native]::GetWindowRect($process.MainWindowHandle, [ref]$rect) | Out-Null
        $w = $rect.Right - $rect.Left
        $h = $rect.Bottom - $rect.Top
        if ($w -le 0 -or $h -le 0) { throw 'Window rectangle is invalid.' }

        $bitmap = New-Object System.Drawing.Bitmap $w, $h
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $hdc = $graphics.GetHdc()
        # flag 2 = PW_RENDERFULLCONTENT, needed for DirectComposition content.
        [UiShot.Native]::PrintWindow($process.MainWindowHandle, $hdc, 2) | Out-Null
        $graphics.ReleaseHdc($hdc)
        $graphics.Dispose()

        # GetWindowRect includes the resize border, and on a DPI-scaled display
        # PrintWindow leaves an unpainted band along the right/bottom edge
        # (measured: 8px at 1366x768 and at 1920x1080). Crop it so the evidence
        # does not look like clipped content.
        $trimmed = RemoveUnpaintedEdge -Bitmap $bitmap
        $bitmap.Dispose()

        $file = Join-Path $OutputDirectory "$slug-$modeTag-$sizeTag.png"
        $trimmed.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
        $trimmed.Dispose()
        Write-Host "  captured $slug -> $(Split-Path -Leaf $file)" -ForegroundColor Green
        $captured++
    }

    Write-Host "`nDone: $captured screenshot(s) in $OutputDirectory" -ForegroundColor Cyan
}
finally {
    $process.Refresh()
    if (-not $process.HasExited) {
        # Closing the window only hides to tray, so terminate outright.
        $process.Kill()
        $process.WaitForExit(10000) | Out-Null
    }
}

