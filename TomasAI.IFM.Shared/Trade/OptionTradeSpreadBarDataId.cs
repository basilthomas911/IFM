using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade;

/// <summary>
/// MessagePack-serializable identifier for option trade spread bar data, composed of
/// OrderId, TradeId, ValueDate, TradeType, and BarDate.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation:
/// "OrderId.TradeId.ValueDate.TradeType.BarDate" where ValueDate = yyyyMMdd and BarDate = yyyyMMddHHmmss.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record struct OptionTradeSpreadBarDataId(
    /// <summary>The parent order identifier.</summary>
    [property: Key(0)] int OrderId,
    /// <summary>The trade identifier within the order.</summary>
    [property: Key(1)] int TradeId,
    /// <summary>The value (trading) date.</summary>
    [property: Key(2)] DateOnly ValueDate,
    /// <summary>The option trade strategy/type.</summary>
    [property: Key(3)] TradeType TradeType,
    /// <summary>The bar timestamp associated with this data point.</summary>
    [property: Key(4)] DateTime BarDate) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public OptionTradeSpreadBarDataId() : this(0, 0, default, TradeType.Unknown, default) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string.
    /// </summary>
    public string Format()
        => $"{OrderId}.{TradeId}.{ValueDate:yyyyMMdd}.{TradeType}.{BarDate:yyyyMMddHHmmss}";

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}