using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

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
