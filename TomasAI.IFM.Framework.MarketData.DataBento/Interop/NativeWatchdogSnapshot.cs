using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

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
