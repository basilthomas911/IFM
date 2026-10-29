using TomasAI.IFM.Application.MarketData.Rust.Api;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
/// <summary>Complete required Host capability composition. No operation falls
/// through an unsupported interface default or constructs the legacy backend.</summary>
public sealed class NativeHostServices(NativeHostApplicationServices application,NativeHostRuntimeOperations runtime,CurrentLeaseContract leases):IRustMarketDataHostServices
{
    /// <inheritdoc/>
    public TomasAI.IFM.Application.MarketData.Contracts.IOptionChainCache OptionChainCache=>application.OptionChainCache;
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::System.Boolean> AcquireMonitoredOptionAsync(global::TomasAI.IFM.Shared.MarketData.Contracts.Ticker.TickerStreamOwner owner, global::TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel contract, global::System.DateOnly valueDate, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.AcquireMonitoredOptionAsync(owner,contract,valueDate,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Pricing.CompositionDiscoveryResult> AcquireOptionScopeAsync(global::TomasAI.IFM.Application.MarketData.Pricing.CompositionDiscoveryRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.AcquireOptionScopeAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionLeaseResult> AcquireSelectedSubscriptionLeasesAsync(global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionAcquireBatchRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return leases.AcquireSelectedSubscriptionLeasesAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Pricing.CompositionSnapshotResult> CaptureCompositionAsync(global::System.String dataset, global::TomasAI.IFM.Application.MarketData.Pricing.CompositionSnapshotRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.CaptureCompositionAsync(dataset,request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Pricing.MarketCompositionSnapshot?> CaptureOptionRiskAsync(global::System.String contractId, global::System.DateOnly valueDate, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.CaptureOptionRiskAsync(contractId,valueDate,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::System.Boolean> CloseTradeFeedRecoveryAsync(global::System.DateOnly date, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.CloseTradeFeedRecoveryAsync(date,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::System.Boolean> DisableTradeFeedRecoveryAsync(global::System.String tradeId, global::System.DateOnly date, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.DisableTradeFeedRecoveryAsync(tradeId,date,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::System.Boolean> EnableTradeFeedRecoveryAsync(global::System.String tradeId, global::System.DateOnly date, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.EnableTradeFeedRecoveryAsync(tradeId,date,cancellationToken); }
    /// <inheritdoc/>
    /// <summary>Reads the published historical schema boundary.</summary>
    public global::System.Threading.Tasks.Task<global::System.DateTimeOffset?> GetHistoricalAvailableEndAsync(global::TomasAI.IFM.Application.MarketData.Contracts.Historical.MarketDataHistoricalRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.GetHistoricalAvailableEndAsync(request,cancellationToken); }
    /// <summary>Estimates historical acquisition without submitting a paid job.</summary>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.Historical.MarketDataHistoricalEstimate> EstimateHistoricalAsync(global::TomasAI.IFM.Application.MarketData.Contracts.Historical.MarketDataHistoricalRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.EstimateHistoricalAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Shared.EventSourcing.ServiceResult<global::TomasAI.IFM.Domain.MarketData.Shared.ViewModels.EvaluatedOptionChainReadModel>> EvaluateOptionChainAsync(global::TomasAI.IFM.Domain.MarketData.Shared.Queries.GetEvaluatedOptionChainQuery query, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.EvaluateOptionChainAsync(query,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.MarketDataReadSnapshot> GetBackendReadSnapshotAsync(global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return runtime.GetBackendReadSnapshotAsync(cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.MarketDataBackendStatus> GetBackendStatusAsync(global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return runtime.GetBackendStatusAsync(cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionLeaseResult> GetSubscriptionLeasesAsync(global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionOwnerQuery request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return leases.GetSubscriptionLeasesAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::System.Boolean> InjectDevelopmentOptionWorkerFaultAsync(global::TomasAI.IFM.Application.MarketData.Contracts.MarketDataDevelopmentFaultRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return runtime.InjectDevelopmentOptionWorkerFaultAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.Historical.HistoricalDataLoaderState> LoadHistoricalAsync(global::TomasAI.IFM.Application.MarketData.Contracts.Historical.MarketDataHistoricalRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.LoadHistoricalAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::System.Boolean> LoadTradeFeedRecoveryAsync(global::System.String tradeId, global::System.DateOnly date, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.LoadTradeFeedRecoveryAsync(tradeId,date,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Pricing.CompositionPreparationResult> PrepareCachedCompositionAsync(global::TomasAI.IFM.Application.MarketData.Pricing.CompositionPreparationKey key, global::TomasAI.IFM.Application.MarketData.Contracts.OptionChainSnapshotRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.PrepareCachedCompositionAsync(key,request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Pricing.CompositionPreparationResult> PrepareCompositionAsync(global::TomasAI.IFM.Application.MarketData.Pricing.CompositionPreparationKey key, global::TomasAI.IFM.Application.MarketData.Pricing.CompositionMarketDataPlan plan, global::System.String horizon, global::System.DateTimeOffset deadline, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.PrepareCompositionAsync(key,plan,horizon,deadline,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[]> PrepareSessionAsync(global::System.DateOnly valueDate, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.PrepareSessionAsync(valueDate,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Shared.MarketData.Contracts.EconomicCalendarEntry[]> ReadEconomicCalendarAsync(global::System.DateOnly fromInclusive, global::System.DateOnly toInclusive, global::System.String[]? countryCodes, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.ReadEconomicCalendarAsync(fromInclusive,toInclusive,countryCodes,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarReadModel[]> ReadFiveMinuteSeedBarsAsync(global::System.String contractId, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.ReadFiveMinuteSeedBarsAsync(contractId,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Domain.Trade.Shared.Recovery.TradeFeedRecoveryRegistration[]> ReadTradeFeedRecoveryAsync(global::System.DateOnly date, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.ReadTradeFeedRecoveryAsync(date,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Shared.MarketData.Contracts.TreasuryCurveSnapshot?> ReadTreasuryCurveAsync(global::System.DateOnly asOfDate, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.ReadTreasuryCurveAsync(asOfDate,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Shared.MarketData.Contracts.TreasuryCurveSnapshot[]> ReadTreasuryCurvesAsync(global::System.DateOnly fromInclusive, global::System.DateOnly toInclusive, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.ReadTreasuryCurvesAsync(fromInclusive,toInclusive,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Subscriptions.DurableRealization?> ReconcileCommittedSubscriptionsAsync(global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.ReconcileCommittedSubscriptionsAsync(cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.MarketDataRecoveryResult> RecoverMarketDataAsync(global::TomasAI.IFM.Application.MarketData.Contracts.MarketDataRecoveryRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return runtime.RecoverMarketDataAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.InstrumentDefinitionSnapshot> RefreshInstrumentDefinitionsAsync(global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.RefreshInstrumentDefinitionsAsync(cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::System.Int32> RefreshOptionCalendarAsync(global::System.String symbol, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.RefreshOptionCalendarAsync(symbol,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Pricing.WorkerOptionChainResult> ReleaseCompositionAsync(global::System.String dataset, global::TomasAI.IFM.Application.MarketData.Pricing.WorkerOptionChainRelease request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.ReleaseCompositionAsync(dataset,request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::System.Boolean> ReleaseMonitoredOptionAsync(global::TomasAI.IFM.Shared.MarketData.Contracts.Ticker.TickerStreamOwner owner, global::System.String contractId, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.ReleaseMonitoredOptionAsync(owner,contractId,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Pricing.WorkerOptionChainResult> ReleaseOptionScopeAsync(global::TomasAI.IFM.Application.MarketData.Pricing.WorkerOptionChainRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.ReleaseOptionScopeAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionLeaseResult> ReleaseSubscriptionLeaseAsync(global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionReleaseRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return leases.ReleaseSubscriptionLeaseAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionLeaseResult> RenewSubscriptionLeaseAsync(global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionRenewRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return leases.RenewSubscriptionLeaseAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.OptionObservationRepairResult> RepairOptionObservationsAsync(global::TomasAI.IFM.Application.MarketData.Contracts.OptionObservationRepairRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return runtime.RepairOptionObservationsAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task ResetMarketDataAsync(global::System.DateOnly valueDate, global::System.Guid operationId, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return runtime.ResetMarketDataAsync(valueDate,operationId,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionLeaseResult> StartStreamingFuturesOptionChainDataAsync(global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionAcquireRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return leases.StartStreamingFuturesOptionChainDataAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionLeaseResult> StartStreamingFuturesOptionTickDataAsync(global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionAcquireRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return leases.StartStreamingFuturesOptionTickDataAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionLeaseResult> StartStreamingFuturesTickDataAsync(global::TomasAI.IFM.Application.MarketData.Contracts.SubscriptionAcquireRequest request, global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return leases.StartStreamingFuturesTickDataAsync(request,cancellationToken); }
    /// <inheritdoc/>
    public global::System.Threading.Tasks.Task<global::TomasAI.IFM.Application.MarketData.Pricing.TreasuryPricingResult> WarmTreasuryPricingAsync(global::System.Threading.CancellationToken cancellationToken = default) { runtime.ObserveIdentity(); return application.WarmTreasuryPricingAsync(cancellationToken); }
}
