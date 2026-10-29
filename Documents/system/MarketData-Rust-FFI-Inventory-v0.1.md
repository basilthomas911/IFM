# Rust MarketData FFI: source and API inventory

Date: 2026-10-25. Static snapshot of the current working tree, including uncommitted changes.

Companion: [design](MarketData-Rust-FFI-Lifecycle-Design-v0.1.md). Source paths below are relative to the repository root. This is a source inventory, not proof that every branch executes in the running Development profile. Generated/build outputs are excluded by `rg --files`; nested test utilities are identified separately. No runtime cutover was performed.

## Preservation rule for this inventory

Execution plan: [Rust MarketData API implementation](MarketData-Rust-FFI-Implementation-Plan-v0.1.md). All new Rust-backend implementation and its managed adapter belong under `TomasAI.IFM.Application.MarketData.Rust`. Inventory counts describe the existing baseline, not future files.

The agreed implementation adds a separate Rust FFI backend while keeping the current backend and existing `native.rust/DatabentoFeed.Rust` implementation unchanged. Per-file descriptions such as "port", "replace" or "retire" below identify the responsibility replaced or bypassed **within the new backend**, not authorization to edit/remove the baseline file. Copy/adapt existing native provider code into the new library with recorded provenance. Baseline worker executables, ABI/DLL and build paths remain available. Any baseline removal or shared-source consolidation requires a later explicit decision. Backend composition, new native packaging and backend-specific recovery adapters are the allowed integration changes; business consumers and existing public wire contracts stay compatible.

## 1. Scope counts

