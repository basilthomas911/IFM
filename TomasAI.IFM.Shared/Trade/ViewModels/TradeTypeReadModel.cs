using MessagePack;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model wrapping a trade strategy/type.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit property with a sequential MessagePack key
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradeTypeReadModel
{
    /// <summary>The option trade strategy/type.</summary>
    [Key(0)]
    public TradeType TradeType { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public TradeTypeReadModel() { }

    /// <summary>Creates a new trade type view model.</summary>
    /// <param name="tradeType">The trade strategy/type.</param>
    public TradeTypeReadModel(TradeType tradeType)
    {
        TradeType = tradeType;
    }

    [IgnoreMember]
    public bool IsValid => TradeType != TradeType.Unknown;
}