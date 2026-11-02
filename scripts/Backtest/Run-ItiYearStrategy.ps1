param(
    [string]$Python = 'C:/Users/basil/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
)
$ErrorActionPreference = 'Stop'
$source = '.artifacts/iti-year-nov2025-oct2026'
$output = 'outputs/iti-dc-first-reversal-no-stop-0945-1545-nov2025-oct2026'
# Preparation/build run independently; this driver starts only on completed proof.
while (-not (Test-Path -LiteralPath "$source/input-validation.json") -or
       -not ((Get-Content .artifacts/iti-year-daily-build.log -Raw) -match 'Build succeeded')) {
    Start-Sleep -Seconds 5
}
& "$PSScriptRoot/Run-ItiBacktest.ps1" -InputFile "$source/input.json" `
    -ArtifactDirectory .artifacts/iti-year-replay -BuildArtifactDirectory .artifacts/iti-backtest-year-build `
    -NoBuild -DailyComputeOnly
$runs = @(Get-ChildItem .artifacts/iti-year-replay -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'run.json') })
if ($runs.Count -ne 1) { throw 'Expected exactly one annual replay receipt.' }
$run = $runs[0].FullName
& $Python "$PSScriptRoot/analyze_dc_to_overshoot.py" --start-time 09:45 --policy hold `
    --exit-rule first-reversal --output-dir $output --run-dir $run --input-dir $source --workers 4
if ($LASTEXITCODE -ne 0) { throw 'Annual strategy analysis failed; evidence retained.' }
& $Python "$PSScriptRoot/verify_dc_two_band_stop.py" --output-dir $output
if ($LASTEXITCODE -ne 0) { throw 'Annual independent verification failed; evidence retained.' }
& $Python "$PSScriptRoot/verify_year_reconciliation.py" --output-dir $output
if ($LASTEXITCODE -ne 0) { throw 'Annual source/October reconciliation failed; evidence retained.' }
Write-Output "Annual strategy completed and verified: $output"
