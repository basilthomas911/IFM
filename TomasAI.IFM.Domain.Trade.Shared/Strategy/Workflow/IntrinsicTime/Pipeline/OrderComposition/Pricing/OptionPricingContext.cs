using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

/// <summary>Exact reference context prepared away from the tick path. T is computed for every pass.</summary>
[MessagePackObject]
public sealed record OptionPricingContext(
    [property: Key(0)] OptionPricingConvention Contract,
    [property: Key(1)] OptionPricingCalendar Calendar,
    [property: Key(2)] TreasuryContinuousRate Rate,
    [property: Key(3)] DateTimeOffset ValidUntilUtc,
    [property: Key(4)] Guid GenerationId,
    [property: Key(5)] string PricerVersion,
    [property: Key(6)] int MaximumQuoteAgeMilliseconds,
    [property: Key(7)] int MaximumQuoteSkewMilliseconds,
    [property: Key(8)] string PublicationPolicyVersion);
