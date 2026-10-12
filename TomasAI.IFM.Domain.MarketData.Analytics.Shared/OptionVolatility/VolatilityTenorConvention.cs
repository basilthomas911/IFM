using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

[MessagePackObject]
public sealed record VolatilityTenorConvention(
    [property: Key(0)] int TargetCalendarDays,
    [property: Key(1)] VolatilityMoneynessConvention MoneynessConvention,
    [property: Key(2)] VolatilityOptionSideSelection OptionSideSelection,
    [property: Key(3)] ImmutableArray<string> EligibleProductFamilies,
    [property: Key(4)] VolatilitySideCombinationMethod SideCombinationMethod);
