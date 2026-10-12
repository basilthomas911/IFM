using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

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
