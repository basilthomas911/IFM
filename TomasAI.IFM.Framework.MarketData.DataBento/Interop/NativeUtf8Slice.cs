using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 8)]
internal struct NativeUtf8Slice
{
    public uint Offset;
    public uint Length;
}