| Root | Source files |
|---|---:|
| `TomasAI.IFM.Framework.MarketData` | 10 |
| `TomasAI.IFM.Framework.MarketData.DataBento` | 185 |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep` | 18 |
| `TomasAI.IFM.Framework.MarketData.InteractiveBrokers` | 0 |
| `TomasAI.IFM.Application.MarketData` | 429 |
| `TomasAI.IFM.Application.MarketData.Host` | 35 |
| `TomasAI.IFM.Application.MarketData.Host.Worker` | 7 |
| `TomasAI.IFM.Application.BlackBoard` | 52 |
| `native/DatabentoFeed.Native` | 6 |
| `native.rust/DatabentoFeed.Rust` | 18 |
| `native.rust/OptionPricer.Rust` | 6 |
| `TomasAI.IFM.Shared.Contracts/MarketData` | 81 |
| `TomasAI.IFM.Shared.Contracts/OptionPricing` | 16 |
| `TomasAI.IFM.Framework.OptionPricer` | 20 |

API declarations: **71 methods including overloads**, plus `OptionChainCache` and `CoreFuturesRoutesAreRuntimeOwned` properties. Direct type-reference inventory: **113 production/source files**, **58 test/benchmark files**. Type-reference counts include implementations, contracts and composition, not just callers.

## 2. Complete IMarketDataApi operation inventory

| Source | Line | Signature |
|---|---:|---|
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 13 | `Task<bool> InjectDevelopmentOptionWorkerFaultAsync(MarketDataDevelopmentFaultRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 16 | `Task<Pricing.TreasuryPricingResult> WarmTreasuryPricingAsync(CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 20 | `Task<TomasAI.IFM.Shared.MarketData.Contracts.TreasuryCurveSnapshot?> ReadTreasuryCurveAsync(DateOnly asOfDate, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 24 | `Task<TomasAI.IFM.Shared.MarketData.Contracts.TreasuryCurveSnapshot[]> ReadTreasuryCurvesAsync(DateOnly fromInclusive, DateOnly toInclusive, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 28 | `Task<TomasAI.IFM.Shared.MarketData.Contracts.EconomicCalendarEntry[]> ReadEconomicCalendarAsync(DateOnly fromInclusive, DateOnly toInclusive, string[]? countryCodes, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 32 | `Task<InstrumentDefinitionSnapshot> RefreshInstrumentDefinitionsAsync(CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 36 | `Task<int> RefreshOptionCalendarAsync(string symbol, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 40 | `Task<TomasAI.IFM.Shared.EventSourcing.ServiceResult<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.EvaluatedOptionChainReadModel>> EvaluateOptionChainAsync( TomasAI.IFM.Domain.MarketData.Shared.Queries.GetEvaluatedOptionChainQuery query, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 44 | `Task ResetMarketDataAsync(DateOnly valueDate, Guid operationId, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 47 | `Task<Subscriptions.DurableRealization?> ReconcileCommittedSubscriptionsAsync(CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 50 | `Task<WorkerOptionChainResult> ReleaseCompositionAsync(string dataset, WorkerOptionChainRelease request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 53 | `Task<bool> AcquireMonitoredOptionAsync(TomasAI.IFM.Shared.MarketData.Contracts.Ticker.TickerStreamOwner owner, TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel contract, DateOnly valueDate, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 57 | `Task<bool> ReleaseMonitoredOptionAsync(TomasAI.IFM.Shared.MarketData.Contracts.Ticker.TickerStreamOwner owner, string contractId, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 61 | `Task<TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarReadModel[]> ReadFiveMinuteSeedBarsAsync(string contractId, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 64 | `Task<MarketDataReadSnapshot> GetBackendReadSnapshotAsync(CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 67 | `Task<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[]> PrepareSessionAsync(DateOnly valueDate, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 70 | `Task<MarketDataBackendStatus> GetBackendStatusAsync(CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 73 | `Task<MarketDataRecoveryResult> RecoverMarketDataAsync(MarketDataRecoveryRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 76 | `Task<OptionObservationRepairResult> RepairOptionObservationsAsync(OptionObservationRepairRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 79 | `Task<CompositionDiscoveryResult> AcquireOptionScopeAsync(CompositionDiscoveryRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 82 | `Task<WorkerOptionChainResult> ReleaseOptionScopeAsync(WorkerOptionChainRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 85 | `Task<CompositionSnapshotResult> CaptureCompositionAsync(string dataset, CompositionSnapshotRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 88 | `Task<MarketCompositionSnapshot?> CaptureOptionRiskAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 91 | `Task<CompositionPreparationResult> PrepareCompositionAsync(CompositionPreparationKey key, CompositionMarketDataPlan plan, string horizon, DateTimeOffset deadline, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 95 | `Task<CompositionPreparationResult> PrepareCachedCompositionAsync(CompositionPreparationKey key, OptionChainSnapshotRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 99 | `Task<MarketDataHistoricalEstimate> EstimateHistoricalAsync(MarketDataHistoricalRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | 102 | `Task<HistoricalDataLoaderState> LoadHistoricalAsync(MarketDataHistoricalRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.TradeFeedRecovery.cs` | 8 | `Task<bool> CloseTradeFeedRecoveryAsync(DateOnly date, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.TradeFeedRecovery.cs` | 11 | `Task<TradeFeedRecoveryRegistration[]> ReadTradeFeedRecoveryAsync(DateOnly date, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.TradeFeedRecovery.cs` | 14 | `Task<bool> LoadTradeFeedRecoveryAsync(string tradeId, DateOnly date, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.TradeFeedRecovery.cs` | 17 | `Task<bool> EnableTradeFeedRecoveryAsync(string tradeId, DateOnly date, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.TradeFeedRecovery.cs` | 20 | `Task<bool> DisableTradeFeedRecoveryAsync(string tradeId, DateOnly date, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 29 | `bool TryGetMarketInstrumentId(string contractId, out uint marketInstrumentId)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 36 | `FuturesMarketHealthSnapshot GetFuturesMarketHealth(string contractId)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 38 | `Task<TomasAI.IFM.Shared.EventSourcing.ServiceResult<TradeStrategySymbolReadModel[]>> GetTradeStrategySymbolsAsync( TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyType family, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 46 | `Task<SubscriptionLeaseResult> StartStreamingFuturesTickDataAsync( SubscriptionAcquireRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 54 | `Task<bool> StartStreamingFuturesTickDataAsync( string futuresContractId, TickerStreamOwner? owner = null)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 59 | `Task<SubscriptionLeaseResult> StartStreamingFuturesOptionTickDataAsync( SubscriptionAcquireRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 67 | `Task<bool> StartStreamingFuturesOptionTickDataAsync( string futuresOptionContractId, TickerStreamOwner? owner = null)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 72 | `Task<SubscriptionLeaseResult> StartStreamingFuturesOptionChainDataAsync( SubscriptionAcquireRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 80 | `Task<SubscriptionLeaseResult> RenewSubscriptionLeaseAsync( SubscriptionRenewRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 88 | `Task<SubscriptionLeaseResult> ReleaseSubscriptionLeaseAsync( SubscriptionReleaseRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 96 | `Task<SubscriptionLeaseResult> AcquireSelectedSubscriptionLeasesAsync( SubscriptionAcquireBatchRequest request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 104 | `Task<SubscriptionLeaseResult> GetSubscriptionLeasesAsync( SubscriptionOwnerQuery request, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 117 | `bool IsDatabentoFeedUp(TimeSpan? timeout = null)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 123 | `MarketDataFeedRuntimeStatusReadModel GetRuntimeStatus()` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 138 | `bool TryGetOnTheRunFuturesContract( string symbol, out FuturesContractV3ReadModel contract)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 143 | `bool TryGetFuturesTermStructureContracts( string symbol, out FuturesTermStructureContracts contracts)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 155 | `Task<bool> UpdateFuturesTermStructureContractsAsync( string symbol, DateOnly valueDate, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 170 | `Task<bool> UpdateOnTheRunFuturesContractAsync( string symbol, DateOnly valueDate, CancellationToken cancellationToken = default, bool forceProviderRefresh = false)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 179 | `bool TryGetLastTickPrice( string contractId, out FuturesMarketPriceSnapshot snapshot)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 186 | `bool TryGetLastOptionTickPrice( string contractId, out OptionTickerPriceSnapshot snapshot)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 191 | `bool TryGetFuturesSessionStatistics( string contractId, out FuturesSessionStatisticsSnapshot snapshot)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 203 | `bool IsTickDataStreamActive(string contractId)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 205 | `Task StartAsync( DateOnly valueDate, Func<Guid, int, string, Task>? errorMessageHandler = null, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 209 | `Task StopAsync(DateOnly valueDate)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 211 | `Task<FuturesContractV3ReadModel?> GetFuturesContractAsync( string futuresContractId)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 214 | `Task<FuturesContractV3ReadModel[]> GetFuturesContractsAsync( string[] futuresContractIds)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 217 | `Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync( string futuresOptionContractId)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 220 | `Task<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync( string[] futuresOptionContractIds)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 235 | `Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsAsync( string futuresContractId, DateOnly maturityDate)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 240 | `Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsBySymbolAsync( string underlyingSymbol, DateOnly fromMaturityDate, DateOnly throughMaturityDate)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 246 | `Task<OptionContractExpiryReadModel[]> DiscoverOptionContractExpiriesAsync( string underlyingSymbol, DateOnly fromExpiry, DateOnly throughExpiry, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 253 | `Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsByRootAsync( string underlyingSymbol, string providerRoot, DateOnly maturityDate)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 259 | `Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsByRootAsync( string underlyingSymbol, string providerRoot, DateOnly fromMaturityDate, DateOnly throughMaturityDate, CancellationToken cancellationToken = default)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 264 | `Task<decimal?> GetFuturesPriceAsync( string futuresContractId)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 267 | `Task<decimal?> GetFuturesOptionPriceAsync( string futuresOptionContractId)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 274 | `IFuturesLastPriceReader GetFuturesLastPriceReader( string futuresContractId)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 281 | `IFuturesOptionLastPriceReader GetFuturesOptionLastPriceReader( string futuresOptionContractId)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 284 | `Task<bool> StopStreamingFuturesTickDataAsync( string futuresContractId, TickerStreamOwner? owner = null)` |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | 288 | `Task<bool> StopStreamingFuturesOptionTickDataAsync( string futuresOptionContractId, TickerStreamOwner? owner = null)` |

## 3. All direct IMarketDataApi production/source references

Each entry includes every non-comment line referencing the interface in that file. Partial classes and inferred variables mean this is not a complete semantic call graph; section 4 also indexes operation-name references outside the implementation.

- `TomasAI.IFM.Application.Api.Nats.Client/TradeFeedRecoveryApi.cs` â€” lines 7.
- `TomasAI.IFM.Application.Api.Server.Core/Actors/Recovery/OptionPipelineRecoveryRuntime.cs` â€” lines 11.
- `TomasAI.IFM.Application.Api.Server.Core/DependencyInjection/MarketData/RegisterRemoteMarketDataRuntimeServices.cs` â€” lines 174, 185.
- `TomasAI.IFM.Application.Api.Server.Core/Development/Verification/FourHourDatabentoSeedReplay.cs` â€” lines 5.
- `TomasAI.IFM.Application.Api.Server.Core/Development/Verification/OptionRecoveryLiveVerificationService.cs` â€” lines 16, 106.
- `TomasAI.IFM.Application.Api.Server.Core/Development/Verification/RemoteLivePipelineProbe.cs` â€” lines 9.
- `TomasAI.IFM.Application.Api.Server.Core/HostedServices/ApiRecoveryRestartEscalation.cs` â€” lines 11.
- `TomasAI.IFM.Application.Api.Server.Core/HostedServices/OptionFeedRecoveryNotificationService.cs` â€” lines 14.
- `TomasAI.IFM.Application.Api.Server.Core/HostedServices/TradeFeedRecoveryStartupService.cs` â€” lines 14.
- `TomasAI.IFM.Application.Api.Server.Core/MarketData/Initialization/HistoricalDailyAnalyticsInitializationService.cs` â€” lines 26.
- `TomasAI.IFM.Application.Api.Server.Core/MarketData/OptionChains/OptionContractExpiryCalendarRefreshService.cs` â€” lines 59.
- `TomasAI.IFM.Application.Api.Server.Core/MarketData/Rollover/FuturesRolloverPreparationHostedService.cs` â€” lines 14.
- `TomasAI.IFM.Application.Api.Server.Core/MarketData/Sessions/MarketCloseOperations.cs` â€” lines 33, 38.
- `TomasAI.IFM.Application.Api.Server.Core/MarketData/Treasury/UsTreasuryRefreshHostedService.cs` â€” lines 12.
- `TomasAI.IFM.Application.Api.Server.Core/Observability/HealthChecks/MarketDataRuntimeHealthCheck.cs` â€” lines 15.
- `TomasAI.IFM.Application.Api.Server.Core/Startup/Application/ApiApplicationStartupActivities.cs` â€” lines 44.
- `TomasAI.IFM.Application.MarketData.Host/Api/MarketDataApiBackend.cs` â€” lines 142.
- `TomasAI.IFM.Application.MarketData.Host/Hosting/BackendServices.cs` â€” lines 191.
- `TomasAI.IFM.Application.MarketData.Host/OptionChains/HostOptionChainContext.cs` â€” lines 7, 12.
- `TomasAI.IFM.Application.MarketData.Host/OptionChains/OptionContractExpiryCalendarRefreshService.cs` â€” lines 60.
- `TomasAI.IFM.Application.MarketData.Host/OptionChains/QualifiedEvaluatedOptionChain.cs` â€” lines 116.
- `TomasAI.IFM.Application.MarketData.Host/OptionChains/StoredOptionUniverseSource.cs` â€” lines 14.
- `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` â€” lines 10.
- `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.TradeFeedRecovery.cs` â€” lines 5.
- `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` â€” lines 20.
- `TomasAI.IFM.Application.MarketData/DataBento/DatabentoMarketDataApi.cs` â€” lines 18.
- `TomasAI.IFM.Application.MarketData/DataBento/FuturesContractRolloverStartupCheck.cs` â€” lines 13.
- `TomasAI.IFM.Application.MarketData/DataBento/MarketDataServiceCollectionExtensions.cs` â€” lines 45.
- `TomasAI.IFM.Application.MarketData/Historical/HistoricalDataLoader.cs` â€” lines 15, 23.
- `TomasAI.IFM.Application.MarketData/OperationsHealth/MarketDataBackendHealthObserver.cs` â€” lines 10.
- `TomasAI.IFM.Application.MarketData/Remote/MarketDataApiCall.cs` â€” lines 11, 13, 19.
- `TomasAI.IFM.Application.MarketData/Remote/MarketDataApiClient.cs` â€” lines 14, 113.
- `TomasAI.IFM.Application.MarketData/Remote/MarketDataClientReceiver.cs` â€” lines 17.
- `TomasAI.IFM.Application.MarketData/Remote/MarketDataReadMirror.cs` â€” lines 15.
- `TomasAI.IFM.Application.MarketData/Remote/RemoteReferenceDataApi.cs` â€” lines 5.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesAdxSignal/Event/Actor/FuturesAdxSignalEventContext.cs` â€” lines 24, 40.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesAdxSignal/Event/Actor/IFuturesAdxSignalEventContext.cs` â€” lines 25.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesAdxSignal/Event/Extensions/FuturesAdxSignalEventContextExtensions.cs` â€” lines 33.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesAtrSignal/Event/Actor/FuturesAtrSignalEventContext.cs` â€” lines 24, 40.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesAtrSignal/Event/Actor/IFuturesAtrSignalEventContext.cs` â€” lines 25.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesAtrSignal/Event/Extensions/FuturesAtrSignalEventContextExtensions.cs` â€” lines 33.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesItiSignal/Realtime/Actor/FuturesItiSignalRealtimeContext.cs` â€” lines 18, 31.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesItiSignal/Realtime/Actor/IFuturesItiSignalRealtimeContext.cs` â€” lines 14.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesMacdSignal/Event/Actor/FuturesMacdSignalEventContext.cs` â€” lines 24, 40.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesMacdSignal/Event/Actor/IFuturesMacdSignalEventContext.cs` â€” lines 25.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesMacdSignal/Event/Extensions/FuturesMacdSignalEventContextExtensions.cs` â€” lines 33.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesRsiSignal/Event/Actor/FuturesRsiSignalEventContext.cs` â€” lines 23, 39.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesRsiSignal/Event/Actor/IFuturesRsiSignalEventContext.cs` â€” lines 22.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesRsiSignal/Event/Extensions/FuturesRsiSignalEventContextExtensions.cs` â€” lines 30.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesVwapSignal/Realtime/Actor/FuturesVwapSignalRealtimeContext.cs` â€” lines 18, 28.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesVwapSignal/Realtime/Actor/IFuturesVwapSignalRealtimeContext.cs` â€” lines 14.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesVxTermStructureSignal/Realtime/Actor/FuturesVxTermStructureSignalRealtimeContext.cs` â€” lines 17, 25.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesVxTermStructureSignal/Realtime/Actor/IFuturesVxTermStructureSignalRealtimeContext.cs` â€” lines 13.
- `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Realtime/Actor/IMarketOutlookSnapshotRealtimeContext.cs` â€” lines 33.
- `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Realtime/Actor/MarketOutlookSnapshotRealtimeContext.cs` â€” lines 29, 46.
- `TomasAI.IFM.Domain.MarketData.Feed/Command/Actor/MarketDataFeedCommandActor.cs` â€” lines 145.
- `TomasAI.IFM.Domain.MarketData.Feed/Event/Actor/IMarketDataFeedEventContext.cs` â€” lines 22.
- `TomasAI.IFM.Domain.MarketData.Feed/Event/Actor/MarketDataFeedEventContext.cs` â€” lines 22, 40.
- `TomasAI.IFM.Domain.MarketData.Feed/Event/MarketDataFeedEventParameters.cs` â€” lines 16, 23.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesBarData/Event/Actor/FuturesBarDataEventActor.cs` â€” lines 17.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesBarData/Event/Actor/FuturesBarDataEventContext.cs` â€” lines 10.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesBarData/Event/Actor/IFuturesBarDataEventContext.cs` â€” lines 10.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesBarData/Event/FuturesBarDataEventParameters.cs` â€” lines 4.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesEodData/Event/Actor/FuturesEodDataEventContext.cs` â€” lines 10.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesEodData/Event/Actor/IFuturesEodDataEventContext.cs` â€” lines 10.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesEodData/Realtime/Actor/FuturesEodDataRealtimeContext.cs` â€” lines 23, 45.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesEodData/Realtime/Actor/IFuturesEodDataRealtimeContext.cs` â€” lines 24.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesEodData/Realtime/FuturesTickTradeDataInserted.cs` â€” lines 36, 47, 59, 140, 159.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesEodData/Realtime/VxQuoteMarketPriceUpdated.cs` â€” lines 25.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Event/Actor/FuturesOptionTickDataEventActor.cs` â€” lines 15.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Event/Actor/FuturesOptionTickDataEventContext.cs` â€” lines 14.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Event/Actor/IFuturesOptionTickDataEventContext.cs` â€” lines 14.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Event/Actor/QualifiedIndividualOptionFeeds.cs` â€” lines 18.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Event/FuturesOptionTickDataEventParameters.cs` â€” lines 3.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Realtime/Actor/FuturesOptionTickDataRealtimeContext.cs` â€” lines 21, 35.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Realtime/Actor/IFuturesOptionTickDataRealtimeContext.cs` â€” lines 21.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Realtime/FuturesTickTradeDataInserted.cs` â€” lines 25.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesTickData/Event/Actor/FuturesTickDataEventActor.cs` â€” lines 16.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesTickData/Event/Actor/FuturesTickDataEventContext.cs` â€” lines 10.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesTickData/Event/Actor/IFuturesTickDataEventContext.cs` â€” lines 10.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesTickData/Event/FuturesTickDataEventParameters.cs` â€” lines 4.
- `TomasAI.IFM.Domain.MarketData.Feed/Query/Actor/IMarketDataFeedQueryContext.cs` â€” lines 9, 26.
- `TomasAI.IFM.Domain.MarketData.Feed/Query/Actor/MarketDataFeedQueryActor.cs` â€” lines 14.
- `TomasAI.IFM.Domain.MarketData.Feed/Query/Actor/MarketDataFeedQueryContext.cs` â€” lines 9, 25, 46.
- `TomasAI.IFM.Domain.MarketData.Feed/Query/Extensions/MarketDataFeedQueryApiExtensions.cs` â€” lines 13.
- `TomasAI.IFM.Domain.MarketData.Feed/Query/GetFuturesOptionContract.cs` â€” lines 3.
- `TomasAI.IFM.Domain.MarketData.Feed/Query/GetFuturesOptionSpreadData.cs` â€” lines 4.
- `TomasAI.IFM.Domain.MarketData.Feed/Query/MarketDataFeedQueryParameters.cs` â€” lines 4, 14, 21.
- `TomasAI.IFM.Domain.MarketData/EconomicCalendar/Event/Actor/EconomicCalendarEventContext.cs` â€” lines 14, 21.
- `TomasAI.IFM.Domain.MarketData/EconomicCalendar/Event/Actor/IEconomicCalendarEventContext.cs` â€” lines 16.
- `TomasAI.IFM.Domain.MarketData/EconomicCalendar/Event/EconomicCalendarsImported.cs` â€” lines 32.
- `TomasAI.IFM.Domain.MarketData/OptionChainCache/StoredOptionUniverseSource.cs` â€” lines 14.
- `TomasAI.IFM.Domain.MarketData/Query/Actor/IMarketDataQueryContext.cs` â€” lines 23.
- `TomasAI.IFM.Domain.MarketData/Query/Actor/MarketDataQueryContext.cs` â€” lines 22, 39.
- `TomasAI.IFM.Domain.MarketData/Query/UnderlyingLastTradePrice.cs` â€” lines 8.
- `TomasAI.IFM.Domain.MarketData/YieldCurveRate/Event/Actor/IYieldCurveRateEventContext.cs` â€” lines 16.
- `TomasAI.IFM.Domain.MarketData/YieldCurveRate/Event/Actor/YieldCurveRateEventContext.cs` â€” lines 14, 21.
- `TomasAI.IFM.Domain.MarketData/YieldCurveRate/Event/YieldCurveRatesImported.cs` â€” lines 31.
- `TomasAI.IFM.Domain.Reference/Query/Actor/IReferenceQueryContext.cs` â€” lines 10.
- `TomasAI.IFM.Domain.Reference/Query/Actor/ReferenceQueryContext.cs` â€” lines 21, 32.
- `TomasAI.IFM.Domain.Reference/StrategyCatalog/StrategyCatalogMigration.cs` â€” lines 9.
- `TomasAI.IFM.Domain.Reference/StrategyCatalog/StrategyCatalogReferenceAdapter.cs` â€” lines 8.
- `TomasAI.IFM.Domain.Reference/TradeStrategyFamilies/TradeStrategyFamilyCreationService.cs` â€” lines 10.
- `TomasAI.IFM.Domain.Trade/Futures/Option/Position/IronCondor/Plan/Model/IronCondorMonitoringInputReader.cs` â€” lines 20.
- `TomasAI.IFM.Domain.Trade/Futures/Option/Position/IronCondor/Plan/Model/VolatilityMarketPriceReader.cs` â€” lines 9.
- `TomasAI.IFM.Domain.Trade/Futures/Option/Position/IronCondor/Realtime/Actor/IronCondorTradePositionRealtimeContext.cs` â€” lines 15.
- `TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/Development/DevelopmentTradingPortfolioProvisioner.cs` â€” lines 35.
- `TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/MarketCondition/Model/MarketConditionAssessmentSnapshotProvider.cs` â€” lines 13.
- `TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/OrderComposer/Function/Actor/IOrderCompositionFunctionContext.cs` â€” lines 15.
- `TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/OrderComposer/Function/Actor/OrderCompositionFunctionContext.cs` â€” lines 25.
- `TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/OrderComposer/Model/CompositionDiscoveryHandoff.cs` â€” lines 10.
- `TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/Realtime/Actor/IIntrinsicTimeStrategyWorkflowRealtimeContext.cs` â€” lines 52.
- `TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/Realtime/Actor/IntrinsicTimeStrategyWorkflowRealtimeContext.cs` â€” lines 24.

## 4. External operation references

Lexical candidate call/reference sites outside the MarketData implementation. Common lifecycle names (`StartAsync`, `StopAsync`, `GetRuntimeStatus`) are restricted to interface-reference files to limit unrelated matches. Other names can still refer to another interface; verify actual receiver before editing. This index includes method groups, not only invocations.

- `TomasAI.IFM.Application.Api.Nats.Client/MarketDataFeedQueryApi.cs` â€” GetFuturesOptionContractAsync:164.
- `TomasAI.IFM.Application.Api.Nats.Client/MarketDataQueryApi.cs` â€” GetTradeStrategySymbolsAsync:37, GetFuturesContractAsync:71, GetFuturesOptionContractAsync:93, GetFuturesContractsAsync:104, GetFuturesOptionContractsAsync:129.
- `TomasAI.IFM.Application.Api.Nats.Client/ReferenceQueryApi.cs` â€” GetTradeStrategySymbolsAsync:37.
- `TomasAI.IFM.Application.Api.Nats.Client/TradeFeedRecoveryApi.cs` â€” LoadTradeFeedRecoveryAsync:10, EnableTradeFeedRecoveryAsync:12, DisableTradeFeedRecoveryAsync:14.
- `TomasAI.IFM.Application.Api.Server.Core/Actors/Recovery/OptionPipelineRecoveryRuntime.cs` â€” GetRuntimeStatus:41, GetBackendStatusAsync:109, GetBackendStatusAsync:119, RecoverMarketDataAsync:127, GetBackendStatusAsync:132, RepairOptionObservationsAsync:169, GetRuntimeStatus:195.
- `TomasAI.IFM.Application.Api.Server.Core/Development/Verification/FourHourDatabentoSeedReplay.cs` â€” ReadFiveMinuteSeedBarsAsync:8.
- `TomasAI.IFM.Application.Api.Server.Core/Development/Verification/LivePipelineProbe.cs` â€” TryGetOnTheRunFuturesContract:163, GetFuturesMarketHealth:177, TryGetOnTheRunFuturesContract:210, TryGetLastTickPrice:219, TryGetOnTheRunFuturesContract:265, TryGetLastTickPrice:266, TryGetOnTheRunFuturesContract:385, TryGetOnTheRunFuturesContract:405.
- `TomasAI.IFM.Application.Api.Server.Core/Development/Verification/OptionRecoveryLiveVerificationService.cs` â€” InjectDevelopmentOptionWorkerFaultAsync:116, GetBackendStatusAsync:123, GetBackendStatusAsync:156.
- `TomasAI.IFM.Application.Api.Server.Core/Development/Verification/RemoteLivePipelineProbe.cs` â€” GetBackendStatusAsync:17.
- `TomasAI.IFM.Application.Api.Server.Core/HostedServices/ApiRecoveryRestartEscalation.cs` â€” ReadTradeFeedRecoveryAsync:24.
- `TomasAI.IFM.Application.Api.Server.Core/HostedServices/OptionFeedRecoveryNotificationService.cs` â€” StartAsync:33, GetBackendStatusAsync:91, StopAsync:122, StopAsync:124, StopAsync:125.
- `TomasAI.IFM.Application.Api.Server.Core/HostedServices/TradeFeedRecoveryStartupService.cs` â€” GetBackendStatusAsync:43, ReadTradeFeedRecoveryAsync:45, LoadTradeFeedRecoveryAsync:51, ReadTradeFeedRecoveryAsync:52, StartAsync:88, StopAsync:104.
- `TomasAI.IFM.Application.Api.Server.Core/MarketData/Initialization/HistoricalDailyAnalyticsInitializationService.cs` â€” StartAsync:151, PrepareSessionAsync:162.
- `TomasAI.IFM.Application.Api.Server.Core/MarketData/InstrumentDefinitions/InstrumentDefinitionMaintenance.cs` â€” RefreshInstrumentDefinitionsAsync:14.
- `TomasAI.IFM.Application.Api.Server.Core/MarketData/OptionChains/OptionContractExpiryCalendarRefreshService.cs` â€” RefreshOptionCalendarAsync:62.
- `TomasAI.IFM.Application.Api.Server.Core/MarketData/Rollover/FuturesRolloverPreparationHostedService.cs` â€” PrepareSessionAsync:63.
- `TomasAI.IFM.Application.Api.Server.Core/MarketData/Sessions/MarketCloseOperations.cs` â€” CloseTradeFeedRecoveryAsync:87.
- `TomasAI.IFM.Application.Api.Server.Core/MarketData/Treasury/UsTreasuryRefreshHostedService.cs` â€” WarmTreasuryPricingAsync:34, WarmTreasuryPricingAsync:37.
- `TomasAI.IFM.Application.Api.Server.Core/MarketData/VxBootstrapPriceService.cs` â€” TryGetLastTickPrice:26.
- `TomasAI.IFM.Application.Api.Server.Core/Observability/HealthChecks/MarketDataRuntimeHealthCheck.cs` â€” IsDatabentoFeedUp:30, TryGetFuturesTermStructureContracts:177, TryGetOnTheRunFuturesContract:182, TryGetOnTheRunFuturesContract:232, IsTickDataStreamActive:245, TryGetFuturesSessionStatistics:276, GetFuturesMarketHealth:293, GetBackendStatusAsync:309, IsDatabentoFeedUp:312.
- `TomasAI.IFM.Application.Api.Server.Core/Startup/Application/ApiApplicationStartupActivities.cs` â€” PrepareSessionAsync:127, IsDatabentoFeedUp:152, ReadFiveMinuteSeedBarsAsync:427, ReadFiveMinuteSeedBarsAsync:521, IsDatabentoFeedUp:678, IsDatabentoFeedUp:752.
- `TomasAI.IFM.Application.Contracts.MarketData.Feed/ServiceApi/IMarketDataFeedQueryApi.cs` â€” GetFuturesOptionContractAsync:31.
- `TomasAI.IFM.Application.Contracts.MarketData/ServiceApi/IMarketDataQueryApi.cs` â€” GetTradeStrategySymbolsAsync:18, GetFuturesContractAsync:29, GetFuturesOptionContractAsync:31, GetFuturesContractsAsync:32, GetFuturesOptionContractsAsync:35.
- `TomasAI.IFM.Application.Contracts.Reference/Lookups/FundSelectionCatalog.cs` â€” GetTradeStrategySymbolsAsync:20, GetTradeStrategySymbolsAsync:21.
- `TomasAI.IFM.Application.Contracts.Reference/ServiceApi/IReferenceQueryApi.cs` â€” GetTradeStrategySymbolsAsync:34.
- `TomasAI.IFM.Application.Storage/MarketDataDb/MarketDataDbContext.cs` â€” GetFuturesOptionContractAsync:1334.
- `TomasAI.IFM.Application.Storage/SecuritiesDb/ISecuritiesDbReadContext.cs` â€” GetFuturesContractAsync:59, GetFuturesContractAsync:60, GetFuturesContractAsync:61, GetFuturesContractAsync:62, GetFuturesContractsAsync:63, GetFuturesContractsAsync:64, GetFuturesOptionContractAsync:69, GetFuturesOptionContractAsync:70, GetFuturesOptionContractsAsync:76, GetFuturesOptionContractsAsync:77, GetFuturesOptionContractsAsync:78, GetFuturesOptionContractsAsync:79.
- `TomasAI.IFM.Application.Storage/SecuritiesDb/SecuritiesDbContext.cs` â€” GetFuturesContractAsync:643, GetFuturesContractAsync:659, GetFuturesContractAsync:688, GetFuturesContractAsync:692, GetFuturesContractsAsync:700, GetFuturesContractsAsync:706, GetFuturesOptionContractAsync:773, GetFuturesOptionContractAsync:774, GetFuturesOptionContractAsync:780, GetFuturesOptionContractsAsync:811, GetFuturesOptionContractsAsync:823, GetFuturesOptionContractsAsync:846, GetFuturesOptionContractsAsync:852, GetFuturesContractAsync:949.
- `TomasAI.IFM.Application.Storage/SecuritiesDb/SecuritiesDbContextExtensions.cs` â€” GetFuturesContractAsync:259, GetFuturesOptionContractAsync:504, GetFuturesOptionContractAsync:1914, GetFuturesContractAsync:1929, GetFuturesContractAsync:1952.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesItiSignal/Realtime/FuturesMarketPriceUpdated.cs` â€” TryGetOnTheRunFuturesContract:48, TryGetOnTheRunFuturesContract:236, TryGetLastTickPrice:241.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesVwapSignal/Realtime/Actor/FuturesVwapSignalRealtimeActor.cs` â€” TryGetOnTheRunFuturesContract:58.
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesVxTermStructureSignal/Realtime/Actor/FuturesVxTermStructureSignalRealtimeActor.cs` â€” TryGetFuturesTermStructureContracts:65.
- `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Model/MarketOutlookVxTargetResolver.cs` â€” TryGetOnTheRunFuturesContract:28, TryGetOnTheRunFuturesContract:30.
- `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Realtime/FuturesMarketPriceUpdated.cs` â€” GetFuturesPriceAsync:76, TryGetFuturesSessionStatistics:82.
- `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Realtime/FuturesSessionStatisticsUpdated.cs` â€” GetFuturesPriceAsync:45.
- `TomasAI.IFM.Domain.MarketData.Feed/Command/Actor/MarketDataFeedCommandActor.cs` â€” StartAsync:59, StopAsync:62, PrepareSessionAsync:146.
- `TomasAI.IFM.Domain.MarketData.Feed/Event/Extensions/IEventActorContextExtensions.cs` â€” GetFuturesOptionContractAsync:127, GetFuturesContractAsync:142.
- `TomasAI.IFM.Domain.MarketData.Feed/Event/MarketDataFeedReset.cs` â€” ResetMarketDataAsync:41.
- `TomasAI.IFM.Domain.MarketData.Feed/Event/TradeLiveFeedAdded.cs` â€” GetFuturesContractAsync:47, GetFuturesOptionContractAsync:50.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesBarData/Event/FuturesBarDataStreamingStarted.cs` â€” TryGetLastTickPrice:65.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesEodData/Realtime/FuturesTickTradeDataInserted.cs` â€” IsTickDataStreamActive:75, GetFuturesContractAsync:81, TryGetFuturesSessionStatistics:144, TryGetFuturesSessionStatistics:175, TryGetFuturesSessionStatistics:195.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesEodData/Realtime/VxQuoteMarketPriceUpdated.cs` â€” IsTickDataStreamActive:38, GetFuturesContractAsync:44, TryGetFuturesSessionStatistics:59.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Command/EventProjector/FuturesOptionTickDataEventProjector.cs` â€” GetFuturesOptionContractAsync:36.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Event/Actor/FuturesOptionTickDataEventActor.cs` â€” StopStreamingFuturesOptionTickDataAsync:60.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Event/Actor/QualifiedIndividualOptionFeeds.cs` â€” AcquireMonitoredOptionAsync:22, ReleaseMonitoredOptionAsync:24.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Event/FuturesOptionTickDataStreamingStarted.cs` â€” GetFuturesOptionContractAsync:50, StartStreamingFuturesOptionTickDataAsync:52.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Event/FuturesOptionTickDataStreamingStopped.cs` â€” StopStreamingFuturesOptionTickDataAsync:44.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Realtime/FuturesTickTradeDataInserted.cs` â€” IsTickDataStreamActive:36, GetFuturesOptionContractAsync:41, TryGetLastOptionTickPrice:45.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesTickData/Event/Actor/FuturesTickDataEventActor.cs` â€” StopStreamingFuturesTickDataAsync:61.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesTickData/Event/FuturesTickDataStreamingStarted.cs` â€” IsTickDataStreamActive:49, StartStreamingFuturesTickDataAsync:59.
- `TomasAI.IFM.Domain.MarketData.Feed/FuturesTickData/Event/FuturesTickDataStreamingStopped.cs` â€” StopStreamingFuturesTickDataAsync:46.
- `TomasAI.IFM.Domain.MarketData.Feed/Query/Extensions/MarketDataFeedQueryApiExtensions.cs` â€” GetFuturesOptionContractAsync:394.
- `TomasAI.IFM.Domain.MarketData.Feed/Query/GetDatabentoReadiness.cs` â€” GetBackendStatusAsync:19.
- `TomasAI.IFM.Domain.MarketData.Feed/Query/GetFuturesOptionContract.cs` â€” GetFuturesOptionContractAsync:26.
- `TomasAI.IFM.Domain.MarketData.Feed/Query/GetFuturesOptionSpreadData.cs` â€” GetFuturesOptionContractsAsync:26, GetFuturesOptionLastPriceReader:31, GetFuturesOptionLastPriceReader:33.
- `TomasAI.IFM.Domain.MarketData.Securities/FuturesContract/Query/GetFuturesContract.cs` â€” GetFuturesContractAsync:28.
- `TomasAI.IFM.Domain.MarketData.Securities/FuturesContract/Query/GetFuturesContracts.cs` â€” GetFuturesContractsAsync:18.
- `TomasAI.IFM.Domain.MarketData.Securities/FuturesOptionContract/Command/Model/FuturesOptionContractDbContext.cs` â€” GetFuturesOptionContractAsync:12, GetFuturesOptionContractAsync:12, GetFuturesOptionContractsAsync:13, GetFuturesOptionContractsAsync:13, GetFuturesOptionContractsAsync:14, GetFuturesOptionContractsAsync:14.
- `TomasAI.IFM.Domain.MarketData.Securities/FuturesOptionContract/Query/GetFuturesOptionContract.cs` â€” GetFuturesOptionContractAsync:22.
- `TomasAI.IFM.Domain.MarketData.Securities/FuturesOptionContract/Query/GetFuturesOptionContracts.cs` â€” GetFuturesOptionContractsAsync:22.
- `TomasAI.IFM.Domain.MarketData/EconomicCalendar/Event/EconomicCalendarsImported.cs` â€” ReadEconomicCalendarAsync:57.
- `TomasAI.IFM.Domain.MarketData/OptionChainCache/StoredOptionUniverseSource.cs` â€” GetFuturesPriceAsync:36, GetFuturesPriceAsync:50.
- `TomasAI.IFM.Domain.MarketData/Query/Extensions/MarketDataQueryExtensions.Cancellation.cs` â€” GetFuturesContractAsync:30, GetFuturesContractAsync:35, GetFuturesContractAsync:42, GetFuturesOptionContractAsync:44, GetFuturesOptionContractAsync:49, GetFuturesContractsAsync:51, GetFuturesContractsAsync:55, GetFuturesOptionContractsAsync:57, GetFuturesOptionContractsAsync:62, GetFuturesContractAsync:175.
- `TomasAI.IFM.Domain.MarketData/Query/Extensions/MarketDataQueryExtensions.cs` â€” GetFuturesContractAsync:77, GetFuturesContractAsync:82, GetFuturesContractAsync:101, GetFuturesOptionContractAsync:115, GetFuturesOptionContractAsync:121, GetFuturesContractsAsync:136, GetFuturesContractsAsync:141, GetFuturesOptionContractsAsync:155, GetFuturesOptionContractsAsync:161, GetFuturesContractAsync:402.
- `TomasAI.IFM.Domain.MarketData/Query/GetTradeStrategySymbols.cs` â€” GetTradeStrategySymbolsAsync:18.
- `TomasAI.IFM.Domain.MarketData/Query/OptionChainWindowInputs.cs` â€” GetFuturesPriceAsync:26.
- `TomasAI.IFM.Domain.MarketData/Query/QualifiedEvaluatedOptionChain.cs` â€” EvaluateOptionChainAsync:24.
- `TomasAI.IFM.Domain.MarketData/Query/UnderlyingLastTradePrice.cs` â€” TryGetLastTickPrice:10.
- `TomasAI.IFM.Domain.MarketData/YieldCurveRate/Event/YieldCurveRatesImported.cs` â€” ReadTreasuryCurvesAsync:56.
- `TomasAI.IFM.Domain.Reference/Query/GetTradeStrategySymbols.cs` â€” GetTradeStrategySymbolsAsync:18.
- `TomasAI.IFM.Domain.Reference/StrategyCatalog/StrategyCatalogMigration.cs` â€” GetTradeStrategySymbolsAsync:23.
- `TomasAI.IFM.Domain.Reference/StrategyCatalog/StrategyCatalogReferenceAdapter.cs` â€” GetTradeStrategySymbolsAsync:15.
- `TomasAI.IFM.Domain.Reference/TradeStrategyFamilies/TradeStrategyFamilyCreationService.cs` â€” GetTradeStrategySymbolsAsync:36.
- `TomasAI.IFM.Domain.Trade/Futures/Option/Position/IronCondor/Plan/Model/IronCondorMonitoringInputReader.cs` â€” CaptureOptionRiskAsync:332.
- `TomasAI.IFM.Domain.Trade/Futures/Option/Position/IronCondor/Plan/Model/VolatilityMarketPriceReader.cs` â€” TryGetOnTheRunFuturesContract:13, TryGetLastTickPrice:14.
- `TomasAI.IFM.Domain.Trade/Order/Query/GetTradeOrderPreview.cs` â€” GetFuturesOptionContractAsync:29, GetFuturesContractAsync:31.
- `TomasAI.IFM.Domain.Trade/Order/Query/PrepareTradeOrder.cs` â€” GetFuturesContractAsync:34, GetFuturesOptionContractAsync:50.
- `TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/Development/DevelopmentTradingPortfolioProvisioner.cs` â€” GetTradeStrategySymbolsAsync:140, GetTradeStrategySymbolsAsync:141.
- `TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/MarketCondition/Model/MarketConditionAssessmentSnapshotProvider.cs` â€” TryGetOnTheRunFuturesContract:22, GetFuturesMarketHealth:23, TryGetLastTickPrice:26, TryGetOnTheRunFuturesContract:60, TryGetLastTickPrice:60, GetFuturesMarketHealth:95, TryGetOnTheRunFuturesContract:96.
- `TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/OrderComposer/Model/CompositionDiscoveryHandoff.cs` â€” ReconcileCommittedSubscriptionsAsync:30, ReleaseCompositionAsync:47.
- `TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/OrderComposer/Realtime/ExecuteOrderComposition.cs` â€” PrepareCachedCompositionAsync:96, PrepareCompositionAsync:117.
- `TomasAI.IFM.UI.Net.Services/MarketData/MarketDataQueryService.cs` â€” GetFuturesContractAsync:37, GetTradeStrategySymbolsAsync:40, GetTradeStrategySymbolsAsync:42, GetFuturesContractAsync:105, GetFuturesContractAsync:106, GetFuturesContractAsync:109, GetFuturesContractAsync:110, GetFuturesContractsAsync:116, GetFuturesOptionContractAsync:159, GetFuturesOptionContractAsync:160, GetFuturesOptionContractsAsync:179, GetFuturesOptionContractsAsync:180.
- `TomasAI.IFM.UI.Net.Services/MarketDataFeed/MarketDataFeedCommandService.cs` â€” LoadTradeFeedRecoveryAsync:35, EnableTradeFeedRecoveryAsync:37, DisableTradeFeedRecoveryAsync:39, StopStreamingFuturesTickDataAsync:99, StartStreamingFuturesOptionTickDataAsync:115, GetFuturesOptionContractAsync:122, GetFuturesOptionContractAsync:126, GetFuturesOptionContractAsync:176, StopStreamingFuturesOptionTickDataAsync:211.
- `TomasAI.IFM.UI.Net.ViewModels/App/IFMAppViewModel.cs` â€” StopStreamingFuturesTickDataAsync:1806.
- `TomasAI.IFM.UI.Net.ViewModels/MarketData/FuturesContractEditorViewModel.cs` â€” GetFuturesContractsAsync:252.
- `TomasAI.IFM.UI.Net.ViewModels/Trade/IronCondor/IronCondorTradeOrderViewModel.cs` â€” StartStreamingFuturesOptionTickDataAsync:984, StopStreamingFuturesOptionTickDataAsync:1169.
- `TomasAI.IFM.UI.Net.ViewModels/Trade/IronCondor/IronCondorViewModel.cs` â€” LoadTradeFeedRecoveryAsync:274, GetFuturesContractAsync:1188, GetFuturesContractAsync:1222, StartStreamingFuturesOptionTickDataAsync:1457, DisableTradeFeedRecoveryAsync:1666, EnableTradeFeedRecoveryAsync:1750, StopStreamingFuturesOptionTickDataAsync:1782, StopStreamingFuturesOptionTickDataAsync:1828.
- `TomasAI.IFM.UI.Net.Views/Reference/StrategyCatalogReferenceView.cs` â€” GetTradeStrategySymbolsAsync:75.
- `TomasAI.IFM.UI.Net.Views/Reference/TradeStrategyFamilyEditorForm.cs` â€” GetTradeStrategySymbolsAsync:157.
- `TomasAI.IFM.UI.Rust.Bridge.Net/ProviderDefinitionSession.cs` â€” GetFuturesContractAsync:81, GetFuturesOptionContractAsync:94, GetFuturesContractAsync:113, GetFuturesOptionContractAsync:125.
- `TomasAI.IFM.UI.Rust.Bridge.Net/StrategyCatalogSession.cs` â€” GetTradeStrategySymbolsAsync:38.
- `TomasAI.IFM.UI.Rust.Bridge.Net/TradeWorkspaceSession.cs` â€” GetFuturesContractsAsync:79.
- `tools/DevelopmentReferenceRestore/Program.cs` â€” GetFuturesContractsAsync:83, GetFuturesContractsAsync:118, GetFuturesOptionContractAsync:130.

## 5. Framework project-reference edges

- `TomasAI.IFM.Application.Actor.IntegrationTests/TomasAI.IFM.Application.Actor.IntegrationTests.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData.FinancialModelingPrep\TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.csproj" />`
- `TomasAI.IFM.Application.Api.Server.Core/TomasAI.IFM.Application.Api.Server.Core.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData.FinancialModelingPrep\TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.csproj" />`
- `TomasAI.IFM.Application.BlackBoard/TomasAI.IFM.Application.Blackboard.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData.DataBento\TomasAI.IFM.Framework.MarketData.DataBento.csproj" />`
- `TomasAI.IFM.Application.Contracts.Reference/TomasAI.IFM.Application.Contracts.Reference.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData\TomasAI.IFM.Framework.MarketData.csproj" />`
- `TomasAI.IFM.Application.MarketData.Host/TomasAI.IFM.Application.MarketData.Host.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData.FinancialModelingPrep\TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.csproj" />`
- `TomasAI.IFM.Application.MarketData/TomasAI.IFM.Application.MarketData.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData\TomasAI.IFM.Framework.MarketData.csproj" />`; `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData.DataBento\TomasAI.IFM.Framework.MarketData.DataBento.csproj" />`
- `TomasAI.IFM.Application.Storage/TomasAI.IFM.Application.Storage.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData\TomasAI.IFM.Framework.MarketData.csproj" />`
- `TomasAI.IFM.Domain.MarketData.Feed.Benchmarks/TomasAI.IFM.Domain.MarketData.Feed.Benchmarks.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData\TomasAI.IFM.Framework.MarketData.csproj" />`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData.DataBento\TomasAI.IFM.Framework.MarketData.DataBento.csproj" />`
- `TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks/TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData.DataBento\TomasAI.IFM.Framework.MarketData.DataBento.csproj" />`
- `TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests/TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData.DataBento\TomasAI.IFM.Framework.MarketData.DataBento.csproj" />`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData.DataBento\TomasAI.IFM.Framework.MarketData.DataBento.csproj" />`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/TomasAI.IFM.Framework.MarketData.DataBento.UnitTests.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData.DataBento\TomasAI.IFM.Framework.MarketData.DataBento.csproj" />`
- `TomasAI.IFM.Framework.MarketData.DataBento/TomasAI.IFM.Framework.MarketData.DataBento.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData\TomasAI.IFM.Framework.MarketData.csproj" />`
- `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests/TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData.FinancialModelingPrep\TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.csproj" />`
- `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData\TomasAI.IFM.Framework.MarketData.csproj" />`
- `TomasAI.IFM.Framework.MarketData.UnitTests/TomasAI.IFM.Framework.MarketData.UnitTests.csproj` â€” `<ProjectReference Include="..\TomasAI.IFM.Framework.MarketData\TomasAI.IFM.Framework.MarketData.csproj" />`

## 6. Complete source-file inventory and proposed disposition

Disposition is an architectural assignment, not permission to delete each file. DTOs, persistence models, compatibility adapters and numerical routines must be classified at symbol level during implementation. Nested native .NET comparison/benchmark projects are test infrastructure.

### TomasAI.IFM.Framework.MarketData

| Source file | Proposed disposition |
|---|---|
| `TomasAI.IFM.Framework.MarketData/OptionChainCache/OptionChainPublication.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData/OptionChainCache/OptionChainSnapshotStore.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData/ReferenceData/TreasuryRateConversion.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData/ReferenceData/UsTreasuryCurve.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData/TickAggregation/BoundedRealtimeTickPublisher.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData/TickAggregation/BoundedTickLiveEventPublisher.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData/TickAggregation/NullTickLiveEventPublisher.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData/TickAggregation/TickAggregationEventPublisher.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData/TickAggregation/TickLiveRouter.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData/TickAggregation/TickQuoteBufferPool.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |

### TomasAI.IFM.Framework.MarketData.DataBento

| Source file | Proposed disposition |
|---|---|
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/CpuAffinityMode.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/DatabentoFeedOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/DatabentoHistoricalProviderOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/DatabentoServiceCollectionExtensions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedCoreIsolationMode.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedCoreIsolationOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedCpuAffinityOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedDataSourceMode.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedDeploymentProfile.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedDrainOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedGcOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedMemoryOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedNumaOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedProcessorResidencyOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedProcessorSelectionKind.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedRingBackpressureOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedThreadPriority.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedThreadPriorityOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/FeedTransportHealthOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/LogicalProcessorLocation.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/NumaLocalityMode.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/SyntheticFeedOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/DatabentoContractMappingException.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/DatabentoFeedException.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/DatabentoFeedTimeoutException.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/FeedStopDrainIncompleteException.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Historical/DatabentoHistoricalProvider.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/DatabentoFeedStatus.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/DatabentoNativeLibraryResolver.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/FeedState.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/LatestPriceResult64.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/MarketDataKinds.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/MarketRecord64.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/MarketRecordHeader32.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/MarketRecordKind.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/MboRecord64.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeBatchResult.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeConstants.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeContractDetail.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeContractDetailFlags.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeContractQuery.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeContractQueryKind.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeFeedConfig.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeFeedStats.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeHistoricalBatch.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeHistoricalEstimate.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeHistoricalFlags.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeHistoricalRecord.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeHistoricalRecordKind.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeHistoricalRequest.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeHistoricalSchema.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeLatestPriceRequest.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeMethods.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeOptionChainSubscription.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeOptionContractSelection.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeStatus.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeTickerInstrumentMapping.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeTickerSubscription.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeUtf8Slice.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeWaitFlags.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeWaitResult.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeWatchdogFeedStatus.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeWatchdogSnapshot.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/QuoteRecord64.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/SafeContractDetailsResultHandle.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/SafeDbFeedHandle.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/SafeHistoricalResultHandle.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/StatisticsRecord64.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Interop/TradeRecord64.cs` | Replace with new versioned ABI; retain compatibility only during dual-backend validation. |
| `TomasAI.IFM.Framework.MarketData.DataBento/LastPrice/DatabentoLastPriceStore.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/LastPrice/IDatabentoLastPriceReaderProvider.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/LastPrice/IDatabentoLastPriceStore.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/LastPrice/IDatabentoLastPriceWriter.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/BoundedOptionChainTransientEventPublisher.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/DatabentoOptionChainRoute.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/DatabentoOptionChainSessionManager.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/DatabentoOptionChainSessionRequest.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/FuturesOptionChainQuoteChangedServiceEvent.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/FuturesOptionChainTradeChangedServiceEvent.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/IDatabentoOptionChainSessionManager.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/IOptionChainGreeksEnricher.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/IOptionChainStateStore.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/IOptionChainTransientEventPublisher.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/IOptionChainTransientEventSink.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/IRetainedOptionTradeEnricher.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/OptionChainContractState.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/OptionChainSessionKey.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/OptionChainStateStore.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Properties/AssemblyInfo.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/BatchPool.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/BoundedBatchChannel.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/CurrentProcessor.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoContractDetailsQueryResult.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoDiagnostics.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoFeedFactory.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoFeedMonitor.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoInputSymbology.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoInstrumentDefinitionClient.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoLatestPriceClient.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoMarketDataQueries.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoNativeFeedWatchdogStatus.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoNativeWatchdog.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoNativeWatchdogSnapshot.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoOperationRunner.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoQualificationGate.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoRecoveryException.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoRecoveryOrchestrator.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/DatabentoSyntheticQualificationProbe.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedGcCoordinator.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedHealthSnapshot.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedMetricSeverity.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedMetricsSnapshot.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedOptionsValidator.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedPlacementLease.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedQualificationBaseline.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedQualificationObservation.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedQualificationResult.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedReadinessState.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedRecoveryAttempt.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedRecoveryFaultKind.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedRecoveryResult.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedRecoverySchema.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/IDatabentoFeedAlertSink.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/IDatabentoFeedFactory.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/IDatabentoFeedMetricsExporter.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/IDatabentoLatestPriceClient.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/IDatabentoMarketDataQueries.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/IDatabentoOperationRunner.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/IDatabentoOptionChainFeed.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/IDatabentoRecoveryAttemptExecutor.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/IDatabentoRecoveryDelay.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/IDatabentoTickerFeed.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/IInstrumentDefinitionProvider.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/ILatestPriceAdmissionClock.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/IMultiplexedTickerBatchReader.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/ISynchronousBatchReader.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/InstrumentBatch64.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/LatestPriceAdmissionControl.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/LatestPriceFreshnessPolicy.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/LatestPricePolicy.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/LatestPriceQueryInvoker.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/LatestPriceRequest.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/LatestPriceResultFlags.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/LinuxThreadConfiguration.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/MarketDataBatch64.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/MboRecoveryBaseline.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/MonotonicDeadline.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/MultiplexedTickerBatchReader.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/OptionChainDefinitionFilter.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/OptionChainDefinitionRequest.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/OptionChainDefinitions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/OptionChainSubscription.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/OptionContractSelection.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/OptionUniversePolicy.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/ProcessCoreIsolationCoordinator.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/ProcessorCandidate.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/ProcessorCoreKind.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/ProcessorPairResolution.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/ProcessorTopology.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/SyntheticOptionChainFeed.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/SyntheticTickerFeed.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/SystemLatestPriceAdmissionClock.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/TickerInstrumentRegistration.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/TickerSubscription.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/TimestampReplayCursor.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/WindowsThreadAffinity.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/Contracts/DatabentoLiveFeedHealthPolicy.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/Contracts/DatabentoLiveFeedHealthState.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/Contracts/ITickAggregationMetricsSource.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/Contracts/ITickAggregationService.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/Contracts/ITickContractMappingProvider.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/Contracts/ITickContractMappingStore.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/Contracts/ITickValueDateProvider.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/Contracts/ITickerStreamRouteController.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/Contracts/TickAggregationContractStatus.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/Contracts/TickAggregationOptions.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/Contracts/TickAggregationTickerStatus.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/Contracts/TickContractMapping.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/Contracts/UtcTickValueDateProvider.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/DatabentoTickContractMappingStore.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/DatabentoTradeNormalizer.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/FuturesSessionAccumulator.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/FuturesVwapSourceAccumulator.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/TickAggregationLog.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |
| `TomasAI.IFM.Framework.MarketData.DataBento/TickAggregation/TickAggregationService.cs` | Port executable runtime to Rust; keep only managed public DTO/adaptor compatibility as needed. |

### TomasAI.IFM.Framework.MarketData.FinancialModelingPrep

| Source file | Proposed disposition |
|---|---|
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Configuration/FinancialModelingPrepOptions.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Configuration/FinancialModelingPrepServiceCollectionExtensions.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Exceptions/FinancialModelingPrepAuthenticationException.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Exceptions/FinancialModelingPrepConfigurationException.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Exceptions/FinancialModelingPrepContractException.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Exceptions/FinancialModelingPrepException.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Exceptions/FinancialModelingPrepRateLimitException.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Exceptions/FinancialModelingPrepResponseException.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Exceptions/FinancialModelingPrepResponseTooLargeException.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Exceptions/FinancialModelingPrepUnavailableException.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Exceptions/FinancialModelingPrepValidationException.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/FinancialModelingPrepEconomicCalendar.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/FinancialModelingPrepTreasuryCurve.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Internal/FinancialModelingPrepEconomicCalendarDto.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Internal/FinancialModelingPrepHttpClient.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Internal/FinancialModelingPrepProviderUtilities.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Internal/FinancialModelingPrepRequestGate.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |
| `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Internal/FinancialModelingPrepTreasuryRateDto.cs` | Port reference-data HTTP/parsing/calculation runtime; preserve managed contracts. |

### TomasAI.IFM.Framework.MarketData.InteractiveBrokers

| Source file | Proposed disposition |
|---|---|

### TomasAI.IFM.Application.MarketData

| Source file | Proposed disposition |
|---|---|
| `TomasAI.IFM.Application.MarketData/ApplicationDiagnosticLog.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/Contracts/FuturesContractRolloverConfigurationException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/FuturesMarketHealthSnapshot.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/FuturesTermStructureContracts.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/FuturesEodAnalyticsAssembly.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/HistoricalAcquisitionCheckpoint.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/HistoricalAcquisitionStage.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/HistoricalDataLoaderAudit.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/HistoricalDataLoaderGap.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/HistoricalDataLoaderRoll.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/HistoricalDataLoaderState.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/HistoricalDataLoaderStatus.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/IFuturesEodAnalyticsAssembler.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/IFuturesExchangeBusinessCalendar.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/IHistoricalAcquisitionCheckpointSink.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/IHistoricalAnalyticsSignalReader.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/IHistoricalDailyReplayPublisher.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/IHistoricalDataLoaderStore.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/IHistoricalObservationSink.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/IHistoricalObservationStore.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/IHistoricalReplayPublisher.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/IHistoricalSeriesRequestResolver.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/IMarketDataHistoricalApi.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/IMarketSessionCalendar.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/MarketAnalyticsSignalFamily.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/MarketAnalyticsSignalResult.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/MarketDataHistoricalEstimate.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/MarketDataHistoricalManifest.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/MarketDataHistoricalRequest.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/MarketDataHistoricalSeriesRequest.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/MarketSessionBounds.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/NormalizedHistoricalBatch.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/NormalizedHistoricalTrade.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/NullHistoricalAnalyticsSignalReader.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/NullHistoricalDailyReplayPublisher.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/Historical/NullHistoricalReplayPublisher.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/IInstrumentDefinitionStore.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.TradeFeedRecovery.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/IOptionChainCache.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/IOptionChainStatisticsStore.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/IReferenceDataApi.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/IStrategyOptionChainParameterStore.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/ITradeStrategySymbolCatalog.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/ITradeStrategySymbolSource.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/ITradeStrategySymbolStore.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/InstrumentDefinitionSelectionMapper.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/InstrumentDefinitionSnapshot.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/InvalidFuturesOptionQuoteException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/MarketDataApiAlreadyRunningException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/MarketDataApiException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/MarketDataApiNotRunningException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/MarketDataApiValueDateMismatchException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/MarketDataBatchResolutionException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/MarketDataCapacityExceededException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/MarketDataContractException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/MarketDataContractKindMismatchException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/MarketDataContractMappingException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/MarketDataContractNotFoundException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/MarketDataDevelopmentFaultRequest.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/MarketDataPricingInputUnavailableException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/MarketDataRouteConflictException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/OnTheRunFuturesContractNotFoundException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/OptionChainCacheScope.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/OptionChainCacheStatus.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/OptionChainConflictException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/OptionChainSnapshotOutcome.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/OptionChainSnapshotRequest.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/OptionChainSnapshotResult.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/StrategyMarketDataProfile.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionAcquireBatchRequest.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionAcquireRequest.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionAssetKind.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionChainKey.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionIdentity.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionLeasePurpose.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionLeaseResult.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionLeaseSelection.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionLeaseToken.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionLeaseView.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionOwnerKey.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionOwnerQuery.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionReleaseRequest.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionRenewRequest.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionResultCode.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionTarget.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/SubscriptionTickerKey.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/TickAggregationNotRunningException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/TradeStrategyProduct.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/UnavailableOptionChainCache.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/Contracts/UnderlyingTickerNotRunningException.cs` | Preserve C# API/DTO contract; supply explicit ABI mappings, no CLR object crossing. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoContractMetadata.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoContractRegistration.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoContractRegistrationRegistry.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoCurrentFuturesContractResolver.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoDatasetFeedHealth.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoDatasetSelection.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoMarketDataApi.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoMarketDataApiHealth.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoMarketDataApiOptions.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoMarketDataCatalog.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoMarketDataEpoch.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoMarketDataEpochFactory.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoMarketDataEpochHealth.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoMarketDataRuntimeOptions.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoOptionRouteRegistry.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoTickerStreamRouteController.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/DatabentoTradeStrategySymbolSource.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/FuturesContractRolloverStartupCheck.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/FuturesRolloverPreparationPolicy.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/Historical/CmeFuturesMarketSessionCalendar.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/Historical/ConfiguredHistoricalSeriesRequestResolver.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/Historical/DatabentoHistoricalApi.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/Historical/DatabentoHistoricalOptions.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/Historical/DatabentoHistoricalSeriesProfile.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/Historical/HistoricalBudgetExceededException.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/IDatabentoContractRegistrationRegistry.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/IDatabentoCurrentFuturesContractResolver.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/IDatabentoMarketDataCatalog.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/IDatabentoMarketDataEpoch.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/IDatabentoMarketDataEpochFactory.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/IFuturesContractRolloverStartupCheck.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/MarketDataServiceCollectionExtensions.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/OptionExpiryCalendarPolicy.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/ReferenceCountedTickAggregationEventPublisher.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/CompositeMarketDataOperationsRecorder.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoBulkWatchdogSnapshot.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoContractAuthority.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoContractRole.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoDatasetEvaluation.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoDatasetFailureReason.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoDatasetHealthEvaluator.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoDatasetResetRequest.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoDatasetResetResult.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoDatasetState.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoDisplayHealth.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoFeedCriticality.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoFeedWatchdogStatus.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoHardAttemptEvidence.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoHardRecoveryEngine.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoHardRecoveryOutcome.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoHardRecoveryPolicy.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoHardRecoveryRequest.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoHardRecoveryResult.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoLifecycleRuntime.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoLifecycleSnapshot.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoLifecycleState.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoMajorStatus.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoMarketDataWatchdogService.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoOperationReason.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoRecoveryEpisodeCoordinator.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoRecoveryEpisodePolicy.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoRecoveryEpisodeState.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoRecoveryEpisodeStatusException.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoRecoveryHoldingBuffer.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoRecoveryHoldingSnapshot.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoRecoveryRequestOutcome.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoRecoveryRequestResult.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoSoftGatePolicy.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoSoftGateResult.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoSoftRecoveryGate.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoStage3Options.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoStageMetric.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoTerminalFaultSignal.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoWatchdogMetrics.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoWatchdogMetricsSnapshot.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoWatchdogObservation.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatabentoWatchdogOptions.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatasetIncidentDecision.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatasetIncidentSnapshot.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatasetIncidentStateMachine.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatasetIncidentTransition.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatasetRecoveryAction.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatasetWorkerAdmission.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/DatasetWorkerAdmissionRegistry.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/FuturesRolloverContractAssignment.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/ICurrentFuturesContractCatalog.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/IDatabentoContractAuthority.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/IDatabentoDatasetProcessRecovery.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/IDatabentoLifecycleRuntime.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/IDatabentoRecoveryRequester.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/IDatabentoWatchdogPublisher.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/IMarketDataLifecycleRequests.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/IMarketDataServiceStore.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/IRecoveryInfrastructureProbe.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/InMemoryMarketDataServiceStore.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/NullDatabentoWatchdogPublisher.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/RecoveryInfrastructureKind.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/RecoveryProbeResult.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Resiliency/UnavailableDatabentoDatasetProcessRecovery.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/ResolvedCurrentFuturesContract.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/SyntheticFuturesContractFactory.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/SyntheticPersistenceIsolationGuard.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatabentoHardAttemptResult.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatabentoLocalQualificationEvaluator.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatabentoSupervisedWorkerOptions.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetDesiredSubscriptionRegistry.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetPublicationEnvelope.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetPublicationFrameCodec.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetPublicationIngress.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetPublicationKind.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetRecoveryManifestSnapshot.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetSubscriptionContract.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetSubscriptionManifest.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetWorkerAggregationDiagnostics.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetWorkerControlFrame.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetWorkerCurrentValues.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetWorkerDiagnostics.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetWorkerDrainDiagnostics.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetWorkerFrameCodec.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetWorkerMessageKind.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetWorkerProcessRecoveryService.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetWorkerProcessSnapshot.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetWorkerProcessSupervisor.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetWorkerRecordProgress.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetWorkerStartRequest.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/HardWorkerContainmentResult.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/IDatasetWorkerProcess.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/LatestOptionObservations.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/MarketDataHostConnection.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/OptionTradeAcknowledgment.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/PooledFrameBuffer.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/RemoteDatasetWorkerProcess.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/SupervisedDatabentoHardRecoveryRuntime.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/SupervisedDatabentoLifecycleRuntime.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DataBento/Workers/WorkerPricingContextCodec.cs` | Replace process/control/recovery implementation with dataset-owner runtime; preserve externally used status semantics. |
| `TomasAI.IFM.Application.MarketData/DatabentoSessionAnalytics.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/FinancialModelingPrep/FinancialModelingPrepReferenceDataApi.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/FinancialModelingPrep/FmpImportDataset.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/FinancialModelingPrep/FmpImportDateResult.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/FinancialModelingPrep/FmpImportDateStatus.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/FinancialModelingPrep/FmpMarketDataImportCoordinator.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/FinancialModelingPrep/FmpMarketDataImportException.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/FinancialModelingPrep/FmpMarketDataImportOptions.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/FinancialModelingPrep/FmpMarketDataImportRequest.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/FinancialModelingPrep/FmpMarketDataImportResult.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/FinancialModelingPrep/FmpMarketDataServiceCollectionExtensions.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/FinancialModelingPrep/IFmpMarketDataImportCoordinator.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/FinancialModelingPrep/MarketDataImportPolicyOptions.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/Historical/FuturesEodAnalyticsAssembler.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/Historical/HistoricalAnalyticsWarmupOptions.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/Historical/HistoricalAnalyticsWarmupOutcome.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/Historical/HistoricalAnalyticsWarmupResult.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/Historical/HistoricalAnalyticsWarmupService.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/Historical/HistoricalDataLoader.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/Historical/RsiHistoricalSeedWindowModel.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/IMarketDataOperationsRecorder.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/IMarketOutlookHotCache.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/IMarketOutlookHotCacheWriter.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/IMarketOutlookOperations.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/IMarketOutlookUpdateReader.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/IMarketOutlookUpdateWriter.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketDataOperationMeasurement.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketDataOperationOutcome.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketDataOperationStage.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookComponentType.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookComponentWrite.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookHotCache.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookHotCacheMetrics.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookHotCacheWriteResult.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookInputState.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookProcessorMetrics.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookProcessorMetricsSnapshot.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookSourcePosition.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookUpdate.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookUpdateChannel.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookUpdateKind.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookUpdateMetricSnapshot.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/MarketOutlookUpdateSubmission.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/MarketOutlook/SessionIndicatorReadiness.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OperationsHealth/FuturesItiRuntimeOutcome.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OperationsHealth/FuturesItiRuntimeSnapshot.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OperationsHealth/FuturesItiSignalRuntimeTelemetry.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OperationsHealth/ILivePipelineProbe.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OperationsHealth/LivePipelineEvidence.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OperationsHealth/LivePipelineMonitor.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OperationsHealth/LivePipelineMonitorOptions.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OperationsHealth/MarketDataBackendHealthObserver.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OperationsHealth/MarketDataOperationsHealthObserver.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OperationsHealth/MarketDataOperationsHealthService.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OperationsHealth/MarketDataOperationsHealthSnapshot.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OperationsHealth/MarketDataOperationsStatus.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OperationsHealth/MarketDataStageHealth.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/IOptionChainSnapshotPublisher.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/IOptionUniversePlanner.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/IOptionUniverseSource.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/OptionChainBackgroundUpdater.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/OptionChainCache.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/OptionChainCacheServiceCollectionExtensions.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/OptionChainCacheTelemetry.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/OptionChainCandidatePreparation.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/OptionChainCoverageObservations.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/OptionChainPublication.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/OptionChainPublicationFrame.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/OptionFeedSessionCleanup.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/OptionUniversePlanResult.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/OptionUniversePlanner.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/PreparedOptionUniverse.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/StrategyOptionChainScope.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/OptionalApplicationDiagnosticLog.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/Pricing/Black76OptionChainGreeksEnricher.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/Black76PricingModel.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/ChainIvObservation.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/ChainSelectionCalculation.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CoalescedOptionChainPricing.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CommittedOptionChainOwnership.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionContextRefreshResult.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionDiscoveryRequest.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionDiscoveryResult.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionFutureDefinition.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionInstrumentSnapshot.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionMarketDataPlan.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionMarketInstrument.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionMarketPage.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionMarketPreparation.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionMarketSourceException.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionPreparation.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionPreparationKey.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionPreparationResult.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionPreparationService.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionRoutePlan.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionSnapshotRequest.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/CompositionSnapshotResult.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/EuropeanOptionUniverse.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/EuropeanOptionUniverseResult.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/FixtureTradeBlotterSnapshotSource.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/ICompositionMarketDataApi.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/ICompositionMarketSource.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/ICompositionPreparationStore.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/ICompositionRoutePlanStore.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/IMarketCompositionSnapshotProvider.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/IOptionPricingContextProvider.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/IOptionTradeEvidenceWriter.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/IRecoverableOptionDiscovery.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/ITradeBlotterSnapshotSource.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/IndividualOptionObservationPublisher.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/IndividualOptionRiskReader.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/MarketCompositionSnapshot.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/MarketCompositionSnapshotProvider.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionChainAtmIvEstimator.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionChainPricingInputStore.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionChainPricingInputs.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionDefinitionCandidate.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionPricingContextProvider.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionPricingContextResult.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionPricingPassResult.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionPricingReferenceBundle.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionPricingRefreshPolicy.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionPricingValue.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionSelectionValue.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionTradeEvidence.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionTradePricing.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionTradeRealtimeEvent.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/OptionTradeSource.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/PricingSemanticHash.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/PricingSourceClockPolicy.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/QualifiedCompositionDiscovery.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/QualifiedOptionDefinition.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/RecoveringOptionLeaseRegistry.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/ReviewedEsOptionReference.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/ReviewedOptionReference.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/StagedTradeLeg.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/TradeBlotterDirection.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/TradeBlotterLegStager.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/TradeBlotterOptionRight.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/TradeBlotterPositionIntent.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/TradeBlotterStagingRequest.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/TradeBlotterStagingResult.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/TradeBlotterStrategy.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/TreasuryPricingProvider.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/TreasuryPricingResult.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/TreasuryPublicationDate.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/TreasuryPublicationPolicy.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/UsTreasuryPublicationCalendar.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/WorkerFuturesCompositionSource.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/WorkerOptionChainOwner.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/WorkerOptionChainOwnership.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/WorkerOptionChainRelease.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/WorkerOptionChainRequest.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/WorkerOptionChainResult.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/WorkerOptionChainRuntime.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Pricing/WorkerOptionDefinition.cs` | Review: port volatile lease/cache/context execution; retain strategy policy/reference persistence outside Rust. |
| `TomasAI.IFM.Application.MarketData/Recovery/OptionFeedProcessingHealth.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/Recovery/OptionWorkerReadiness.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/Recovery/TradeFeedRecoveryService.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/RemainingApplicationDiagnosticLog.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/Remote/MarketDataApiCall.cs` | Preserve API-to-Host NATS and read mirrors; remove only Host-to-child transport. |
| `TomasAI.IFM.Application.MarketData/Remote/MarketDataApiClient.Backend.cs` | Preserve API-to-Host NATS and read mirrors; remove only Host-to-child transport. |
| `TomasAI.IFM.Application.MarketData/Remote/MarketDataApiClient.TradeFeedRecovery.cs` | Preserve API-to-Host NATS and read mirrors; remove only Host-to-child transport. |
| `TomasAI.IFM.Application.MarketData/Remote/MarketDataApiClient.cs` | Preserve API-to-Host NATS and read mirrors; remove only Host-to-child transport. |
| `TomasAI.IFM.Application.MarketData/Remote/MarketDataClientReceiver.cs` | Preserve API-to-Host NATS and read mirrors; remove only Host-to-child transport. |
| `TomasAI.IFM.Application.MarketData/Remote/MarketDataReadMirror.State.cs` | Preserve API-to-Host NATS and read mirrors; remove only Host-to-child transport. |
| `TomasAI.IFM.Application.MarketData/Remote/MarketDataReadMirror.cs` | Preserve API-to-Host NATS and read mirrors; remove only Host-to-child transport. |
| `TomasAI.IFM.Application.MarketData/Remote/MarketDataRemoteAdmission.cs` | Preserve API-to-Host NATS and read mirrors; remove only Host-to-child transport. |
| `TomasAI.IFM.Application.MarketData/Remote/RemoteReferenceDataApi.cs` | Preserve API-to-Host NATS and read mirrors; remove only Host-to-child transport. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/DesiredSubscriptionManifest.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/DesiredSubscriptionRoute.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/DurableCompositionRuntime.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/DurableRealization.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/DurableSubscriptionDelivery.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/ICommittedOptionOwnership.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/IDurableCompositionReconciler.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/MarketDataSubscriptionCoordinator.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/BusinessSubscriptionSourceKind.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/BusinessSubscriptionSourceReference.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/DurableAuthorityMutation.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/DurableAuthorityState.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/DurableAuthorityStatus.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/DurableIntentResult.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/DurableIntentResultCode.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/DurableSubscriptionContract.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/DurableSubscriptionLease.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/DurableSubscriptionOutboxItem.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/DurableSubscriptionOwner.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/DurableSubscriptionRelease.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/DurableSubscriptionSnapshot.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/DurableSubscriptionTicker.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/DurableSubscriptionTransition.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/ICommittedBusinessEventJournal.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/ICommittedBusinessSubscriptionSource.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Persistence/IDurableSubscriptionIntentStore.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/Stage4SubscriptionOptions.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/SubscriptionDatasetAvailability.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/Subscriptions/TickerLeasePolicy.cs` | Retain durable intent persistence; consolidate runtime ownership in Rust. |
| `TomasAI.IFM.Application.MarketData/TradeStrategySymbols/InstrumentDefinitionRefresh.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/TradeStrategySymbols/StoredInstrumentDefinitionSymbolCatalog.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/TradeStrategySymbols/StoredInstrumentDefinitionSymbolSource.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |
| `TomasAI.IFM.Application.MarketData/TradeStrategySymbols/TradeStrategySymbolCatalog.cs` | Retain application workflow/persistence; redirect runtime dependencies through IMarketDataApi/Rust facade. |

### TomasAI.IFM.Application.MarketData.Host

| Source file | Proposed disposition |
|---|---|
| `TomasAI.IFM.Application.MarketData.Host/Api/MarketDataApiBackend.TradeRecovery.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Api/MarketDataApiBackend.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Hosting/BackendServices.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Hosting/Program.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Hosting/RecoveryTestVisibility.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Logging/MarketDataHostLogging.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Messaging/MarketDataNatsService.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Messaging/OptionChainPublicationRelay.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Messaging/WorkerPublicationRelay.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/OptionChains/FrozenEmulatorOptionChain.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/OptionChains/HostOptionChainContext.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/OptionChains/InstrumentDefinitionStore.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/OptionChains/OptionChainStrikeWindow.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/OptionChains/OptionChainStrikeWindowResult.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/OptionChains/OptionChainSubscriptionCoverage.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/OptionChains/OptionChainWindowInputs.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/OptionChains/OptionContractExpiryCalendarRefreshService.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/OptionChains/QualifiedEvaluatedOptionChain.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/OptionChains/StoredOptionDefinitionRangeLoader.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/OptionChains/StoredOptionUniverseSource.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/OptionChains/TradeStrategySymbolStore.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Pricing/HostCommittedOptionOwnership.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Pricing/HostCompositionMarketDataApi.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Pricing/QualifiedIndividualOptionFeeds.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Seeding/FourHourDatabentoSeedReplay.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Seeding/VxBootstrapPriceService.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Storage/HistoricalDataLoaderStore.cs` | Retain service/DB/NATS boundary; delegate provider/session/cache work to Rust. |
| `TomasAI.IFM.Application.MarketData.Host/Workers/MarketDataDatasetMonitor.cs` | Replace child-process supervision with thin Rust runtime adapter; retain Host process. |
| `TomasAI.IFM.Application.MarketData.Host/Workers/MarketDataDevelopmentFault.cs` | Replace child-process supervision with thin Rust runtime adapter; retain Host process. |
| `TomasAI.IFM.Application.MarketData.Host/Workers/MarketDataHostState.cs` | Replace child-process supervision with thin Rust runtime adapter; retain Host process. |
| `TomasAI.IFM.Application.MarketData.Host/Workers/MarketDataOptionObservationRepair.cs` | Replace child-process supervision with thin Rust runtime adapter; retain Host process. |
| `TomasAI.IFM.Application.MarketData.Host/Workers/MarketDataWorkerHost.cs` | Replace child-process supervision with thin Rust runtime adapter; retain Host process. |
| `TomasAI.IFM.Application.MarketData.Host/Workers/MarketDataWorkerLeases.cs` | Replace child-process supervision with thin Rust runtime adapter; retain Host process. |
| `TomasAI.IFM.Application.MarketData.Host/Workers/MarketDataWorkerManifest.cs` | Replace child-process supervision with thin Rust runtime adapter; retain Host process. |
| `TomasAI.IFM.Application.MarketData.Host/Workers/MarketDataWorkerRecovery.cs` | Replace child-process supervision with thin Rust runtime adapter; retain Host process. |

### TomasAI.IFM.Application.MarketData.Host.Worker

| Source file | Proposed disposition |
|---|---|
| `TomasAI.IFM.Application.MarketData.Host.Worker/Hosting/DatasetWorkerArguments.cs` | Preserve current worker executable; new Rust backend bypasses it and owns dataset lifecycle through Tokio. |
| `TomasAI.IFM.Application.MarketData.Host.Worker/Hosting/DatasetWorkerAssemblyMarker.cs` | Preserve current worker executable; new Rust backend bypasses it and owns dataset lifecycle through Tokio. |
| `TomasAI.IFM.Application.MarketData.Host.Worker/Hosting/Program.cs` | Preserve current worker executable; new Rust backend bypasses it and owns dataset lifecycle through Tokio. |
| `TomasAI.IFM.Application.MarketData.Host.Worker/Interop/NativeMethods.cs` | Preserve current worker executable; new Rust backend bypasses it and owns dataset lifecycle through Tokio. |
| `TomasAI.IFM.Application.MarketData.Host.Worker/Logging/DatasetWorkerLogging.cs` | Preserve current worker executable; new Rust backend bypasses it and owns dataset lifecycle through Tokio. |
| `TomasAI.IFM.Application.MarketData.Host.Worker/Messaging/PipeDatasetWorkerPublisher.cs` | Preserve current worker executable; new Rust backend bypasses it and owns dataset lifecycle through Tokio. |
| `TomasAI.IFM.Application.MarketData.Host.Worker/Runtime/DatasetWorkerRuntime.cs` | Preserve current worker executable; new Rust backend bypasses it and owns dataset lifecycle through Tokio. |

### TomasAI.IFM.Application.BlackBoard

| Source file | Proposed disposition |
|---|---|
| `TomasAI.IFM.Application.BlackBoard/BlackboardService.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/CachedDatabentoMarketDataQueries.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/DatabentoContractMappingCache.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/DatabentoContractMappingCacheEntry.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/DomainEventsCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/EventNameIdCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/EventProjectorStateCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/EventSourcingBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/EventStreamIdCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/ForwardLossRatioMapCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/FuturesContractCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/FuturesContractSymbolCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/FuturesEodDataCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/FuturesEodDataRangeCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/FuturesItiSignalAveragePredictedTrendDeltaCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/FuturesItiSignalAveragePredictedTrendDeltaRangeCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/FuturesItiSignalMDICacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/FuturesOptionTickDataStreamingParameterCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/FuturesRsiDailySignalCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/FuturesRsiSignalCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/FuturesTickDataStreamingParameterCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/HedgePositionTradeIdCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/IBlackboardService.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/ICachedDatabentoMarketDataQueries.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/IDatabentoContractMappingCache.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/IEventSourcingBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/IMarketDataAnalyticsBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/IMarketDataBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/IMarketDataFeedBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/IMarketDataSecuritiesBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/IReferenceBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/ITradeBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/IronCondorMDILimitCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/MarketDataAnalyticsBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/MarketDataBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/MarketDataFeedBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/MarketDataSecuritiesBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/NormalCurveTableCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/OptionTradeCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/ReferenceBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/ReferenceLookupCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/RiskFreeRateCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/SignalProcessorCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/StopLossLimitCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/StreamingRequestIdCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/TradeBlackboard.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/TradeOrderCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/TradePlanForwardLossLimitCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/TradePositionActionCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/VixFuturesContractIdCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/VixFuturesEodDataCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |
| `TomasAI.IFM.Application.BlackBoard/VixFuturesOpenPriceCacheModel.cs` | Adapt provider query/cache integration where referenced; unrelated blackboards remain. |

### native/DatabentoFeed.Native

| Source file | Proposed disposition |
|---|---|
| `native/DatabentoFeed.Native/include/databento_feed_native.h` | Preserve current C++ backend; new Rust library independently implements equivalent provider features. |
| `native/DatabentoFeed.Native/src/databento_feed_native.cpp` | Preserve current C++ backend; new Rust library independently implements equivalent provider features. |
| `native/DatabentoFeed.Native/src/databento_live_normalization.hpp` | Preserve current C++ backend; new Rust library independently implements equivalent provider features. |
| `native/DatabentoFeed.Native/src/instrument_mapping_selector.hpp` | Preserve current C++ backend; new Rust library independently implements equivalent provider features. |
| `native/DatabentoFeed.Native/src/latest_price_session_guard.hpp` | Preserve current C++ backend; new Rust library independently implements equivalent provider features. |
| `native/DatabentoFeed.Native/tests/databento_feed_native_tests.cpp` | Retain/extend parity tests. |

### native.rust/DatabentoFeed.Rust

| Source file | Proposed disposition |
|---|---|
| `native.rust/DatabentoFeed.Rust/build.rs` | Preserve this implementation unchanged; copy audited decoder/SDK code and test ideas into the new library and change lifecycle only there. |
| `native.rust/DatabentoFeed.Rust/dotnet/DatabentoFeed.Native.Benchmarks/BenchmarkNativeApi.cs` | Retain/extend parity tests. |
| `native.rust/DatabentoFeed.Rust/dotnet/DatabentoFeed.Native.Benchmarks/NativeFeedBenchmarks.cs` | Retain/extend parity tests. |
| `native.rust/DatabentoFeed.Rust/dotnet/DatabentoFeed.Native.Benchmarks/NativeProducerBenchmarks.cs` | Retain/extend parity tests. |
| `native.rust/DatabentoFeed.Rust/dotnet/DatabentoFeed.Native.Benchmarks/PInvokeDrainBenchmarks.cs` | Retain/extend parity tests. |
| `native.rust/DatabentoFeed.Rust/dotnet/DatabentoFeed.Native.Benchmarks/Program.cs` | Retain/extend parity tests. |
| `native.rust/DatabentoFeed.Rust/dotnet/DatabentoFeed.Native.ComparisonTests/NativeParityTests.cs` | Retain/extend parity tests. |
| `native.rust/DatabentoFeed.Rust/dotnet/DatabentoFeed.Native.Interop/Abi.cs` | Preserve this implementation unchanged; copy audited decoder/SDK code and test ideas into the new library and change lifecycle only there. |
| `native.rust/DatabentoFeed.Rust/dotnet/DatabentoFeed.Native.Interop/NativeApi.cs` | Preserve this implementation unchanged; copy audited decoder/SDK code and test ideas into the new library and change lifecycle only there. |
| `native.rust/DatabentoFeed.Rust/dotnet/DatabentoFeed.Native.Interop/PreparedSyntheticFeed.cs` | Preserve this implementation unchanged; copy audited decoder/SDK code and test ideas into the new library and change lifecycle only there. |
| `native.rust/DatabentoFeed.Rust/dotnet/DatabentoFeed.Native.Interop/SyntheticFeedRunner.cs` | Preserve this implementation unchanged; copy audited decoder/SDK code and test ideas into the new library and change lifecycle only there. |
| `native.rust/DatabentoFeed.Rust/src/abi.rs` | Preserve this implementation unchanged; copy audited decoder/SDK code and test ideas into the new library and change lifecycle only there. |
| `native.rust/DatabentoFeed.Rust/src/diagnostics.rs` | Preserve this implementation unchanged; copy audited decoder/SDK code and test ideas into the new library and change lifecycle only there. |
| `native.rust/DatabentoFeed.Rust/src/engine.rs` | Preserve this implementation unchanged; copy audited decoder/SDK code and test ideas into the new library and change lifecycle only there. |
| `native.rust/DatabentoFeed.Rust/src/lib.rs` | Preserve this implementation unchanged; copy audited decoder/SDK code and test ideas into the new library and change lifecycle only there. |
| `native.rust/DatabentoFeed.Rust/src/live.rs` | Preserve this implementation unchanged; copy audited decoder/SDK code and test ideas into the new library and change lifecycle only there. |
| `native.rust/DatabentoFeed.Rust/src/windows.rs` | Preserve this implementation unchanged; copy audited decoder/SDK code and test ideas into the new library and change lifecycle only there. |
| `native.rust/DatabentoFeed.Rust/tests/ffi_synthetic.rs` | Retain/extend parity tests. |

### native.rust/OptionPricer.Rust

| Source file | Proposed disposition |
|---|---|
| `native.rust/OptionPricer.Rust/build.rs` | Reuse and qualify numerical kernel where applicable; preserve pricing model coverage. |
| `native.rust/OptionPricer.Rust/include/ifm_option_pricer_native.h` | Reuse and qualify numerical kernel where applicable; preserve pricing model coverage. |
| `native.rust/OptionPricer.Rust/src/abi.rs` | Reuse and qualify numerical kernel where applicable; preserve pricing model coverage. |
| `native.rust/OptionPricer.Rust/src/black76.rs` | Reuse and qualify numerical kernel where applicable; preserve pricing model coverage. |
| `native.rust/OptionPricer.Rust/src/lib.rs` | Reuse and qualify numerical kernel where applicable; preserve pricing model coverage. |
| `native.rust/OptionPricer.Rust/tests/ffi_scalar.rs` | Retain/extend parity tests. |

### TomasAI.IFM.Shared.Contracts/MarketData

| Source file | Proposed disposition |
|---|---|
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Historical/HistoricalDataSchema.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Historical/HistoricalProviderEstimate.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Historical/HistoricalProviderFile.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Historical/HistoricalProviderJob.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Historical/HistoricalProviderJobState.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Historical/HistoricalProviderRecord.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Historical/HistoricalProviderRecordBatch.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Historical/HistoricalProviderRecordKind.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Historical/HistoricalProviderRequest.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Historical/HistoricalSymbology.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Historical/IHistoricalRecordReader.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Historical/IMarketDataHistoricalProvider.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/LastPrice/IFuturesLastPriceReader.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/LastPrice/IFuturesOptionLastPriceReader.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/LastPrice/LastQuoteTickSnapshot.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/LastPrice/LastQuoteTickWithGreeksSnapshot.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/LastPrice/LastTradeTickSnapshot.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/LastPrice/LastTradeTickWithGreeksSnapshot.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/LastPrice/OptionGreeksFailureReason.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/LastPrice/OptionGreeksPriceSource.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/LastPrice/OptionGreeksSnapshot.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/OptionChainStatisticsSnapshot.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Pricing/IOptionPricingConventionStore.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Pricing/OptionExerciseStyle.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Pricing/OptionPremiumStyle.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Pricing/OptionPremiumTickRule.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Pricing/OptionPricingCalendar.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Pricing/OptionPricingContext.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Pricing/OptionPricingConvention.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Pricing/OptionPricingFailure.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Pricing/OptionPricingQuote.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Pricing/OptionSettlementStyle.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Pricing/PricingDayCount.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Pricing/PricingOptionRight.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Pricing/PricingUnderlyingKind.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/ReferenceData/EconomicCalendarEntry.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/ReferenceData/IEconomicCalendar.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/ReferenceData/ITreasuryCurve.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/ReferenceData/ITreasuryCurveIdentity.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/ReferenceData/TreasuryContinuousRate.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/ReferenceData/TreasuryContinuousRateResult.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/ReferenceData/TreasuryCurveSnapshot.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/ReferenceData/TreasuryRateConvention.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/ReferenceData/TreasuryRateConversionPolicy.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/ReferenceData/TreasuryRatePoint.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/ReferenceData/TreasuryTenor.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/TickAggregation/ITickAggregationEventPublisher.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/TickAggregation/ITickAggregationPublisherDiagnostics.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/TickAggregation/ITickLiveEventPublisher.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/TickAggregation/ITickLiveEventSink.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/TickAggregation/ITickLiveRouter.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/TickAggregation/ITickQuoteBufferLease.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/TickAggregation/ITickQuoteBufferPool.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/TickAggregation/RealtimeTickPublisherFailure.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/TickAggregation/RealtimeTickPublisherPolicy.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/TickAggregation/RealtimeTickPublisherQuoteBudgetExceededException.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/TickAggregation/RealtimeTickPublisherSaturatedException.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/TickAggregation/RealtimeTickPublisherSnapshot.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/TickAggregation/RealtimeTickPublisherUnavailableException.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Ticker/OptionTickerPriceSnapshot.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Ticker/TickerContractDetails.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Ticker/TickerPriceSnapshot.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Ticker/TickerQuoteSnapshot.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Ticker/TickerStreamOwner.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Contracts/Ticker/TickerTradeSnapshot.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Definitions/ContractDetail.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Definitions/ContractKind.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Definitions/ExactInstrumentDefinition.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Definitions/InstrumentKey.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Definitions/OptionContractDefinition.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Definitions/OptionRightSelection.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Diagnostics/ContractMappingDirection.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Diagnostics/FeedDrainDiagnostics.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Diagnostics/FeedDrainStage.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Diagnostics/TickAggregationMetricsSnapshot.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Diagnostics/TickAggregationProcessingFailure.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Diagnostics/TickAggregationProcessingStage.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Diagnostics/TickAggregationRecordProgress.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Pricing/OptionPremiumTicks.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Pricing/OptionPricingQualification.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/MarketData/Pricing/ReviewedOptionConvention.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |

### TomasAI.IFM.Shared.Contracts/OptionPricing

| Source file | Proposed disposition |
|---|---|
| `TomasAI.IFM.Shared.Contracts/OptionPricing/CashDividend.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/DividendKind.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/ExerciseKind.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/ImpliedVolatilityResult.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/ImpliedVolatilityValues.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/OptionPriceDeltaValues.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/OptionPriceResult.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/OptionPricingRequest.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/OptionSide.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/OptionValues.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/PremiumKind.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/PriceDeltaResult.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/PricingFailure.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/PricingResult.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/PricingSettings.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |
| `TomasAI.IFM.Shared.Contracts/OptionPricing/UnderlyingKind.cs` | Preserve shared wire/domain DTOs and semantics; map explicitly at ABI boundary. |

### TomasAI.IFM.Framework.OptionPricer

| Source file | Proposed disposition |
|---|---|
| `TomasAI.IFM.Framework.OptionPricer/Black76/Black76ImpliedVolatilityParameter.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Black76/Black76PriceBatchParameter.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Black76/Black76PriceParameter.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Black76/Black76Result.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Black76/LossProbability.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Black76/OptionModel.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Interop/NativeImpliedGreeksResult.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Interop/NativeMethods.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Interop/OptionPricerBackend.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Interop/OptionPricerImplementation.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Interop/OptionPricerNativeConstants.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Interop/OptionPricerNativeLibraryResolver.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Interop/OptionPricerNativeStatus.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Interop/RustOptionModel.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Pricing/CashDividendModel.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Pricing/Models/CreditSpreadPricerArgs.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Pricing/Models/OptionSpreadResult.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Pricing/Models/OptionStyle.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Pricing/OptionCalculator.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |
| `TomasAI.IFM.Framework.OptionPricer/Pricing/OptionSpreadPricer.cs` | Dependency candidate: port only the market-data-used numerical model closure after model parity; unrelated pricing remains managed. |

## 7. Recovery and integration boundary files outside core roots

- `TomasAI.IFM.Api.DatabaseBackup.Host/Services/Recovery/DevelopmentDatabaseReplacementPlatform.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Actors/Recovery/OptionPipelineRecoveryCoordinator.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Actors/Recovery/OptionPipelineRecoveryRuntime.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Actors/Recovery/RealtimePublicationRecoveryService.cs`
- `TomasAI.IFM.Application.Api.Server.Core/DependencyInjection/Observability/RegisterHostOptionsAndHealth.cs`
- `TomasAI.IFM.Application.Api.Server.Core/DependencyInjection/Recovery/RegisterRecoveryServices.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Observability/HealthChecks/FmpConfigurationHealthCheck.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Observability/HealthChecks/MarketDataRuntimeHealthCheck.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Observability/Logging/RecoveryFatalOpenTelemetry.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Observability/StatusConsole/DatabentoWatchdogStatusConsolePublisher.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Recovery/Databento/Composition/ApiDatabentoRecoveryComposition.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Recovery/Databento/Composition/ApiDatabentoRecoveryHostActivation.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Recovery/Databento/Contracts/IApiDatabentoRecoveryActions.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Recovery/Databento/HardRecovery/ApiDatabentoRecoveryPipeline.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Recovery/Databento/SoftRecovery/ApiDatabentoRecoveryActions.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Recovery/Databento/Verification/DatabentoCandidateTickStorageProof.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Recovery/Databento/Verification/RecoveryInfrastructureProbes.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Recovery/Databento/Verification/SupervisorRecoveryCanaryProbe.cs`
- `TomasAI.IFM.Application.Api.Server.Core/Recovery/Shutdown/ApiFatalRecoveryShutdown.cs`
- `TomasAI.IFM.Application.Contracts.Supervisor/Recovery/Commands/RecoveryCanaryCommand.cs`
- `TomasAI.IFM.Application.Contracts.Supervisor/Recovery/Events/RecoveryCanaryRecordedEvent.cs`
- `TomasAI.IFM.Application.Contracts.Supervisor/Recovery/RecoveryProofScope.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics/FuturesVwapSignal/Recovery/FuturesVwapHistoricalReplayPublisher.cs`

## 8. Existing test and benchmark inventory

These are candidate parity/fault-injection suites, not tests executed for this design. Inventory includes direct API tests plus source in MarketData-named test/benchmark projects and recovery tests in LivePipeline/API/ServerManager projects.

- `TomasAI.IFM.Application.Actor.IntegrationTests/IntegrationDatabentoFeedFactory.cs`
- `TomasAI.IFM.Application.Actor.IntegrationTests/IntegrationDatabentoLifecycleRuntime.cs`
- `TomasAI.IFM.Application.Blackboard.UnitTests/DatabentoContractMappingCacheTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/CoalescedOptionChainPricingTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/CompositionMarketPreparationTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/CompositionRoutePlanTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoContractRegistrationRegistryTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoCurrentFuturesContractReconciliationTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoCurrentFuturesContractResolverTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoFeedUpVerificationTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoHardRecoveryBaselineTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoHardRecoveryEngineTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoHardRecoveryFaultMatrixTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoHardRecoveryGenerationIsolationTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoHardRecoveryLocalQualificationFaultTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoProductionEpochTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoRecoveryEpisodeCoordinatorTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoRecoveryHoldingBufferTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoResiliencyTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatabentoSoftRecoveryGateTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetDesiredSubscriptionRegistryTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetIncidentStateMachineTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetPublicationCandidateHoldingTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetPublicationGenerationFenceTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetRealtimeGenerationFenceTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetWorkerApiDependencyInjectionTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetWorkerContainmentQualificationTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetWorkerCurrentValuesTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetWorkerDiagnosticsTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetWorkerManifestIntegrationTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetWorkerManifestProtocolTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetWorkerProcessSupervisorTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetWorkerPublisherTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DatasetWorkerRecoveryDisposalTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/DurableSubscriptionDeliveryTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/FinancialModelingPrepReferenceDataApiTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/FmpMarketDataImportCoordinatorTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/FuturesContractV3ReadModelTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/FuturesEodAnalyticsAssemblerTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/FuturesRolloverBddTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/FuturesRolloverPreparationPolicyTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/Harness/DeterministicMarketDataApi.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/Harness/FakeFrameworkServices.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/Harness/MarketDataApiTestContext.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/HistoricalAnalyticsWarmupServiceTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/HistoricalDataLoaderTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/IndicatorInitializationWindowTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/IndividualOptionObservationTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/IndividualOptionRiskReaderTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/InstrumentDefinitionRefreshTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/InstrumentDefinitionSelectionMultiplierTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/MarketDataApiContractApprovalTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/MarketDataApiLifecycleContractTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/MarketDataApiNatsContractTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/MarketDataApiQueryAndPriceContractTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/MarketDataApiStreamingContractTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/MarketDataClientReceiverLifecycleTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/MarketDataHostCompositionTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/MarketDataHostNatsRecoveryIntegrationTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/MarketDataOperationsHealthServiceTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/MarketDataOperationsProjectionTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/MarketDataReadMirrorTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OptionChainAtmIvEstimatorTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OptionChainBackgroundUpdaterTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OptionChainCacheTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OptionExpiryCalendarPolicyTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OptionFeedProcessingHealthTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OptionObservationCatchUpTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OptionPremiumTickTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OptionPricingRefreshPolicyTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OptionTradePricingTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OptionTradeRetentionTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OptionWorkerReadinessTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OrderCompositionDiscoveryTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OrderCompositionPreparationTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OrderCompositionPricingPrerequisiteTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OrderCompositionRetainedTradeTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OrderCompositionSelectionSnapshotTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OrderCompositionSnapshotTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OrderCompositionSustainedLoadTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/OrderCompositionWorkerTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/PricingSemanticHashCompatibilityTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/ProcessResourceQualificationCollection.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/RecoveringOptionLeaseRegistryTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/ReviewedEsOptionReferenceTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/ReviewedFuturesPricingRoutingTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/SessionIndicatorReadinessTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/Stage4LegacyCharacterizationTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/Stage4SubscriptionBoundsTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/Stage4SubscriptionContractTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/Stage4SubscriptionCoordinatorTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/Stage4SubscriptionLifecycleTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/StrategyOptionChainParameterSetTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/StrategyOptionChainPreparationTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/SupervisedDatabentoHardRecoveryRuntimeTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/SupervisedHostPublisherLifecycleTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/SyntheticPersistenceIsolationGuardTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/TradeBlotterLegStagerTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/TradeFeedRecoveryServiceTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/TradeStrategyDatasetDiscoveryTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/TradeStrategySymbolCatalogTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/UnifiedPricingConsumerCompatibilityTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/UsTreasuryCurveTests.cs`
- `TomasAI.IFM.Application.MarketData.UnitTests/WorkerSnapshotAllocationTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/DatabentoOptionQuoteBackfill.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/DatabentoOptionStatisticsBackfill.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/EconomicCalendarDbTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/FmpQueryOptimizationTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/FuturesAtrSignalDbTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/FuturesTradeSignalRepairTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/HistoricalDataLoadPersistenceTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/MarketDataDbContextConventionTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/MarketDataDbContextTestFactory.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/MarketDataDbTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/MarketDataPostgresSchemaTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/MarketDataProjectionBatchValidationTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/MarketDataProjectionPolicyTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/OptionChainStatisticsPostgresTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/OptionTradeEvidenceStorageTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/OptionVolatilityPostgresTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/PostgresAcceptedSignalTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/PostgresEodObservationTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/RealOptionChainQuoteDataTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/RsiSeedTickLookupTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/SampleData.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/StrategyOptionChainParameterStorageTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/TickAggregationStorageTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/TickQuoteCqlBufferPoolTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/TickQuoteParameterOwnershipTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/TradeTickQuoteRoutingTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataServiceDb/MarketDataServiceDbContextConventionTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataServiceDb/MarketDataServicePostgresIntegrationTests.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataServiceDb/Stage4DurableIntentPostgresTests.cs`
- `TomasAI.IFM.Domain.Application.UnitTests/UsTreasuryRefreshHostedServiceTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesAdxSignal/FuturesAdxSignalCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesAdxSignal/FuturesAdxSignalQueryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesAtrSignal/FuturesAtrSignalCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesAtrSignal/FuturesAtrSignalQueryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesItiSignal/FuturesItiSignalCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesItiSignal/FuturesItiSignalQueryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesItiSignal/FuturesItiSignalRealtimeIngressFeatureTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesMacdSignal/FuturesMacdSignalCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesMacdSignal/FuturesMacdSignalQueryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesRsiSignal/FuturesRsiDailyCollectionFeatureTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesRsiSignal/FuturesRsiSignalCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesRsiSignal/FuturesRsiSignalQueryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesTdiSignal/FuturesTdiSignalCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesTdiSignal/FuturesTdiSignalQueryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesTradeSessionBarSignal/FuturesTradeSessionBarPublicationFeatureTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesTradeSignal/FuturesTradeSignalCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesTradeSignal/FuturesTradeSignalQueryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/FuturesVwapSignal/FuturesVwapSignalCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/MarketOutlookSnapshot/MarketOutlookHotCacheScenarios.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/OptionVolatility/VolatilityRankPercentileFeatureTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.BDDTests/SampleData.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks/FuturesBarPublicationValidationBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks/FuturesItiIngressBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks/IndicatorBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks/Program.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks/Stage4OptionVolatilityBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks/ValidationBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesAdxSignal/FuturesAdxSignalCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesAdxSignal/FuturesAdxSignalQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesAtrSignal/FuturesAtrSignalCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesAtrSignal/FuturesAtrSignalQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesIntradaySignalRealtimePipelineIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesIntradaySignalStartupTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesItiSignal/FuturesItiSignalCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesItiSignal/FuturesItiSignalDailyPipelineIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesItiSignal/FuturesItiSignalLevelCycleIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesItiSignal/FuturesItiSignalQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesItiSignal/FuturesItiSignalRealtimeActorIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesItiSignal/ItiPipelineIntegrationCollection.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesMacdSignal/FuturesMacdSignalCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesMacdSignal/FuturesMacdSignalQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesRsiSignal/FuturesRsiSignalCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesRsiSignal/FuturesRsiSignalQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesTdiSignal/FuturesTdiSignalCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesTdiSignal/FuturesTdiSignalQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesTradeSessionBarSignalIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesTradeSignal/FuturesTradeSignalCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/FuturesTradeSignal/FuturesTradeSignalQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/LiveVwapProjectionTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/MarketDataAnalyticsFixture.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/MarketOutlookSnapshot/MarketOutlookHotCachePipelineIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/RejectedAnalyticsEventPersistenceIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/SampleData.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/AccumulatorRecovery/RestoredSignalLifecycleSerializationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/Architecture/AnalyticsCommandInitializationConventionTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/Architecture/AnalyticsCommandQueryWireConventionTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/Architecture/AnalyticsEventWireConventionTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/Architecture/ClosedObservationIngressValidationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/Architecture/ClosedObservationNoOpGuardTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/Architecture/CommandActorMapConventionTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/Architecture/EventQueryRealtimeActorHandlerConventionTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/Architecture/IntradayFourHourInitializationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/Command/Api/ActorMarketDataAnalyticsCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/CommandActorValidationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/EventProjectorDescriptorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesAdxSignal/FuturesAdxCommandConventionTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesAdxSignal/FuturesAdxSignalCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesAdxSignal/FuturesAdxSignalEventActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesAdxSignal/FuturesAdxSignalQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesAdxSignal/FuturesAdxSignalRealtimeActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesAtrSignal/FuturesAtrPublicationGuardTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesAtrSignal/FuturesAtrSignalCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesAtrSignal/FuturesAtrSignalEventActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesAtrSignal/FuturesAtrSignalQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesAtrSignal/FuturesAtrWilderAccumulatorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesIntradaySignalActivationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesItiSignal/FuturesItiSignalCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesItiSignal/FuturesItiSignalEventActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesItiSignal/FuturesItiSignalGeneratedCompleteTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesItiSignal/FuturesItiSignalQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesItiSignal/FuturesItiSignalRealtimeActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesItiSignal/FuturesItiSignalTimeFrameTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesItiSignal/GetFuturesItiSignalHistoryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesMacdSignal/FuturesMacdConfigurationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesMacdSignal/FuturesMacdSignalCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesMacdSignal/FuturesMacdSignalEventActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesMacdSignal/FuturesMacdSignalQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesRsiSignal/FuturesRsiEventApplicationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesRsiSignal/FuturesRsiHistoricalSeedTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesRsiSignal/FuturesRsiSignalCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesRsiSignal/FuturesRsiSignalEventActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesRsiSignal/FuturesRsiSignalHotCacheTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesRsiSignal/FuturesRsiSignalQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesRsiSignal/FuturesRsiSignalStateRepositoryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesRsiSignal/FuturesRsiSignalTimerTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTdiSignal/FuturesRsiSignalsGeneratedTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTdiSignal/FuturesTdiSignalCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTdiSignal/FuturesTdiSignalComputeTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTdiSignal/FuturesTdiSignalEventActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTdiSignal/FuturesTdiSignalQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTdiSignal/FuturesTdiSignalRealtimeActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTradeSessionBarSignal/FuturesTradeSessionBarAccumulatorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTradeSessionBarSignal/FuturesTradeSessionBarSignalContractTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTradeSessionBarSignal/FuturesTradeSessionBarSignalStateRepositoryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTradeSignal/FuturesTradeSignalCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTradeSignal/FuturesTradeSignalCommandStateChangeTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTradeSignal/FuturesTradeSignalEventActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTradeSignal/FuturesTradeSignalQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesVwapSignal/FuturesVwapAccumulatorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesVwapSignal/FuturesVwapHistoricalReplayPublisherTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesVwapSignal/FuturesVwapRecoveryGuardTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesVxTermStructureSignal/FuturesVxTermStructureAccumulatorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/HistoricalDataLoader/FuturesEmaBbHistoricalDailyReplayPublisherTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/HistoricalDataLoader/HistoricalDataLoaderContractTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/HistoricalDataLoader/HistoricalDataLoaderEventHandlerTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/MarketDataAnalyticsTestFixture.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/MarketOutlookSnapshot/LatestMarketOutlookSnapshotPublisherTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/MarketOutlookSnapshot/MarketOutlookCombinationVerificationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/MarketOutlookSnapshot/MarketOutlookDailyPreviewCalculatorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/MarketOutlookSnapshot/MarketOutlookHotCacheTestCollection.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/MarketOutlookSnapshot/MarketOutlookHotCacheTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/MarketOutlookSnapshot/MarketOutlookPriceVolatilityClassifierTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/MarketOutlookSnapshot/MarketOutlookProcessorTestRuntime.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/MarketOutlookSnapshot/MarketOutlookSnapshotCommandContractTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/MarketOutlookSnapshot/MarketOutlookSnapshotEventProjectorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/MarketOutlookSnapshot/MarketOutlookSnapshotQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/MarketOutlookSnapshot/MarketOutlookSnapshotRealtimeActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/MarketOutlookSnapshot/MarketOutlookUpdateProcessorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/OptionVolatility/InMemoryOptionVolatilityRepositoryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/OptionVolatility/LiveOptionChainIvCollectorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/OptionVolatility/OptionVolatilityTestData.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/OptionVolatility/PostgresOptionVolatilityContractTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/OptionVolatility/SyntheticVolatilityHistoryFixtureTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/OptionVolatility/VolatilityContractTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/OptionVolatility/VolatilityRankPercentileCalculatorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/OptionVolatility/VolatilitySampleCoordinatorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/OptionVolatility/VolatilitySeriesConstructorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/PeriodRangeStateRepositoryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/PeriodSignalHotCacheTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/PeriodSignalLifecycleTimerTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/Query/AlignedAnalyticsQueryHandlerTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/Query/Api/ActorMarketDataAnalyticsQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/RegimeDiscovery/RegimeDiscoveryMarketSignalSnapshotProviderTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/SampleData.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/Shared/ValidationConcurrencyTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/SharedContracts/Mdsi1SharedContractTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/SignalOwnership/FuturesSignalAccumulatorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/SnapshotStateRepositoryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/UnitTestAssembly.cs`
- `TomasAI.IFM.Domain.MarketData.Benchmarks/CalendarRangeBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Benchmarks/EconomicCalendarImportBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Benchmarks/OptionChainLatencyBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Benchmarks/Program.cs`
- `TomasAI.IFM.Domain.MarketData.Benchmarks/StrategyOptionChainCacheBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Benchmarks/ValidationBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Benchmarks/YieldCurveRateBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Benchmarks/YieldCurveRateParseDispatchBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/FuturesBarData/FuturesBarDataCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/FuturesBarData/FuturesBarDataQueryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/FuturesClosingPrice/FuturesClosingPriceCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/FuturesEodData/FuturesEodDailyPercentChangeScenarios.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/FuturesEodData/FuturesEodDataCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/FuturesEodData/FuturesEodDataQueryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/FuturesOptionTickData/FuturesOptionTickDataCommandHandlerTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/FuturesOptionTickData/FuturesOptionTickDataCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/FuturesOptionTickData/FuturesOptionTickDataEventHandlerTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/FuturesOptionTickData/FuturesOptionTickDataQueryHandlerTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/FuturesOptionTickData/FuturesOptionTickDataQueryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/FuturesTickData/FuturesTickDataCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/FuturesTickData/FuturesTickDataQueryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/MarketDataFeed/MarketDataFeedCommandHandlerTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/MarketDataFeed/MarketDataFeedCommandTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/MarketDataFeed/MarketDataFeedEventHandlerTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/MarketDataFeed/MarketDataFeedQueryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/MarketDataFeedBddFixture.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/SampleData.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests/TypedActorContextFactory.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.Benchmarks/AsyncFanOutBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.Benchmarks/EodStatisticsBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.Benchmarks/Program.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.Benchmarks/ReplayScalingBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.Benchmarks/TickAggregationBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/FuturesBarData/FuturesBarDataCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/FuturesBarData/FuturesBarDataQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/FuturesClosingPrice/FuturesClosingPriceCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/FuturesEodData/FuturesEodDataCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/FuturesEodData/FuturesEodDataQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/FuturesMarketPrice/FuturesMarketPriceRealtimeActorIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/FuturesOptionTickData/FuturesOptionTickDataCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/FuturesOptionTickData/FuturesOptionTickDataQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/FuturesTickData/FuturesTickDataCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/FuturesTickData/FuturesTickDataQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/MarketDataFeed/MarketDataFeedCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/MarketDataFeed/MarketDataFeedQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/MarketDataFeedFixture.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/Recovery/HardResetTerminalHostVerificationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/Recovery/NewRecoveryCompositionIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/SampleData.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/TickAggregation/BoundedTickAggregationPublisherTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/TickAggregation/FuturesTickTradeToEodCommandIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/TickAggregation/Stage3NatsOutageTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/TickAggregation/TickAggregationEventPublisherRealtimeTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/TickAggregation/TickStorageGenerationNatsIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests/TickAggregation/TickerStreamActorWorkflowTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/Architecture/FeedCommandConventionTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/Architecture/FeedMessageWireConventionTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/Command/Api/ActorMarketDataFeedCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/CommandActorValidationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/EodStatisticsTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/Event/Api/ActorMarketDataFeedEventApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/EventProjectorDescriptorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesBarData/FuturesBarDataCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesBarData/FuturesBarDataEventActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesBarData/FuturesBarDataQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesBarData/FuturesBarDataTimerTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesBarData/FuturesBarMarketPriceTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesBarWindowPartitionsTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesClosingPrice/FuturesClosingPriceCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesEodData/FuturesEodDataBaselineContractTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesEodData/FuturesEodDataCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesEodData/FuturesEodDataEventActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesEodData/FuturesEodDataQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesEodData/FuturesSessionStatisticsCalculationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesMarketPrice/FuturesMarketPriceRealtimeActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesOptionTickData/FuturesOptionTickDataCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesOptionTickData/FuturesOptionTickDataEventActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesOptionTickData/FuturesOptionTickDataQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesTickData/FuturesTickDataCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesTickData/FuturesTickDataEventActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/FuturesTickData/FuturesTickDataQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/MarketDataFeed/MarketDataFeedCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/MarketDataFeed/MarketDataFeedEventActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/MarketDataFeed/MarketDataFeedQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/MarketDataFeed/MarketOpenSessionTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/MarketDataFeedTestFixture.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/OptionTradeLiveFeedMapTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/Query/Api/ActorMarketDataFeedQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/Query/Api/DatabentoNatsQueryClientTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/Realtime/BaseRealtimeProjectorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/Realtime/FuturesEodMinuteWriterTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/SampleData.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/TickAggregation/IndividualOptionQuoteNotificationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/TickAggregation/TickAggregationContractTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/TickAggregation/TickAggregationRealtimeArchitectureTests.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests/TypedActorContextFactory.cs`
- `TomasAI.IFM.Domain.MarketData.IntegrationTests/DownloadLogActorFlowTests.cs`
- `TomasAI.IFM.Domain.MarketData.IntegrationTests/DownloadLogIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.IntegrationTests/DownloadLogRecoveryTests.cs`
- `TomasAI.IFM.Domain.MarketData.IntegrationTests/EconomicCalendar/EconomicCalendarCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.IntegrationTests/EconomicCalendar/EconomicCalendarImportFlowTests.cs`
- `TomasAI.IFM.Domain.MarketData.IntegrationTests/EconomicCalendar/EconomicCalendarQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.IntegrationTests/MarketConditionDownloadLogIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.IntegrationTests/MarketDataFixture.cs`
- `TomasAI.IFM.Domain.MarketData.IntegrationTests/MarketDataQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.IntegrationTests/Oct1EvaluatedOptionChainLiveTests.cs`
- `TomasAI.IFM.Domain.MarketData.IntegrationTests/OptionChainLatencyLiveTests.cs`
- `TomasAI.IFM.Domain.MarketData.IntegrationTests/SampleData.cs`
- `TomasAI.IFM.Domain.MarketData.IntegrationTests/TradeStrategySymbolQueryTransportTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.BDDTests/FuturesContractCommandHandlerTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.BDDTests/FuturesOptionContractCommandHandlerTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.BDDTests/SampleData.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.Benchmarks/BatchIoBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.Benchmarks/CommandAuditBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.Benchmarks/ContractIdBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.Benchmarks/Program.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.Benchmarks/ReplayScalingBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.Benchmarks/StateMemoryBenchmarks.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests/FuturesContractCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests/FuturesContractQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests/FuturesOptionContractCommandApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests/FuturesOptionContractQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests/SampleData.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests/SecuritiesDatabaseFixture.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests/Stage2DefinitionRuntimeTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests/StartupOptionExpiryCacheIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.UnitTests/FuturesContract/FuturesContractCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.UnitTests/FuturesContract/FuturesContractQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.UnitTests/FuturesContract/InstrumentDefinitionQueryTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.UnitTests/FuturesOptionContract/FuturesOptionContractCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.UnitTests/FuturesOptionContract/FuturesOptionContractPagingTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.UnitTests/FuturesOptionContract/FuturesOptionContractQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.UnitTests/SampleData.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.UnitTests/SecuritiesCommandConventionTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.UnitTests/SecuritiesEventProjectorTests.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.UnitTests/SecuritiesFixture.cs`
- `TomasAI.IFM.Domain.MarketData.Securities.UnitTests/SecuritiesOptimizationTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/Architecture/ImportCommandWireMigrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/Architecture/MarketDataRootMessageWireConventionTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/DownloadLog/DownloadLogBddTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/DownloadLog/DownloadLogCommandValidationTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/DownloadLog/DownloadLogContractTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/DownloadLog/DownloadLogVerificationTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/DownloadLogMessageWireContractTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/EconomicCalendar/EconomicCalendarCommandActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/EconomicCalendar/EconomicCalendarIdValidationRulesTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/EconomicCalendar/EconomicCalendarIdentityPrecisionTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/EconomicCalendar/EconomicCalendarOptimizationTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/EconomicCalendar/EconomicCalendarPageRequestTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/EconomicCalendar/EconomicCalendarQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/EconomicCalendar/EconomicCalendarRangeTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/EconomicCalendar/EconomicCalendarTestFixture.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/EconomicCalendar/EconomicCalendarsImportedHandlerTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/EvaluatedOptionChainSummaryTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/FrozenEmulatorOptionChainTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/FuturesOptionContractIdTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/FuturesReferenceMetadataTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/GetValueDateTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/ImportEventContractTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/InstrumentDefinitionImportTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/MarketDataCommandConventionTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/MarketDataFeedMonitoringWindowTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/MarketDataFeedSessionHealthPolicyTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/MarketDataQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/MarketDataTestFixture.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/OptionChainStrikeWindowTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/OptionChainSubscriptionCoverageTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/OptionSpreadLegSelectionTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/PostgresImportBoundary.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/ProviderOptionValidationTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/Query/Api/ActorMarketDataQueryApiTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/Query/TradingCalendarTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/RealFrozenOptionChainIntegrationTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/SampleData.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/UnderlyingLastTradePriceTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/YieldCurveRateCommandStateTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/YieldCurveRateQueryActorTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/YieldCurveRateValidationRulesTests.cs`
- `TomasAI.IFM.Domain.MarketData.UnitTests/YieldCurveRatesImportedHandlerTests.cs`
- `TomasAI.IFM.Domain.Reference.IntegrationTests/ConfigurationStrategyCatalogActorTests.cs`
- `TomasAI.IFM.Domain.Reference.UnitTests/Queries/ConfigurationStrategyCatalogTests.cs`
- `TomasAI.IFM.Domain.Reference.UnitTests/Queries/TradeStrategyCatalogTransportTests.cs`
- `TomasAI.IFM.Domain.Reference.UnitTests/Queries/TradeStrategyFamilyCreationTests.cs`
- `TomasAI.IFM.Domain.Trade.UnitTests/Strategy/Workflow/IntrinsicTime/Development/DevelopmentTradingPortfolioTests.cs`
- `TomasAI.IFM.Domain.Trade.UnitTests/Strategy/Workflow/IntrinsicTime/MarketCondition/MarketConditionAssessmentProviderTests.cs`
- `TomasAI.IFM.Domain.Trade.UnitTests/Strategy/Workflow/IntrinsicTime/TradeSelection/CompositionDiscoveryHandoffTests.cs`
- `TomasAI.IFM.Domain.Trade.UnitTests/TradeFlow/VolatilityPriceTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks/ApplicationPriceBenchmark.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks/LastPriceBenchmark.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks/Program.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests/ContractDetailsIntegrationTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests/ContractMappingIntegrationTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests/LatestPriceIntegrationTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests/LiveTestGate.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests/OptionChainDefinitionIntegrationTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/AssemblyInfo.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/ContractDetailsSmokeTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/ContractMappingSmokeTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/DatabentoOneHourLiveSmokeTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/DatabentoSmokeFixture.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/DatabentoSoakCounters.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/DatabentoTickCsvCapture.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/HistoricalEstimateSmokeTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/LatestPriceSmokeTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/LiveCurrentFuturesTickSmokeTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/LiveSessionVolumeSmokeTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/LiveTestGate.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/LiveTickerSmokeTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/OptionChainSmokeTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/BoundedBatchChannelTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/BoundedOptionChainTransientEventPublisherTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/ContractDetailsTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/DatabentoCompositionLiveTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/DatabentoDiagnosticsTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/DatabentoLastPriceStoreTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/DatabentoLiveFeedHealthPolicyTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/DatabentoOperationRunnerTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/DatabentoOptionChainSessionManagerTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/DatabentoTickContractMappingStoreTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/FeedLifecycleTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/FeedMonitoringTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/FeedOptionsTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/FeedRecoveryTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/FuturesSessionAccumulatorTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/HistoricalProviderTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/InstrumentDefinitionClientTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/LatestPriceTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/NativeAbiTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/NativeLifecycleTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/OptionChainDefinitionTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/OptionChainStateCopyTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/Phase6QualificationTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/ProcessorTopologyTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/SyntheticTickerFeedTests.cs`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/TickAggregationServiceTests.cs`
- `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests/FinancialModelingPrepEconomicCalendarTests.cs`
- `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests/FinancialModelingPrepLiveTests.cs`
- `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests/FinancialModelingPrepResilienceAndRegistrationTests.cs`
- `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests/FinancialModelingPrepTreasuryCurveTests.cs`
- `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests/TestSupport.cs`
- `TomasAI.IFM.Framework.MarketData.UnitTests/BlockingSpscRingBufferTests.cs`
- `TomasAI.IFM.Framework.MarketData.UnitTests/BoundedTickLiveEventPublisherTests.cs`
- `TomasAI.IFM.Framework.MarketData.UnitTests/LastPriceReaderContractTests.cs`
- `TomasAI.IFM.Framework.MarketData.UnitTests/RealtimeTickPublisherRecoveryTests.cs`
- `TomasAI.IFM.Framework.MarketData.UnitTests/ReferenceDataContractTests.cs`
- `TomasAI.IFM.Framework.MarketData.UnitTests/SampleData.cs`
- `TomasAI.IFM.Framework.MarketData.UnitTests/TickQuoteBufferPoolTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/ApiDatabentoRecoveryActionVerificationTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/ApiDatabentoRecoveryHostActivationTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/ApiDatabentoRecoveryPipelineTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/ApiRecoveryRestartHandoffTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/FourHourDatabentoSeedReplayTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/LivePipelineProbeIntegrationTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/MarketDataApiCutoverRegistrationTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/OptionPipelineRecoveryCoordinatorTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/OptionPipelineRecoveryRuntimeTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/OptionRecoveryLiveVerificationTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/OptionRecoveryNotificationIntegrationTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/RealtimeActorReplacementFactoryTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/SessionAnalyticsInitializationTests.cs`
- `TomasAI.IFM.LivePipeline.IntegrationTests/TradeFeedRecoveryStartupTests.cs`
- `TomasAI.IFM.Shared.UnitTests/MarketData/EconomicCalendarTests.cs`
- `TomasAI.IFM.Shared.UnitTests/MarketData/FuturesContractIdTests.cs`
- `TomasAI.IFM.Shared.UnitTests/MarketData/FuturesOptionContractIdTests.cs`
- `TomasAI.IFM.UI.Net.Presentation.UnitTests/Operations/MarketDataOperationsHealthTests.cs`
- `TomasAI.IFM.UI.Net.Presentation.UnitTests/Services/MarketDataAnalyticsCommandServiceTests.cs`
- `TomasAI.IFM.UI.Net.Presentation.UnitTests/Services/MarketDataFeedServiceTests.cs`
- `TomasAI.IFM.UI.Net.Presentation.UnitTests/Services/MarketDataQueryServiceTests.cs`
- `TomasAI.IFM.UI.Net.Presentation.UnitTests/ViewModels/MarketDataFeedHealthMonitorTests.cs`
- `TomasAI.IFM.UI.Net.Presentation.UnitTests/ViewModels/MarketDataViewModelTests.cs`
- `TomasAI.IFM.UI.Net.SystemTests/Layout/MarketDataOperationsHealthRenderingTests.cs`
- `TomasAI.IFM.UI.Rust.SystemTests/MarketDataManagerParityTests.cs`

## 9. Deployment/build integration references

- `TomasAI.IFM.Application.Api.Server.Core/TomasAI.IFM.Application.Api.Server.Core.csproj`
- `TomasAI.IFM.Application.Api.Server/appsettings.Development.json`
- `TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj`
- `TomasAI.IFM.Application.ServerManager/Operations/Publish-IFMApplications.ps1`
- `TomasAI.IFM.Domain.Trade.IntegratedTests/TomasAI.IFM.Domain.Trade.IntegratedTests.csproj`
- `TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks/TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks.csproj`
- `TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests/TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests.csproj`
- `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests.csproj`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/TomasAI.IFM.Framework.MarketData.DataBento.UnitTests.csproj`
- `TomasAI.IFM.Framework.MarketData.DataBento/TomasAI.IFM.Framework.MarketData.DataBento.csproj`
- `native.rust/DatabentoFeed.Rust/Cargo.toml`
- `native.rust/DatabentoFeed.Rust/build-native.ps1`
- `native.rust/DatabentoFeed.Rust/dotnet/DatabentoFeed.Native.Benchmarks/DatabentoFeed.Native.Benchmarks.csproj`
- `native.rust/DatabentoFeed.Rust/dotnet/DatabentoFeed.Native.ComparisonTests/DatabentoFeed.Native.ComparisonTests.csproj`
- `native/DatabentoFeed.Native/build-native.sh`
- `scripts/Build/Publish-MarketDataHost.ps1`
- `scripts/Databento/Run-MarketCloseSoak.ps1`
- `scripts/Development/Get-IFMDevelopmentProcess.ps1`
- `scripts/Test-OrderCompositionPrerequisites.ps1`
