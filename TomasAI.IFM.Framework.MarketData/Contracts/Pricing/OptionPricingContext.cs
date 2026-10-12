using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

/// <summary>Exact reference context prepared away from the tick path. T is computed for every pass.</summary>
/// <param name="Contract">The contract.</param>
/// <param name="Calendar">The calendar.</param>
/// <param name="Rate">The rate.</param>
/// <param name="ValidUntilUtc">The valid until utc.</param>
/// <param name="GenerationId">The generation id.</param>
/// <param name="PricerVersion">The pricer version.</param>
/// <param name="MaximumQuoteAgeMilliseconds">The maximum quote age milliseconds.</param>
/// <param name="MaximumQuoteSkewMilliseconds">The maximum quote skew milliseconds.</param>
/// <param name="PublicationPolicyVersion">The publication policy version.</param>
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
    [property: Key(8)] string PublicationPolicyVersion)
{
    [Key(9)] public int MaximumSourceClockLeadMilliseconds { get; init; }
}
