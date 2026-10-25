using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

[MessagePackObject]
public sealed record VolatilityPricingConvention(
    [property: Key(0)] string ExerciseConvention,
    [property: Key(1)] string PremiumConvention,
    [property: Key(2)] string SettlementConvention,
    [property: Key(3)] ImmutableArray<string> ApplicablePricerVersions,
    [property: Key(4)] string NumericalPolicyVersion);
