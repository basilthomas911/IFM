using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Pricing;
namespace TomasAI.IFM.Application.MarketData.Remote;
public partial class MarketDataApiClient
{
    public Task<OptionObservationRepairResult> RepairOptionObservationsAsync(OptionObservationRepairRequest request, CancellationToken cancellationToken = default)
        => CallAsync<OptionObservationRepairResult>(nameof(RepairOptionObservationsAsync), [typeof(OptionObservationRepairRequest), typeof(CancellationToken)], [request, null], cancellationToken);
    public Task<bool> InjectDevelopmentOptionWorkerFaultAsync(MarketDataDevelopmentFaultRequest request, CancellationToken cancellationToken = default)
        => CallAsync<bool>(nameof(InjectDevelopmentOptionWorkerFaultAsync), [typeof(MarketDataDevelopmentFaultRequest), typeof(CancellationToken)], [request, null], cancellationToken);
    public Task<Pricing.TreasuryPricingResult> WarmTreasuryPricingAsync(CancellationToken cancellationToken = default)
        => CallAsync<Pricing.TreasuryPricingResult>(nameof(WarmTreasuryPricingAsync), [typeof(CancellationToken)], [null], cancellationToken);

    public Task<TomasAI.IFM.Shared.MarketData.Contracts.TreasuryCurveSnapshot?> ReadTreasuryCurveAsync(DateOnly asOfDate, CancellationToken cancellationToken = default)
        => CallAsync<TomasAI.IFM.Shared.MarketData.Contracts.TreasuryCurveSnapshot?>(nameof(ReadTreasuryCurveAsync), [typeof(DateOnly), typeof(CancellationToken)], [asOfDate, null], cancellationToken);

    public Task<TomasAI.IFM.Shared.MarketData.Contracts.TreasuryCurveSnapshot[]> ReadTreasuryCurvesAsync(DateOnly fromInclusive, DateOnly toInclusive, CancellationToken cancellationToken = default)
        => CallAsync<TomasAI.IFM.Shared.MarketData.Contracts.TreasuryCurveSnapshot[]>(nameof(ReadTreasuryCurvesAsync), [typeof(DateOnly), typeof(DateOnly), typeof(CancellationToken)], [fromInclusive, toInclusive, null], cancellationToken);

    public Task<TomasAI.IFM.Shared.MarketData.Contracts.EconomicCalendarEntry[]> ReadEconomicCalendarAsync(DateOnly fromInclusive, DateOnly toInclusive, string[]? countryCodes, CancellationToken cancellationToken = default)
        => CallAsync<TomasAI.IFM.Shared.MarketData.Contracts.EconomicCalendarEntry[]>(nameof(ReadEconomicCalendarAsync), [typeof(DateOnly), typeof(DateOnly), typeof(string[]), typeof(CancellationToken)], [fromInclusive, toInclusive, countryCodes, null], cancellationToken);

    public Task<TomasAI.IFM.Application.MarketData.Contracts.InstrumentDefinitionSnapshot> RefreshInstrumentDefinitionsAsync(CancellationToken cancellationToken = default)
        => CallAsync<TomasAI.IFM.Application.MarketData.Contracts.InstrumentDefinitionSnapshot>(nameof(RefreshInstrumentDefinitionsAsync), [typeof(CancellationToken)], [null], cancellationToken);

    public Task<int> RefreshOptionCalendarAsync(string symbol, CancellationToken cancellationToken = default)
        => CallAsync<int>(nameof(RefreshOptionCalendarAsync), [typeof(string), typeof(CancellationToken)], [symbol, null], cancellationToken);

    public Task<TomasAI.IFM.Shared.EventSourcing.ServiceResult<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.EvaluatedOptionChainReadModel>> EvaluateOptionChainAsync(
        TomasAI.IFM.Domain.MarketData.Shared.Queries.GetEvaluatedOptionChainQuery query, CancellationToken cancellationToken = default)
        => CallAsync<TomasAI.IFM.Shared.EventSourcing.ServiceResult<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.EvaluatedOptionChainReadModel>>(nameof(EvaluateOptionChainAsync), [typeof(TomasAI.IFM.Domain.MarketData.Shared.Queries.GetEvaluatedOptionChainQuery), typeof(CancellationToken)], [query, null], cancellationToken);

