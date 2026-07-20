using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade;

/// <summary>
/// MessagePack-serializable identifier for a trade plan forward loss limit,
/// composed of OrderId, TradeId, ValueDate, and TradeType.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation:
/// "OrderId.TradeId.ValueDate.TradeType" where ValueDate is yyyyMMdd.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradePlanForwardLossLimitEntityId(
    /// <summary>The order identifier.</summary>
    [property: Key(0)] int OrderId,
    /// <summary>The trade identifier within the order.</summary>
    [property: Key(1)] int TradeId,
    /// <summary>The value (trading) date.</summary>
    [property: Key(2)] DateOnly ValueDate,
    /// <summary>The option trade type.</summary>
    [property: Key(3)] TradeType TradeType) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public TradePlanForwardLossLimitEntityId() : this(0, 0, default, TradeType.Unknown) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string.
    /// </summary>
    public string Format()
        => $"{OrderId}.{TradeId}.{ValueDate:yyyyMMdd}.{TradeType}";

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}