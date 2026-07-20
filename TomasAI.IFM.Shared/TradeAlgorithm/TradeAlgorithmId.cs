using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.TradeAlgorithm;

/// <summary>
/// MessagePack-serializable identifier for a trade algorithm execution, composed of value date, order id, trade id, and trade type.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation:
/// "ValueDate.OrderId.TradeId.TradeType" where ValueDate is yyyyMMdd.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradeAlgorithmId(
    [property: Key(0)] DateOnly ValueDate,
    [property: Key(1)] int OrderId,
    [property: Key(2)] int TradeId,
    [property: Key(3)] TradeType TradeType) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public TradeAlgorithmId() : this(default, 0, 0, TradeType.Unknown) { }

    /// <summary>
    /// Factory method for creating a new trade algorithm identifier.
    /// </summary>
    public static TradeAlgorithmId Create(DateOnly valueDate, int orderId, int tradeId, TradeType tradeType)
        => new(valueDate, orderId, tradeId, tradeType);

    /// <summary>
    /// Formats the identifier as a dot-separated key: "yyyyMMdd.OrderId.TradeId.TradeType".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[96], $"{ValueDate:yyyyMMdd}.{OrderId}.{TradeId}.{TradeType}");

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(Create(ValueDate, OrderId, TradeId, TradeType), Formatting.None);
}
