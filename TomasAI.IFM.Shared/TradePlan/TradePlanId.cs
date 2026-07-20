using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.TradePlan;

/// <summary>
/// MessagePack-serializable identifier for an Iron Condor trade plan, composed of order ID, trade ID,
/// trade type, value date, trade date, and maturity date.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation:
/// "OrderId.TradeId.TradeType.ValueDate.TradeDate.MaturityDate" with dates in yyyyMMdd format.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record IronCondorTradePlanId(
    /// <summary>The order identifier associated with the trade plan.</summary>
    [property: Key(0)] int OrderId,
    /// <summary>The trade identifier within the order.</summary>
    [property: Key(1)] int TradeId,
    /// <summary>The trade type (e.g., LongIronCondor, ShortIronCondor).</summary>
    [property: Key(2)] TradeType TradeType,
    /// <summary>The valuation (value) date of the trade plan.</summary>
    [property: Key(3)] DateOnly ValueDate,
    /// <summary>The execution (trade) date of the underlying trade.</summary>
    [property: Key(4)] DateOnly TradeDate,
    /// <summary>The maturity (expiry) date for the trade plan.</summary>
    [property: Key(5)] DateOnly MaturityDate) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public IronCondorTradePlanId() : this(0, 0, TradeType.Unknown, default, default, default) { }

    /// <summary>
    /// Factory method to create a new Iron Condor trade plan identifier.
    /// </summary>
    public static IronCondorTradePlanId Create(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, DateOnly tradeDate, DateOnly maturityDate)
        => new(orderId, tradeId, tradeType, valueDate, tradeDate, maturityDate);

    /// <summary>
    /// Formats the identifier as a dot-separated string.
    /// </summary>
    public string Format()
        => $"{OrderId}.{TradeId}.{TradeType}.{ValueDate:yyyyMMdd}.{TradeDate:yyyyMMdd}.{MaturityDate:yyyyMMdd}";

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}