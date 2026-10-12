using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

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
