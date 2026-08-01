using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal static class NativeConstants
{
    public const uint AbiVersion = 1;
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
    Mbo = 3
}

[Flags]
public enum MarketDataKinds : byte
{
    None = 0,
    Quote = 1,
    Trade = 2,
    MboOrderUpdate = 4
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

[StructLayout(LayoutKind.Explicit, Pack = 8, Size = 64)]
public readonly struct MarketRecord64
{
    [FieldOffset(0)] public readonly MarketRecordHeader32 Header;
    [FieldOffset(0)] public readonly QuoteRecord64 Quote;
    [FieldOffset(0)] public readonly TradeRecord64 Trade;
    [FieldOffset(0)] public readonly MboRecord64 Mbo;
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
    public fixed ulong Reserved[5];
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
    public fixed ulong Reserved[4];
}

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 8)]
internal struct NativeUtf8Slice
{
    public uint Offset;
    public uint Length;
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
