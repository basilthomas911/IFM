using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>A separately executable component in a generic order.</summary>
[MessagePackObject]
public sealed record TradeOrderComponentDefinition
{
    [Key(0)] public Guid ComponentId { get; init; }
    [Key(1)] public TradeStrategyKind StrategyKind { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(2)] public TradeLegDefinition[] Legs { get; init; } = [];
    [Key(3)] public bool PermitBalancedPartialAcceptance { get; init; }
    [Key(4)] public int ReservedTradeId { get; init; }
    [Key(5)] public decimal? SignedNetDebitLimit { get; init; }
    [Key(6)] public decimal? MinimumSignedNetDebitLimit { get; init; }
    [Key(7)] public decimal? MaximumSignedNetDebitLimit { get; init; }
    [Key(8)] public decimal? TickIncrement { get; init; }
}
