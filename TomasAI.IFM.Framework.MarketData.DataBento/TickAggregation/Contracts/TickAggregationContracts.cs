using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

public interface ITickAggregationService : IAsyncDisposable
{
    bool IsRunning { get; }
    TickAggregationContractStatus GetContractStatus(string contractId);
    TickAggregationTickerStatus GetTickerStatus(string futuresContractId);

    /// <summary>
    /// Reads the latest normalized market-price hot-cache snapshot without checking stream ownership.
    /// </summary>
    /// <param name="contractId">The domain contract identifier.</param>
    /// <param name="snapshot">The latest combined quote and trade snapshot when available.</param>
    /// <returns><see langword="true"/> when the contract cache has observed a price.</returns>
    bool TryGetLastTickPrice(string contractId, out FuturesMarketPriceSnapshot snapshot);

    /// <summary>
    /// Reads the latest normalized futures-option snapshot, including cached Greeks when available,
    /// without consulting stream ownership.
    /// </summary>
    bool TryGetLastOptionTickPrice(string contractId, out OptionTickerPriceSnapshot snapshot);

    /// <summary>Reads the latest complete provider-neutral session open/high/low snapshot.</summary>
    bool TryGetFuturesSessionStatistics(
        string contractId,
        out FuturesSessionStatisticsSnapshot snapshot)
    {
        snapshot = default;
        return false;
    }

    /// <summary>Returns whether at least one workflow currently owns the contract's transient stream.</summary>
    bool IsTickDataStreamActive(string contractId);

    /// <summary>Adds an idempotent workflow owner and activates the route for the first owner.</summary>
    bool StartTickDataStream(TickerStreamOwner owner, string contractId);

    /// <summary>Removes a workflow owner and deactivates the route after the final owner leaves.</summary>
    bool StopTickDataStream(TickerStreamOwner owner, string contractId);
    ValueTask StartAsync();
    ValueTask StopAsync();
}

public readonly record struct TickAggregationContractStatus(
    string ContractId,
    AssetTypeId AssetTypeId,
    bool ServiceRunning,
    bool ContractConfigured,
    bool ContractRunning,
    bool StreamActive = false,
    DateTimeOffset? LastSourceRecordObservedAtUtc = null,
    DateTimeOffset? LastMarketPricePublishedAtUtc = null,
    DateTimeOffset? LastDurableTickPublishedAtUtc = null,
    DateTimeOffset? StreamActivatedAtUtc = null,
    DateTimeOffset? LastAcceptedCacheUpdateAtUtc = null,
    DateTimeOffset? LastAcceptedSourceEventAtUtc = null,
    long AcceptedCacheUpdates = 0,
    long RejectedCacheUpdates = 0)
{
    /// <summary>Gets the route-level health of accepted Databento input.</summary>
    public DatabentoLiveFeedHealthState HealthAt(DateTimeOffset utcNow) =>
        DatabentoLiveFeedHealthPolicy.Evaluate(
            StreamActive,
            StreamActivatedAtUtc,
            LastAcceptedCacheUpdateAtUtc,
            LastAcceptedSourceEventAtUtc,
            utcNow);
}

/// <summary>Health of one explicitly enabled Databento route.</summary>
public enum DatabentoLiveFeedHealthState
{
    Inactive,
    Green,
    Yellow,
    Red
}

/// <summary>
/// Authoritative 5/15-minute policy evaluated from accepted hot-cache mutations.
/// Source age participates so an old backlog cannot make a route appear current.
/// </summary>
public static class DatabentoLiveFeedHealthPolicy
{
    public static readonly TimeSpan GreenLimit = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan YellowLimit = TimeSpan.FromMinutes(15);

    public static DatabentoLiveFeedHealthState Evaluate(
        bool streamActive,
        DateTimeOffset? streamActivatedAtUtc,
        DateTimeOffset? lastAcceptedCacheUpdateAtUtc,
        DateTimeOffset? lastAcceptedSourceEventAtUtc,
        DateTimeOffset utcNow)
    {
        if (!streamActive)
            return DatabentoLiveFeedHealthState.Inactive;

        var acceptedAge = Age(utcNow, lastAcceptedCacheUpdateAtUtc ?? streamActivatedAtUtc);
        var sourceAge = lastAcceptedSourceEventAtUtc is null
            ? acceptedAge
            : Age(utcNow, lastAcceptedSourceEventAtUtc);
        var effectiveAge = acceptedAge >= sourceAge ? acceptedAge : sourceAge;
        if (effectiveAge <= GreenLimit)
            return DatabentoLiveFeedHealthState.Green;
        if (effectiveAge <= YellowLimit)
            return DatabentoLiveFeedHealthState.Yellow;
        return DatabentoLiveFeedHealthState.Red;
    }

    static TimeSpan Age(DateTimeOffset utcNow, DateTimeOffset? timestamp) =>
        timestamp is null
            ? TimeSpan.MaxValue
            : utcNow <= timestamp.Value
                ? TimeSpan.Zero
                : utcNow - timestamp.Value;
}

