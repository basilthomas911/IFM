using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
internal unsafe struct NativeOptionChainSubscription
{
    public uint StructSize;
    public uint AbiVersion;
    public uint DataKinds;
    public uint ContractCount;
    public fixed ulong Reserved[2];
}
