[CmdletBinding()]
param([string]$Configuration = 'Debug', [string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot '.artifacts/market-data-host'
}
$hostOutput = [IO.Path]::GetFullPath($OutputDirectory)
$invoke = Join-Path $PSScriptRoot 'Invoke-DotNet.ps1'
& $invoke publish (Join-Path $repositoryRoot 'TomasAI.IFM.Application.MarketData.Host/TomasAI.IFM.Application.MarketData.Host.csproj') '-m:1' --configuration $Configuration --no-self-contained --output $hostOutput --nologo
if ($LASTEXITCODE -ne 0) { throw 'MarketData Host publish failed.' }
& $invoke publish (Join-Path $repositoryRoot 'TomasAI.IFM.Application.MarketData.Host.Worker/TomasAI.IFM.Application.MarketData.Host.Worker.csproj') '-m:1' --configuration $Configuration --no-self-contained --output (Join-Path $hostOutput 'Workers') --nologo
if ($LASTEXITCODE -ne 0) { throw 'MarketData Worker publish failed.' }
Write-Output "MarketData Host and named futures/options worker apphosts published to $hostOutput"
