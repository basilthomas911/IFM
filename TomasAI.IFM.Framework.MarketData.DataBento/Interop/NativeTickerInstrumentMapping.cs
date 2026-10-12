using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

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
