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
    bool TryRead(TimeSpan timeout, out TBatch? batch);
    TBatch Read(TimeSpan timeout);
    bool IsCompleted { get; }
}

public readonly record struct InstrumentBatch64(
    InstrumentKey Instrument,
    MarketDataBatch64 Batch) : IDisposable
{
    public void Dispose() => Batch.Dispose();
}

public interface IMultiplexedTickerBatchReader : IDisposable
{
    bool TryRead(out InstrumentBatch64 batch);
    bool TryRead(TimeSpan timeout, out InstrumentBatch64 batch);
    bool TryRead(TimeSpan timeout, CancellationToken cancellationToken, out InstrumentBatch64 batch)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TryRead(timeout, out batch);
    }
    InstrumentBatch64 Read(TimeSpan timeout);
    bool IsCompleted { get; }
}

public interface IDatabentoTickerFeed : IDisposable
{
    void Subscribe(ReadOnlySpan<TickerSubscription> subscriptions, TimeSpan timeout);
    /// <summary>
    /// Prepares the feed, invokes <paramref name="startConsumer"/> after readers exist,
    /// and activates native ring publication only after that callback returns.
    /// </summary>
    void Start(TimeSpan timeout, Action<TimeSpan> startConsumer);
    void Stop(TimeSpan timeout);
    void Stop(TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stop(timeout);
    }
    ISynchronousBatchReader<MarketDataBatch64> GetReader(InstrumentKey instrument);
    IMultiplexedTickerBatchReader GetMultiplexedReader();
    IReadOnlyList<TickerInstrumentRegistration> GetInstruments();
    FeedHealthSnapshot GetHealth();
}

public interface IDatabentoFeedFactory
{
    IDatabentoTickerFeed CreateTickerFeed(DatabentoFeedOptions options);
    IDatabentoOptionChainFeed CreateOptionChainFeed(DatabentoFeedOptions options);
    IDatabentoMarketDataQueries CreateMarketDataQueries(DatabentoFeedOptions options);
    IDatabentoLatestPriceClient CreateLatestPriceClient(DatabentoFeedOptions options);
}

public enum LatestPricePolicy : byte
{
    LastTrade = 1,
    QuoteMidpoint = 2,
    Bid = 3,
    Ask = 4
}

public enum LatestPriceFreshnessPolicy : byte
{
    NextObserved = 1,
    ReplayLookbackThenLive = 2
}

[Flags]
public enum LatestPriceResultFlags : byte
{
    None = 0,
    BidValid = 1,
    AskValid = 2,
    TradeValid = 4,
    ReplayContributed = 8,
    FinalRecordLive = 16
}

public sealed record LatestPriceRequest
{
    public required string Dataset { get; init; }
    public required string Symbol { get; init; }
    public DatabentoInputSymbology InputSymbology { get; init; } =
        DatabentoInputSymbology.RawSymbol;
    public LatestPricePolicy PricePolicy { get; init; } =
        LatestPricePolicy.LastTrade;
    public LatestPriceFreshnessPolicy FreshnessPolicy { get; init; } =
        LatestPriceFreshnessPolicy.NextObserved;
    public TimeSpan ReplayLookback { get; init; }
}

public interface IDatabentoLatestPriceClient
{
    LatestPriceResult64 GetLatestPrice(
        LatestPriceRequest request,
        TimeSpan timeout);
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

public sealed record DatabentoContractDetailsQueryResult(
    DatabentoFeedStatus Status,
    IReadOnlyList<ContractDetail?> Details,
    string? ErrorMessage)
{
    public bool IsSuccess => Status == DatabentoFeedStatus.Ok;

    public static DatabentoContractDetailsQueryResult Success(
        IReadOnlyList<ContractDetail?> details) =>
        new(DatabentoFeedStatus.Ok, details, null);

    public static DatabentoContractDetailsQueryResult Failure(
        DatabentoFeedStatus status,
        string errorMessage) =>
        new(status, [], errorMessage);
}

public interface IDatabentoMarketDataQueries
{
    /// <summary>Latest provider definition interval for all supported instruments in this dataset.</summary>
    IReadOnlyList<ContractDetail> GetDatasetDefinitions(TimeSpan? timeout = null)
        => throw new NotSupportedException("Dataset-wide definition discovery is not implemented by this provider.");

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

