using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

/// <summary>Failure has no usable rate; callers must not substitute zero or a neighboring tenor.</summary>
public sealed record TreasuryContinuousRateResult(TreasuryContinuousRate? Value, string? Error)
{
    public bool Succeeded => Value is not null && Error is null;
    public static TreasuryContinuousRateResult Failed(string error) => new(null, error);
}
