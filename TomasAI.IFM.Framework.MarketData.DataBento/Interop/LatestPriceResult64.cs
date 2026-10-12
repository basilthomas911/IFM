using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1815", Justification = "This sequential native ABI buffer is transported by pointer and is not a value identity.")]
[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
public readonly struct LatestPriceResult64
{
    public readonly uint InstrumentId;
    public readonly ushort PublisherId;
    public readonly LatestPricePolicy SelectedPolicy;
    public readonly LatestPriceResultFlags Flags;
    public readonly long SelectedPrice;
    public readonly long BidPrice;
    public readonly long AskPrice;
    public readonly long LastTradePrice;
    public readonly long EventTimestampNanoseconds;
    public readonly long ReceiveTimestampNanoseconds;
    public readonly uint BidSize;
    public readonly uint AskSize;

    /// <summary>Initializes a new LatestPriceResult64 instance.</summary>
    /// <param name="instrumentId">The provider instrument identifier.</param>
    /// <param name="publisherId">The provider publisher identifier.</param>
    /// <param name="selectedPolicy">The policy used to select the returned price.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="selectedPrice">The price selected by the requested latest-price policy.</param>
    /// <param name="bidPrice">The best bid price.</param>
    /// <param name="askPrice">The best ask price.</param>
    /// <param name="lastTradePrice">The most recent trade price reported by the provider.</param>
    /// <param name="eventTimestampNanoseconds">The source event timestamp in nanoseconds since the Unix epoch.</param>
    /// <param name="receiveTimestampNanoseconds">The receive timestamp in nanoseconds since the Unix epoch.</param>
    /// <param name="bidSize">The quantity available at the best bid.</param>
    /// <param name="askSize">The quantity available at the best ask.</param>
    internal LatestPriceResult64(
        uint instrumentId,
        ushort publisherId,
        LatestPricePolicy selectedPolicy,
        LatestPriceResultFlags flags,
        long selectedPrice,
        long bidPrice = 0,
        long askPrice = 0,
        long lastTradePrice = 0,
        long eventTimestampNanoseconds = 0,
        long receiveTimestampNanoseconds = 0,
        uint bidSize = 0,
        uint askSize = 0)
    {
        InstrumentId = instrumentId;
        PublisherId = publisherId;
        SelectedPolicy = selectedPolicy;
        Flags = flags;
        SelectedPrice = selectedPrice;
        BidPrice = bidPrice;
        AskPrice = askPrice;
        LastTradePrice = lastTradePrice;
        EventTimestampNanoseconds = eventTimestampNanoseconds;
        ReceiveTimestampNanoseconds = receiveTimestampNanoseconds;
        BidSize = bidSize;
        AskSize = askSize;
    }

    public bool HasBid => (Flags & LatestPriceResultFlags.BidValid) != LatestPriceResultFlags.None;
    public bool HasAsk => (Flags & LatestPriceResultFlags.AskValid) != LatestPriceResultFlags.None;
    public bool HasLastTrade => (Flags & LatestPriceResultFlags.TradeValid) != LatestPriceResultFlags.None;
    public bool UsedReplay =>
        (Flags & LatestPriceResultFlags.ReplayContributed) != LatestPriceResultFlags.None;
    public bool IsLive =>
        (Flags & LatestPriceResultFlags.FinalRecordLive) != LatestPriceResultFlags.None;
}
