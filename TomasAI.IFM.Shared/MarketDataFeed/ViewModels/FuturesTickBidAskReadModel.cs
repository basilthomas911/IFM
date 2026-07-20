using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// Represents a futures tick price snapshot including price and size.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record struct FuturesTickBidAskReadModel
{
    /// <summary>Request identifier.</summary>
    [Key(0)]
    public int RequestId { get; init; }

    /// <summary>Date/time when the tick was recorded.</summary>
    [Key(1)]
    public DateTime TickDate { get; init; }

    /// <summary>Intraday tick time component.</summary>
    [Key(2)]
    public long TickTime { get; init; }

    /// <summary>Tick price.</summary>
    [Key(3)]
    public double Price { get; init; }

    /// <summary>Tick size (quantity).</summary>
    [Key(4)]
    public int Size { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesTickBidAskReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized properties.
    /// </summary>
    public FuturesTickBidAskReadModel(
        int requestId,
        DateTime tickDate,
        long tickTime,
        double price,
        int size)
    {
        RequestId = requestId;
        TickDate = tickDate;
        TickTime = tickTime;
        Price = price;
        Size = size;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [IgnoreMember]
    public bool IsValid
        => RequestId != 0 && TickDate != default && TickTime != 0 && Price != 0 && Size != 0;
}
