[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][uri]$NatsUrl,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
if ($NatsUrl.Scheme -ne 'nats' -or $NatsUrl.Host -notin @('127.0.0.1', 'localhost') -or $NatsUrl.Port -eq 4222) {
    throw 'Supply an isolated local qualification broker, separate from the application broker on port 4222.'
}
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runRoot = Join-Path $repositoryRoot ('TestResults/risk-manager-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
$previousBroker = $env:IFM_FINANCIAL_TEST_NATS_URL
$previousRender = $env:IFM_FINANCIAL_UI_RENDER_DIR
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$checks = @(
    @('TomasAI.IFM.Domain.Trade.UnitTests', ''),
    @('TomasAI.IFM.Domain.Portfolio.UnitTests', ''),
    @('TomasAI.IFM.Domain.Portfolio.IntegrationTests', 'FullyQualifiedName~FundRiskTerminalIntegrationTests|FullyQualifiedName~CapacityReservationIntegrationTests|FullyQualifiedName~FundRiskAuthorizationIntegrationTests'),
    @('TomasAI.IFM.Domain.Trade.IntegratedTests', 'Category=RiskDeliveryRuntime|Category=PortfolioFinancialRuntime|Category=FullWorkflowRisk|Category=SuccessiveWorkflowStages|Category=RiskWorkflowTrace'),
    @('TomasAI.IFM.UI.Net.SystemTests', 'FullyQualifiedName~RiskHistoryUiTests|FullyQualifiedName~Retained_history_renders')
)
$summary = [System.Collections.Generic.List[object]]::new()
Push-Location $repositoryRoot
try {
    $env:IFM_FINANCIAL_TEST_NATS_URL = $NatsUrl.AbsoluteUri.TrimEnd('/')
    $env:IFM_FINANCIAL_UI_RENDER_DIR = Join-Path $runRoot 'rendered-ui'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $index = 0
    # These projects share test schemas. Never run their schema/fault qualification concurrently.
    foreach ($check in $checks) {
        $index++
        $output = Join-Path $runRoot ("$index-" + $check[0])
        $arguments = @('test', $check[0], '--no-restore', '-m:1', '-v:q', '--logger', 'trx', '--results-directory', $output)
        if ($NoBuild) { $arguments += '--no-build' }
        if ($check[1]) { $arguments += @('--filter', $check[1]) }
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) { throw "Qualification failed: $($check[0]); inspect $output" }
        $reports = @(Get-ChildItem -LiteralPath $output -Filter '*.trx')
        if ($reports.Count -ne 1) { throw "Expected one test report in $output." }
        [xml]$report = Get-Content -LiteralPath $reports[0].FullName
        $count = $report.TestRun.ResultSummary.Counters
        if ([int]$count.total -le 0 -or [int]$count.total -ne [int]$count.passed) { throw "Zero, failed or skipped gate tests in $output." }
        $summary.Add([pscustomobject]@{ Project = $check[0]; Filter = $check[1]; Passed = [int]$count.passed; Report = $reports[0].FullName })
    }
    & dotnet build TomasAI.IFM.UI.Net --no-restore -m:1 -v:q
    if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
    & dotnet build TomasAI.IFM.Application.Api.Server --no-restore -m:1 -v:q
    if ($LASTEXITCODE -ne 0) { throw 'API build failed.' }
    Push-Location (Join-Path $repositoryRoot 'TomasAI.IFM.Application.Api.Server')
    try {
        & dotnet 'bin/Debug/net10.0/TomasAI.IFM.Application.Api.Server.dll' --verify-startup-only *> (Join-Path $runRoot 'startup.log')
        if ($LASTEXITCODE -ne 0) { throw 'Development composition-root verification failed.' }
    }
    finally { Pop-Location }
    $summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runRoot 'summary.json') -Encoding UTF8
    $summary | Format-Table Project, Passed -AutoSize
    Write-Output "Gate evidence: $runRoot"
}
finally {
    Pop-Location
    $env:IFM_FINANCIAL_TEST_NATS_URL = $previousBroker
    $env:IFM_FINANCIAL_UI_RENDER_DIR = $previousRender
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
}
