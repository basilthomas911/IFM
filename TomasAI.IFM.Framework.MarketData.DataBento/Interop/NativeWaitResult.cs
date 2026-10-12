using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

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
