using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade;

/// <summary>
/// MessagePack-serializable identifier for a trade plan, composed of OrderId, TradeId, and ValueDate.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation:
/// "OrderId.TradeId.ValueDate" where ValueDate is yyyyMMdd.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record struct TradePlanEntityId(
    /// <summary>The unique identifier for the order associated with the trade plan.</summary>
    [property: Key(0)] int OrderId,
    /// <summary>The unique identifier for the trade within the order.</summary>
    [property: Key(1)] int TradeId,
    /// <summary>The date associated with the trade plan (value/settlement date).</summary>
    [property: Key(2)] DateOnly ValueDate) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public TradePlanEntityId() : this(0, 0, default) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "OrderId.TradeId.ValueDate".
    /// </summary>
    public string Format()
        => $"{OrderId}.{TradeId}.{ValueDate:yyyyMMdd}";

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}