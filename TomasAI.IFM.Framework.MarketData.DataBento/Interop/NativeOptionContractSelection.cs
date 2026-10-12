using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

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
