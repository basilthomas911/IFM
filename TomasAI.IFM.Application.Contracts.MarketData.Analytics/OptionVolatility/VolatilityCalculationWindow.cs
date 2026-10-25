using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

/// <summary>The exact expected prior exchange sessions. Dates outside this window are never substitutes.</summary>
public sealed record VolatilityCalculationWindow(
    DateOnly CurrentExchangeValueDate,
    ImmutableArray<DateOnly> PriorExchangeSessionDates);
