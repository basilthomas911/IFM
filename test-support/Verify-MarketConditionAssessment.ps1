param([string]$EvidenceDirectory = (Join-Path $PSScriptRoot '..\.codex-mc-evidence'))
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repository
$previousEvidence = $env:IFM_MC_EVIDENCE_DIR
$previousDomain = $env:IFM_TEST_ACTOR_DOMAIN
$previousNats = $env:IFM_TEST_NATS_URL
function Test-Project([string]$Project, [string]$Filter) {
    & dotnet test "$Project/$Project.csproj" --no-restore --filter $Filter
    if ($LASTEXITCODE -ne 0) { throw "Qualification failed: $Project ($LASTEXITCODE)" }
}
try {
    # Requires PostgreSQL, Scylla, Redis and an isolated JetStream server on localhost:14222.
    # This runner neither starts a production feed nor enables production workflows.
    $env:IFM_MC_EVIDENCE_DIR = [IO.Path]::GetFullPath($EvidenceDirectory)
    Test-Project 'TomasAI.IFM.Domain.Trade.UnitTests' 'FullyQualifiedName~MarketCondition|FullyQualifiedName~RegimeDiscovery|FullyQualifiedName~IntrinsicTimeStrategyWorkflow|FullyQualifiedName~MarketAssessmentSelection'
    Test-Project 'TomasAI.IFM.Domain.Trade.BDDTests' 'FullyQualifiedName~MarketCondition|FullyQualifiedName~MarketAssessment'
    Test-Project 'TomasAI.IFM.Application.MarketData.UnitTests' 'FullyQualifiedName~DatasetWorkerCurrentValuesTests'
    Test-Project 'TomasAI.IFM.Application.Storage.IntegrationTests' 'FullyQualifiedName~MarketConditionAssessmentConfigurationTests'
    $env:IFM_TEST_ACTOR_DOMAIN = 'TomasAI.IFM.Domain.Trade,TomasAI.IFM.Domain.MarketData.Analytics'
    $env:IFM_TEST_NATS_URL = 'nats://127.0.0.1:14222'
    Test-Project 'TomasAI.IFM.Domain.Trade.IntegratedTests' 'FullyQualifiedName~IntrinsicTimeStrategyWorkflowRuntimeIntegrationTests'
    $env:IFM_TEST_ACTOR_DOMAIN = $previousDomain
    Test-Project 'TomasAI.IFM.Domain.MarketData.IntegrationTests' 'FullyQualifiedName~MarketConditionDownloadLogIntegrationTests'
    Test-Project 'TomasAI.IFM.Domain.Trade.VerificationTests' 'FullyQualifiedName~MarketAssessmentQualification'
    Test-Project 'TomasAI.IFM.UI.Net.Presentation.UnitTests' 'FullyQualifiedName~MarketAssessmentPresenter'
    Test-Project 'TomasAI.IFM.UI.Net.SystemTests' 'FullyQualifiedName~MarketAssessmentObservationTests'
    foreach ($project in @('TomasAI.IFM.Application.Api.Server', 'TomasAI.IFM.UI.Net')) {
        & dotnet build "$project/$project.csproj" --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project ($LASTEXITCODE)" }
    }
    & git -c core.safecrlf=false diff --check
    if ($LASTEXITCODE -ne 0) { throw 'Whitespace validation failed' }
} finally {
    $env:IFM_MC_EVIDENCE_DIR = $previousEvidence
    $env:IFM_TEST_ACTOR_DOMAIN = $previousDomain
    $env:IFM_TEST_NATS_URL = $previousNats
    Pop-Location
}
