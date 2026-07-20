using MessagePack;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing the risk position type for futures.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record RiskPositionTypeReadModel
{
    /// <summary>The risk position type.</summary>
    [Key(0)]
    public RiskPositionType RiskPositionType { get; init; }

    /// <summary>Parameterless constructor for MessagePack and other serializers.</summary>
    public RiskPositionTypeReadModel() 
    {
        RiskPositionType = RiskPositionType.Unknown;
    }

    /// <summary>
    /// Creates a new <see cref="RiskPositionTypeReadModel"/> with the specified risk position type.
    /// </summary>
    /// <param name="riskPositionType">The risk position type.</param>
    public RiskPositionTypeReadModel(RiskPositionType riskPositionType)
    {
        RiskPositionType = riskPositionType;
    }
}
