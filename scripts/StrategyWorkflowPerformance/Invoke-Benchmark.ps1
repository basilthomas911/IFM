[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Label,
    [int]$Samples = 30,
    [int]$Warmups = 3,
    [string]$Scenarios = 'Daily/LongFuture,Weekly/ShortFuture,Monthly/BullCallDebit,Daily/BearPutDebit,Weekly/ShortBalancedIronCondor',
    [switch]$Trace,
    [string]$TestAssembly = 'TomasAI.IFM.Domain.Trade.IntegratedTests/bin/Release/net10.0/TomasAI.IFM.Domain.Trade.IntegratedTests.dll',
    [string]$OutputRoot = 'TestResults/strategy-workflow-performance'
)

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
if ($Label -notmatch '^[A-Za-z0-9_-]+$') { throw 'Label must contain only letters, digits, underscores or hyphens.' }
if ($Samples -lt 1 -or $Warmups -lt 0) { throw 'Samples must be positive and warmups non-negative.' }
$assemblyPath = (Resolve-Path -LiteralPath (Join-Path $workspace $TestAssembly)).Path
$outputPath = [IO.Path]::GetFullPath((Join-Path (Join-Path $workspace $OutputRoot) $Label))
if (-not $outputPath.StartsWith($workspace.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Benchmark output must remain within the workspace.'
}
if (Test-Path -LiteralPath $outputPath) { throw "Choose a new label; evidence already exists at $outputPath" }
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$settings = @{
    DOTNET_ENVIRONMENT = 'Development'
    ASPNETCORE_ENVIRONMENT = 'Development'
    IFM_FINANCIAL_TEST_NATS_URL = 'nats://127.0.0.1:14222'
    IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=event-source-test-db'
    IFM_WORKFLOW_BENCHMARK = '1'
    IFM_WORKFLOW_BENCHMARK_WARMUPS = [string]$Warmups
    IFM_WORKFLOW_BENCHMARK_SAMPLES = [string]$Samples
    IFM_WORKFLOW_BENCHMARK_SCENARIOS = $Scenarios
    IFM_WORKFLOW_BENCHMARK_MODE = $Label
    IFM_WORKFLOW_BENCHMARK_OUTPUT = (Join-Path $outputPath 'samples.jsonl')
    IFM_WORKFLOW_BENCHMARK_TRACE = $(if ($Trace) { '1' } else { '0' })
}
$previous = @{}
foreach ($name in $settings.Keys) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, $settings[$name], 'Process')
}
try {
    Push-Location $workspace
    $revision = git rev-parse HEAD
    $changes = git diff --stat
    $assemblies = @('TomasAI.IFM.Domain.Trade', 'TomasAI.IFM.Domain.Portfolio', 'TomasAI.IFM.Application.Storage', 'TomasAI.IFM.Domain.Trade.IntegratedTests') | ForEach-Object {
        $path = Join-Path (Split-Path -Parent $assemblyPath) ($_ + '.dll')
        [ordered]@{ Name = $_; Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    }
    [ordered]@{
        Label = $Label; StartedAtUtc = [DateTime]::UtcNow.ToString('O'); Commit = $revision
        BuildConfiguration = 'Release'; TestAssembly = $assemblyPath; Assemblies = @($assemblies)
        SamplesPerScenario = $Samples; WarmupsPerScenario = $Warmups; Scenarios = $Scenarios
        TraceEnabled = $Trace.IsPresent; Environment = 'Development'; Machine = [Environment]::MachineName
        OS = [Environment]::OSVersion.ToString(); Sdk = (dotnet --version)
        Nats = $settings.IFM_FINANCIAL_TEST_NATS_URL; EventDatabase = 'localhost:5432/event-source-test-db'
        DatabasePolicy = 'Fresh equivalent unique financial scopes; shared database retained and grows between batches.'
        WorkingChanges = @($changes)
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outputPath 'run.json') -Encoding UTF8
    # Windows PowerShell otherwise treats xUnit's stderr failure marker as a terminating
    # error and interrupts the runner before it can save the detailed TRX failure.
    $ErrorActionPreference = 'Continue'
    & dotnet vstest $assemblyPath '/TestCaseFilter:Category=WorkflowPerformanceBenchmark' "/Logger:trx;LogFileName=$Label.trx" "/ResultsDirectory:$outputPath" 2>&1 |
        Tee-Object -FilePath (Join-Path $outputPath 'run.log')
    $testExitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($testExitCode -ne 0) { throw "Benchmark failed with exit code $testExitCode. See $outputPath" }
    # VSTest can exit successfully when a stale assembly matches no tests.
    # Require complete evidence before declaring this batch successful.
    $samplePath = $settings.IFM_WORKFLOW_BENCHMARK_OUTPUT
    if (-not (Test-Path -LiteralPath $samplePath)) { throw "No benchmark samples were produced: $samplePath" }
    $rows = @(Get-Content -LiteralPath $samplePath | ForEach-Object { $_ | ConvertFrom-Json })
    $scenarioCount = @($Scenarios.Split(',') | Where-Object { $_.Trim().Length -gt 0 }).Count
    $expectedSamples = 1 + $scenarioCount * ($Warmups + $Samples)
    $samplesWritten = @($rows | Where-Object RecordType -eq 'sample')
    if (@($rows | Where-Object RecordType -eq 'metadata').Count -ne 1 -or
        @($rows | Where-Object RecordType -eq 'run_completed').Count -ne 1 -or
        @($rows | Where-Object RecordType -eq 'run_failed').Count -ne 0 -or
        $samplesWritten.Count -ne $expectedSamples -or
        @($samplesWritten | Where-Object { $_.Success -ne $true }).Count -ne 0) {
        throw "Incomplete benchmark evidence; expected $expectedSamples successful samples. See $samplePath"
    }
}
finally {
    Pop-Location
    foreach ($name in $settings.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
