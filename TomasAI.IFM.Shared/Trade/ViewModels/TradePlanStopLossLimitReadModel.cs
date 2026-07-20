using MessagePack;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing the stop-loss limit for a trade plan snapshot.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradePlanStopLossLimitReadModel
{
    /// <summary>The configured stop-loss limit value.</summary>
    [Key(0)]
    public double StopLossLimit { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public TradePlanStopLossLimitReadModel() { }

    /// <summary>Creates a new stop-loss limit view model.</summary>
    /// <param name="stopLossLimit">The stop-loss limit value.</param>
    public TradePlanStopLossLimitReadModel(double stopLossLimit)
    {
        StopLossLimit = stopLossLimit;
    }

    [IgnoreMember]
    public bool IsValid => StopLossLimit > 0;
}