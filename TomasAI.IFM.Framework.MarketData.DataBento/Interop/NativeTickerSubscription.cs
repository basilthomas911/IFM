using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

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
