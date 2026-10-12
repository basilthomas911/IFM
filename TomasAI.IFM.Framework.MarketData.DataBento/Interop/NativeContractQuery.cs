using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
internal unsafe struct NativeContractQuery
{
    public uint StructSize;
    public uint AbiVersion;
    public NativeContractQueryKind QueryKind;
    public uint TimeoutMilliseconds;
    public uint DatasetOffset;
    public uint DatasetLength;
    public uint SymbolCount;
    public uint Reserved32;
    public fixed ulong Reserved[4];
}
