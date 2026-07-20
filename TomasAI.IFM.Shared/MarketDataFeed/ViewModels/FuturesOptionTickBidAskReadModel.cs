using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// Represents a futures option tick price snapshot including bid/ask prices and sizes.
/// </summary>
/// <remarks>
/// MessagePack serializable. Follows the same pattern as <see cref="FuturesOptionTickDataV2ReadModel"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionTickBidAskReadModel
{
    /// <summary>Date/time when the tick was recorded.</summary>
    [Key(0)]
    public DateTime TickDate { get; init; }

    /// <summary>Intraday tick time component.</summary>
    [Key(1)]
    public long TickTime { get; init; }

    /// <summary>Best bid price.</summary>
    [Key(2)]
    public double BidPrice { get; init; }

    /// <summary>Best ask price.</summary>
    [Key(3)]
    public double AskPrice { get; init; }

    /// <summary>Best bid size (quantity).</summary>
    [Key(4)]
    public int BidSize { get; init; }

    /// <summary>Best ask size (quantity).</summary>
    [Key(5)]
    public int AskSize { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesOptionTickBidAskReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized properties.
    /// </summary>
    public FuturesOptionTickBidAskReadModel(
        DateTime tickDate,
        long tickTime,
        double bidPrice,
        double askPrice,
        int bidSize,
        int askSize)
    {
        TickDate = tickDate;
        TickTime = tickTime;
        BidPrice = bidPrice;
        AskPrice = askPrice;
        BidSize = bidSize;
        AskSize = askSize;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
