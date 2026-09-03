$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$allowed = Join-Path $root 'TomasAI.IFM.Application.MarketData\DataBento\Resiliency\DatabentoLifecycleRuntime.cs'
$targets = @(
    Join-Path $root 'TomasAI.IFM.Application.Api.Server'
    Join-Path $root 'TomasAI.IFM.Application.MarketData'
    Join-Path $root 'TomasAI.IFM.Domain.MarketData.Feed'
)
$violations = Get-ChildItem -LiteralPath $targets -Recurse -Filter '*.cs' |
    Where-Object { $_.FullName -ne $allowed } |
    Select-String -Pattern '(marketDataApi|MarketDataApi)\.(StartAsync|StopAsync)\('
if ($violations) {
    $violations | ForEach-Object { Write-Error "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
    throw 'Only DatabentoLifecycleRuntime may invoke DatabentoMarketDataApi lifecycle mutations.'
}
Write-Output 'Databento lifecycle ownership gate passed.'
