using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

/// <summary>Safe diagnostic data; failed pricing never supplies usable numeric output.</summary>
[MessagePackObject]
public sealed record OptionPricingFailure(
    [property: Key(0)] string Code,
    [property: Key(1)] string Input,
    [property: Key(2)] string ContractId,
    [property: Key(3)] string Detail,
    [property: Key(4)] bool Retryable = false);
