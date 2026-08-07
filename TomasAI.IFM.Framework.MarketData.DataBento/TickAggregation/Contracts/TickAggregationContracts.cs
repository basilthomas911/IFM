using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

public interface ITickAggregationService : IAsyncDisposable
{
    bool IsRunning { get; }
    ValueTask StartAsync();
    ValueTask StopAsync();
}

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
    AssetTypeId AssetTypeId);

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
        AssetTypeId assetTypeId);
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
