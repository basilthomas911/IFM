using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

/// <summary>Explicit complete calendar coverage, including exchange value dates and pricing convention.</summary>
/// <param name="Version">The version.</param>
/// <param name="TimeZoneId">The time zone id.</param>
/// <param name="CoverageFrom">The coverage from.</param>
/// <param name="CoverageUntil">The coverage until.</param>
/// <param name="ValueDateRollover">The value date rollover.</param>
/// <param name="TradingDates">The trading dates.</param>
[MessagePackObject]
public sealed record OptionPricingCalendar(
    [property: Key(0)] string Version,
    [property: Key(1)] string TimeZoneId,
    [property: Key(2)] DateOnly CoverageFrom,
    [property: Key(3)] DateOnly CoverageUntil,
    [property: Key(4)] TimeOnly ValueDateRollover,
    [property: Key(5)] ImmutableArray<DateOnly> TradingDates);
