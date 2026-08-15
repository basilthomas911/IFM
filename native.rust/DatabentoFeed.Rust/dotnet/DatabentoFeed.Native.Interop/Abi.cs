using System.Runtime.InteropServices;

namespace DatabentoFeed.Native.Interop;

public static class Dbf
{
    public const uint AbiVersion = 1;
    public const int Ok = 0;
    public const int InvalidArgument = 1;
    public const int InvalidState = 2;
    public const int AbiMismatch = 3;
    public const int Timeout = 7;
    public const int BufferTooSmall = 8;
    public const int RingOverrun = 9;
    public const int NotSupported = 14;
    public const uint FeedTicker = 1;
    public const uint DataSourceSynthetic = 1;
    public const uint MarketDataQuote = 1;
    public const uint MarketDataTrade = 2;
    public const uint MarketDataMbo = 4;
    public const uint WaitData = 1;
    public const uint WaitTerminal = 2;
    public const uint WaitFault = 4;
    public const uint StateStopped = 7;
    public const uint StateFaulted = 8;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct FeedConfigV1
{
    public uint StructSize, AbiVersion, DataSource, FeedKind;
    public ulong RingMemoryBytes;
    public uint SpinIterations, RingFullTimeoutUs, SyntheticRecordCount,
        SyntheticRecordsPerSecond, SyntheticInstrumentCount, HeartbeatIntervalMs, Flags;
    public ushort ProducerProcessorGroup, ProducerLogicalProcessor, DrainProcessorGroup,
        DrainLogicalProcessor;
    public int ProducerPriority, DrainPriority;
    public ushort NumaNode, Reserved16;
    public uint DatasetOffset, DatasetLength;
    public ulong SyntheticStartSequence;
    public uint ForcedMigrationIntervalRecords;
    public ushort ProducerAlternateProcessorGroup, ProducerAlternateLogicalProcessor,
        DrainAlternateProcessorGroup, DrainAlternateLogicalProcessor;
    public uint Reserved32;
    public fixed ulong Reserved[3];
}

[StructLayout(LayoutKind.Sequential)]
public struct TickerSubscriptionV1
{
    public uint StructSize, AbiVersion, SymbolOffset, SymbolLength, InputSymbology, DataKinds;
    public ulong Reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct TickerInstrumentMappingV1
{
    public uint StructSize, AbiVersion, SubscriptionIndex, InstrumentId;
    public ushort PublisherId, Reserved16;
    public uint RequestedSymbolOffset;
    public ushort RequestedSymbolLength, RawSymbolLength;
    public uint RawSymbolOffset;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct OptionChainSubscriptionV1
{
    public uint StructSize, AbiVersion, DataKinds, ContractCount;
    public fixed ulong Reserved[2];
}

[StructLayout(LayoutKind.Sequential)]
public struct OptionContractSelectionV1
{
    public uint StructSize, AbiVersion, InstrumentId;
    public ushort PublisherId;
    public byte OptionRight, Reserved8;
    public uint RawSymbolOffset, RawSymbolLength;
    public ulong Reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct WaitResultV1
{
    public uint StructSize, AbiVersion, Flags, State;
    public ulong AvailableRecords;
    public int TerminalStatus;
    public uint Reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct BatchResultV1
{
    public uint StructSize, AbiVersion, RecordsRead, MoreAvailable;
    public ulong FirstSequence, LastSequence;
}

[StructLayout(LayoutKind.Sequential)]
public struct StatsV1
{
    public uint StructSize, AbiVersion, State;
    public int TerminalStatus;
    public ulong RingCapacityRecords, RingUsedRecords, RingHighWaterRecords, RecordsProduced,
        RecordsConsumed, SignalCount, WaitCount, RingFullEpisodes, RingOverruns,
        AllocatedReadBufferRecords;
    public ushort ObservedProducerProcessorGroup, ObservedProducerLogicalProcessor;
    public uint ProducerAffinityVerified;
    public ulong ProducerProcessorSampleCount, ProducerProcessorMigrationCount;
    public uint ProducerOffAssignmentCount, ProducerUniqueProcessorCount;
}

[StructLayout(LayoutKind.Sequential)]
public struct Utf8SliceV1 { public uint Offset, Length; }

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ContractQueryV1
{
    public uint StructSize, AbiVersion, QueryKind, TimeoutMs, DatasetOffset, DatasetLength,
        SymbolCount, Reserved32;
    public fixed ulong Reserved[4];
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ContractDetailV1 { public fixed byte Bytes[192]; }

[StructLayout(LayoutKind.Sequential)]
public unsafe struct LatestPriceRequestV1
{
    public uint StructSize, AbiVersion, SelectedPolicy, FreshnessPolicy, InputSymbology,
        ReplayLookbackMs;
    public Utf8SliceV1 Dataset, Symbol;
    public byte* Utf8Blob;
    public uint Utf8BlobBytes, Reserved32;
    public fixed ulong Reserved[4];
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct LatestPriceResult64 { public fixed byte Bytes[64]; }

[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct MarketRecord64
{
    [FieldOffset(0)] public uint InstrumentId;
    [FieldOffset(4)] public ushort PublisherId;
    [FieldOffset(6)] public byte RecordKind;
    [FieldOffset(7)] public byte Flags;
    [FieldOffset(8)] public long TsEventNs;
    [FieldOffset(16)] public long TsRecvNs;
    [FieldOffset(24)] public uint Sequence;
    [FieldOffset(26)] public ushort SourceSchema;
    [FieldOffset(32)] public long Value0;
    [FieldOffset(40)] public long Value1;
    [FieldOffset(48)] public ulong Value2;
    [FieldOffset(56)] public long Value3;
}
