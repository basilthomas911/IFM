using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[MessagePackObject]
public sealed record WorkerOptionChainResult(
    [property: Key(0)] bool Active,
    [property: Key(1)] Shared.MarketData.Contracts.Pricing.OptionPricingFailure? Failure,
    [property: Key(2)] string? ContextDigest = null);
