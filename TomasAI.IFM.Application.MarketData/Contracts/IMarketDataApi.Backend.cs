using MessagePack;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Pricing;
namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Marks operations whose authority belongs to Host rather than an individual provider worker.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MarketDataHostOperationAttribute : Attribute;

public partial interface IMarketDataApi
{
    [MarketDataHostOperation]
    Task<Pricing.TreasuryPricingResult> WarmTreasuryPricingAsync(CancellationToken cancellationToken = default)
        => Task.FromException<Pricing.TreasuryPricingResult>(new NotSupportedException("Backend reference operation is not configured."));

    [MarketDataHostOperation]
    Task<TomasAI.IFM.Framework.MarketData.Contracts.TreasuryCurveSnapshot?> ReadTreasuryCurveAsync(DateOnly asOfDate, CancellationToken cancellationToken = default)
        => Task.FromException<TomasAI.IFM.Framework.MarketData.Contracts.TreasuryCurveSnapshot?>(new NotSupportedException("Backend reference operation is not configured."));

    [MarketDataHostOperation]
    Task<TomasAI.IFM.Framework.MarketData.Contracts.TreasuryCurveSnapshot[]> ReadTreasuryCurvesAsync(DateOnly fromInclusive, DateOnly toInclusive, CancellationToken cancellationToken = default)
        => Task.FromException<TomasAI.IFM.Framework.MarketData.Contracts.TreasuryCurveSnapshot[]>(new NotSupportedException("Backend reference operation is not configured."));

    [MarketDataHostOperation]
    Task<TomasAI.IFM.Framework.MarketData.Contracts.EconomicCalendarEntry[]> ReadEconomicCalendarAsync(DateOnly fromInclusive, DateOnly toInclusive, string[]? countryCodes, CancellationToken cancellationToken = default)
        => Task.FromException<TomasAI.IFM.Framework.MarketData.Contracts.EconomicCalendarEntry[]>(new NotSupportedException("Backend reference operation is not configured."));

    [MarketDataHostOperation]
    Task<InstrumentDefinitionSnapshot> RefreshInstrumentDefinitionsAsync(CancellationToken cancellationToken = default)
        => Task.FromException<InstrumentDefinitionSnapshot>(new NotSupportedException("Backend reference operation is not configured."));

    [MarketDataHostOperation]
    Task<int> RefreshOptionCalendarAsync(string symbol, CancellationToken cancellationToken = default)
        => Task.FromException<int>(new NotSupportedException("Backend reference operation is not configured."));

