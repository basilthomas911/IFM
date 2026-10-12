using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
internal struct NativeBatchResult
{
    public uint StructSize;
    public uint AbiVersion;
    public uint RecordsRead;
    public uint MoreAvailable;
    public ulong FirstSequence;
    public ulong LastSequence;
}
