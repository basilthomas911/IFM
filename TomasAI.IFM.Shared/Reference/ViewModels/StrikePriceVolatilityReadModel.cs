using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.Reference.ViewModels;

/// <summary>
/// MessagePack-serializable view model describing a strike price volatility configuration for a symbol,
/// parameterized by trade and market context plus delta/offset parameters.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; the derived
/// identifier is excluded from MessagePack via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StrikePriceVolatilityReadModel
{
    /// <summary>Underlying symbol (e.g., futures root).</summary>
    [Key(0)]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Trade strategy/type the definition applies to.</summary>
    [Key(1)]
    public TradeType TradeType { get; init; }

    /// <summary>Overall market direction/trend.</summary>
    [Key(2)]
    public MarketDirectionType MarketTrend { get; init; }

    /// <summary>Market volatility regime.</summary>
    [Key(3)]
    public MarketVolatilityType MarketVolatility { get; init; }

    /// <summary>Price direction (volatility trend) classification.</summary>
    [Key(4)]
    public PriceDirectionType MarketVolatilityTrend { get; init; }

    /// <summary>Target delta used to select option strikes.</summary>
    [Key(5)]
    public int Delta { get; init; }

    /// <summary>Strike offset from ATM or reference strike.</summary>
    [Key(6)]
    public int StrikePriceOffset { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public StrikePriceVolatilityReadModel() { }

    /// <summary>
    /// Full constructor to create a strike price volatility definition.
    /// </summary>
    public StrikePriceVolatilityReadModel(
        string symbol,
        TradeType tradeType,
        MarketDirectionType marketTrend,
        MarketVolatilityType marketVolatility,
        PriceDirectionType marketVolatilityTrend,
        int delta,
        int strikePriceOffset)
    {
        Symbol = symbol ?? string.Empty;
        TradeType = tradeType;
        MarketTrend = marketTrend;
        MarketVolatility = marketVolatility;
        MarketVolatilityTrend = marketVolatilityTrend;
        Delta = delta;
        StrikePriceOffset = strikePriceOffset;
    }

    /// <summary>Derived identifier (excluded from MessagePack) composed from the model fields.</summary>
    [JsonIgnore]
    [IgnoreMember]
    public StrikePriceVolatilityId Id => new(Symbol, TradeType, MarketTrend, MarketVolatility, MarketVolatilityTrend);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => !string.IsNullOrEmpty(Symbol);

    /// <summary>Returns a compact JSON representation of the model.</summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}