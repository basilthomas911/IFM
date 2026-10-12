using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
internal struct NativeHistoricalRequest
{
    public uint StructSize;
    public uint AbiVersion;
    public NativeHistoricalSchema Schema;
    public uint InputSymbology;
    public NativeHistoricalFlags Flags;
    public uint SymbolCount;
    public NativeUtf8Slice Dataset;
    public long StartTimestampNanoseconds;
    public long EndTimestampNanoseconds;
    public ulong RecordLimit;
    public uint TimeoutMilliseconds;
    public uint Reserved;
}
