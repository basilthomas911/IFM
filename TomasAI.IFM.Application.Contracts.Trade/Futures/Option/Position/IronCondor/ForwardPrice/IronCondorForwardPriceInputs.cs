using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using MessagePack;
namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;

/// <summary>Frozen complete strategy input; dates use the exchange session, quotes retain their source identities.</summary>
[MessagePackObject]
public sealed record IronCondorForwardPriceInputs
{
    [Key(0)] public DateOnly TradeDate { get; init; }
    [Key(1)] public DateOnly MaturityDate { get; init; }
    [Key(2)] public IronCondorForwardPriceLeg[] OptionLegs { get; init; } = [];
}

/// <summary>One exact retained position leg with already calculated option prices and actual contract conventions.</summary>
[MessagePackObject]
public sealed record IronCondorForwardPriceLeg(
    [property: Key(0)] Guid TradeLegId,
    [property: Key(1)] string ContractId,
    [property: Key(2)] bool IsCall,
    [property: Key(3)] int SignedQuantity,
    [property: Key(4)] double TheoreticalPrice,
    [property: Key(5)] double Delta,
    [property: Key(6)] decimal MarkPrice,
    [property: Key(7)] decimal ContractMultiplier,
    [property: Key(8)] string Currency);