    [MarketDataHostOperation]
    Task<TomasAI.IFM.Shared.EventSourcing.ServiceResult<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.EvaluatedOptionChainReadModel>> EvaluateOptionChainAsync(
        TomasAI.IFM.Domain.MarketData.Shared.Queries.GetEvaluatedOptionChainQuery query, CancellationToken cancellationToken = default)
        => Task.FromException<TomasAI.IFM.Shared.EventSourcing.ServiceResult<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.EvaluatedOptionChainReadModel>>(new NotSupportedException("Option chain evaluation is not configured."));
    [MarketDataHostOperation]
    Task ResetMarketDataAsync(DateOnly valueDate, Guid operationId, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("Backend reset is not configured."));
    [MarketDataHostOperation]
    Task<Subscriptions.DurableRealization?> ReconcileCommittedSubscriptionsAsync(CancellationToken cancellationToken = default)
        => Task.FromException<Subscriptions.DurableRealization?>(new NotSupportedException("Backend committed ownership is not configured."));
    [MarketDataHostOperation]
    Task<WorkerOptionChainResult> ReleaseCompositionAsync(string dataset, WorkerOptionChainRelease request, CancellationToken cancellationToken = default)
        => Task.FromException<WorkerOptionChainResult>(new NotSupportedException("Backend composition release is not configured."));
    [MarketDataHostOperation]
    Task<bool> AcquireMonitoredOptionAsync(TomasAI.IFM.Framework.MarketData.Contracts.Ticker.TickerStreamOwner owner,
        TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel contract, DateOnly valueDate, CancellationToken cancellationToken = default)
        => Task.FromException<bool>(new NotSupportedException("Backend monitoring is not configured."));
    [MarketDataHostOperation]
    Task<bool> ReleaseMonitoredOptionAsync(TomasAI.IFM.Framework.MarketData.Contracts.Ticker.TickerStreamOwner owner,
        string contractId, CancellationToken cancellationToken = default)
        => Task.FromException<bool>(new NotSupportedException("Backend monitoring is not configured."));
    [MarketDataHostOperation]
    Task<TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarReadModel[]> ReadFiveMinuteSeedBarsAsync(string contractId, CancellationToken cancellationToken = default)
        => Task.FromException<TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarReadModel[]>(new NotSupportedException("Backend seed replay is not configured."));
    [MarketDataHostOperation]
    Task<MarketDataReadSnapshot> GetBackendReadSnapshotAsync(CancellationToken cancellationToken = default)
        => Task.FromException<MarketDataReadSnapshot>(new NotSupportedException("Backend cache synchronization is not configured."));
    [MarketDataHostOperation]
    Task<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[]> PrepareSessionAsync(DateOnly valueDate, CancellationToken cancellationToken = default)
        => Task.FromException<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[]>(new NotSupportedException("Backend session preparation is not configured."));
    [MarketDataHostOperation]
    Task<MarketDataBackendStatus> GetBackendStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromException<MarketDataBackendStatus>(new NotSupportedException("Backend status is not configured."));
    [MarketDataHostOperation]
    Task<MarketDataRecoveryResult> RecoverMarketDataAsync(MarketDataRecoveryRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<MarketDataRecoveryResult>(new NotSupportedException("Backend recovery is not configured."));
    [MarketDataHostOperation]
    Task<CompositionDiscoveryResult> AcquireOptionScopeAsync(CompositionDiscoveryRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<CompositionDiscoveryResult>(new NotSupportedException("Option discovery is not configured."));
    [MarketDataHostOperation]
    Task<WorkerOptionChainResult> ReleaseOptionScopeAsync(WorkerOptionChainRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<WorkerOptionChainResult>(new NotSupportedException("Option discovery is not configured."));
    [MarketDataHostOperation]
    Task<CompositionSnapshotResult> CaptureCompositionAsync(string dataset, CompositionSnapshotRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<CompositionSnapshotResult>(new NotSupportedException("Composition capture is not configured."));
    [MarketDataHostOperation]
    Task<MarketCompositionSnapshot?> CaptureOptionRiskAsync(string contractId, DateOnly valueDate, CancellationToken cancellationToken = default)
        => Task.FromException<MarketCompositionSnapshot?>(new NotSupportedException("Option risk capture is not configured."));
    [MarketDataHostOperation]
    Task<CompositionPreparationResult> PrepareCompositionAsync(CompositionPreparationKey key, CompositionMarketDataPlan plan,
        string horizon, DateTimeOffset deadline, CancellationToken cancellationToken = default)
        => Task.FromException<CompositionPreparationResult>(new NotSupportedException("Composition preparation is not configured."));
    [MarketDataHostOperation]
    Task<CompositionPreparationResult> PrepareCachedCompositionAsync(CompositionPreparationKey key, OptionChainSnapshotRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromException<CompositionPreparationResult>(new NotSupportedException("Composition preparation is not configured."));
    [MarketDataHostOperation]
    Task<MarketDataHistoricalEstimate> EstimateHistoricalAsync(MarketDataHistoricalRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<MarketDataHistoricalEstimate>(new NotSupportedException("Historical estimates are not configured."));
    [MarketDataHostOperation]
    Task<HistoricalDataLoaderState> LoadHistoricalAsync(MarketDataHistoricalRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<HistoricalDataLoaderState>(new NotSupportedException("Historical loading is not configured."));
}

/// <summary>Immutable backend observation; process and dataset identities remain distinct.</summary>
[MessagePackObject]
public sealed record MarketDataBackendStatus([property: Key(0)] bool Ready, [property: Key(1)] DateOnly? ValueDate,
    [property: Key(2)] IReadOnlyList<MarketDataDatasetStatus> Datasets,
    [property: Key(3)] Guid HostInstanceId = default, [property: Key(4)] long Revision = 0,
    [property: Key(5)] byte[][]? WorkerObservations = null);
[MessagePackObject]
public sealed record MarketDataDatasetStatus([property: Key(0)] string Role, [property: Key(1)] string Dataset,
    [property: Key(2)] Guid WorkerInstanceId, [property: Key(3)] Guid GenerationId,
    [property: Key(4)] int ProcessId, [property: Key(5)] bool Running, [property: Key(6)] bool Healthy,
    [property: Key(7)] string Lifecycle, [property: Key(8)] string? Error,
    [property: Key(9)] DateOnly? ValueDate = null, [property: Key(10)] Guid OperationId = default,
    [property: Key(11)] long ManifestRevision = 1, [property: Key(12)] DateTimeOffset ChangedAtUtc = default);
[MessagePackObject]
public sealed record MarketDataRecoveryRequest([property: Key(0)] Guid OperationId, [property: Key(1)] string Role,
    [property: Key(2)] string Dataset, [property: Key(3)] Guid ExpectedWorkerInstanceId,
    [property: Key(4)] Guid ExpectedGenerationId, [property: Key(5)] DateOnly ValueDate, [property: Key(6)] string Reason);
[MessagePackObject]
public sealed record MarketDataRecoveryResult([property: Key(0)] Guid OperationId, [property: Key(1)] string Role,
    [property: Key(2)] string Dataset, [property: Key(3)] bool Recovered,
    [property: Key(4)] bool CleanupConfirmed, [property: Key(5)] bool LiveFeedEnabled,
    [property: Key(6)] string? Error, [property: Key(7)] Guid WorkerInstanceId, [property: Key(8)] Guid GenerationId);

[MessagePackObject]
public sealed record MarketDataReadSnapshot([property: Key(0)] MarketDataBackendStatus Status,
    [property: Key(1)] Databento.Workers.DatasetSubscriptionManifest[] Manifests,
    [property: Key(2)] TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[] Contracts,
    [property: Key(3)] Databento.Workers.DatasetPublicationEnvelope[] CurrentValues,
    [property: Key(4)] OptionChainCache.OptionChainPublication[]? OptionChains = null,
    [property: Key(5)] MarketDataBootstrapPrice[]? BootstrapPrices = null);
[MessagePackObject]
public sealed record MarketDataBootstrapPrice([property: Key(0)] TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events.FuturesMarketPriceSnapshot Price,
    [property: Key(1)] string Source);
