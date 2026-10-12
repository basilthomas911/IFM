using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 24)]
internal struct NativeHistoricalBatch
{
    public uint StructSize;
    public uint AbiVersion;
    public uint RecordsRead;
    public uint MoreAvailable;
    public ulong BatchOrdinal;
}
