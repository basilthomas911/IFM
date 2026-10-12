using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1815", Justification = "This sequential native ABI buffer is transported by pointer and is not a value identity.")]
[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
public readonly struct QuoteRecord64
{
    public readonly MarketRecordHeader32 Header;
    public readonly long BidPrice;
    public readonly long AskPrice;
    public readonly uint BidSize;
    public readonly uint AskSize;
    public readonly uint BidCount;
    public readonly uint AskCount;

    /// <summary>Initializes a new QuoteRecord64 instance.</summary>
    /// <param name="header">The common instrument, timestamp, and sequence header.</param>
    /// <param name="bidPrice">The best bid price.</param>
    /// <param name="askPrice">The best ask price.</param>
    /// <param name="bidSize">The quantity available at the best bid.</param>
    /// <param name="askSize">The quantity available at the best ask.</param>
    /// <param name="bidCount">The number of orders at the best bid.</param>
    /// <param name="askCount">The number of orders at the best ask.</param>
    public QuoteRecord64(
        MarketRecordHeader32 header, long bidPrice, long askPrice,
        uint bidSize, uint askSize, uint bidCount, uint askCount)
    {
        Header = header; BidPrice = bidPrice; AskPrice = askPrice;
        BidSize = bidSize; AskSize = askSize; BidCount = bidCount; AskCount = askCount;
    }
}
