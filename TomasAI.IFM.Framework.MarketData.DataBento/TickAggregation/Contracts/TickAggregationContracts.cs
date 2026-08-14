using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

public interface ITickAggregationService : ITickerDataReaderFactory, IAsyncDisposable
{
    bool IsRunning { get; }
    TickAggregationContractStatus GetContractStatus(string contractId);
    TickAggregationTickerStatus GetTickerStatus(string futuresContractId);
    ValueTask StartAsync();
    ValueTask StopAsync();
}

public readonly record struct TickAggregationContractStatus(
    string ContractId,
    AssetTypeId AssetTypeId,
    bool ServiceRunning,
    bool ContractConfigured,
    bool ContractRunning);

public readonly record struct TickAggregationTickerStatus(
    string FuturesContractId,
    bool ServiceRunning,
    bool TickerConfigured,
    bool TickerRunning);

public interface ITickAggregationMetricsSource
{
    TickAggregationMetricsSnapshot GetMetrics();
}

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
    int ActiveTickers,
    int ServiceOwnedQuoteBuffers);

public readonly record struct TickContractMapping(
    string Dataset,
    DateOnly DefinitionDate,
    ushort PublisherId,
    uint InstrumentId,
    string ContractId,
    AssetTypeId AssetTypeId,
    TickerContractDetails? ContractDetails = null);

/// <summary>
/// Controls transient delivery when a contract obtains its first lease or
/// releases its final lease.
/// </summary>
public interface ITickerLeaseRouteController
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
    public TimeSpan ReaderPollTimeout { get; init; } = TimeSpan.FromMilliseconds(50);
}