public readonly record struct TickAggregationTickerStatus(
    string FuturesContractId,
    bool ServiceRunning,
    bool TickerConfigured,
    bool TickerRunning);

public interface ITickAggregationMetricsSource
{
    TickAggregationMetricsSnapshot GetMetrics();
}

public enum TickAggregationProcessingStage
{
    Idle = 0,
    Starting = 1,
    ValueDateFlush = 2,
    StatisticsReplayPublish = 3,
    TradeReplayPublish = 4,
    QuoteUpdate = 5,
    QuoteMarketPricePublish = 6,
    QuoteLiveRoute = 7,
    QuoteFlush = 8,
    TradeUpdate = 9,
    TradeMarketPricePublish = 10,
    TradeLiveRoute = 11,
    TradeQuoteFlush = 12,
    TradePublish = 13,
    StatisticsUpdate = 14,
    StatisticsPublish = 15
}

public sealed record TickAggregationRecordProgress(
    string Dataset,
    string ContractId,
    string RecordKind,
    ushort PublisherId,
    uint InstrumentId,
    uint SourceSequence,
    DateTimeOffset StartedAtUtc);

public sealed record TickAggregationProcessingFailure(
    string Dataset,
    string ContractId,
    string RecordKind,
    ushort PublisherId,
    uint InstrumentId,
    uint SourceSequence,
    TickAggregationProcessingStage Stage,
    DateTimeOffset FailedAtUtc,
    TimeSpan ProcessingDuration,
    string ExceptionType,
    string ExceptionMessage);

public readonly record struct TickAggregationMetricsSnapshot(
    long SourceQuoteRecords,
    long SourceTradeRecords,
    long EmittedQuoteBatches,
    long EmittedQuoteItems,
    long EmittedTradeEvents,
    long BufferFullFlushes,
    long PartialQuoteFlushes,
    long DuplicateSourceSequences,
    long OutOfOrderSourceSequences,
    long SourceSequenceGaps,
    long PublicationFailures,
    long ProcessingFailures,
    int ActiveTickers,
    int ServiceOwnedQuoteBuffers)
{
    public long RecordsStarted { get; init; }
    public long RecordsCompleted { get; init; }
    public long SourceMboRecords { get; init; }
    public long SourceStatisticsRecords { get; init; }
    public long StatisticsReplayCompleteRecords { get; init; }
    public long TradeReplayCompleteRecords { get; init; }
    public long UnsupportedRecords { get; init; }
    public long CurrentProcessingDurationTicks { get; init; }
    public long TotalProcessingDurationTicks { get; init; }
    public long MaximumProcessingDurationTicks { get; init; }
    public DateTimeOffset? LastRecordStartedAtUtc { get; init; }
    public DateTimeOffset? LastRecordCompletedAtUtc { get; init; }
    public DateTimeOffset? LastRecordFailedAtUtc { get; init; }
    public TickAggregationProcessingStage CurrentStage { get; init; }
    public TickAggregationRecordProgress? InFlightRecord { get; init; }
    public TickAggregationProcessingFailure? LastFailure { get; init; }
}

public readonly record struct TickContractMapping(
    string Dataset,
    DateOnly DefinitionDate,
    ushort PublisherId,
    uint InstrumentId,
    string ContractId,
    AssetTypeId AssetTypeId,
    TickerContractDetails? ContractDetails = null);

/// <summary>
/// Controls transient delivery when a contract obtains its first stream owner or
/// releases its final stream owner.
/// </summary>
public interface ITickerStreamRouteController
{
    void Activate(TickContractMapping mapping);
    void Deactivate(TickContractMapping mapping);
}

public interface ITickContractMappingProvider
{
    bool TryGetMapping(
        string dataset,
        DateOnly definitionDate,
        InstrumentKey instrument,
        out TickContractMapping mapping);

    /// <summary>
    /// Resolves the instrument identity returned by a live feed registration.
    /// Implementations may use the requested/raw symbol as a definition-scoped
    /// fallback when provider metadata and the live session use different
    /// instrument identifiers for the same contract.
    /// </summary>
    bool TryResolveFeedMapping(
        string dataset,
        DateOnly definitionDate,
        TickerInstrumentRegistration registration,
        out TickContractMapping mapping) =>
        TryGetMapping(dataset, definitionDate, registration.Instrument, out mapping);
}

public interface ITickContractMappingStore : ITickContractMappingProvider
{
    void SetTickMapping(
        string dataset,
        DateOnly definitionDate,
        ushort publisherId,
        uint instrumentId,
        string contractId,
        AssetTypeId assetTypeId,
        TickerContractDetails? contractDetails = null);
}

public interface ITickValueDateProvider
{
    DateOnly GetValueDate(DateTime timestampUtc);
}

public sealed class UtcTickValueDateProvider : ITickValueDateProvider
{
    public DateOnly GetValueDate(DateTime timestampUtc) => DateOnly.FromDateTime(timestampUtc.ToUniversalTime());
}

public sealed record TickAggregationOptions
{
    public required string Dataset { get; init; }
    public required DateOnly DefinitionDate { get; init; }
    public TimeSpan FeedStartTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan FeedStopTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
