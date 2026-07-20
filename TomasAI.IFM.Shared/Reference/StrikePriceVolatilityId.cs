using MessagePack;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.Trade;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Reference;

/// <summary>
/// MessagePack-serializable identifier for a strike price volatility definition,
/// composed of symbol, trade type, market trend, market volatility, and volatility trend.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation:
/// "Symbol.TradeType.MarketTrend.MarketVolatility.MarketVolatilityTrend".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StrikePriceVolatilityId(
    /// <summary>The futures/underlying symbol.</summary>
    [property: Key(0)] string Symbol,
    /// <summary>The option trade type/strategy.</summary>
    [property: Key(1)] TradeType TradeType,
    /// <summary>Overall market direction/trend.</summary>
    [property: Key(2)] MarketDirectionType MarketTrend,
    /// <summary>Market volatility regime.</summary>
    [property: Key(3)] MarketVolatilityType MarketVolatility,
    /// <summary>Price direction (volatility trend) classification.</summary>
    [property: Key(4)] PriceDirectionType MarketVolatilityTrend) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public StrikePriceVolatilityId()
        : this(string.Empty, TradeType.Unknown, default, default, default) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string.
    /// </summary>
    public string Format()
        => $"{Symbol}.{TradeType}.{MarketTrend}.{MarketVolatility}.{MarketVolatilityTrend}";

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString()
        => JsonConvert.SerializeObject(this, Formatting.None);
}