using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.Reference;

/// <summary>
/// MessagePack-serializable identifier for an MDI forward loss ratio definition,
/// composed of MDI bucket, trend direction, and trade type.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation:
/// "MDI.TrendDirection.TradeType".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record MDIForwardLossRatioId(
    /// <summary>Market Direction Indicator (MDI) bucket.</summary>
    [property: Key(0)] int MDI,
    /// <summary>Intrinsic time trend direction.</summary>
    [property: Key(1)] IntrinsicTimeTrendType TrendDirection,
    /// <summary>Option trade strategy/type.</summary>
    [property: Key(2)] TradeType TradeType) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public MDIForwardLossRatioId() : this(0, default, TradeType.Unknown) { }

    /// <summary>
    /// Factory method to create a new MDI forward loss ratio identifier.
    /// </summary>
    public static MDIForwardLossRatioId Create(int mdi, IntrinsicTimeTrendType trendDirection, TradeType tradeType)
        => new(mdi, trendDirection, tradeType);

    /// <summary>
    /// Formats the identifier as a dot-separated string: "MDI.TrendDirection.TradeType".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[80], $"{MDI}.{TrendDirection}.{TradeType}");
}