    DatabentoContractDetailsQueryResult TryGetContractDetails(
        string[] contractNames,
        TimeSpan? timeout = null)
    {
        try
        {
            return DatabentoContractDetailsQueryResult.Success(
                GetContractDetails(contractNames, timeout));
        }
        catch (DatabentoFeedTimeoutException exception)
        {
            return DatabentoContractDetailsQueryResult.Failure(
                DatabentoFeedStatus.Timeout,
                exception.Message);
        }
        catch (DatabentoFeedException exception)
        {
            return DatabentoContractDetailsQueryResult.Failure(
                exception.Status,
                exception.Message);
        }
    }
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
    /// <summary>
    /// Prepares the feed, invokes <paramref name="startConsumer"/> after the reader exists,
    /// and activates native ring publication only after that callback returns.
    /// </summary>
    void Start(TimeSpan timeout, Action<TimeSpan> startConsumer);
    void Stop(TimeSpan timeout);
    ISynchronousBatchReader<MarketDataBatch64> Reader { get; }
    FeedHealthSnapshot GetHealth();
}

public enum FeedDrainStage
{
    Idle = 0,
    WaitingForNativeSignal = 1,
    ReadingNativeBatch = 2,
    RoutingNativeRecord = 3,
    FlushingPartialBatches = 4,
    PublishingManagedBatch = 5,
    ReadingNativeStatistics = 6,
    Completed = 7,
    Faulted = 8
}

public sealed record FeedDrainDiagnostics
{
    public required FeedDrainStage Stage { get; init; }
    public required long NativeReadCallCount { get; init; }
    public required uint LastNativeReadRecordCount { get; init; }
    public required ulong LastNativeReadFirstSequence { get; init; }
    public required ulong LastNativeReadLastSequence { get; init; }
    public required uint LastNativeReadRecordsRouted { get; init; }
    public required int CurrentNativeReadRecordIndex { get; init; }
    public required string CurrentRecordKind { get; init; }
    public required ushort CurrentPublisherId { get; init; }
    public required uint CurrentInstrumentId { get; init; }
    public required uint CurrentSourceSequence { get; init; }
    public required bool ManagedBatchPublishActive { get; init; }
    public required int ManagedBatchPublishRecordCount { get; init; }
    public required ushort ManagedBatchPublisherId { get; init; }
    public required uint ManagedBatchInstrumentId { get; init; }
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
    public int ChannelBatchCapacity { get; init; }
    public int ChannelBatchCount { get; init; }
    public int PoolBatchCapacity { get; init; }
    public int PoolFreeBatchCount { get; init; }
    public ulong DrainPassLimitHitCount { get; init; }
    public TimeSpan MaximumChannelFullWait { get; init; }
    public FeedProcessorSelectionKind ProcessorSelection { get; init; }
    public LogicalProcessorLocation? ResolvedNativeProducer { get; init; }
    public LogicalProcessorLocation? AlternateNativeProducer { get; init; }
    public LogicalProcessorLocation? ObservedNativeProducer { get; init; }
    public LogicalProcessorLocation? ResolvedManagedDrain { get; init; }
    public LogicalProcessorLocation? AlternateManagedDrain { get; init; }
    public LogicalProcessorLocation? ObservedManagedDrain { get; init; }
    public bool NativeProducerAffinityVerified { get; init; }
    public bool ManagedDrainAffinityVerified { get; init; }
    public ulong NativeProducerProcessorSamples { get; init; }
    public ulong NativeProducerProcessorMigrations { get; init; }
    public uint NativeProducerUniqueProcessors { get; init; }
    public ulong NativeProducerOffAssignmentSamples { get; init; }
    public ulong ManagedDrainProcessorSamples { get; init; }
    public ulong ManagedDrainProcessorMigrations { get; init; }
    public uint ManagedDrainUniqueProcessors { get; init; }
    public ulong ManagedDrainOffAssignmentSamples { get; init; }
    public FeedDrainDiagnostics? DrainDiagnostics { get; init; }
}
