using MessagePack;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing the forward loss ratio for a trade plan snapshot.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradePlanForwardLossRatioReadModel
{
    /// <summary>The computed forward loss ratio.</summary>
    [Key(0)]
    public double ForwardLossRatio { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public TradePlanForwardLossRatioReadModel() { }

    /// <summary>
    /// Creates a new forward loss ratio view model.
    /// </summary>
    /// <param name="forwardLossRatio">The forward loss ratio value.</param>
    public TradePlanForwardLossRatioReadModel(double forwardLossRatio)
    {
        ForwardLossRatio = forwardLossRatio;
    }

    [IgnoreMember]
    public bool IsValid => ForwardLossRatio > 0;
}