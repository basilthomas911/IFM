using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;

/// <summary>Unscaled signed output of the original spread adjustments. No position-direction sign inversion is applied.</summary>
[MessagePackObject]
public sealed record IronCondorForwardPrice
{
    [Key(0)] public decimal ForwardPrice { get; init; }
    [Key(1)] public decimal PutForwardPricePoints { get; init; }
    [Key(2)] public decimal CallForwardPricePoints { get; init; }
    [Key(3)] public decimal ContractMultiplier { get; init; }
    [Key(4)] public string Currency { get; init; } = "";
    [Key(5)] public int StrategyQuantity { get; init; }
    [Key(6)] public ForwardPriceSessionId PositionSession { get; init; }
    [Key(7)] public long PositionSequence { get; init; }
    [Key(8)] public long RouteGeneration { get; init; }
    [Key(9)] public Guid SourceEventId { get; init; }
    [Key(10)] public DateTime CalculatedAtUtc { get; init; }
    [Key(11)] public string InputFingerprint { get; init; } = "";
    [Key(12)] public string ModelVersion { get; init; } = "IronCondor/LegacyDeltaTimeRawSum/v1";
    /// <summary>Gets the exact four-leg observations and day-count inputs used by the calculation.</summary>
    [Key(13)] public IronCondorForwardPriceInputs IronCondorForwardPriceInputs { get; init; } = new();
}
