param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

$ErrorActionPreference = 'Stop'
$resolvedExecutable = (Resolve-Path -LiteralPath $ExecutablePath).Path
$process = Start-Process -FilePath $resolvedExecutable -WindowStyle Hidden -PassThru

try {
    Start-Sleep -Seconds 2
    $process.Refresh()

    if ($process.HasExited) {
        throw "Published application exited during startup with code $($process.ExitCode)."
    }
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id
        $process.WaitForExit()
    }
}
