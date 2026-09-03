using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal static class NativeConstants
{
    public const uint AbiVersion = 3;
    public const uint WaitInfinite = uint.MaxValue;
    public const ushort UnpinnedProcessor = ushort.MaxValue;
}

public enum DatabentoFeedStatus
{
    Ok = 0,
    InvalidArgument = 1,
    InvalidState = 2,
    AbiMismatch = 3,
    NoMemory = 4,
    OsError = 5,
    DatabentoError = 6,
    Timeout = 7,
    BufferTooSmall = 8,
    RingOverrun = 9,
    ConnectionLimit = 10,
    RateLimit = 11,
    SymbolResolutionFailed = 12,
    IncompleteDefinitions = 13,
    NotSupported = 14,
    InternalError = 15,
    AffinityConfigurationFailed = 16,
    PriorityConfigurationFailed = 17,
    MemoryLockFailed = 18,
    NumaConfigurationFailed = 19,
    CoreIsolationFailed = 20,
    StopDrainIncomplete = 21,
    ConnectionHung = 22,
    PageConfigurationFailed = 23
}

public enum MarketRecordKind : byte
{
    Quote = 1,
    Trade = 2,
    Mbo = 3,
    Statistics = 4,
    StatisticsReplayComplete = 5,
    TradeReplayComplete = 6
}

[Flags]
public enum MarketDataKinds : byte
{
    None = 0,
    Quote = 1,
    Trade = 2,
    MboOrderUpdate = 4,
    Statistics = 8,
    SessionVolume = 16
}

public enum FeedState : uint
{
    Created = 1,
    Subscribed = 2,
    Starting = 3,
    ConsumerSetup = 4,
    Running = 5,
    Stopping = 6,
    Stopped = 7,
    Faulted = 8
}

[Flags]
internal enum NativeWaitFlags : uint
{
    None = 0,
    Data = 1,
    Terminal = 2,
    Fault = 4
}

internal enum NativeContractQueryKind : uint
{
    Exact = 1,
    Ticker = 2,
    InstrumentId = 3
}

