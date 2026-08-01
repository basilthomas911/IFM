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
    IDatabentoMarketDataQueries CreateMarketDataQueries(DatabentoFeedOptions options);
}

public enum ContractKind : byte
{
    Future = 1,
    CallOption = 2,
    PutOption = 3
}

public sealed record ContractDetail
{
    public required string Dataset { get; init; }
    public required string RawSymbol { get; init; }
    public required string Ticker { get; init; }
    public required string Underlying { get; init; }
    public required InstrumentKey Instrument { get; init; }
    public required ContractKind ContractKind { get; init; }
    public ulong RawInstrumentId { get; init; }
    public uint UnderlyingInstrumentId { get; init; }
    public int? ContractMultiplier { get; init; }
    public long? StrikePrice { get; init; }
    public long? MinimumPriceIncrement { get; init; }
    public long? MinimumPriceIncrementAmount { get; init; }
    public ulong? ExpirationTimestampNanoseconds { get; init; }
    public ulong? ActivationTimestampNanoseconds { get; init; }
    public DateOnly? MaturityDate { get; init; }
    public byte? MaturityWeek { get; init; }
    public required string Currency { get; init; }
    public required string SettlementCurrency { get; init; }
    public required string Exchange { get; init; }
    public required string SecurityType { get; init; }
    public required string Cfi { get; init; }
    public required string UnitOfMeasure { get; init; }
}

public interface IDatabentoMarketDataQueries
{
    OptionChainDefinitions GetChainDefinitions(
        OptionChainDefinitionRequest request,
        TimeSpan? timeout = null);

    uint ContractIdToInstrumentId(
        string contractId,
        TimeSpan? timeout = null);

    string InstrumentIdToContractId(
        uint instrumentId,
        TimeSpan? timeout = null);

    ContractDetail? GetContractDetail(
        string contractName,
        TimeSpan? timeout = null);

    IReadOnlyList<ContractDetail> GetContractDetails(
        string ticker,
        TimeSpan? timeout = null);

    IReadOnlyList<ContractDetail?> GetContractDetails(
        string[] contractNames,
        TimeSpan? timeout = null);
}

[Flags]
public enum OptionRightSelection : byte
{
    None = 0,
    Call = 1,
    Put = 2,
    Both = Call | Put
}

public enum OptionUniversePolicy : byte
{
    ParentOptionSymbol = 1,
    UnderlyingFuture = 2,
    ExplicitOptionRoots = 3
}

public sealed record OptionChainDefinitionRequest
{
    public required string Dataset { get; init; }
    public required string Underlying { get; init; }
    public required DateOnly MaturityDate { get; init; }
    public OptionUniversePolicy UniversePolicy { get; init; } =
        OptionUniversePolicy.ParentOptionSymbol;
    public IReadOnlyList<string> ExplicitOptionRoots { get; init; } = [];
    public OptionRightSelection Rights { get; init; } = OptionRightSelection.Both;
}

public sealed record OptionContractDefinition
{
    public required string Dataset { get; init; }
    public required string RawSymbol { get; init; }
    public required string Ticker { get; init; }
    public required string Underlying { get; init; }
    public required InstrumentKey Instrument { get; init; }
    public required OptionRightSelection Right { get; init; }
    public required decimal StrikePrice { get; init; }
    public required DateOnly MaturityDate { get; init; }
    public ulong? ExpirationTimestampNanoseconds { get; init; }
    public ulong? ActivationTimestampNanoseconds { get; init; }
    public long? MinimumPriceIncrement { get; init; }
    public int? ContractMultiplier { get; init; }
}

public sealed record OptionChainDefinitions
{
    public required string Dataset { get; init; }
    public required string Underlying { get; init; }
    public required DateOnly MaturityDate { get; init; }
    public required OptionUniversePolicy UniversePolicy { get; init; }
    public required OptionRightSelection Rights { get; init; }
    public required IReadOnlyList<OptionContractDefinition> Contracts { get; init; }
}

public sealed record OptionContractSelection(
    string RawSymbol,
    InstrumentKey Instrument,
    OptionRightSelection Right = OptionRightSelection.None);

public sealed record OptionChainSubscription
{
    public required string Underlying { get; init; }
    public required DateOnly MaturityDate { get; init; }
    public required IReadOnlyList<decimal> Strikes { get; init; }
    public OptionRightSelection Rights { get; init; } = OptionRightSelection.Both;
    public required IReadOnlyList<OptionContractDefinition> ResolvedContracts { get; init; }
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
    string? Warning)
{
    public bool TransportReady { get; init; }
    public bool TradingReady { get; init; }
    public int BaselineReadyInstrumentCount { get; init; }
    public int InstrumentCount { get; init; }
}
