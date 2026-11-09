[CmdletBinding()]
param(
    [ValidateRange(1,120)][int]$Minutes = 30,
    [string]$ResultsDirectory = 'E:\IFM\RedisRemoval\20261108\development-soak',
    [switch]$Resume
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$start = [DateTimeOffset]::UtcNow
$deadline = $start.AddMinutes($Minutes)
$samples = @()
if ($Resume) {
    $samples = @(Get-Content (Join-Path $ResultsDirectory 'samples.json') -Raw | ConvertFrom-Json)
    if (-not $samples.Count -or [DateTimeOffset]::UtcNow - [DateTimeOffset]$samples[-1].ObservedUtc -gt [TimeSpan]::FromMinutes(5)) {
        throw 'Cannot resume a stale Development observation.'
    }
    $start = [DateTimeOffset]$samples[0].ObservedUtc
    $deadline = $start.AddMinutes($Minutes)
}
do {
    $observed = [DateTimeOffset]::UtcNow
    $response = Invoke-WebRequest 'http://localhost:22543/health/ready' -TimeoutSec 15 -SkipHttpErrorCheck
    $api = $response.Content | ConvertFrom-Json
    $native = Invoke-RestMethod 'http://127.0.0.1:22544/health/ready' -TimeoutSec 15
    $redis = (& docker inspect redis --format '{{.State.Running}}').Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot verify Development Redis state.' }
    $owned = @(& (Join-Path $PSScriptRoot '../Development/Get-IFMDevelopmentProcess.ps1') | Where-Object Ownership -eq 'Owned')
    foreach ($role in @('manager','api','market-data','scheduler','ui')) {
        if (@($owned | Where-Object Role -eq $role).Count -ne 1) { throw "Expected exactly one Development $role process." }
    }
    $processes = @(Get-Process -ErrorAction SilentlyContinue | Where-Object ProcessName -in @(
        'IFMServerManager','TomasAI.IFM.Application.Api.Server',
        'TomasAI.IFM.Application.MarketData.Host','TomasAI.IFM.UI.Rust.eGui'))
    $sample = [pscustomobject]@{
        ObservedUtc=$observed; ApiHttp=$response.StatusCode; Api=$api.status; Native=$native.status
        RedisRunning=$redis; Checks=$api.entries.live_pipeline.data.pipeline.checks
        Roles=@($owned | Select-Object Role,ProcessId,StartedAtUtc)
        Processes=@($processes | Select-Object Id,ProcessName,WorkingSet64,PrivateMemorySize64,CPU)
    }
    $samples += $sample
    $samples | ConvertTo-Json -Depth 15 | Set-Content (Join-Path $ResultsDirectory 'samples.json')
    $response.Content | Set-Content (Join-Path $ResultsDirectory 'latest-ready.json')
    if ($response.StatusCode -ne 200 -or $api.status -ne 'Healthy' -or $native.status -ne 'Ready' -or $redis -ne 'false') {
        throw "Redis-free Development qualification failed at $observed."
    }
    Write-Output "$observed API Healthy; Rust Host Ready; Redis stopped."
    if ([DateTimeOffset]::UtcNow -lt $deadline) { Start-Sleep -Seconds 30 }
} while ([DateTimeOffset]::UtcNow -lt $deadline)
[pscustomobject]@{StartedUtc=$start;FinishedUtc=[DateTimeOffset]::UtcNow;Samples=$samples.Count;Healthy=$true} |
    ConvertTo-Json | Set-Content (Join-Path $ResultsDirectory 'summary.json')
