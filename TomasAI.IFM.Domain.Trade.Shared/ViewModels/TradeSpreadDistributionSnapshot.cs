using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.ViewModels;

/// <summary>
/// Trade-owned, wire-compatible snapshot of the spread distribution values recorded with a trade event.
/// </summary>
[MessagePackObject]
public sealed record TradeSpreadDistributionSnapshot
{
    [Key(0)] public long Id { get; init; }
    [Key(1)] public int TradeId { get; init; }
    [Key(2)] public DateOnly ValueDate { get; init; }
    [Key(3)] public TradeType TradeType { get; init; }
    [Key(4)] public TradeStatus TradeStatus { get; init; }
    [Key(5)] public int DaysToExpiry { get; init; }
    [Key(6)] public double ForwardPrice { get; init; }
    [Key(7)] public double LossProbability { get; init; }
    [Key(8)] public decimal LossThreshold { get; init; }
    [Key(9)] public int LossThresholdCount { get; init; }
    [Key(10)] public double ShortVolatility { get; init; }
    [Key(11)] public double LongVolatility { get; init; }
    [Key(12)] public double ForwardLossRatio { get; init; }
    [Key(13)] public DateTime CreatedOn { get; init; }
}
