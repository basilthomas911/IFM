using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1815", Justification = "This sequential native ABI buffer is transported by pointer and is not a value identity.")]
[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
public readonly struct TradeRecord64
{
    public readonly MarketRecordHeader32 Header;
    public readonly long Price;
    public readonly uint Size;
    public readonly byte Action;
    public readonly byte Side;
    public readonly byte DbnFlags;
    public readonly byte Depth;
    public readonly int TimestampInDeltaNanoseconds;
    public readonly byte ChannelId;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    public readonly long TimestampOutNanoseconds;

    /// <summary>Initializes a new TradeRecord64 instance.</summary>
    /// <param name="header">The common instrument, timestamp, and sequence header.</param>
    /// <param name="price">The trade or statistics price.</param>
    /// <param name="size">The trade quantity.</param>
    /// <param name="action">The trade action identifying a new trade or correction.</param>
    /// <param name="side">The side of the trade reported by the provider.</param>
    /// <param name="dbnFlags">The original DBN record flags.</param>
    public TradeRecord64(
        MarketRecordHeader32 header, long price, uint size, byte action,
        byte side, byte dbnFlags)
    {
        Header = header; Price = price; Size = size; Action = action; Side = side;
        DbnFlags = dbnFlags; Depth = 0; TimestampInDeltaNanoseconds = 0;
        ChannelId = 0; _reserved0 = _reserved1 = _reserved2 = 0;
        TimestampOutNanoseconds = 0;
    }
}
