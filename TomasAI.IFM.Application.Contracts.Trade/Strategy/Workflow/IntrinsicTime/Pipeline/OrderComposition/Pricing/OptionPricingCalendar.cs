using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

/// <summary>Explicit complete calendar coverage, including exchange value dates and pricing convention.</summary>
[MessagePackObject]
public sealed record OptionPricingCalendar(
    [property: Key(0)] string Version,
    [property: Key(1)] string TimeZoneId,
    [property: Key(2)] DateOnly CoverageFrom,
    [property: Key(3)] DateOnly CoverageUntil,
    [property: Key(4)] TimeOnly ValueDateRollover,
    [property: Key(5)] ImmutableArray<DateOnly> TradingDates);