[Flags]
internal enum NativeContractDetailFlags : uint
{
    None = 0,
    Found = 1,
    HasStrikePrice = 2,
    HasMinimumPriceIncrement = 4,
    HasExpiration = 8,
    HasActivation = 16,
    HasMaturityDate = 32,
    HasMultiplier = 64,
    HasMinimumPriceIncrementAmount = 128,
    HasMaturityWeek = 256
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
public readonly struct MarketRecordHeader32
{
    public readonly uint InstrumentId;
    public readonly ushort PublisherId;
    public readonly MarketRecordKind RecordKind;
    public readonly byte Flags;
    public readonly long EventTimestampNanoseconds;
    public readonly long ReceiveTimestampNanoseconds;
    public readonly uint Sequence;
    public readonly ushort SourceSchema;
    public readonly ushort Reserved;

    public MarketRecordHeader32(
        uint instrumentId, ushort publisherId, MarketRecordKind recordKind,
        byte flags, long eventTimestampNanoseconds, long receiveTimestampNanoseconds,
        uint sequence, ushort sourceSchema = 0)
    {
        InstrumentId = instrumentId; PublisherId = publisherId; RecordKind = recordKind;
        Flags = flags; EventTimestampNanoseconds = eventTimestampNanoseconds;
        ReceiveTimestampNanoseconds = receiveTimestampNanoseconds;
        Sequence = sequence; SourceSchema = sourceSchema; Reserved = 0;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
public readonly struct QuoteRecord64
{
    public readonly MarketRecordHeader32 Header;
    public readonly long BidPrice;
    public readonly long AskPrice;
    public readonly uint BidSize;
    public readonly uint AskSize;
    public readonly uint BidCount;
    public readonly uint AskCount;

    public QuoteRecord64(
        MarketRecordHeader32 header, long bidPrice, long askPrice,
        uint bidSize, uint askSize, uint bidCount, uint askCount)
    {
        Header = header; BidPrice = bidPrice; AskPrice = askPrice;
        BidSize = bidSize; AskSize = askSize; BidCount = bidCount; AskCount = askCount;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
public readonly struct TradeRecord64
{
    public readonly MarketRecordHeader32 Header;
    public readonly long Price;
    public readonly uint Size;
    public readonly byte Action;
    public readonly byte Side;
    public readonly byte DbnFlags;
    public readonly byte Depth;
    public readonly int TimestampInDeltaNanoseconds;
    public readonly byte ChannelId;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    public readonly long TimestampOutNanoseconds;

    public TradeRecord64(
        MarketRecordHeader32 header, long price, uint size, byte action,
        byte side, byte dbnFlags)
    {
        Header = header; Price = price; Size = size; Action = action; Side = side;
        DbnFlags = dbnFlags; Depth = 0; TimestampInDeltaNanoseconds = 0;
        ChannelId = 0; _reserved0 = _reserved1 = _reserved2 = 0;
        TimestampOutNanoseconds = 0;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
public readonly struct MboRecord64
{
    public readonly MarketRecordHeader32 Header;
    public readonly ulong OrderId;
    public readonly long Price;
    public readonly uint Size;
    public readonly int TimestampInDeltaNanoseconds;
    public readonly byte Action;
    public readonly byte Side;
    public readonly byte DbnFlags;
    public readonly byte ChannelId;
    private readonly uint _reserved;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
public readonly struct StatisticsRecord64
{
    public readonly MarketRecordHeader32 Header;
    public readonly long Price;
    public readonly long Quantity;
    public readonly long ReferenceTimestampNanoseconds;
    public readonly ushort StatisticType;
    public readonly ushort ChannelId;
    public readonly byte UpdateAction;
    public readonly byte StatisticFlags;
    private readonly ushort _reserved16;

    public StatisticsRecord64(
        MarketRecordHeader32 header,
        long price,
        long quantity,
        long referenceTimestampNanoseconds,
        ushort statisticType,
        ushort channelId,
        byte updateAction,
        byte statisticFlags)
    {
        Header = header;
        Price = price;
        Quantity = quantity;
        ReferenceTimestampNanoseconds = referenceTimestampNanoseconds;
        StatisticType = statisticType;
        ChannelId = channelId;
        UpdateAction = updateAction;
        StatisticFlags = statisticFlags;
        _reserved16 = 0;
    }
}

[StructLayout(LayoutKind.Explicit, Pack = 8, Size = 64)]
public readonly struct MarketRecord64
{
    [FieldOffset(0)] public readonly MarketRecordHeader32 Header;
    [FieldOffset(0)] public readonly QuoteRecord64 Quote;
    [FieldOffset(0)] public readonly TradeRecord64 Trade;
    [FieldOffset(0)] public readonly MboRecord64 Mbo;
    [FieldOffset(0)] public readonly StatisticsRecord64 Statistics;

    public MarketRecord64(QuoteRecord64 quote)
    {
        this = default;
        Quote = quote;
    }

    public MarketRecord64(TradeRecord64 trade)
    {
        this = default;
        Trade = trade;
    }

    public MarketRecord64(StatisticsRecord64 statistics)
    {
        this = default;
        Statistics = statistics;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 128)]
internal unsafe struct NativeFeedConfig
{
    public uint StructSize;
    public uint AbiVersion;
    public uint DataSource;
    public uint FeedKind;
    public ulong RingMemoryBytes;
    public uint SpinIterations;
    public uint RingFullTimeoutMicroseconds;
    public uint SyntheticRecordCount;
    public uint SyntheticRecordsPerSecond;
    public uint SyntheticInstrumentCount;
    public uint HeartbeatIntervalMilliseconds;
    public uint Flags;
    public ushort ProducerProcessorGroup;
    public ushort ProducerLogicalProcessor;
    public ushort DrainProcessorGroup;
    public ushort DrainLogicalProcessor;
    public int ProducerPriority;
    public int DrainPriority;
    public ushort NumaNode;
    public ushort Reserved16;
    public uint DatasetOffset;
    public uint DatasetLength;
    public ulong SyntheticStartSequence;
    public uint ForcedMigrationIntervalRecords;
    public ushort ProducerAlternateProcessorGroup;
    public ushort ProducerAlternateLogicalProcessor;
    public ushort DrainAlternateProcessorGroup;
    public ushort DrainAlternateLogicalProcessor;
    public uint Reserved32;
    public ulong StatisticsReplayStartTimestampNanoseconds;
    public ulong TradeReplayStartTimestampNanoseconds;
    public fixed ulong Reserved[1];
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
internal struct NativeTickerSubscription
{
    public uint StructSize;
    public uint AbiVersion;
    public uint SymbolOffset;
    public uint SymbolLength;
    public uint InputSymbology;
    public uint DataKinds;
    public ulong Reserved;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
internal struct NativeTickerInstrumentMapping
{
    public uint StructSize;
    public uint AbiVersion;
    public uint SubscriptionIndex;
    public uint InstrumentId;
    public ushort PublisherId;
    public ushort Reserved16;
    public uint RequestedSymbolOffset;
    public ushort RequestedSymbolLength;
    public ushort RawSymbolLength;
    public uint RawSymbolOffset;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
internal unsafe struct NativeOptionChainSubscription
{
    public uint StructSize;
    public uint AbiVersion;
    public uint DataKinds;
    public uint ContractCount;
    public fixed ulong Reserved[2];
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
internal struct NativeOptionContractSelection
{
    public uint StructSize;
    public uint AbiVersion;
    public uint InstrumentId;
    public ushort PublisherId;
    public byte OptionRight;
    public byte Reserved8;
    public uint RawSymbolOffset;
    public uint RawSymbolLength;
    public ulong Reserved;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
internal struct NativeWaitResult
{
    public uint StructSize;
    public uint AbiVersion;
    public NativeWaitFlags Flags;
    public FeedState State;
    public ulong AvailableRecords;
    public DatabentoFeedStatus TerminalStatus;
    public uint Reserved;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
internal struct NativeBatchResult
{
    public uint StructSize;
    public uint AbiVersion;
    public uint RecordsRead;
    public uint MoreAvailable;
    public ulong FirstSequence;
    public ulong LastSequence;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 128)]
internal unsafe struct NativeFeedStats
{
    public uint StructSize;
    public uint AbiVersion;
    public FeedState State;
    public DatabentoFeedStatus TerminalStatus;
    public ulong RingCapacityRecords;
    public ulong RingUsedRecords;
    public ulong RingHighWaterRecords;
    public ulong RecordsProduced;
    public ulong RecordsConsumed;
    public ulong SignalCount;
    public ulong WaitCount;
    public ulong RingFullEpisodes;
    public ulong RingOverruns;
    public ulong AllocatedReadBufferRecords;
    public ushort ObservedProducerProcessorGroup;
    public ushort ObservedProducerLogicalProcessor;
    public uint ProducerAffinityVerified;
    public ulong ProducerProcessorSampleCount;
    public ulong ProducerProcessorMigrationCount;
    public uint ProducerOffAssignmentCount;
    public uint ProducerUniqueProcessorCount;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
internal unsafe struct NativeWatchdogSnapshot
{
    public uint StructSize;
    public uint AbiVersion;
    public uint EntryCount;
    public uint RequiredCount;
    public ulong ObservedMonotonicNanoseconds;
    public ulong SnapshotSequence;
    public fixed ulong Reserved[4];
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 320)]
internal unsafe struct NativeWatchdogFeedStatus
{
    public uint StructSize;
    public uint AbiVersion;
    public ulong FeedInstanceId;
    public ulong GenerationId;
    public uint FeedKind;
    public uint MajorStatus;
    public FeedState State;
    public DatabentoFeedStatus TerminalStatus;
    public uint ProducerAlive;
    public uint ConsumerReady;
    public uint ExpectedSubscriptions;
    public uint ReceivedSubscriptions;
    public ulong HeartbeatCount;
    public ulong ProviderMessageCount;
    public ulong LastHeartbeatMonotonicNanoseconds;
    public ulong LastProviderMessageMonotonicNanoseconds;
    public ulong RecordsProduced;
    public ulong RecordsConsumed;
    public ulong RingCapacityRecords;
    public ulong RingUsedRecords;
    public ulong RingHighWaterRecords;
    public ulong RingOverruns;
    public fixed byte Dataset[56];
    public fixed byte FailureDetail[128];
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 8)]
internal struct NativeUtf8Slice
{
    public uint Offset;
    public uint Length;
}

internal enum NativeHistoricalSchema : uint
{
    Definition = 1,
    OhlcvOneMinute = 2,
    Trades = 3,
    Statistics = 4,
    OhlcvDaily = 5
}

[Flags]
internal enum NativeHistoricalFlags : uint
{
    None = 0,
    Synthetic = 1
}

internal enum NativeHistoricalRecordKind : uint
{
    Definition = 1,
    Ohlcv = 2,
    Trade = 3,
    Statistic = 4
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
internal struct NativeHistoricalRequest
{
    public uint StructSize;
    public uint AbiVersion;
    public NativeHistoricalSchema Schema;
    public uint InputSymbology;
    public NativeHistoricalFlags Flags;
    public uint SymbolCount;
    public NativeUtf8Slice Dataset;
    public long StartTimestampNanoseconds;
    public long EndTimestampNanoseconds;
    public ulong RecordLimit;
    public uint TimeoutMilliseconds;
    public uint Reserved;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
internal struct NativeHistoricalEstimate
{
    public uint StructSize;
    public uint AbiVersion;
    public double EstimatedCostUsd;
    public ulong EstimatedBytes;
    public ulong EstimatedRecords;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 120)]
internal unsafe struct NativeHistoricalRecord
{
    public uint StructSize;
    public uint AbiVersion;
    public NativeHistoricalRecordKind RecordKind;
    public NativeHistoricalSchema Schema;
    public uint InstrumentId;
    public ushort PublisherId;
    public ushort ConditionFlags;
    public long EventTimestampNanoseconds;
    public long SourceSequence;
    public long OpenPrice;
    public long HighPrice;
    public long LowPrice;
    public long CloseOrTradePrice;
    public ulong VolumeOrSize;
    public byte Action;
    public byte Side;
    private fixed byte _reserved[6];
    public fixed byte Symbol[32];
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 24)]
internal struct NativeHistoricalBatch
{
    public uint StructSize;
    public uint AbiVersion;
    public uint RecordsRead;
    public uint MoreAvailable;
    public ulong BatchOrdinal;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
internal unsafe struct NativeContractQuery
{
    public uint StructSize;
    public uint AbiVersion;
    public NativeContractQueryKind QueryKind;
    public uint TimeoutMilliseconds;
    public uint DatasetOffset;
    public uint DatasetLength;
    public uint SymbolCount;
    public uint Reserved32;
    public fixed ulong Reserved[4];
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 192)]
internal unsafe struct NativeContractDetail
{
    public uint StructSize;
    public uint AbiVersion;
    public NativeContractDetailFlags Flags;
    public uint InstrumentId;
    public ushort PublisherId;
    public byte ContractKind;
    public byte MaturityMonth;
    public byte MaturityDay;
    public byte MaturityWeek;
    public ushort MaturityYear;
    public uint UnderlyingId;
    public int ContractMultiplier;
    public ulong RawInstrumentId;
    public long StrikePrice;
    public long MinimumPriceIncrement;
    public long MinimumPriceIncrementAmount;
    public ulong ExpirationTimestampNanoseconds;
    public ulong ActivationTimestampNanoseconds;
    public NativeUtf8Slice RawSymbol;
    public NativeUtf8Slice Asset;
    public NativeUtf8Slice Underlying;
    public NativeUtf8Slice Currency;
    public NativeUtf8Slice SettlementCurrency;
    public NativeUtf8Slice Exchange;
    public NativeUtf8Slice SecurityType;
    public NativeUtf8Slice Cfi;
    public NativeUtf8Slice UnitOfMeasure;
    public fixed ulong Reserved[5];
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 88)]
internal unsafe struct NativeLatestPriceRequest
{
    public uint StructSize;
    public uint AbiVersion;
    public LatestPricePolicy SelectedPolicy;
    private readonly byte _selectedPolicyPadding0;
    private readonly byte _selectedPolicyPadding1;
    private readonly byte _selectedPolicyPadding2;
    public LatestPriceFreshnessPolicy FreshnessPolicy;
    private readonly byte _freshnessPadding0;
    private readonly byte _freshnessPadding1;
    private readonly byte _freshnessPadding2;
    public DatabentoInputSymbology InputSymbology;
    private readonly byte _symbologyPadding0;
    private readonly byte _symbologyPadding1;
    private readonly byte _symbologyPadding2;
    public uint ReplayLookbackMilliseconds;
    public NativeUtf8Slice Dataset;
    public NativeUtf8Slice Symbol;
    public byte* Utf8Blob;
    public uint Utf8BlobBytes;
    public uint Reserved32;
    public fixed ulong Reserved[4];
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
public readonly struct LatestPriceResult64
{
    public readonly uint InstrumentId;
    public readonly ushort PublisherId;
    public readonly LatestPricePolicy SelectedPolicy;
    public readonly LatestPriceResultFlags Flags;
    public readonly long SelectedPrice;
    public readonly long BidPrice;
    public readonly long AskPrice;
    public readonly long LastTradePrice;
    public readonly long EventTimestampNanoseconds;
    public readonly long ReceiveTimestampNanoseconds;
    public readonly uint BidSize;
    public readonly uint AskSize;

    internal LatestPriceResult64(
        uint instrumentId,
        ushort publisherId,
        LatestPricePolicy selectedPolicy,
        LatestPriceResultFlags flags,
        long selectedPrice,
        long bidPrice = 0,
        long askPrice = 0,
        long lastTradePrice = 0,
        long eventTimestampNanoseconds = 0,
        long receiveTimestampNanoseconds = 0,
        uint bidSize = 0,
        uint askSize = 0)
    {
        InstrumentId = instrumentId;
        PublisherId = publisherId;
        SelectedPolicy = selectedPolicy;
        Flags = flags;
        SelectedPrice = selectedPrice;
        BidPrice = bidPrice;
        AskPrice = askPrice;
        LastTradePrice = lastTradePrice;
        EventTimestampNanoseconds = eventTimestampNanoseconds;
        ReceiveTimestampNanoseconds = receiveTimestampNanoseconds;
        BidSize = bidSize;
        AskSize = askSize;
    }

    public bool HasBid => (Flags & LatestPriceResultFlags.BidValid) != 0;
    public bool HasAsk => (Flags & LatestPriceResultFlags.AskValid) != 0;
    public bool HasLastTrade => (Flags & LatestPriceResultFlags.TradeValid) != 0;
    public bool UsedReplay =>
        (Flags & LatestPriceResultFlags.ReplayContributed) != 0;
    public bool IsLive =>
        (Flags & LatestPriceResultFlags.FinalRecordLive) != 0;
}
