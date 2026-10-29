# The frozen interface is the coverage authority. Every operation must receive an
# explicit implementation assignment; a new interface member fails regeneration
# rather than silently being assigned to a default unsupported route.
$ErrorActionPreference = 'Stop'
$implementationRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$baseline = Get-Content -LiteralPath (Join-Path $implementationRoot 'baseline/CurrentBackend.json') -Raw | ConvertFrom-Json
$groups = [ordered]@{
    'runtime' = @('InjectDevelopmentOptionWorkerFaultAsync', 'ResetMarketDataAsync', 'PrepareSessionAsync', 'GetBackendStatusAsync', 'RecoverMarketDataAsync', 'GetFuturesMarketHealth', 'IsDatabentoFeedUp', 'GetRuntimeStatus', 'StartAsync', 'StopAsync', 'GetBackendReadSnapshotAsync')
    'subscriptions' = @('AcquireMonitoredOptionAsync', 'ReleaseMonitoredOptionAsync', 'RepairOptionObservationsAsync', 'StartStreamingFuturesTickDataAsync', 'StartStreamingFuturesOptionTickDataAsync', 'StartStreamingFuturesOptionChainDataAsync', 'RenewSubscriptionLeaseAsync', 'ReleaseSubscriptionLeaseAsync', 'AcquireSelectedSubscriptionLeasesAsync', 'GetSubscriptionLeasesAsync', 'IsTickDataStreamActive', 'StopStreamingFuturesTickDataAsync', 'StopStreamingFuturesOptionTickDataAsync')
    'catalog' = @('RefreshInstrumentDefinitionsAsync', 'TryGetMarketInstrumentId', 'TryGetOnTheRunFuturesContract', 'TryGetFuturesTermStructureContracts', 'UpdateFuturesTermStructureContractsAsync', 'UpdateOnTheRunFuturesContractAsync', 'GetFuturesContractAsync', 'GetFuturesContractsAsync', 'GetFuturesOptionContractAsync', 'GetFuturesOptionContractsAsync', 'GetFuturesOptionChainContractsAsync', 'GetFuturesOptionChainContractsBySymbolAsync', 'DiscoverOptionContractExpiriesAsync', 'GetFuturesOptionChainContractsByRootAsync')
    'latest-and-aggregation' = @('ReadFiveMinuteSeedBarsAsync', 'TryGetLastTickPrice', 'TryGetLastOptionTickPrice', 'TryGetFuturesSessionStatistics', 'GetFuturesPriceAsync', 'GetFuturesOptionPriceAsync', 'GetFuturesLastPriceReader', 'GetFuturesOptionLastPriceReader')
    'pricing-and-option-chains' = @('EvaluateOptionChainAsync', 'ReleaseCompositionAsync', 'AcquireOptionScopeAsync', 'ReleaseOptionScopeAsync', 'CaptureCompositionAsync', 'CaptureOptionRiskAsync', 'PrepareCompositionAsync', 'PrepareCachedCompositionAsync')
    'reference' = @('WarmTreasuryPricingAsync', 'ReadTreasuryCurveAsync', 'ReadTreasuryCurvesAsync', 'ReadEconomicCalendarAsync', 'RefreshOptionCalendarAsync')
    'historical' = @('EstimateHistoricalAsync', 'LoadHistoricalAsync')
    'durable-application' = @('ReconcileCommittedSubscriptionsAsync', 'CloseTradeFeedRecoveryAsync', 'ReadTradeFeedRecoveryAsync', 'LoadTradeFeedRecoveryAsync', 'EnableTradeFeedRecoveryAsync', 'DisableTradeFeedRecoveryAsync', 'GetTradeStrategySymbolsAsync')
}
$assignment = @{}
foreach ($group in $groups.Keys) {
    foreach ($operation in $groups[$group]) {
        if ($assignment.ContainsKey($operation)) { throw "Duplicate assignment: $operation" }
        $assignment[$operation] = $group
    }
}
$methods = foreach ($method in $baseline.apiMethods) {
    if (!$assignment.ContainsKey($method.name)) { throw "Unassigned interface operation: $($method.name)" }
    $group = $assignment[$method.name]
    [ordered]@{
        name = $method.name
        signature = $method.signature
        source = $method.source
        responsibility = $group
        nativeOwner = $(if ($group -eq 'durable-application') { 'Runtime realization only; retain durable application authority' } else { $group })
        managedOwner = 'Existing contracts, application persistence/policy, event mapping and caller adaptation'
        acceptance = 'Exact current C# results/events under identical inputs; explained approved differences only'
        implemented = $true
        qualified = $false
        implementationEvidence = 'qualification/FacadeContract; qualification/HostCompositionBridge; managed/Api; host/Api'
        qualificationScope = 'Component evidence in docs/IMPLEMENTATION-STATUS.md; complete identical-input Host/actor acceptance still required'
    }
}
$known = @($baseline.apiMethods.name | Sort-Object -Unique)
foreach ($operation in $assignment.Keys) { if ($operation -notin $known) { throw "Assignment is not an interface member: $operation" } }
$result = [ordered]@{
    schema = 1
    baselineCommit = $baseline.commit
    note = 'Implemented means an explicit facade route and concrete native/application owner exist. Qualified means full identical-input operation acceptance, which is distinct from component evidence and is not yet claimed.'
    methods = @($methods)
    properties = @(
        [ordered]@{ name = 'OptionChainCache'; responsibility = 'option_chain'; implemented = $true; qualified = $false },
        [ordered]@{ name = 'CoreFuturesRoutesAreRuntimeOwned'; responsibility = 'runtime'; implemented = $true; qualified = $false }
    )
}
$path = Join-Path $implementationRoot 'docs/OperationMatrix.json'
[System.IO.File]::WriteAllText($path, ($result | ConvertTo-Json -Depth 8) + "`n", [System.Text.UTF8Encoding]::new($false))
Write-Output "Explicit concrete assignments: $($methods.Count) method signatures; 2 properties. Complete operation acceptance is recorded separately from component qualification."
