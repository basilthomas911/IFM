using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1815", Justification = "This sequential native ABI buffer is transported by pointer and is not a value identity.")]
[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
public readonly struct MboRecord64
{
    public readonly MarketRecordHeader32 Header;
    public readonly ulong OrderId;
    public readonly long Price;
    public readonly uint Size;
    public readonly int TimestampInDeltaNanoseconds;
    public readonly byte Action;
    public readonly byte Side;
    public readonly byte DbnFlags;
    public readonly byte ChannelId;
    private readonly uint _reserved;
}