    public Task ResetMarketDataAsync(DateOnly valueDate, Guid operationId, CancellationToken cancellationToken = default)
        => CallAsync(nameof(ResetMarketDataAsync), [typeof(DateOnly), typeof(Guid), typeof(CancellationToken)], [valueDate, operationId, null], cancellationToken);
    public Task<Subscriptions.DurableRealization?> ReconcileCommittedSubscriptionsAsync(CancellationToken cancellationToken = default)
        => CallAsync<Subscriptions.DurableRealization?>(nameof(ReconcileCommittedSubscriptionsAsync), [typeof(CancellationToken)], [null], cancellationToken);
    public Task<WorkerOptionChainResult> ReleaseCompositionAsync(string dataset, WorkerOptionChainRelease request, CancellationToken cancellationToken = default)
        => CallAsync<WorkerOptionChainResult>(nameof(ReleaseCompositionAsync), [typeof(string), typeof(WorkerOptionChainRelease), typeof(CancellationToken)], [dataset, request, null], cancellationToken);
    public Task<bool> AcquireMonitoredOptionAsync(TomasAI.IFM.Shared.MarketData.Contracts.Ticker.TickerStreamOwner owner,
        TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel contract, DateOnly valueDate, CancellationToken cancellationToken = default)
        => CallAsync<bool>(nameof(AcquireMonitoredOptionAsync), [typeof(TomasAI.IFM.Shared.MarketData.Contracts.Ticker.TickerStreamOwner), typeof(TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel), typeof(DateOnly), typeof(CancellationToken)], [owner, contract, valueDate, null], cancellationToken);
    public Task<bool> ReleaseMonitoredOptionAsync(TomasAI.IFM.Shared.MarketData.Contracts.Ticker.TickerStreamOwner owner,
        string contractId, CancellationToken cancellationToken = default)
        => CallAsync<bool>(nameof(ReleaseMonitoredOptionAsync), [typeof(TomasAI.IFM.Shared.MarketData.Contracts.Ticker.TickerStreamOwner), typeof(string), typeof(CancellationToken)], [owner, contractId, null], cancellationToken);
    public Task<TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarReadModel[]> ReadFiveMinuteSeedBarsAsync(string contractId, CancellationToken cancellationToken = default)
        => CallAsync<TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarReadModel[]>(nameof(ReadFiveMinuteSeedBarsAsync), [typeof(string), typeof(CancellationToken)], [contractId, null], cancellationToken);
    public Task<MarketDataReadSnapshot> GetBackendReadSnapshotAsync(CancellationToken cancellationToken = default)
        => CallAsync<MarketDataReadSnapshot>(nameof(GetBackendReadSnapshotAsync), [typeof(CancellationToken)], [null], cancellationToken);
    public Task<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[]> PrepareSessionAsync(DateOnly valueDate, CancellationToken cancellationToken = default)
        => CallAsync<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[]>(nameof(PrepareSessionAsync), [typeof(DateOnly), typeof(CancellationToken)], [valueDate, null], cancellationToken);
    public Task<MarketDataBackendStatus> GetBackendStatusAsync(CancellationToken cancellationToken = default)
        => CallAsync<MarketDataBackendStatus>(nameof(GetBackendStatusAsync), [typeof(CancellationToken)], [null], cancellationToken);
    public Task<MarketDataRecoveryResult> RecoverMarketDataAsync(MarketDataRecoveryRequest request, CancellationToken cancellationToken = default)
        => CallAsync<MarketDataRecoveryResult>(nameof(RecoverMarketDataAsync), [typeof(MarketDataRecoveryRequest), typeof(CancellationToken)], [request, null], cancellationToken);
    public Task<CompositionDiscoveryResult> AcquireOptionScopeAsync(CompositionDiscoveryRequest request, CancellationToken cancellationToken = default)
        => CallAsync<CompositionDiscoveryResult>(nameof(AcquireOptionScopeAsync), [typeof(CompositionDiscoveryRequest), typeof(CancellationToken)], [request, null], cancellationToken);
    public Task<WorkerOptionChainResult> ReleaseOptionScopeAsync(WorkerOptionChainRequest request, CancellationToken cancellationToken = default)
        => CallAsync<WorkerOptionChainResult>(nameof(ReleaseOptionScopeAsync), [typeof(WorkerOptionChainRequest), typeof(CancellationToken)], [request, null], cancellationToken);
    public Task<CompositionSnapshotResult> CaptureCompositionAsync(string dataset, CompositionSnapshotRequest request, CancellationToken cancellationToken = default)
        => CallAsync<CompositionSnapshotResult>(nameof(CaptureCompositionAsync), [typeof(string), typeof(CompositionSnapshotRequest), typeof(CancellationToken)], [dataset, request, null], cancellationToken);
    public Task<MarketCompositionSnapshot?> CaptureOptionRiskAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken = default)
        => CallAsync<MarketCompositionSnapshot?>(nameof(CaptureOptionRiskAsync), [typeof(string), typeof(DateOnly), typeof(CancellationToken)], [contractId, valueDate, null], cancellationToken);
    public Task<CompositionPreparationResult> PrepareCompositionAsync(CompositionPreparationKey key, CompositionMarketDataPlan plan, string horizon,
        DateTimeOffset deadline, CancellationToken cancellationToken = default)
        => CallAsync<CompositionPreparationResult>(nameof(PrepareCompositionAsync), [typeof(CompositionPreparationKey), typeof(CompositionMarketDataPlan), typeof(string), typeof(DateTimeOffset), typeof(CancellationToken)], [key, plan, horizon, deadline, null], cancellationToken);
    public Task<CompositionPreparationResult> PrepareCachedCompositionAsync(CompositionPreparationKey key, OptionChainSnapshotRequest request, CancellationToken cancellationToken = default)
        => CallAsync<CompositionPreparationResult>(nameof(PrepareCachedCompositionAsync), [typeof(CompositionPreparationKey), typeof(OptionChainSnapshotRequest), typeof(CancellationToken)], [key, request, null], cancellationToken);
    public Task<MarketDataHistoricalEstimate> EstimateHistoricalAsync(MarketDataHistoricalRequest request, CancellationToken cancellationToken = default)
        => CallAsync<MarketDataHistoricalEstimate>(nameof(EstimateHistoricalAsync), [typeof(MarketDataHistoricalRequest), typeof(CancellationToken)], [request, null], cancellationToken);
    public Task<HistoricalDataLoaderState> LoadHistoricalAsync(MarketDataHistoricalRequest request, CancellationToken cancellationToken = default)
        => CallAsync<HistoricalDataLoaderState>(nameof(LoadHistoricalAsync), [typeof(MarketDataHistoricalRequest), typeof(CancellationToken)], [request, null], cancellationToken);
}
