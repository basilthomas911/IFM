using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public enum DatabentoInputSymbology : byte
{
    RawSymbol = 1,
    InstrumentId = 2
}

public readonly record struct InstrumentKey(
    ushort PublisherId,
    uint InstrumentId);

public readonly record struct TickerSubscription(
    string Symbol,
    DatabentoInputSymbology InputSymbology,
    MarketDataKinds DataKinds);

public sealed record TickerInstrumentRegistration(
    string RequestedSymbol,
    string RawSymbol,
    InstrumentKey Instrument);

public interface ISynchronousBatchReader<TBatch>
    where TBatch : class, IDisposable
{
    bool TryRead(out TBatch? batch);
    TBatch Read(TimeSpan timeout);
    bool IsCompleted { get; }
}

public interface IDatabentoTickerFeed : IDisposable
{
    void Subscribe(ReadOnlySpan<TickerSubscription> subscriptions, TimeSpan timeout);
    void Start(TimeSpan timeout);
    void Stop(TimeSpan timeout);
    ISynchronousBatchReader<MarketDataBatch64> GetReader(InstrumentKey instrument);
    IReadOnlyList<TickerInstrumentRegistration> GetInstruments();
    FeedHealthSnapshot GetHealth();
}

public interface IDatabentoFeedFactory
{
    IDatabentoTickerFeed CreateTickerFeed(DatabentoFeedOptions options);
    IDatabentoOptionChainFeed CreateOptionChainFeed(DatabentoFeedOptions options);
}

public sealed record OptionContractSelection(
    string RawSymbol,
    InstrumentKey Instrument);

public sealed record OptionChainSubscription
{
    public required string Underlying { get; init; }
    public required DateOnly MaturityDate { get; init; }
    public required IReadOnlyList<OptionContractSelection> ResolvedContracts { get; init; }
    public MarketDataKinds DataKinds { get; init; } =
        MarketDataKinds.Quote | MarketDataKinds.Trade;
}

public interface IDatabentoOptionChainFeed : IDisposable
{
    void Subscribe(OptionChainSubscription subscription, TimeSpan timeout);
    void Start(TimeSpan timeout);
    void Stop(TimeSpan timeout);
    ISynchronousBatchReader<MarketDataBatch64> Reader { get; }
    FeedHealthSnapshot GetHealth();
}

public sealed record FeedHealthSnapshot(
    FeedState State,
    DatabentoFeedStatus TerminalStatus,
    ulong RingCapacityRecords,
    ulong RingUsedRecords,
    ulong RingHighWaterRecords,
    ulong RecordsProduced,
    ulong RecordsConsumed,
    ulong BatchesPublished,
    ulong ChannelFullCount,
    ulong PoolMissCount,
    long DrainAllocatedBytes,
    string? Warning);
