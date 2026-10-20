using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Order;

/// <summary>A selected leg's displayed quote snapshot; input to provisional preview computation only.</summary>
[MessagePackObject]
public sealed record TradeOrderPreviewLeg(
    [property: Key(0)] string ContractId,
    [property: Key(1)] int Side,
    [property: Key(2)] int Quantity,
    [property: Key(3)] decimal? Strike,
    [property: Key(4)] bool IsCall,
    [property: Key(5)] bool IsFuture,
    [property: Key(6)] decimal? Bid,
    [property: Key(7)] decimal? Ask,
    [property: Key(8)] double? Delta,
    [property: Key(9)] double? Vega,
    [property: Key(10)] decimal? CashMultiplier);

/// <summary>Exact draft and quote inputs. This snapshot cannot authorize trading or reserve funds.</summary>
[MessagePackObject]
public sealed record TradeOrderPreviewRequest(
    [property: Key(0)] long InputRevision,
    [property: Key(1)] TradeOrderPreviewLeg[] SelectedLegs,
    [property: Key(2)] int StrategyQuantity,
    [property: Key(3)] decimal? EnteredOrderPrice,
    [property: Key(4)] decimal TickIncrement,
    [property: Key(5)] decimal? UnderlyingReferencePrice);

/// <summary>Backend-computed draft economics. Null values remain unavailable, never assumed zero.</summary>
[MessagePackObject]
public sealed record TradeOrderPreview
{
    [Key(0)] public long InputRevision { get; init; }
    [Key(1)] public bool HasCompleteQuotes { get; init; }
    [Key(2)] public bool HasBalancedQuantities { get; init; }
    [Key(3)] public decimal? OrderPrice { get; init; }
    [Key(4)] public decimal? NetBid { get; init; }
    [Key(5)] public decimal? NetAsk { get; init; }
    [Key(6)] public decimal? NetMid { get; init; }
    [Key(7)] public double? NetDelta { get; init; }
    [Key(8)] public double? NetVega { get; init; }
    [Key(9)] public decimal? CallWidth { get; init; }
    [Key(10)] public decimal? PutWidth { get; init; }
    [Key(11)] public decimal? CallSpreadValue { get; init; }
    [Key(12)] public decimal? PutSpreadValue { get; init; }
    [Key(13)] public decimal? EstimatedCredit { get; init; }
    [Key(14)] public decimal? MaximumProfit { get; init; }
    [Key(15)] public decimal? MaximumLoss { get; init; }
    [Key(16)] public decimal? MaximumReturn { get; init; }
    [Key(17)] public bool HasPayoffEstimate { get; init; }
    [Key(18)] public bool HasUnlimitedProfit { get; init; }
    [Key(19)] public bool HasUnlimitedLoss { get; init; }
}
