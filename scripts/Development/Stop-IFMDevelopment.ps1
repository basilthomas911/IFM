[CmdletBinding()]
param(
    [int] $TimeoutSeconds = 20,
    [switch] $VerifyStopped
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$getProcessScript = Join-Path $PSScriptRoot "Get-IFMDevelopmentProcess.ps1"
$running = @(& $getProcessScript)
$unowned = @($running | Where-Object { $_.Ownership -ne "Owned" })
if ($unowned.Count -gt 0) {
    $details = $unowned | ForEach-Object {
        "{0} PID {1} ({2})" -f $_.Role, $_.ProcessId, $_.Ownership
    }
    $commands = $unowned | ForEach-Object { "Stop-Process -Id $($_.ProcessId)" }
    throw "Unowned or ambiguous IFM Development processes were found. No process was stopped: $($details -join ', '). After validating each process, stop it explicitly with: $($commands -join '; ')"
}

if ($running.Count -eq 0) {
    Write-Output "IFM Development application processes are stopped."
    return
}

$manager = $running | Where-Object Role -eq "manager" | Select-Object -First 1
if ($null -ne $manager) {
    try {
        $pipe = [IO.Pipes.NamedPipeClientStream]::new(
            ".",
            "IFM.ServerManager.Development.v1",
            [IO.Pipes.PipeDirection]::Out,
            [IO.Pipes.PipeOptions]::Asynchronous)
        try {
            $pipe.Connect(2000)
            $writer = [IO.StreamWriter]::new($pipe, [Text.UTF8Encoding]::new($false), 1024, $true)
            try {
                $writer.AutoFlush = $true
                $writer.WriteLine("shutdown")
            }
            finally {
                $writer.Dispose()
            }
        }
        finally {
            $pipe.Dispose()
        }
    }
    catch {
        Write-Warning "The Development control pipe was unavailable; validated owned processes will use bounded forced cleanup."
    }
}

$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    Start-Sleep -Milliseconds 200
    $remaining = @(& $getProcessScript | Where-Object Ownership -eq "Owned")
} while ($remaining.Count -gt 0 -and [DateTimeOffset]::UtcNow -lt $deadline)

foreach ($process in $remaining | Sort-Object @{ Expression = { if ($_.Role -eq "manager") { 1 } else { 0 } } }) {
    Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
}

if ($remaining.Count -gt 0) {
    Start-Sleep -Milliseconds 500
}

$final = @(& $getProcessScript)
if ($VerifyStopped -and $final.Count -gt 0) {
    $details = $final | ForEach-Object { "{0} PID {1}" -f $_.Role, $_.ProcessId }
    throw "IFM Development application processes remain: $($details -join ', ')."
}

if ($final.Count -eq 0) {
    Write-Output "IFM Development application processes are stopped."
}
else {
    $final
}
